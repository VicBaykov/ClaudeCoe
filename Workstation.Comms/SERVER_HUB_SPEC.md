# Server Hub Specification

Спецификация SignalR хаба для серверной части (Instructor Module).

## 📋 Обзор

Серверный хаб должен быть реализован в ASP.NET Core приложении инструктора и обрабатывать подключения от рабочих мест.

---

## 🔌 Конфигурация

### Program.cs или Startup.cs

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Добавить SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// Добавить CORS если нужно
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost", "http://workstation-client")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors();
app.UseRouting();

// Маппинг хаба
app.MapHub<WorkstationHub>("/ws/workstation");

app.Run();
```

---

## 🎯 WorkstationHub Implementation

### Базовая реализация хаба

```csharp
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace InstructorModule.Hubs
{
    /// <summary>
    /// SignalR хаб для связи с рабочими местами
    /// </summary>
    public class WorkstationHub : Hub
    {
        // Хранилище подключенных рабочих мест
        private static readonly ConcurrentDictionary<string, WorkstationConnection> _workstations
            = new ConcurrentDictionary<string, WorkstationConnection>();

        private readonly ILogger<WorkstationHub> _logger;
        private readonly IWorkstationService _workstationService; // Ваш сервис

        public WorkstationHub(
            ILogger<WorkstationHub> logger,
            IWorkstationService workstationService)
        {
            _logger = logger;
            _workstationService = workstationService;
        }

        #region Методы, вызываемые клиентом

        /// <summary>
        /// Регистрация рабочего места
        /// </summary>
        [HubMethodName("RegisterWorkstation")]
        public async Task RegisterWorkstation(WorkstationInfoDto info)
        {
            try
            {
                _logger.LogInformation(
                    "Registering workstation {WorkstationId} from connection {ConnectionId}",
                    info.WorkstationId,
                    Context.ConnectionId);

                // Сохранить связь ConnectionId -> WorkstationId
                var connection = new WorkstationConnection
                {
                    ConnectionId = Context.ConnectionId,
                    WorkstationId = info.WorkstationId,
                    WorkstationInfo = info,
                    ConnectedAt = DateTime.UtcNow,
                    LastHeartbeat = DateTime.UtcNow
                };

                _workstations.AddOrUpdate(
                    info.WorkstationId,
                    connection,
                    (key, old) => connection);

                // Добавить в группу по типу подсистемы (для broadcast)
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    $"Subsystem_{info.SubsystemType}");

                // Сохранить в БД/сервисе
                await _workstationService.RegisterWorkstationAsync(info);

                _logger.LogInformation(
                    "Workstation {WorkstationId} registered successfully",
                    info.WorkstationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to register workstation {WorkstationId}",
                    info.WorkstationId);
                throw;
            }
        }

        /// <summary>
        /// Получение heartbeat от рабочего места
        /// </summary>
        [HubMethodName("Heartbeat")]
        public async Task Heartbeat(HeartbeatMessageDto heartbeat)
        {
            _logger.LogDebug("Heartbeat from {WorkstationId}", heartbeat.WorkstationId);

            if (_workstations.TryGetValue(heartbeat.WorkstationId, out var connection))
            {
                connection.LastHeartbeat = DateTime.UtcNow;
                connection.Status = heartbeat.Status;
            }

            // Сохранить статус
            await _workstationService.UpdateWorkstationStatusAsync(
                heartbeat.WorkstationId,
                heartbeat.Status);
        }

        /// <summary>
        /// Получение лог-записи от рабочего места
        /// </summary>
        [HubMethodName("SendLog")]
        public async Task SendLog(LogEntryDto logEntry)
        {
            _logger.LogDebug(
                "Log from {WorkstationId}: {Category} - {Message}",
                logEntry.WorkstationId,
                logEntry.Category,
                logEntry.Message);

            try
            {
                // Сохранить в БД/очередь
                await _workstationService.SaveLogEntryAsync(logEntry);

                // Опционально: отправить в real-time dashboard
                await Clients.Group("Instructors").SendAsync(
                    "ReceiveWorkstationLog",
                    logEntry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save log entry");
            }
        }

        /// <summary>
        /// Получение отчета о сессии
        /// </summary>
        [HubMethodName("SendSessionReport")]
        public async Task SendSessionReport(SessionReportDto report)
        {
            _logger.LogInformation(
                "Session report from {WorkstationId} for session {SessionId}",
                report.WorkstationId,
                report.SessionId);

            try
            {
                // Сохранить отчет в БД
                await _workstationService.SaveSessionReportAsync(report);

                // Уведомить инструктора
                await Clients.Group("Instructors").SendAsync(
                    "ReceiveSessionReport",
                    report);

                _logger.LogInformation(
                    "Session report saved: {SessionId}, Score: {Score}",
                    report.SessionId,
                    report.Score);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save session report");
                throw;
            }
        }

        #endregion

        #region Методы для отправки команд клиенту

        /// <summary>
        /// Отправить команду конкретному рабочему месту
        /// </summary>
        public async Task SendCommandToWorkstation(
            string workstationId,
            SessionCommandDto command)
        {
            if (!_workstations.TryGetValue(workstationId, out var connection))
            {
                _logger.LogWarning(
                    "Workstation {WorkstationId} not found",
                    workstationId);
                return;
            }

            _logger.LogInformation(
                "Sending command {CommandType} to workstation {WorkstationId}",
                command.CommandType,
                workstationId);

            await Clients.Client(connection.ConnectionId).SendAsync(
                "ReceiveSessionCommand",
                command);
        }

        /// <summary>
        /// Отправить команду всем рабочим местам определенной подсистемы
        /// </summary>
        public async Task SendCommandToSubsystem(
            SubsystemType subsystemType,
            SessionCommandDto command)
        {
            _logger.LogInformation(
                "Sending command {CommandType} to subsystem {Subsystem}",
                command.CommandType,
                subsystemType);

            await Clients.Group($"Subsystem_{subsystemType}").SendAsync(
                "ReceiveSessionCommand",
                command);
        }

        /// <summary>
        /// Broadcast команды всем подключенным рабочим местам
        /// </summary>
        public async Task BroadcastCommand(SessionCommandDto command)
        {
            _logger.LogInformation(
                "Broadcasting command {CommandType} to all workstations",
                command.CommandType);

            await Clients.All.SendAsync("ReceiveSessionCommand", command);
        }

        #endregion

        #region События подключения/отключения

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation(
                "Client connected: {ConnectionId}",
                Context.ConnectionId);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation(
                "Client disconnected: {ConnectionId}",
                Context.ConnectionId);

            // Найти и удалить рабочее место из активных
            var workstation = _workstations.Values
                .FirstOrDefault(w => w.ConnectionId == Context.ConnectionId);

            if (workstation != null)
            {
                _logger.LogWarning(
                    "Workstation {WorkstationId} disconnected",
                    workstation.WorkstationId);

                _workstations.TryRemove(workstation.WorkstationId, out _);

                // Обновить статус в БД
                await _workstationService.MarkWorkstationDisconnectedAsync(
                    workstation.WorkstationId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        #endregion

        #region Вспомогательные методы

        /// <summary>
        /// Получить список активных рабочих мест
        /// </summary>
        public IEnumerable<WorkstationConnection> GetActiveWorkstations()
        {
            return _workstations.Values.ToList();
        }

        /// <summary>
        /// Проверить, подключено ли рабочее место
        /// </summary>
        public bool IsWorkstationConnected(string workstationId)
        {
            return _workstations.ContainsKey(workstationId);
        }

        #endregion
    }

    /// <summary>
    /// Информация о подключении рабочего места
    /// </summary>
    public class WorkstationConnection
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WorkstationId { get; set; } = string.Empty;
        public WorkstationInfoDto WorkstationInfo { get; set; } = null!;
        public DateTime ConnectedAt { get; set; }
        public DateTime LastHeartbeat { get; set; }
        public string Status { get; set; } = "Unknown";
    }
}
```

---

## 📦 DTO Models (Data Transfer Objects)

Эти модели должны совпадать с моделями клиента:

```csharp
namespace InstructorModule.Hubs.Models
{
    /// <summary>
    /// Информация о рабочем месте
    /// </summary>
    public class WorkstationInfoDto
    {
        public string WorkstationId { get; set; } = string.Empty;
        public WorkstationType WorkstationType { get; set; }
        public SubsystemType SubsystemType { get; set; }
        public string? DisplayName { get; set; }
        public string ClientVersion { get; set; } = "1.0.0";
        public string? Metadata { get; set; }
        public DateTime RegisteredAt { get; set; }
    }

    /// <summary>
    /// Heartbeat сообщение
    /// </summary>
    public class HeartbeatMessageDto
    {
        public string WorkstationId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? ActiveSessionId { get; set; }
        public string Status { get; set; } = "Idle";
        public string? SystemMetrics { get; set; }
    }

    /// <summary>
    /// Команда для рабочего места
    /// </summary>
    public class SessionCommandDto
    {
        public string CommandId { get; set; } = Guid.NewGuid().ToString();
        public SessionCommandType CommandType { get; set; }
        public string? SessionId { get; set; }
        public string? ScenarioId { get; set; }
        public string? UserId { get; set; }
        public SubsystemType? TargetSubsystem { get; set; }
        public Dictionary<string, object>? Parameters { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int Priority { get; set; }
    }

    /// <summary>
    /// Лог-запись
    /// </summary>
    public class LogEntryDto
    {
        public string WorkstationId { get; set; } = string.Empty;
        public string? SessionId { get; set; }
        public LogLevel Level { get; set; }
        public string Category { get; set; } = "General";
        public string Message { get; set; } = string.Empty;
        public string? Payload { get; set; }
        public DateTime Timestamp { get; set; }
        public string? Tag { get; set; }
        public string? StackTrace { get; set; }
    }

    /// <summary>
    /// Отчет о сессии
    /// </summary>
    public class SessionReportDto
    {
        public string SessionId { get; set; } = string.Empty;
        public string WorkstationId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string ScenarioId { get; set; } = string.Empty;
        public SessionStatus Status { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public double DurationSeconds { get; set; }
        public List<ScenarioStepDto> CompletedSteps { get; set; } = new();
        public List<SessionErrorDto> Errors { get; set; } = new();
        public double? Score { get; set; }
        public Dictionary<string, object>? Metrics { get; set; }
        public string? Notes { get; set; }
    }

    public class ScenarioStepDto
    {
        public string StepId { get; set; } = string.Empty;
        public string StepName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool IsSuccess { get; set; }
        public int Attempts { get; set; }
        public string? Data { get; set; }
    }

    public class SessionErrorDto
    {
        public string ErrorType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? StepId { get; set; }
        public bool IsCritical { get; set; }
    }

    // Enums
    public enum WorkstationType { Touch = 1, VR = 2, Hybrid = 3 }
    public enum SubsystemType { None = 0, Gangway = 1, Doors = 2, HVAC = 3, /* ... */ }
    public enum SessionCommandType { StartSession = 1, StopSession = 2, /* ... */ }
    public enum SessionStatus { NotStarted = 0, Running = 1, /* ... */ }
    public enum LogLevel { Debug = 0, Info = 1, Warning = 2, Error = 3, Critical = 4 }
}
```

---

## 🎯 Пример использования хаба

### Контроллер инструктора

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

[ApiController]
[Route("api/[controller]")]
public class InstructorController : ControllerBase
{
    private readonly IHubContext<WorkstationHub> _hubContext;

    public InstructorController(IHubContext<WorkstationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <summary>
    /// Запустить сессию на рабочем месте
    /// </summary>
    [HttpPost("start-session")]
    public async Task<IActionResult> StartSession(
        [FromBody] StartSessionRequest request)
    {
        var command = new SessionCommandDto
        {
            CommandType = SessionCommandType.StartSession,
            SessionId = Guid.NewGuid().ToString(),
            ScenarioId = request.ScenarioId,
            UserId = request.UserId
        };

        // Отправить команду через хаб
        var hub = _hubContext.Clients.All;
        await hub.SendAsync("ReceiveSessionCommand", command);

        return Ok(new { SessionId = command.SessionId });
    }

    /// <summary>
    /// Остановить сессию
    /// </summary>
    [HttpPost("stop-session")]
    public async Task<IActionResult> StopSession(string sessionId)
    {
        var command = new SessionCommandDto
        {
            CommandType = SessionCommandType.StopSession,
            SessionId = sessionId
        };

        await _hubContext.Clients.All.SendAsync("ReceiveSessionCommand", command);

        return Ok();
    }

    /// <summary>
    /// Запросить отчет
    /// </summary>
    [HttpPost("request-report")]
    public async Task<IActionResult> RequestReport(string workstationId)
    {
        var command = new SessionCommandDto
        {
            CommandType = SessionCommandType.RequestReport
        };

        // Отправить конкретному рабочему месту
        // Нужно расширить WorkstationHub методом SendCommandToWorkstation
        // и вызвать его через strongly-typed hub context

        return Ok();
    }
}

public class StartSessionRequest
{
    public string ScenarioId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
}
```

---

## 🔒 Безопасность (опционально)

### Добавление аутентификации

```csharp
// Startup.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/ws/workstation"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

// WorkstationHub.cs
[Authorize] // Требовать аутентификацию
public class WorkstationHub : Hub
{
    // ...
}
```

---

## 📊 Мониторинг

### Добавление метрик

```csharp
public class WorkstationHub : Hub
{
    private readonly IMetrics _metrics; // Prometheus, App Insights и т.д.

    [HubMethodName("RegisterWorkstation")]
    public async Task RegisterWorkstation(WorkstationInfoDto info)
    {
        _metrics.Increment("workstation.registrations");
        _metrics.Gauge("workstation.active_count", _workstations.Count);

        // ... остальной код
    }
}
```

---

## ✅ Чек-лист для серверной реализации

- [ ] Создать WorkstationHub класс
- [ ] Реализовать все методы из спецификации
- [ ] Добавить обработку ошибок и логирование
- [ ] Настроить CORS если нужно
- [ ] Добавить сохранение данных в БД
- [ ] Настроить аутентификацию (опционально)
- [ ] Добавить мониторинг/метрики
- [ ] Протестировать с ConsoleDemo клиентом
- [ ] Протестировать с Unity клиентом
- [ ] Настроить балансировку нагрузки (если нужно)

---

## 🧪 Тестирование

Используйте консольное демо для тестирования хаба:

```bash
cd Workstation.Comms/src/Workstation.Comms.ConsoleDemo
dotnet run
```

Проверьте:
- ✅ Регистрация рабочего места
- ✅ Heartbeat сообщения
- ✅ Отправка логов
- ✅ Отправка отчетов
- ✅ Получение команд от сервера

---

**Удачи с реализацией серверного хаба!** 🚀
