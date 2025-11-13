# Архитектурная документация

Детальное описание архитектуры модуля связи рабочих мест.

---

## 📐 Clean Architecture Layers

```
┌────────────────────────────────────────────────────────────────┐
│                     PRESENTATION LAYER                          │
│  ┌──────────────────────┐       ┌──────────────────────┐       │
│  │ Unity Adapter        │       │ Console Demo         │       │
│  │                      │       │                      │       │
│  │ - MonoBehaviour      │       │ - Test Application   │       │
│  │ - Unity Services     │       │ - Mock Scenarios     │       │
│  └──────────────────────┘       └──────────────────────┘       │
└────────────────────────────────────────────────────────────────┘
                           ▲
                           │ Depends on
                           │
┌────────────────────────────────────────────────────────────────┐
│                     APPLICATION LAYER                           │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │ WorkstationClient (Orchestration)                        │  │
│  │                                                          │  │
│  │ - Connection Management     - Auto Reconnection         │  │
│  │ - Registration              - Heartbeat Management      │  │
│  │ - Message Buffering         - Event Translation         │  │
│  │ - Retry Logic               - State Management          │  │
│  └──────────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │ MessageBuffer<T>                                         │  │
│  │ - Priority Queue            - Overflow Handling          │  │
│  └──────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────┘
                           ▲
                           │ Depends on
                           │
┌────────────────────────────────────────────────────────────────┐
│                    INFRASTRUCTURE LAYER                         │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │ SignalRTransportClient (ITransportClient)                │  │
│  │                                                          │  │
│  │ - HubConnection                - Custom Retry Policy    │  │
│  │ - WebSocket Transport          - Connection Events      │  │
│  │ - Auto Reconnection            - Message Routing        │  │
│  └──────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────┘
                           ▲
                           │ Depends on
                           │
┌────────────────────────────────────────────────────────────────┐
│                        DOMAIN LAYER                             │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │ Interfaces                                               │  │
│  │ - IWorkstationClient       - ITransportClient            │  │
│  └──────────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │ Entities (Domain Models)                                 │  │
│  │ - WorkstationInfo          - SessionCommand              │  │
│  │ - LogEntry                 - SessionReport               │  │
│  │ - HeartbeatMessage                                       │  │
│  └──────────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │ Enums                                                    │  │
│  │ - WorkstationType          - SubsystemType               │  │
│  │ - SessionCommandType       - ConnectionState             │  │
│  │ - SessionStatus            - LogLevel                    │  │
│  └──────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────┘
```

---

## 🔄 Sequence Diagram: Подключение и регистрация

```mermaid
sequenceDiagram
    participant Unity as Unity Application
    participant WS as WorkstationClient
    participant TR as SignalRTransport
    participant HUB as Server Hub
    participant DB as Database

    Unity->>WS: ConnectAsync()
    activate WS

    WS->>TR: StartAsync()
    activate TR

    TR->>HUB: WebSocket Handshake
    HUB-->>TR: Connection Accepted
    TR-->>WS: Connected Event
    WS-->>Unity: ConnectionState.Connected

    Unity->>WS: RegisterAsync(info)
    WS->>TR: SendAsync("RegisterWorkstation", info)
    TR->>HUB: RegisterWorkstation(info)

    HUB->>DB: Save Workstation Info
    DB-->>HUB: Saved

    HUB->>HUB: Add to Groups
    HUB-->>TR: Registration OK
    TR-->>WS: Message Received
    WS-->>Unity: ConnectionState.Registered

    WS->>WS: StartHeartbeat()
    deactivate TR
    deactivate WS

    loop Every 30 seconds
        WS->>TR: SendAsync("Heartbeat", msg)
        TR->>HUB: Heartbeat(msg)
        HUB->>DB: Update Last Heartbeat
    end
```

---

## 📤 Sequence Diagram: Отправка логов с буферизацией

