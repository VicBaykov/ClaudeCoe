using System;
using System.Threading.Tasks;
using UnityEngine;
using Microsoft.Extensions.Logging;
using Workstation.Comms.Application.Configuration;
using Workstation.Comms.Application.Services;
using Workstation.Comms.Domain.Entities;
using Workstation.Comms.Domain.Enums;
using Workstation.Comms.Infrastructure.SignalR;
using Workstation.Comms.Infrastructure.SignalR.Configuration;
using Workstation.Comms.UnityAdapter.Services;

namespace Workstation.Comms.UnityAdapter.Behaviours
{
    /// <summary>
    /// Unity MonoBehaviour для управления подключением рабочего места к серверу инструктора
    /// Прикрепите этот компонент к GameObject в сцене
    /// </summary>
    public class WorkstationClientBehaviour : MonoBehaviour
    {
        [Header("Server Configuration")]
        [Tooltip("URL сервера инструктора (например, http://localhost:5000)")]
        public string serverUrl = "http://localhost:5000";

        [Tooltip("Путь к SignalR хабу (например, /ws/workstation)")]
        public string hubPath = "/ws/workstation";

        [Header("Workstation Configuration")]
        [Tooltip("Уникальный ID рабочего места")]
        public string workstationId = "WS-001";

        [Tooltip("Название рабочего места")]
        public string displayName = "Gangway Training Workstation";

        [Tooltip("Тип рабочего места")]
        public WorkstationType workstationType = WorkstationType.Hybrid;

        [Tooltip("Подсистема поезда")]
        public SubsystemType subsystemType = SubsystemType.Gangway;

        [Header("Connection Settings")]
        [Tooltip("Автоматически подключаться при старте")]
        public bool autoConnectOnStart = true;

        [Tooltip("Интервал heartbeat (секунды)")]
        public float heartbeatIntervalSeconds = 30f;

        [Header("Debug")]
        [Tooltip("Показывать подробные логи")]
        public bool verboseLogging = true;

        // Приватные поля
        private UnityWorkstationService? _service;
        private string? _currentSessionId;
        private bool _isInitialized = false;

        // Публичные свойства для доступа из других скриптов
        public UnityWorkstationService? Service => _service;
        public ConnectionState ConnectionState => _service?.ConnectionState ?? ConnectionState.Disconnected;
        public bool IsConnected => ConnectionState == ConnectionState.Registered;

        // Unity события для подписки из других компонентов
        public event Action<SessionCommand>? OnSessionCommandReceived;
        public event Action<ConnectionState>? OnConnectionStateChanged;

        private async void Start()
        {
            if (autoConnectOnStart)
            {
                await InitializeAndConnectAsync();
            }
        }

        /// <summary>
        /// Инициализировать и подключиться к серверу
        /// </summary>
        public async Task InitializeAndConnectAsync()
        {
            if (_isInitialized)
            {
                Debug.LogWarning("WorkstationClient already initialized");
                return;
            }

            try
            {
                Debug.Log($"Initializing workstation client for {workstationId}...");

                // Создать логгер для Unity
                var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddProvider(new UnityLoggerProvider());
                    builder.SetMinimumLevel(verboseLogging ? Microsoft.Extensions.Logging.LogLevel.Debug : Microsoft.Extensions.Logging.LogLevel.Information);
                });

                // Настроить опции SignalR
                var signalROptions = new SignalROptions
                {
                    ServerUrl = serverUrl,
                    HubPath = hubPath,
                    EnableAutoReconnect = true
                };

                // Настроить опции клиента
                var clientOptions = new WorkstationClientOptions
                {
                    ServerUrl = serverUrl,
                    HubPath = hubPath,
                    HeartbeatInterval = TimeSpan.FromSeconds(heartbeatIntervalSeconds),
                    AutoReconnect = true,
                    AutoStartHeartbeat = true
                };

                // Создать транспортный клиент
                var transportClient = new SignalRTransportClient(
                    signalROptions,
                    loggerFactory.CreateLogger<SignalRTransportClient>());

                // Создать клиент рабочего места
                var workstationClient = new WorkstationClient(
                    transportClient,
                    clientOptions,
                    loggerFactory.CreateLogger<WorkstationClient>());

                // Создать сервис
                _service = new UnityWorkstationService(workstationClient);

                // Подписаться на события
                _service.OnConnectionStateChanged += HandleConnectionStateChanged;
                _service.OnSessionCommandReceived += HandleSessionCommandReceived;
                _service.OnError += HandleError;

                // Создать информацию о рабочем месте
                var workstationInfo = new WorkstationInfo
                {
                    WorkstationId = workstationId,
                    DisplayName = displayName,
                    WorkstationType = workstationType,
                    SubsystemType = subsystemType,
                    ClientVersion = Application.version
                };

                // Инициализировать (подключиться и зарегистрироваться)
                await _service.InitializeAsync(workstationInfo);

