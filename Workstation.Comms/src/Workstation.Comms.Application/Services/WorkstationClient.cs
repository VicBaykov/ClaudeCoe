using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Workstation.Comms.Application.Buffers;
using Workstation.Comms.Application.Configuration;
using Workstation.Comms.Domain.Entities;
using Workstation.Comms.Domain.Enums;
using Workstation.Comms.Domain.Interfaces;

namespace Workstation.Comms.Application.Services
{
    /// <summary>
    /// Основная реализация клиента рабочего места
    /// </summary>
    public class WorkstationClient : IWorkstationClient
    {
        private readonly ITransportClient _transportClient;
        private readonly WorkstationClientOptions _options;
        private readonly ILogger<WorkstationClient> _logger;

        private readonly MessageBuffer<LogEntry> _logBuffer;
        private readonly MessageBuffer<SessionReport> _reportBuffer;

        private ConnectionState _connectionState = ConnectionState.Disconnected;
        private WorkstationInfo? _workstationInfo;
        private Timer? _heartbeatTimer;
        private int _reconnectAttempts = 0;
        private bool _disposed = false;

        public ConnectionState ConnectionState => _connectionState;
        public WorkstationInfo? WorkstationInfo => _workstationInfo;

        public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
        public event EventHandler<SessionCommandReceivedEventArgs>? SessionCommandReceived;
        public event EventHandler<ErrorEventArgs>? ErrorOccurred;

        public WorkstationClient(
            ITransportClient transportClient,
            WorkstationClientOptions options,
            ILogger<WorkstationClient> logger)
        {
            _transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _logBuffer = new MessageBuffer<LogEntry>(_options.MaxBufferSize);
            _reportBuffer = new MessageBuffer<SessionReport>(_options.MaxBufferSize);

            SubscribeToTransportEvents();
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_connectionState == ConnectionState.Connected || _connectionState == ConnectionState.Registered)
            {
                _logger.LogWarning("Already connected");
                return;
            }

            try
            {
                SetConnectionState(ConnectionState.Connecting);
                _logger.LogInformation("Connecting to server at {ServerUrl}", _options.ServerUrl);

                await _transportClient.StartAsync(cancellationToken);

                SetConnectionState(ConnectionState.Connected);
                _logger.LogInformation("Successfully connected to server");
                _reconnectAttempts = 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to server");
                SetConnectionState(ConnectionState.Failed);
                RaiseError($"Connection failed: {ex.Message}", ex);

                if (_options.AutoReconnect)
                {
                    _ = Task.Run(() => TryReconnectAsync(cancellationToken), cancellationToken);
                }

                throw;
            }
        }

        public async Task RegisterAsync(WorkstationInfo workstationInfo, CancellationToken cancellationToken = default)
        {
            if (_connectionState != ConnectionState.Connected)
            {
                throw new InvalidOperationException("Must be connected before registering");
            }

            try
            {
                _logger.LogInformation("Registering workstation {WorkstationId}", workstationInfo.WorkstationId);

                await _transportClient.SendAsync("RegisterWorkstation", workstationInfo, cancellationToken);

                _workstationInfo = workstationInfo;
                SetConnectionState(ConnectionState.Registered);
                _logger.LogInformation("Successfully registered workstation");

                // Автоматически начать heartbeat если настроено
                if (_options.AutoStartHeartbeat)
                {
                    StartHeartbeat(_options.HeartbeatInterval);
                }

                // Отправить буферизованные сообщения
                await FlushBuffersAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register workstation");
                RaiseError($"Registration failed: {ex.Message}", ex);
                throw;
            }
        }