```mermaid
sequenceDiagram
    participant Unity as Unity App
    participant WS as WorkstationClient
    participant BUF as MessageBuffer
    participant TR as SignalRTransport
    participant HUB as Server Hub

    Note over Unity,HUB: Normal Operation (Connected)

    Unity->>WS: SendLogAsync(log1)
    WS->>WS: Check Connection State
    WS->>TR: SendAsync("SendLog", log1)
    TR->>HUB: SendLog(log1)
    HUB-->>TR: OK

    Note over Unity,HUB: Connection Lost

    TR-->>WS: ConnectionStateChanged(false)
    WS->>WS: SetState(Reconnecting)

    Unity->>WS: SendLogAsync(log2)
    WS->>WS: Check Connection State
    WS->>BUF: Enqueue(log2)
    Note over BUF: log2 buffered

    Unity->>WS: SendLogAsync(log3)
    WS->>BUF: Enqueue(log3)
    Note over BUF: log3 buffered

    Note over Unity,HUB: Reconnection

    WS->>TR: TryReconnectAsync()
    TR->>HUB: WebSocket Reconnect
    HUB-->>TR: Connected
    TR-->>WS: ConnectionStateChanged(true)

    WS->>WS: FlushBuffersAsync()
    loop For each buffered message
        WS->>TR: SendAsync("SendLog", logN)
        TR->>HUB: SendLog(logN)
    end

    WS->>BUF: Clear()
```

---

## 📥 Sequence Diagram: Получение команды от инструктора

```mermaid
sequenceDiagram
    participant INS as Instructor UI
    participant API as API Controller
    participant CTX as HubContext
    participant HUB as WorkstationHub
    participant TR as SignalRTransport
    participant WS as WorkstationClient
    participant Unity as Unity App

    INS->>API: POST /api/start-session
    API->>API: Create SessionCommand

    API->>CTX: SendAsync("ReceiveSessionCommand", cmd)
    CTX->>HUB: Broadcast to clients
    HUB->>TR: ReceiveSessionCommand(cmd)

    TR->>TR: Trigger registered handler
    TR->>WS: OnSessionCommandReceived(cmd)

    WS->>WS: Validate command
    WS->>Unity: SessionCommandReceived Event
    Unity->>Unity: HandleCommand(cmd)

    alt StartSession
        Unity->>Unity: LoadScenario(cmd.ScenarioId)
    else StopSession
        Unity->>Unity: StopCurrentScenario()
        Unity->>WS: SendSessionReportAsync()
    else RequestReport
        Unity->>Unity: GenerateReport()
        Unity->>WS: SendSessionReportAsync()
    end
```

---

## 🧩 Component Diagram

```mermaid
graph TB
    subgraph "Unity Application"
        MB[MonoBehaviour:<br/>WorkstationClientBehaviour]
        US[UnityWorkstationService]
        SM[ScenarioManager]
        UI[UI Components]
    end

    subgraph "Application Core"
        WC[WorkstationClient]
        BUF[MessageBuffer]
        OPT[ClientOptions]
    end

    subgraph "Transport Layer"
        SRT[SignalRTransportClient]
        HC[HubConnection]
    end

    subgraph "Domain"
        IWC[IWorkstationClient]
        ITC[ITransportClient]
        ENT[Entities]
        ENUMS[Enums]
    end

    subgraph "Server Side"
        WH[WorkstationHub]
        SVC[WorkstationService]
        DB[(Database)]
    end

    MB --> US
    US --> WC
    SM --> MB
    UI --> MB

    WC -.implements.-> IWC
    WC --> BUF
    WC --> OPT
    WC --> ITC

    SRT -.implements.-> ITC
    SRT --> HC

    WC --> ENT
    WC --> ENUMS

    HC <-.SignalR<br/>WebSocket.-> WH
    WH --> SVC
    SVC --> DB

    style IWC fill:#e1f5ff
    style ITC fill:#e1f5ff
    style WC fill:#c8e6c9
    style SRT fill:#fff9c4
    style MB fill:#f8bbd0
```

---

## 🔐 Dependency Graph

```mermaid
graph LR
    Demo[Console Demo] --> App
    Unity[Unity Adapter] --> App

    App[Application Layer] --> Domain
    Infra[Infrastructure.SignalR] --> Domain

    Demo --> Infra
    Unity --> Infra

    Domain[Domain Layer]

    style Domain fill:#4caf50,color:#fff
    style App fill:#2196f3,color:#fff
    style Infra fill:#ff9800,color:#fff
    style Demo fill:#9c27b0,color:#fff
    style Unity fill:#e91e63,color:#fff
```

