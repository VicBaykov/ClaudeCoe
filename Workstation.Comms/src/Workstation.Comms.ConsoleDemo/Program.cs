using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Workstation.Comms.Application.Configuration;
using Workstation.Comms.Application.Services;
using Workstation.Comms.Domain.Entities;
using Workstation.Comms.Domain.Enums;
using Workstation.Comms.Domain.Interfaces;
using Workstation.Comms.Infrastructure.SignalR;
using Workstation.Comms.Infrastructure.SignalR.Configuration;

namespace Workstation.Comms.ConsoleDemo
{
    /// <summary>
    /// Консольное демо-приложение для тестирования модуля связи рабочего места
    /// </summary>
    class Program
    {
        private static IWorkstationClient? _client;
        private static WorkstationInfo? _workstationInfo;
        private static bool _isRunning = true;
        private static string? _currentSessionId;

        static async Task Main(string[] args)
        {
            Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║   Workstation Communications Module - Console Demo        ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            try
            {
                // Настройки (можно вынести в appsettings.json)
                var serverUrl = "http://localhost:5000";
                var hubPath = "/ws/workstation";
                var workstationId = $"WS-CONSOLE-{Guid.NewGuid().ToString().Substring(0, 8)}";

                Console.WriteLine($"Server URL: {serverUrl}");
                Console.WriteLine($"Hub Path: {hubPath}");
                Console.WriteLine($"Workstation ID: {workstationId}");
                Console.WriteLine();

                // Создать логгер
                var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder
                        .AddConsole()
                        .SetMinimumLevel(LogLevel.Debug);
                });

                // Создать и настроить клиент
                _client = CreateWorkstationClient(serverUrl, hubPath, loggerFactory);

                // Подписаться на события
                SubscribeToEvents();

                // Создать информацию о рабочем месте
                _workstationInfo = new WorkstationInfo
                {
                    WorkstationId = workstationId,
                    DisplayName = "Console Test Workstation",
                    WorkstationType = WorkstationType.Touch,
                    SubsystemType = SubsystemType.Gangway,
                    ClientVersion = "1.0.0-demo"
                };

                // Подключиться
                Console.WriteLine("Connecting to server...");
                await _client.ConnectAsync();

                Console.WriteLine("Registering workstation...");
                await _client.RegisterAsync(_workstationInfo);

                Console.WriteLine();
                Console.WriteLine("✓ Successfully connected and registered!");
                Console.WriteLine();

                // Запустить цикл отправки тестовых логов
                var logTask = Task.Run(SendPeriodicLogsAsync);

                // Показать интерактивное меню
                await RunInteractiveMenuAsync();

                // Дождаться завершения задачи логирования
                _isRunning = false;
                await logTask;

                // Отключиться
                Console.WriteLine("\nDisconnecting...");
                await _client.DisconnectAsync();
                _client.Dispose();

                Console.WriteLine("✓ Disconnected successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        private static IWorkstationClient CreateWorkstationClient(
            string serverUrl,
            string hubPath,
            ILoggerFactory loggerFactory)
        {
            // Настройки SignalR
            var signalROptions = new SignalROptions
            {
                ServerUrl = serverUrl,
                HubPath = hubPath,
                EnableAutoReconnect = true,
                HandshakeTimeoutSeconds = 15,
                KeepAliveIntervalSeconds = 15,
                ServerTimeoutSeconds = 30
            };

            // Настройки клиента
            var clientOptions = new WorkstationClientOptions
            {
                ServerUrl = serverUrl,
                HubPath = hubPath,
                HeartbeatInterval = TimeSpan.FromSeconds(30),
                AutoReconnect = true,
                AutoStartHeartbeat = true,
                MaxReconnectAttempts = 5,
                MaxBufferSize = 1000
            };

            // Создать транспортный клиент
            var transportClient = new SignalRTransportClient(
                signalROptions,
                loggerFactory.CreateLogger<SignalRTransportClient>());

            // Создать клиент рабочего места
            return new WorkstationClient(
                transportClient,
                clientOptions,
                loggerFactory.CreateLogger<WorkstationClient>());
        }

        private static void SubscribeToEvents()
        {
            if (_client == null) return;

            _client.ConnectionStateChanged += (sender, args) =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n[CONNECTION] {args.OldState} → {args.NewState}");
                Console.ResetColor();
            };