        public async Task SendLogAsync(LogEntry logEntry, CancellationToken cancellationToken = default)
        {
            if (_connectionState != ConnectionState.Registered)
            {
                _logger.LogDebug("Not registered, buffering log entry");
                _logBuffer.Enqueue(logEntry);
                return;
            }

            try
            {
                await _transportClient.SendAsync("SendLog", logEntry, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send log, buffering");
                _logBuffer.Enqueue(logEntry);
            }
        }

        public async Task SendSessionReportAsync(SessionReport report, CancellationToken cancellationToken = default)
        {
            if (_connectionState != ConnectionState.Registered)
            {
                _logger.LogDebug("Not registered, buffering report");
                _reportBuffer.Enqueue(report, priority: 10); // Высокий приоритет для отчётов
                return;
            }

            try
            {
                await _transportClient.SendAsync("SendSessionReport", report, cancellationToken);
                _logger.LogInformation("Session report sent for session {SessionId}", report.SessionId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send report, buffering");
                _reportBuffer.Enqueue(report, priority: 10);
            }
        }

        public async Task SendHeartbeatAsync(HeartbeatMessage heartbeat, CancellationToken cancellationToken = default)
        {
            if (_connectionState != ConnectionState.Registered)
            {
                return;
            }

            try
            {
                await _transportClient.SendAsync("Heartbeat", heartbeat, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send heartbeat");
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                StopHeartbeat();
                await _transportClient.StopAsync(cancellationToken);
                SetConnectionState(ConnectionState.Disconnected);
                _logger.LogInformation("Disconnected from server");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disconnect");
                throw;
            }
        }

        public void StartHeartbeat(TimeSpan interval)
        {
            StopHeartbeat();

            _logger.LogInformation("Starting heartbeat with interval {Interval}", interval);

            _heartbeatTimer = new Timer(
                async _ => await SendHeartbeatCallback(),
                null,
                TimeSpan.Zero,
                interval);
        }

        public void StopHeartbeat()
        {
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;
            _logger.LogDebug("Heartbeat stopped");
        }

        private async Task SendHeartbeatCallback()
        {
            if (_workstationInfo == null || _connectionState != ConnectionState.Registered)
            {
                return;
            }

            var heartbeat = new HeartbeatMessage
            {
                WorkstationId = _workstationInfo.WorkstationId,
                Status = "Active"
            };

            await SendHeartbeatAsync(heartbeat);
        }

        private void SubscribeToTransportEvents()
        {
            // Подписка на событие изменения состояния соединения
            _transportClient.ConnectionStateChanged += OnTransportConnectionStateChanged;

            // Регистрация обработчиков входящих команд
            _transportClient.On<SessionCommand>("ReceiveSessionCommand", OnSessionCommandReceived);
        }

        private void OnTransportConnectionStateChanged(object? sender, TransportConnectionStateChangedEventArgs e)
        {
            if (!e.IsConnected && _connectionState == ConnectionState.Registered)
            {
                _logger.LogWarning("Connection lost");
                SetConnectionState(ConnectionState.Reconnecting);

                if (_options.AutoReconnect)
                {
                    _ = Task.Run(() => TryReconnectAsync(CancellationToken.None));
                }
            }
        }

        private void OnSessionCommandReceived(SessionCommand command)
        {
            _logger.LogInformation("Received command: {CommandType}", command.CommandType);
            SessionCommandReceived?.Invoke(this, new SessionCommandReceivedEventArgs(command));
        }

        private async Task TryReconnectAsync(CancellationToken cancellationToken)
        {
            while (_reconnectAttempts < _options.MaxReconnectAttempts && !cancellationToken.IsCancellationRequested)
            {
                _reconnectAttempts++;

                var delay = CalculateReconnectDelay(_reconnectAttempts);
                _logger.LogInformation("Reconnect attempt {Attempt}/{Max} in {Delay}s",
                    _reconnectAttempts, _options.MaxReconnectAttempts, delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);

                try
                {
                    await ConnectAsync(cancellationToken);

                    // Если есть сохранённая информация о рабочем месте, перерегистрироваться
                    if (_workstationInfo != null)
                    {
                        await RegisterAsync(_workstationInfo, cancellationToken);
                    }

                    _logger.LogInformation("Reconnected successfully");
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Reconnect attempt {Attempt} failed", _reconnectAttempts);
                }
            }

            _logger.LogError("Failed to reconnect after {Attempts} attempts", _reconnectAttempts);
            SetConnectionState(ConnectionState.Failed);
        }

        private TimeSpan CalculateReconnectDelay(int attempt)
        {
            // Exponential backoff: 2^attempt * base delay
            var delaySeconds = Math.Pow(2, attempt - 1) * _options.ReconnectBaseDelaySeconds;
            delaySeconds = Math.Min(delaySeconds, _options.ReconnectMaxDelaySeconds);
            return TimeSpan.FromSeconds(delaySeconds);
        }

        private async Task FlushBuffersAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Flushing buffers: {LogCount} logs, {ReportCount} reports",
                _logBuffer.Count, _reportBuffer.Count);

            // Отправить буферизованные логи
            foreach (var log in _logBuffer.DequeueAll())
            {
                try
                {
                    await _transportClient.SendAsync("SendLog", log, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send buffered log");
                }
            }

            // Отправить буферизованные отчёты
            foreach (var report in _reportBuffer.DequeueAll())
            {
                try
                {
                    await _transportClient.SendAsync("SendSessionReport", report, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send buffered report");
                }
            }
        }

        private void SetConnectionState(ConnectionState newState)
        {
            var oldState = _connectionState;
            _connectionState = newState;

            _logger.LogDebug("Connection state changed: {OldState} -> {NewState}", oldState, newState);
            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(oldState, newState));
        }

        private void RaiseError(string message, Exception? exception = null)
        {
            ErrorOccurred?.Invoke(this, new ErrorEventArgs(message, exception));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            StopHeartbeat();
            _transportClient?.Dispose();
            _disposed = true;

            GC.SuppressFinalize(this);
        }
    }
}
