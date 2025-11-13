# Quick Start Guide

Быстрый старт для разработчиков.

---

## ⚡ 5-минутный старт

### Шаг 1: Клонирование и сборка (1 мин)

```bash
cd Workstation.Comms
dotnet restore
dotnet build
```

### Шаг 2: Запуск консольного демо (2 мин)

```bash
cd src/Workstation.Comms.ConsoleDemo
dotnet run
```

**Ожидаемый результат:**
```
╔════════════════════════════════════════════════════════════╗
║   Workstation Communications Module - Console Demo        ║
╚════════════════════════════════════════════════════════════╝

Server URL: http://localhost:5000
Workstation ID: WS-CONSOLE-12345678

Connecting to server...
✓ Successfully connected and registered!

[CONNECTION] Disconnected → Connecting
[CONNECTION] Connecting → Connected
[CONNECTION] Connected → Registered
[LOG SENT] #1 at 14:30:15
```

### Шаг 3: Unity интеграция (2 мин)

1. Скопируйте DLL из `bin/Release/netstandard2.1/` в `Assets/Plugins/`
2. Создайте GameObject с компонентом `WorkstationClientBehaviour`
3. Настройте Server URL в Inspector
4. Запустите сцену

**Готово!** Ваше рабочее место подключено.

---

## 🎯 Основные сценарии использования

### Сценарий 1: Базовое подключение

```csharp
// В Unity MonoBehaviour или любом C# приложении

var options = new WorkstationClientOptions
{
    ServerUrl = "http://your-server:5000",
    HubPath = "/ws/workstation"
};

var transport = new SignalRTransportClient(signalROptions, logger);
var client = new WorkstationClient(transport, options, logger);

await client.ConnectAsync();
await client.RegisterAsync(workstationInfo);

// Готово к работе!
```

### Сценарий 2: Отправка логов

```csharp
var log = new LogEntry
{
    WorkstationId = "WS-001",
    Category = "UserAction",
    Message = "User clicked button",
    Level = LogLevel.Info
};

await client.SendLogAsync(log);
```

### Сценарий 3: Обработка команд

```csharp
client.SessionCommandReceived += (sender, args) =>
{
    switch (args.Command.CommandType)
    {
        case SessionCommandType.StartSession:
            StartTraining(args.Command.ScenarioId);
            break;

        case SessionCommandType.StopSession:
            StopTraining();
            break;
    }
};
```

### Сценарий 4: Отправка отчета

```csharp
var report = new SessionReport
{
    SessionId = "session-123",
    WorkstationId = "WS-001",
    Status = SessionStatus.Completed,
    Score = 95.5,
    CompletedSteps = steps
};

await client.SendSessionReportAsync(report);
```

---

## 📦 Что внутри?

### Проекты

| Проект | Описание | Зависимости |
|--------|----------|-------------|
| **Domain** | Интерфейсы и модели | Нет |
| **Application** | Бизнес-логика | Domain |
| **Infrastructure.SignalR** | SignalR транспорт | Domain |
| **UnityAdapter** | Unity обёртка | Application, Infrastructure |
| **ConsoleDemo** | Тестовое приложение | Application, Infrastructure |

### Ключевые файлы

```
Workstation.Comms/
├── README.md                    ⭐ Начните отсюда
├── QUICKSTART.md               ⭐ Вы здесь
├── ARCHITECTURE.md             📐 Архитектурные диаграммы
├── UNITY_INTEGRATION.md        🎮 Гайд по Unity
├── SERVER_HUB_SPEC.md          🖥️  Спецификация сервера
└── src/                        💻 Исходный код
```

---

## 🔑 Ключевые интерфейсы

### IWorkstationClient

**Главный интерфейс для работы с модулем:**

