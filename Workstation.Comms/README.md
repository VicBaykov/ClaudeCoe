# Workstation Communications Module

Модуль связи для рабочих мест тренажёра по обслуживанию подсистем поезда. Обеспечивает двустороннюю коммуникацию между рабочими местами (Unity-приложениями) и модулем инструктора (ASP.NET Core).

## 📋 Содержание

- [Обзор архитектуры](#обзор-архитектуры)
- [Структура проекта](#структура-проекта)
- [Протокол общения](#протокол-общения)
- [Быстрый старт](#быстрый-старт)
- [Интеграция с Unity](#интеграция-с-unity)
- [Консольное демо](#консольное-демо)
- [API Reference](#api-reference)
- [Конфигурация](#конфигурация)

---

## 🏗 Обзор архитектуры

Модуль построен по принципам **Clean Architecture** с четким разделением слоев:

```
┌─────────────────────────────────────────────────────────────┐
│                    Presentation Layer                        │
│  ┌─────────────────────┐      ┌─────────────────────┐       │
│  │  Unity Adapter      │      │  Console Demo       │       │
│  │  (MonoBehaviour)    │      │  (Test App)         │       │
│  └─────────────────────┘      └─────────────────────┘       │
├─────────────────────────────────────────────────────────────┤
│                    Application Layer                         │
│  ┌──────────────────────────────────────────────────┐       │
│  │  WorkstationClient (Use Cases)                   │       │
│  │  - Connection management                         │       │
│  │  - Message buffering                             │       │
│  │  - Auto-reconnection                             │       │
│  └──────────────────────────────────────────────────┘       │
├─────────────────────────────────────────────────────────────┤
│                    Infrastructure Layer                      │
│  ┌──────────────────────────────────────────────────┐       │
│  │  SignalRTransportClient                          │       │
│  │  - WebSocket transport                           │       │
│  │  - Automatic reconnection                        │       │
│  └──────────────────────────────────────────────────┘       │
├─────────────────────────────────────────────────────────────┤
│                    Domain Layer                              │
│  ┌──────────────────────────────────────────────────┐       │
│  │  Interfaces & Models                             │       │
│  │  - IWorkstationClient, ITransportClient          │       │
│  │  - WorkstationInfo, SessionCommand, LogEntry     │       │
│  └──────────────────────────────────────────────────┘       │
└─────────────────────────────────────────────────────────────┘
```

### Ключевые принципы

1. **Dependency Inversion**: Внешние слои зависят от внутренних через интерфейсы
2. **Separation of Concerns**: Каждый слой имеет четкую ответственность
3. **Testability**: Легко тестировать благодаря абстракциям
4. **Platform Independence**: Domain и Application слои независимы от платформы

---

## 📁 Структура проекта

```
Workstation.Comms/
├── Workstation.Comms.sln
├── README.md
└── src/
    ├── Workstation.Comms.Domain/              # Доменный слой
    │   ├── Entities/
    │   │   ├── WorkstationInfo.cs             # Информация о рабочем месте
    │   │   ├── SessionCommand.cs              # Команда от инструктора
    │   │   ├── LogEntry.cs                    # Запись лога
    │   │   ├── SessionReport.cs               # Отчет о сессии
    │   │   └── HeartbeatMessage.cs            # Heartbeat сообщение
    │   ├── Enums/
    │   │   ├── SessionCommandType.cs          # Типы команд
    │   │   ├── WorkstationType.cs             # Типы рабочих мест
    │   │   ├── SubsystemType.cs               # Типы подсистем
    │   │   ├── ConnectionState.cs             # Состояния соединения
    │   │   ├── SessionStatus.cs               # Статусы сессии
    │   │   └── LogLevel.cs                    # Уровни логирования
    │   └── Interfaces/
    │       ├── IWorkstationClient.cs          # Интерфейс клиента
    │       └── ITransportClient.cs            # Интерфейс транспорта
    │
    ├── Workstation.Comms.Application/         # Слой приложения
    │   ├── Services/
    │   │   └── WorkstationClient.cs           # Основная реализация клиента
    │   ├── Configuration/
    │   │   └── WorkstationClientOptions.cs    # Настройки клиента
    │   └── Buffers/
    │       └── MessageBuffer.cs               # Буфер сообщений
    │
    ├── Workstation.Comms.Infrastructure.SignalR/  # Транспортный слой
    │   ├── SignalRTransportClient.cs          # SignalR реализация
    │   └── Configuration/
    │       └── SignalROptions.cs              # Настройки SignalR
    │
    ├── Workstation.Comms.UnityAdapter/        # Unity адаптер
    │   ├── Services/
    │   │   └── UnityWorkstationService.cs     # Сервис для Unity
    │   └── Behaviours/
    │       └── WorkstationClientBehaviour.cs  # MonoBehaviour компонент
    │
    └── Workstation.Comms.ConsoleDemo/         # Консольное демо
        └── Program.cs                          # Тестовое приложение
```

---

## 🔌 Протокол общения

### Выбранный транспорт: **SignalR over WebSocket**

#### Обоснование выбора:

| Критерий | SignalR | WebSocket | HTTP Long-Polling |
|----------|---------|-----------|-------------------|
| Двусторонняя связь | ✅ Встроенная | ✅ Да | ⚠️ Сложная |
| Автоматическое переподключение | ✅ Встроенное | ❌ Вручную | ❌ Вручную |
| Fallback механизм | ✅ Автоматически | ❌ Нет | N/A |
| RPC-стиль вызовов | ✅ Typed Hubs | ❌ Вручную | ❌ Вручную |
| Поддержка групп | ✅ Встроенная | ❌ Вручную | ❌ Вручную |
| Heartbeat | ✅ Встроенный | ⚠️ Вручную | ⚠️ Вручную |

### Сообщения протокола

#### 1. Регистрация рабочего места

**Клиент → Сервер** (метод `RegisterWorkstation`)

```json
{
  "workstationId": "WS-001",
  "displayName": "Gangway Training Station",
  "workstationType": 3,  // Hybrid
  "subsystemType": 1,    // Gangway
  "clientVersion": "1.0.0",
  "registeredAt": "2024-01-15T10:30:00Z"
}
```

#### 2. Heartbeat

**Клиент → Сервер** (метод `Heartbeat`)

```json
{
  "workstationId": "WS-001",
  "timestamp": "2024-01-15T10:30:00Z",
  "activeSessionId": "session-123",
  "status": "InSession"
}
```

#### 3. Команды от инструктора

**Сервер → Клиент** (метод `ReceiveSessionCommand`)

```json
{
  "commandId": "cmd-456",
  "commandType": 1,  // StartSession
  "sessionId": "session-123",
  "scenarioId": "gangway-basic-maintenance",
  "userId": "user-789",
  "timestamp": "2024-01-15T10:35:00Z",
  "priority": 5
}
```

Типы команд:
- `1` - StartSession
- `2` - StopSession
- `3` - PauseSession
- `4` - ResumeSession
- `5` - RequestReport
- `6` - SetSubsystem
- `7` - LoadScenario
- `8` - ResetWorkstation
- `9` - RequestStatus

#### 4. Отправка логов

**Клиент → Сервер** (метод `SendLog`)

```json
{
  "workstationId": "WS-001",
  "sessionId": "session-123",
  "level": 1,  // Info
  "category": "UserAction",
  "message": "User opened tool panel",
  "payload": "{\"toolId\": \"wrench-01\", \"position\": {\"x\": 100, \"y\": 200}}",
  "timestamp": "2024-01-15T10:36:00Z"
}
```

#### 5. Отправка отчета о сессии

**Клиент → Сервер** (метод `SendSessionReport`)

```json
{
  "sessionId": "session-123",
  "workstationId": "WS-001",
  "userId": "user-789",
  "scenarioId": "gangway-basic-maintenance",
  "status": 3,  // Completed
  "startTime": "2024-01-15T10:35:00Z",
  "endTime": "2024-01-15T10:50:00Z",
  "durationSeconds": 900,
  "score": 87.5,
  "completedSteps": [
    {
      "stepId": "step-1",
      "stepName": "Inspect connections",
      "startTime": "2024-01-15T10:35:00Z",
      "endTime": "2024-01-15T10:40:00Z",
      "isSuccess": true,
      "attempts": 1
    }
  ],
  "errors": []
}
```

---

## 🚀 Быстрый старт

### 1. Сборка проекта

```bash
# Клонировать репозиторий
cd Workstation.Comms

# Восстановить зависимости
dotnet restore

# Собрать solution
dotnet build

# Запустить тесты (когда появятся)
dotnet test
```

### 2. Запуск консольного демо

```bash
cd src/Workstation.Comms.ConsoleDemo
dotnet run
```

**Примечание**: Для работы нужен запущенный сервер инструктора на `http://localhost:5000` с хабом `/ws/workstation`.

---

## 🎮 Интеграция с Unity

### Шаг 1: Копирование DLL

После сборки скопируйте следующие DLL в Unity проект (папка `Assets/Plugins/`):

```
Workstation.Comms.Domain.dll
Workstation.Comms.Application.dll
Workstation.Comms.Infrastructure.SignalR.dll
Workstation.Comms.UnityAdapter.dll
Microsoft.AspNetCore.SignalR.Client.dll (+ зависимости)
```

### Шаг 2: Создание GameObject

1. Создайте пустой GameObject в сцене
2. Добавьте компонент `WorkstationClientBehaviour`

### Шаг 3: Настройка компонента

В Inspector настройте параметры:

```
Server Configuration:
  Server URL: http://instructor-server:5000
  Hub Path: /ws/workstation

Workstation Configuration:
  Workstation ID: WS-GANGWAY-01
  Display Name: Gangway Training Station
  Workstation Type: Hybrid
  Subsystem Type: Gangway

Connection Settings:
  Auto Connect On Start: ✓
  Heartbeat Interval Seconds: 30
```

### Шаг 4: Подписка на события

```csharp
using UnityEngine;
using Workstation.Comms.Domain.Entities;
using Workstation.Comms.Domain.Enums;
using Workstation.Comms.UnityAdapter.Behaviours;

public class ScenarioManager : MonoBehaviour
{
    private WorkstationClientBehaviour _workstationClient;

    void Start()
    {
        _workstationClient = FindObjectOfType<WorkstationClientBehaviour>();

        // Подписаться на команды от инструктора
        _workstationClient.OnSessionCommandReceived += HandleSessionCommand;
        _workstationClient.OnConnectionStateChanged += HandleConnectionStateChanged;
    }

    void HandleSessionCommand(SessionCommand command)
    {
        switch (command.CommandType)
        {
            case SessionCommandType.StartSession:
                LoadScenario(command.ScenarioId);
                break;

            case SessionCommandType.StopSession:
                StopCurrentScenario();
                break;

            // ... другие команды
        }
    }

    void HandleConnectionStateChanged(ConnectionState state)
    {
        Debug.Log($"Connection state: {state}");

        if (state == ConnectionState.Registered)
        {
            // Готовы к работе
        }
    }

    void OnUserAction(string action)
    {
        // Отправить лог действия пользователя
        _workstationClient.LogUserAction(action, "User performed action in VR");
    }
}
```

### Шаг 5: Логирование событий

```csharp
// Простой лог
_workstationClient.LogEvent("UserAction", "Button clicked", LogLevel.Info);

// Действие пользователя
_workstationClient.LogUserAction("OpenToolPanel", "{\"toolId\": \"wrench-01\"}");
```

---

## 💻 Консольное демо

Консольное приложение для тестирования без Unity:

```bash
dotnet run --project src/Workstation.Comms.ConsoleDemo
```

### Возможности:

1. **Автоматическая регистрация** на сервере
2. **Периодическая отправка логов** (каждые 10 секунд)
3. **Интерактивное меню**:
   - Отправка тестовых логов
   - Отправка mock-отчетов
   - Просмотр статуса соединения
   - Симуляция действий пользователя
   - Симуляция ошибок

4. **Обработка команд** от инструктора в реальном времени

---

## 📚 API Reference

### IWorkstationClient

Основной интерфейс для работы с модулем связи.

#### Методы

```csharp
// Подключиться к серверу
Task ConnectAsync(CancellationToken cancellationToken = default);

// Зарегистрировать рабочее место
Task RegisterAsync(WorkstationInfo workstationInfo, CancellationToken cancellationToken = default);

// Отправить лог
Task SendLogAsync(LogEntry logEntry, CancellationToken cancellationToken = default);

// Отправить отчет о сессии
Task SendSessionReportAsync(SessionReport report, CancellationToken cancellationToken = default);

// Отправить heartbeat
Task SendHeartbeatAsync(HeartbeatMessage heartbeat, CancellationToken cancellationToken = default);

// Отключиться
Task DisconnectAsync(CancellationToken cancellationToken = default);

// Управление heartbeat
void StartHeartbeat(TimeSpan interval);
void StopHeartbeat();
```

#### События

```csharp
// Изменение состояния соединения
event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

// Получение команды от инструктора
event EventHandler<SessionCommandReceivedEventArgs>? SessionCommandReceived;

// Ошибка
event EventHandler<ErrorEventArgs>? ErrorOccurred;
```

#### Свойства

```csharp
// Текущее состояние соединения
ConnectionState ConnectionState { get; }

// Информация о рабочем месте
WorkstationInfo? WorkstationInfo { get; }
```

---

## ⚙️ Конфигурация

### WorkstationClientOptions

```csharp
new WorkstationClientOptions
{
    ServerUrl = "http://localhost:5000",
    HubPath = "/ws/workstation",
    HeartbeatInterval = TimeSpan.FromSeconds(30),
    ConnectionTimeout = TimeSpan.FromSeconds(30),
    MaxReconnectAttempts = 10,
    ReconnectBaseDelaySeconds = 2.0,
    ReconnectMaxDelaySeconds = 60.0,
    MaxBufferSize = 1000,
    AutoReconnect = true,
    AutoStartHeartbeat = true
};
```

### SignalROptions

```csharp
new SignalROptions
{
    ServerUrl = "http://localhost:5000",
    HubPath = "/ws/workstation",
    HandshakeTimeoutSeconds = 15,
    KeepAliveIntervalSeconds = 15,
    ServerTimeoutSeconds = 30,
    EnableAutoReconnect = true,
    MaxReconnectDelayMilliseconds = 60000,
    AccessToken = "your-jwt-token" // опционально
};
```

---

## 🔧 Нефункциональные требования

### ✅ Асинхронность

- Все операции I/O используют `async`/`await`
- Никаких блокирующих вызовов `.Result` или `.Wait()`
- Поддержка `CancellationToken` для отмены операций

### ✅ Устойчивость к сбоям

- **Автоматическое переподключение** с exponential backoff
- **Буферизация сообщений** при обрыве связи (до 1000 сообщений по умолчанию)
- **Приоритизация отчетов** над обычными логами
- **Graceful degradation** - клиент продолжает работать при потере связи

### ✅ Логирование

- Использование `Microsoft.Extensions.Logging.ILogger`
- Логирование всех ключевых событий:
  - Подключение/отключение
  - Регистрация
  - Отправка/получение сообщений
  - Ошибки с stack trace

### ✅ Конфигурируемость

- URL сервера
- Параметры таймаутов
- Интервал heartbeat
- Размер буфера
- Политика переподключения

---

## 🛠 Расширение функциональности

### Добавление нового типа команды

1. Добавить enum в `SessionCommandType.cs`:
```csharp
public enum SessionCommandType
{
    // ... существующие
    CustomCommand = 100
}
```

2. Обработать в Unity или консольном приложении:
```csharp
case SessionCommandType.CustomCommand:
    HandleCustomCommand(command);
    break;
```

### Добавление нового транспорта

1. Реализовать `ITransportClient`:
```csharp
public class WebSocketTransportClient : ITransportClient
{
    // Реализация методов
}
```

2. Использовать вместо SignalR:
```csharp
var transport = new WebSocketTransportClient(options, logger);
var client = new WorkstationClient(transport, clientOptions, logger);
```

---

## 📊 Диаграмма последовательности

```
Workstation          Transport (SignalR)         Instructor Server
    |                        |                           |
    |-- ConnectAsync() ----->|                           |
    |                        |<---- WebSocket Handshake ->|
    |<-- Connected ----------|                           |
    |                        |                           |
    |-- RegisterAsync() ---->|                           |
    |                        |-- RegisterWorkstation --->|
    |                        |<-- Registration OK -------|
    |<-- Registered ---------|                           |
    |                        |                           |
    |-- StartHeartbeat() --->|                           |
    |   (every 30s)          |                           |
    |                        |-- Heartbeat ------------->|
    |                        |                           |
    |                        |<-- ReceiveSessionCommand -|
    |<-- OnCommandReceived --|                           |
    |                        |                           |
    |-- SendLogAsync() ----->|                           |
    |                        |-- SendLog --------------->|
    |                        |                           |
    |-- SendReportAsync() -->|                           |
    |                        |-- SendSessionReport ----->|
    |                        |                           |
```

---

## 🐛 Troubleshooting

### Проблема: Не удается подключиться к серверу

**Решение:**
1. Проверьте, что сервер запущен
2. Проверьте URL и путь к хабу
3. Проверьте файрволл/антивирус
4. Проверьте логи клиента для деталей ошибки

### Проблема: Сообщения не доходят до сервера

**Решение:**
1. Проверьте состояние соединения: `client.ConnectionState`
2. Убедитесь, что рабочее место зарегистрировано
3. Проверьте буфер сообщений - возможно, они буферизуются
4. Проверьте логи на наличие исключений

### Проблема: Unity зависает при подключении

**Решение:**
1. Используйте `async void` в MonoBehaviour для асинхронных вызовов
2. Не используйте `.Result` или `.Wait()` - только `await`
3. Обрабатывайте исключения в `try-catch`

---

## 📝 Лицензия

Proprietary - для внутреннего использования в проекте тренажера.

---

## 👥 Контакты

По вопросам обращаться к команде разработки.

---

**Версия документа:** 1.0
**Дата:** 2024-01-15
