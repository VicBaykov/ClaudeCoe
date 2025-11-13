using System;
using System.Threading;
using System.Threading.Tasks;
using Workstation.Comms.Domain.Entities;
using Workstation.Comms.Domain.Enums;
using Workstation.Comms.Domain.Interfaces;

namespace Workstation.Comms.UnityAdapter.Services
{
    /// <summary>
    /// Сервис для работы с клиентом рабочего места в Unity
    /// Обёртка над IWorkstationClient для удобного использования в Unity
    /// </summary>
    public class UnityWorkstationService
    {
        private readonly IWorkstationClient _client;
        private CancellationTokenSource? _cancellationTokenSource;

        public IWorkstationClient Client => _client;
        public ConnectionState ConnectionState => _client.ConnectionState;
        public WorkstationInfo? WorkstationInfo => _client.WorkstationInfo;

        // События для Unity (делегаты вместо EventHandler для удобства)
        public Action<ConnectionState, ConnectionState>? OnConnectionStateChanged;
        public Action<SessionCommand>? OnSessionCommandReceived;
        public Action<string, Exception?>? OnError;

        public UnityWorkstationService(IWorkstationClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            SubscribeToEvents();
        }

        /// <summary>
        /// Инициализировать и подключиться к серверу
        /// </summary>
        public async Task InitializeAsync(WorkstationInfo workstationInfo)
        {
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                await _client.ConnectAsync(_cancellationTokenSource.Token);
                await _client.RegisterAsync(workstationInfo, _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to initialize workstation service: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Отправить лог события на сервер
        /// </summary>
        public async Task LogEventAsync(string category, string message, LogLevel level = LogLevel.Info, string? payload = null)
        {
            if (_client.WorkstationInfo == null)
            {
                UnityEngine.Debug.LogWarning("WorkstationInfo not set, cannot send log");
                return;
            }

            var logEntry = new LogEntry
            {
                WorkstationId = _client.WorkstationInfo.WorkstationId,
                Category = category,
                Message = message,
                Level = level,
                Payload = payload
            };

            try
            {
                await _client.SendLogAsync(logEntry, _cancellationTokenSource?.Token ?? CancellationToken.None);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Failed to send log: {ex.Message}");
            }
        }

        /// <summary>
        /// Отправить лог действия пользователя
        /// </summary>
        public async Task LogUserActionAsync(string action, string details)
        {
            await LogEventAsync("UserAction", $"User performed: {action}", LogLevel.Info, details);
        }

        /// <summary>
        /// Отправить отчёт о завершённой сессии
        /// </summary>
        public async Task SendSessionReportAsync(SessionReport report)
        {
            try
            {
                await _client.SendSessionReportAsync(report, _cancellationTokenSource?.Token ?? CancellationToken.None);
                UnityEngine.Debug.Log($"Session report sent for session {report.SessionId}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to send session report: {ex.Message}");
            }
        }

        /// <summary>
        /// Отключиться от сервера и освободить ресурсы
        /// </summary>
        public async Task ShutdownAsync()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                await _client.DisconnectAsync(CancellationToken.None);
                _client.Dispose();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Error during shutdown: {ex.Message}");
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void SubscribeToEvents()
        {
            _client.ConnectionStateChanged += (sender, args) =>
            {
                UnityEngine.Debug.Log($"Connection state changed: {args.OldState} -> {args.NewState}");
                OnConnectionStateChanged?.Invoke(args.OldState, args.NewState);
            };

            _client.SessionCommandReceived += (sender, args) =>
            {
                UnityEngine.Debug.Log($"Session command received: {args.Command.CommandType}");
                OnSessionCommandReceived?.Invoke(args.Command);
            };

            _client.ErrorOccurred += (sender, args) =>
            {
                UnityEngine.Debug.LogError($"Error occurred: {args.Message}");
                OnError?.Invoke(args.Message, args.Exception);
            };
        }
    }
}