```csharp
public interface IWorkstationClient
{
    // Подключение
    Task ConnectAsync(CancellationToken ct = default);
    Task RegisterAsync(WorkstationInfo info, CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);

    // Отправка данных
    Task SendLogAsync(LogEntry log, CancellationToken ct = default);
    Task SendSessionReportAsync(SessionReport report, CancellationToken ct = default);
    Task SendHeartbeatAsync(HeartbeatMessage heartbeat, CancellationToken ct = default);

    // Heartbeat управление
    void StartHeartbeat(TimeSpan interval);
    void StopHeartbeat();

    // События
    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    event EventHandler<SessionCommandReceivedEventArgs>? SessionCommandReceived;
    event EventHandler<ErrorEventArgs>? ErrorOccurred;

    // Свойства
    ConnectionState ConnectionState { get; }
    WorkstationInfo? WorkstationInfo { get; }
}
```

---

## 🚀 Расширенные возможности

### Буферизация при обрыве связи

```csharp
// Автоматически буферизует до 1000 сообщений
// При восстановлении связи отправит все

await client.SendLogAsync(log); // Сохранится в буфер если нет связи
```

### Автоматическое переподключение

```csharp
// Настраивается через опции
var options = new WorkstationClientOptions
{
    AutoReconnect = true,
    MaxReconnectAttempts = 10,
    ReconnectBaseDelaySeconds = 2.0, // Exponential backoff
    ReconnectMaxDelaySeconds = 60.0
};
```

### Приоритизация сообщений

```csharp
// Отчёты имеют высокий приоритет
// Будут отправлены первыми после переподключения
await client.SendSessionReportAsync(report); // Приоритет: 10
await client.SendLogAsync(log);              // Приоритет: 0
```

---

## 🧪 Тестирование без сервера

### Mock Transport для Unit-тестов

```csharp
var mockTransport = new Mock<ITransportClient>();
mockTransport.Setup(t => t.IsConnected).Returns(true);

var client = new WorkstationClient(
    mockTransport.Object,
    options,
    logger);

// Тестируйте логику без реального сервера
```

### Консольное демо с mock-данными

```bash
dotnet run --project src/Workstation.Comms.ConsoleDemo

# Выберите в меню:
# 4. Simulate user action
# 5. Simulate error
```

---

## 📚 Что дальше?

### Для Unity разработчиков
→ Читайте [UNITY_INTEGRATION.md](UNITY_INTEGRATION.md)

### Для серверных разработчиков
→ Читайте [SERVER_HUB_SPEC.md](SERVER_HUB_SPEC.md)

### Для архитекторов
→ Читайте [ARCHITECTURE.md](ARCHITECTURE.md)

### Для всех остальных
→ Читайте [README.md](README.md)

---

## ❓ Частые вопросы

### Q: Можно ли использовать без Unity?

**A:** Да! Консольное демо показывает использование в любом .NET приложении.

### Q: Какая версия .NET требуется?

**A:**
- **Библиотеки:** .NET Standard 2.1 (совместимо с Unity)
- **Консольное демо:** .NET 8.0
- **Сервер:** .NET 8.0 (ASP.NET Core)

### Q: Как изменить транспорт с SignalR на WebSocket?

**A:** Реализуйте `ITransportClient` для WebSocket и используйте вместо `SignalRTransportClient`.

### Q: Сколько рабочих мест может подключиться?

**A:** Зависит от сервера. SignalR поддерживает тысячи одновременных подключений. Для масштабирования используйте Redis backplane.

### Q: Что если интернет пропадёт?

**A:** Клиент автоматически переподключится с exponential backoff. Сообщения буферизуются и отправляются после восстановления связи.

### Q: Как отладить проблемы с подключением?

**A:**
1. Включите verbose logging: `verboseLogging = true`
2. Проверьте Unity Console / Application logs
3. Проверьте логи сервера
4. Используйте консольное демо для изоляции проблемы

---

## 🎉 Успехов!

Если возникли вопросы - обращайтесь к команде разработки.

**Happy Coding!** 🚂✨