            _client.SessionCommandReceived += (sender, args) =>
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n[COMMAND RECEIVED] Type: {args.Command.CommandType}");
                Console.WriteLine($"  Session ID: {args.Command.SessionId}");
                Console.WriteLine($"  Scenario ID: {args.Command.ScenarioId}");
                Console.WriteLine($"  User ID: {args.Command.UserId}");
                Console.ResetColor();

                HandleCommand(args.Command);
            };

            _client.ErrorOccurred += (sender, args) =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[ERROR] {args.Message}");
                if (args.Exception != null)
                {
                    Console.WriteLine($"  Exception: {args.Exception.GetType().Name}: {args.Exception.Message}");
                }
                Console.ResetColor();
            };
        }

        private static void HandleCommand(SessionCommand command)
        {
            switch (command.CommandType)
            {
                case SessionCommandType.StartSession:
                    _currentSessionId = command.SessionId;
                    Console.WriteLine($"  → Starting session: {_currentSessionId}");
                    break;

                case SessionCommandType.StopSession:
                    Console.WriteLine($"  → Stopping session: {_currentSessionId}");
                    SendMockSessionReport().Wait();
                    _currentSessionId = null;
                    break;

                case SessionCommandType.RequestReport:
                    Console.WriteLine("  → Sending session report...");
                    SendMockSessionReport().Wait();
                    break;

                default:
                    Console.WriteLine($"  → Command acknowledged: {command.CommandType}");
                    break;
            }
        }

        private static async Task SendPeriodicLogsAsync()
        {
            int logCounter = 0;

            while (_isRunning)
            {
                await Task.Delay(TimeSpan.FromSeconds(10));

                if (_client == null || _workstationInfo == null) continue;

                logCounter++;

                var logEntry = new LogEntry
                {
                    WorkstationId = _workstationInfo.WorkstationId,
                    SessionId = _currentSessionId,
                    Category = "PeriodicTest",
                    Message = $"Test log entry #{logCounter}",
                    Level = LogLevel.Info,
                    Payload = $"{{\"counter\": {logCounter}, \"timestamp\": \"{DateTime.UtcNow:O}\"}}"
                };

                try
                {
                    await _client.SendLogAsync(logEntry);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[LOG SENT] #{logCounter} at {DateTime.Now:HH:mm:ss}");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to send log: {ex.Message}");
                }
            }
        }

        private static async Task SendMockSessionReport()
        {
            if (_client == null || _workstationInfo == null) return;

            var report = new SessionReport
            {
                SessionId = _currentSessionId ?? "test-session-" + Guid.NewGuid().ToString(),
                WorkstationId = _workstationInfo.WorkstationId,
                UserId = "test-user-123",
                ScenarioId = "gangway-basic-maintenance",
                Status = SessionStatus.Completed,
                StartTime = DateTime.UtcNow.AddMinutes(-15),
                EndTime = DateTime.UtcNow,
                DurationSeconds = 900,
                Score = 87.5,
                CompletedSteps =
                [
                    new ScenarioStep
                    {
                        StepId = "step-1",
                        StepName = "Inspect gangway connections",
                        StartTime = DateTime.UtcNow.AddMinutes(-15),
                        EndTime = DateTime.UtcNow.AddMinutes(-12),
                        IsSuccess = true,
                        Attempts = 1
                    },
                    new ScenarioStep
                    {
                        StepId = "step-2",
                        StepName = "Test bellows movement",
                        StartTime = DateTime.UtcNow.AddMinutes(-12),
                        EndTime = DateTime.UtcNow.AddMinutes(-8),
                        IsSuccess = true,
                        Attempts = 2
                    },
                    new ScenarioStep
                    {
                        StepId = "step-3",
                        StepName = "Verify safety mechanisms",
                        StartTime = DateTime.UtcNow.AddMinutes(-8),
                        EndTime = DateTime.UtcNow,
                        IsSuccess = true,
                        Attempts = 1
                    }
                ]
            };

            await _client.SendSessionReportAsync(report);
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"[REPORT SENT] Session: {report.SessionId}, Score: {report.Score}%");
            Console.ResetColor();
        }

        private static async Task RunInteractiveMenuAsync()
        {
            while (_isRunning)
            {
                Console.WriteLine("\n┌─────────────────────────────────────┐");
                Console.WriteLine("│  Interactive Menu                   │");
                Console.WriteLine("├─────────────────────────────────────┤");
                Console.WriteLine("│  1. Send test log                   │");
                Console.WriteLine("│  2. Send mock session report        │");
                Console.WriteLine("│  3. Show connection status          │");
                Console.WriteLine("│  4. Simulate user action            │");
                Console.WriteLine("│  5. Simulate error                  │");
                Console.WriteLine("│  Q. Quit                            │");
                Console.WriteLine("└─────────────────────────────────────┘");
                Console.Write("\nChoice: ");

                var key = Console.ReadKey();
                Console.WriteLine();

                switch (key.Key)
                {
                    case ConsoleKey.D1:
                    case ConsoleKey.NumPad1:
                        await SendTestLogAsync();
                        break;

                    case ConsoleKey.D2:
                    case ConsoleKey.NumPad2:
                        await SendMockSessionReport();
                        break;

                    case ConsoleKey.D3:
                    case ConsoleKey.NumPad3:
                        ShowConnectionStatus();
                        break;

                    case ConsoleKey.D4:
                    case ConsoleKey.NumPad4:
                        await SimulateUserActionAsync();
                        break;

                    case ConsoleKey.D5:
                    case ConsoleKey.NumPad5:
                        await SimulateErrorAsync();
                        break;

                    case ConsoleKey.Q:
                        _isRunning = false;
                        break;

                    default:
                        Console.WriteLine("Invalid choice");
                        break;
                }
            }
        }

        private static async Task SendTestLogAsync()
        {
            if (_client == null || _workstationInfo == null) return;

            var logEntry = new LogEntry
            {
                WorkstationId = _workstationInfo.WorkstationId,
                SessionId = _currentSessionId,
                Category = "ManualTest",
                Message = "User triggered test log",
                Level = LogLevel.Info,
                Payload = $"{{\"triggeredAt\": \"{DateTime.UtcNow:O}\"}}"
            };

            await _client.SendLogAsync(logEntry);
            Console.WriteLine("✓ Test log sent");
        }

        private static void ShowConnectionStatus()
        {
            if (_client == null) return;

            Console.WriteLine("\n┌─────────────────────────────────────┐");
            Console.WriteLine("│  Connection Status                  │");
            Console.WriteLine("├─────────────────────────────────────┤");
            Console.WriteLine($"│  State: {_client.ConnectionState,-24}│");
            Console.WriteLine($"│  Workstation: {_workstationInfo?.WorkstationId,-18}│");
            Console.WriteLine($"│  Subsystem: {_workstationInfo?.SubsystemType,-20}│");
            Console.WriteLine($"│  Session: {_currentSessionId ?? "None",-22}│");
            Console.WriteLine("└─────────────────────────────────────┘");
        }

        private static async Task SimulateUserActionAsync()
        {
            if (_client == null || _workstationInfo == null) return;

            var actions = new[] { "ButtonClick", "PanelTouch", "ToolPickup", "ComponentInspect", "CheckboxToggle" };
            var action = actions[Random.Shared.Next(actions.Length)];

            var logEntry = new LogEntry
            {
                WorkstationId = _workstationInfo.WorkstationId,
                SessionId = _currentSessionId,
                Category = "UserAction",
                Message = $"User performed action: {action}",
                Level = LogLevel.Info,
                Payload = $"{{\"action\": \"{action}\", \"timestamp\": \"{DateTime.UtcNow:O}\"}}"
            };

            await _client.SendLogAsync(logEntry);
            Console.WriteLine($"✓ Simulated action: {action}");
        }

        private static async Task SimulateErrorAsync()
        {
            if (_client == null || _workstationInfo == null) return;

            var logEntry = new LogEntry
            {
                WorkstationId = _workstationInfo.WorkstationId,
                SessionId = _currentSessionId,
                Category = "Error",
                Message = "Simulated error condition",
                Level = LogLevel.Error,
                Payload = $"{{\"errorCode\": \"SIM_ERR_001\", \"details\": \"This is a simulated error for testing\"}}",
                StackTrace = new System.Diagnostics.StackTrace().ToString()
            };

            await _client.SendLogAsync(logEntry);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("✓ Simulated error sent");
            Console.ResetColor();
        }
    }
}
