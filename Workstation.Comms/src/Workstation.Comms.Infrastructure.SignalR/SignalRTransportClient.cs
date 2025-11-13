using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Workstation.Comms.Domain.Interfaces;
using Workstation.Comms.Infrastructure.SignalR.Configuration;

namespace Workstation.Comms.Infrastructure.SignalR
{
    /// <summary>
    /// Реализация транспортного клиента на основе SignalR
    /// </summary>
    public class SignalRTransportClient : ITransportClient
    {
        private readonly SignalROptions _options;
        private readonly ILogger<SignalRTransportClient> _logger;
        private HubConnection? _connection;
        private bool _disposed = false;

        public bool IsConnected => _connection?.State == HubConnectionState.Connected;

        public event EventHandler<TransportConnectionStateChangedEventArgs>? ConnectionStateChanged;
        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

        public SignalRTransportClient(SignalROptions options, ILogger<SignalRTransportClient> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            BuildConnection();
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_connection == null)
            {
                BuildConnection();
            }

            if (_connection!.State == HubConnectionState.Connected)
            {
                _logger.LogWarning("Connection already started");
                return;
            }

            try
            {
                _logger.LogInformation("Starting SignalR connection to {HubUrl}", _options.HubUrl);
                await _connection.StartAsync(cancellationToken);
                _logger.LogInformation("SignalR connection started successfully");

                RaiseConnectionStateChanged(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start SignalR connection");
                RaiseConnectionStateChanged(false, ex.Message);
                throw;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (_connection == null || _connection.State == HubConnectionState.Disconnected)
            {
                return;
            }

            try
            {
                _logger.LogInformation("Stopping SignalR connection");
                await _connection.StopAsync(cancellationToken);
                _logger.LogInformation("SignalR connection stopped");

                RaiseConnectionStateChanged(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping SignalR connection");
                throw;
            }
        }

        public async Task SendAsync<TMessage>(string methodName, TMessage message, CancellationToken cancellationToken = default)
            where TMessage : class
        {
            if (_connection == null || _connection.State != HubConnectionState.Connected)
            {
                throw new InvalidOperationException("Connection is not established");
            }

            try
            {
                _logger.LogDebug("Sending message via method {MethodName}", methodName);
                await _connection.InvokeAsync(methodName, message, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message via method {MethodName}", methodName);
                throw;
            }
        }

        public void On<TMessage>(string methodName, Action<TMessage> handler)
            where TMessage : class
        {
            if (_connection == null)
            {
                throw new InvalidOperationException("Connection not initialized");
            }

            _logger.LogDebug("Registering handler for method {MethodName}", methodName);

            _connection.On<TMessage>(methodName, message =>
            {
                _logger.LogDebug("Received message via method {MethodName}", methodName);

                try
                {
                    handler(message);
                    MessageReceived?.Invoke(this, new MessageReceivedEventArgs(methodName, message));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling message from method {MethodName}", methodName);
                }
            });
        }

        private void BuildConnection()
        {
            _logger.LogDebug("Building SignalR connection");

            var builder = new HubConnectionBuilder()
                .WithUrl(_options.HubUrl, options =>
                {
                    if (!string.IsNullOrEmpty(_options.AccessToken))
                    {
                        options.AccessTokenProvider = () => Task.FromResult<string?>(_options.AccessToken);
                    }
                })
                .WithAutomaticReconnect(new CustomRetryPolicy(_options.MaxReconnectDelayMilliseconds));

            // Настройки таймаутов
            builder.Services.Configure<HubConnectionOptions>(options =>
            {
                options.HandshakeTimeout = TimeSpan.FromSeconds(_options.HandshakeTimeoutSeconds);
                options.KeepAliveInterval = TimeSpan.FromSeconds(_options.KeepAliveIntervalSeconds);
                options.ServerTimeout = TimeSpan.FromSeconds(_options.ServerTimeoutSeconds);
            });

            _connection = builder.Build();

            // Подписка на события соединения
            _connection.Closed += OnConnectionClosed;
            _connection.Reconnecting += OnReconnecting;
            _connection.Reconnected += OnReconnected;
        }

        private Task OnConnectionClosed(Exception? error)
        {
            var errorMessage = error?.Message;
            _logger.LogWarning(error, "SignalR connection closed");
            RaiseConnectionStateChanged(false, errorMessage);
            return Task.CompletedTask;
        }

        private Task OnReconnecting(Exception? error)
        {
            _logger.LogWarning("SignalR connection reconnecting...");
            return Task.CompletedTask;
        }

        private Task OnReconnected(string? connectionId)
        {
            _logger.LogInformation("SignalR connection reconnected with ID {ConnectionId}", connectionId);
            RaiseConnectionStateChanged(true);
            return Task.CompletedTask;
        }

        private void RaiseConnectionStateChanged(bool isConnected, string? error = null)
        {
            ConnectionStateChanged?.Invoke(this, new TransportConnectionStateChangedEventArgs(isConnected, error));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _connection?.DisposeAsync().AsTask().Wait();
            _connection = null;
            _disposed = true;

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Кастомная политика повторных попыток подключения
        /// </summary>
        private class CustomRetryPolicy : IRetryPolicy
        {
            private readonly int _maxDelayMilliseconds;

            public CustomRetryPolicy(int maxDelayMilliseconds)
            {
                _maxDelayMilliseconds = maxDelayMilliseconds;
            }

            public TimeSpan? NextRetryDelay(RetryContext retryContext)
            {
                // Exponential backoff с ограничением максимальной задержки
                var delayMilliseconds = Math.Min(
                    Math.Pow(2, retryContext.PreviousRetryCount) * 1000,
                    _maxDelayMilliseconds);

                return TimeSpan.FromMilliseconds(delayMilliseconds);
            }
        }
    }
}