**Ключевые принципы:**
- Domain layer не зависит ни от чего
- Application зависит только от Domain
- Infrastructure зависит только от Domain
- Presentation слой зависит от Application и Infrastructure

---

## 🔄 State Machine: Connection States

```mermaid
stateDiagram-v2
    [*] --> Disconnected

    Disconnected --> Connecting: ConnectAsync()
    Connecting --> Connected: Success
    Connecting --> Failed: Error

    Connected --> Registered: RegisterAsync()
    Connected --> Disconnected: DisconnectAsync()

    Registered --> Reconnecting: Connection Lost
    Registered --> Disconnected: DisconnectAsync()

    Reconnecting --> Connected: Reconnect Success
    Reconnecting --> Failed: Max Attempts Reached

    Failed --> Connecting: Retry
    Failed --> Disconnected: Stop

    note right of Registered
        Normal operation state.
        Can send/receive messages.
        Heartbeat active.
    end note

    note right of Reconnecting
        Auto-reconnection in progress.
        Messages are buffered.
        Exponential backoff applied.
    end note
```

---

## 🗂 Data Flow Diagram

```mermaid
flowchart TD
    Start([User Action in Unity]) --> Log[Create LogEntry]
    Log --> Check{Connected?}

    Check -->|Yes| Send[Send to Server]
    Check -->|No| Buffer[Add to Buffer]

    Send --> Server[(Server)]
    Server --> DB[(Database)]

    Buffer --> Wait[Wait for Reconnection]
    Wait --> Reconnect{Reconnected?}

    Reconnect -->|Yes| Flush[Flush Buffer]
    Reconnect -->|No| Wait

    Flush --> SendBuffered[Send All Buffered]
    SendBuffered --> Server

    Server --> Process[Process & Store]
    Process --> Dashboard[Real-time Dashboard]
    Process --> Analytics[Analytics Engine]

    style Check fill:#fff3e0
    style Reconnect fill:#fff3e0
    style Buffer fill:#ffebee
    style DB fill:#e8f5e9
    style Dashboard fill:#e3f2fd
```

---

## 🎯 Design Patterns Used

### 1. **Repository Pattern**
- `IWorkstationClient` абстрагирует работу с коммуникацией
- Легко заменить реализацию (Mock для тестов)

### 2. **Adapter Pattern**
- `SignalRTransportClient` адаптирует SignalR к `ITransportClient`
- `UnityWorkstationService` адаптирует клиент для Unity

### 3. **Strategy Pattern**
- `ITransportClient` позволяет заменять транспорт (SignalR → WebSocket → HTTP)

### 4. **Observer Pattern**
- События: `ConnectionStateChanged`, `SessionCommandReceived`, `ErrorOccurred`

### 5. **Singleton Pattern** (в Unity)
- `WorkstationClientBehaviour` обычно единственный в сцене

### 6. **Decorator Pattern**
- Буферизация добавляет функциональность поверх транспорта

### 7. **Dependency Injection**
- Все зависимости передаются через конструкторы

---

## 📊 Performance Considerations

### Memory Management

```
MessageBuffer:
├── Default Size: 1000 messages
├── Memory per LogEntry: ~1-2 KB
├── Max Buffer Memory: ~1-2 MB
└── Overflow Strategy: Remove oldest
```

### Network Optimization

```
Heartbeat:
├── Interval: 30 seconds
├── Payload Size: ~200 bytes
├── Bandwidth: ~6.6 bytes/sec
└── Annual Traffic: ~200 MB/year per workstation
```

### Reconnection Strategy

```
Exponential Backoff:
├── Attempt 1: 2 seconds
├── Attempt 2: 4 seconds
├── Attempt 3: 8 seconds
├── Attempt 4: 16 seconds
├── Attempt 5: 32 seconds
└── Max Delay: 60 seconds
```

---

## 🔧 Extension Points

### Adding New Transport

```csharp
public class WebSocketTransportClient : ITransportClient
{
    // Implement WebSocket-specific logic
}

// Usage
var transport = new WebSocketTransportClient(options, logger);
var client = new WorkstationClient(transport, clientOptions, logger);
```