                _isInitialized = true;
                Debug.Log($"Workstation client initialized successfully for {workstationId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize workstation client: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Отправить лог действия пользователя
        /// Вызывайте этот метод из ваших игровых скриптов
        /// </summary>
        public async void LogUserAction(string action, string details = "")
        {
            if (_service == null)
            {
                Debug.LogWarning("Service not initialized, cannot log action");
                return;
            }

            await _service.LogUserActionAsync(action, details);
        }

        /// <summary>
        /// Отправить произвольное событие лога
        /// </summary>
        public async void LogEvent(string category, string message, LogLevel level = LogLevel.Info)
        {
            if (_service == null)
            {
                Debug.LogWarning("Service not initialized, cannot log event");
                return;
            }

            await _service.LogEventAsync(category, message, level);
        }

        /// <summary>
        /// Обработчик команд от инструктора
        /// </summary>
        private void HandleSessionCommandReceived(SessionCommand command)
        {
            Debug.Log($"Received command: {command.CommandType}");

            // Транслировать событие для других компонентов
            OnSessionCommandReceived?.Invoke(command);

            // Обработка команд
            switch (command.CommandType)
            {
                case SessionCommandType.StartSession:
                    StartSession(command);
                    break;

                case SessionCommandType.StopSession:
                    StopSession(command);
                    break;

                case SessionCommandType.PauseSession:
                    PauseSession(command);
                    break;

                case SessionCommandType.ResumeSession:
                    ResumeSession(command);
                    break;

                case SessionCommandType.RequestReport:
                    SendCurrentSessionReport();
                    break;

                case SessionCommandType.SetSubsystem:
                    SetSubsystem(command);
                    break;

                default:
                    Debug.LogWarning($"Unhandled command type: {command.CommandType}");
                    break;
            }
        }

        private void StartSession(SessionCommand command)
        {
            _currentSessionId = command.SessionId;
            Debug.Log($"Starting session {_currentSessionId} with scenario {command.ScenarioId}");

            // TODO: Реализуйте запуск сценария в вашем приложении
            // Например:
            // ScenarioManager.Instance.LoadScenario(command.ScenarioId);

            LogEvent("Session", $"Session started: {_currentSessionId}", LogLevel.Info);
        }

        private void StopSession(SessionCommand command)
        {
            Debug.Log($"Stopping session {_currentSessionId}");

            // TODO: Реализуйте остановку сценария
            // Например:
            // ScenarioManager.Instance.StopCurrentScenario();

            SendCurrentSessionReport();
            _currentSessionId = null;
        }

        private void PauseSession(SessionCommand command)
        {
            Debug.Log($"Pausing session {_currentSessionId}");
            // TODO: Реализуйте паузу
        }

        private void ResumeSession(SessionCommand command)
        {
            Debug.Log($"Resuming session {_currentSessionId}");
            // TODO: Реализуйте возобновление
        }

        private void SetSubsystem(SessionCommand command)
        {
            if (command.TargetSubsystem.HasValue)
            {
                subsystemType = command.TargetSubsystem.Value;
                Debug.Log($"Switched to subsystem: {subsystemType}");
            }
        }

        private async void SendCurrentSessionReport()
        {
            if (string.IsNullOrEmpty(_currentSessionId) || _service == null)
            {
                Debug.LogWarning("No active session to report");
                return;
            }

            // TODO: Соберите реальные данные из вашего приложения
            var report = new SessionReport
            {
                SessionId = _currentSessionId,
                WorkstationId = workstationId,
                UserId = "test-user", // TODO: получить из системы
                ScenarioId = "test-scenario", // TODO: получить из ScenarioManager
                Status = SessionStatus.Completed,
                StartTime = DateTime.UtcNow.AddMinutes(-10),
                EndTime = DateTime.UtcNow,
                DurationSeconds = 600,
                Score = 85.5
            };

            await _service.SendSessionReportAsync(report);
        }

        private void HandleConnectionStateChanged(ConnectionState oldState, ConnectionState newState)
        {
            Debug.Log($"Connection state: {oldState} -> {newState}");
            OnConnectionStateChanged?.Invoke(newState);
        }

        private void HandleError(string message, Exception? exception)
        {
            Debug.LogError($"Workstation client error: {message}");
            if (exception != null)
            {
                Debug.LogException(exception);
            }
        }

        private async void OnDestroy()
        {
            if (_service != null)
            {
                Debug.Log("Shutting down workstation client...");
                await _service.ShutdownAsync();
            }
        }

        private async void OnApplicationQuit()
        {
            if (_service != null && _isInitialized)
            {
                await _service.ShutdownAsync();
            }
        }
    }

    /// <summary>
    /// Провайдер логгера для Unity Debug.Log
    /// </summary>
    internal class UnityLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new UnityLogger(categoryName);
        }

        public void Dispose() { }
    }

    /// <summary>
    /// Логгер для вывода в Unity Debug.Log
    /// </summary>
    internal class UnityLogger : ILogger
    {
        private readonly string _categoryName;

        public UnityLogger(string categoryName)
        {
            _categoryName = categoryName;
        }

        public IDisposable BeginScope<TState>(TState state) => null!;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = $"[{_categoryName}] {formatter(state, exception)}";

            switch (logLevel)
            {
                case Microsoft.Extensions.Logging.LogLevel.Trace:
                case Microsoft.Extensions.Logging.LogLevel.Debug:
                case Microsoft.Extensions.Logging.LogLevel.Information:
                    Debug.Log(message);
                    break;
                case Microsoft.Extensions.Logging.LogLevel.Warning:
                    Debug.LogWarning(message);
                    break;
                case Microsoft.Extensions.Logging.LogLevel.Error:
                case Microsoft.Extensions.Logging.LogLevel.Critical:
                    Debug.LogError(message);
                    if (exception != null)
                    {
                        Debug.LogException(exception);
                    }
                    break;
            }
        }
    }
}