### Adding New Message Type

```csharp
// 1. Add to Domain/Entities
public record TelemetryMessage
{
    public string WorkstationId { get; init; }
    public Dictionary<string, double> Metrics { get; init; }
}

// 2. Add method to IWorkstationClient
Task SendTelemetryAsync(TelemetryMessage telemetry);

// 3. Implement in WorkstationClient
public async Task SendTelemetryAsync(TelemetryMessage telemetry)
{
    await _transportClient.SendAsync("SendTelemetry", telemetry);
}

// 4. Handle in Server Hub
[HubMethodName("SendTelemetry")]
public async Task SendTelemetry(TelemetryMessage telemetry)
{
    await _telemetryService.SaveAsync(telemetry);
}
```

### Adding Custom Event Handler

```csharp
// В Unity
_workstationClient.Service.Client.SessionCommandReceived += (s, e) =>
{
    if (e.Command.CommandType == SessionCommandType.CustomCommand)
    {
        HandleCustomCommand(e.Command);
    }
};
```

---

## 🧪 Testing Strategy

### Unit Tests

```csharp
[Test]
public async Task WorkstationClient_BuffersMessages_WhenDisconnected()
{
    // Arrange
    var mockTransport = new Mock<ITransportClient>();
    mockTransport.Setup(t => t.IsConnected).Returns(false);

    var client = new WorkstationClient(mockTransport.Object, options, logger);

    // Act
    await client.SendLogAsync(new LogEntry { Message = "test" });

    // Assert
    // Проверить, что сообщение буферизовано
}
```

### Integration Tests

```csharp
[Test]
public async Task EndToEnd_SendAndReceiveMessage()
{
    // Запустить тестовый сервер
    var server = CreateTestServer();

    // Создать реальный клиент
    var client = CreateRealClient(server.Url);

    // Отправить сообщение
    await client.SendLogAsync(log);

    // Проверить, что сервер получил
    Assert.That(server.ReceivedLogs, Contains.Item(log));
}
```

---

## 📈 Scalability

### Horizontal Scaling (Server Side)

```
┌──────────────┐     ┌──────────────┐
│ Workstation  │────▶│ Load         │
│ Client       │     │ Balancer     │
└──────────────┘     └──────┬───────┘
                            │
                ┌───────────┼───────────┐
                │           │           │
                ▼           ▼           ▼
         ┌──────────┐ ┌──────────┐ ┌──────────┐
         │ Hub      │ │ Hub      │ │ Hub      │
         │ Server 1 │ │ Server 2 │ │ Server 3 │
         └────┬─────┘ └────┬─────┘ └────┬─────┘
              │            │            │
              └────────────┼────────────┘
                           │
                           ▼
                    ┌─────────────┐
                    │ Redis       │
                    │ Backplane   │
                    └─────────────┘
```

**Setup:**
```csharp
builder.Services.AddSignalR()
    .AddStackExchangeRedis(config =>
    {
        config.Configuration.EndPoints.Add("redis-server:6379");
    });
```

---

## 🎓 Lessons Learned & Best Practices

### ✅ DO

- ✅ Use async/await everywhere for I/O operations
- ✅ Implement exponential backoff for reconnection
- ✅ Buffer messages during disconnection
- ✅ Use cancellation tokens
- ✅ Log all important events
- ✅ Handle all exceptions gracefully
- ✅ Validate input data
- ✅ Use strongly-typed hub methods

### ❌ DON'T

- ❌ Use `.Result` or `.Wait()` - causes deadlocks
- ❌ Ignore connection state changes
- ❌ Send large payloads (>1MB) through SignalR
- ❌ Block the UI thread
- ❌ Forget to dispose resources
- ❌ Hard-code configuration values
- ❌ Skip error handling

---

## 📚 References

- [ASP.NET Core SignalR Documentation](https://docs.microsoft.com/en-us/aspnet/core/signalr)
- [Clean Architecture by Robert C. Martin](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Unity Scripting API](https://docs.unity3d.com/ScriptReference/)

---

**Version:** 1.0
**Last Updated:** 2024-01-15
