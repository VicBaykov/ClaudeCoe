# Unity Integration Guide

Пошаговая инструкция по интеграции модуля связи в Unity проект.

## 📦 Шаг 1: Сборка модуля

### Вариант A: Релизная сборка (рекомендуется)

```bash
cd Workstation.Comms
dotnet build -c Release
```

Скомпилированные DLL будут в:
```
src/Workstation.Comms.Domain/bin/Release/netstandard2.1/
src/Workstation.Comms.Application/bin/Release/netstandard2.1/
src/Workstation.Comms.Infrastructure.SignalR/bin/Release/netstandard2.1/
src/Workstation.Comms.UnityAdapter/bin/Release/netstandard2.1/
```

### Вариант B: Debug сборка (для отладки)

```bash
dotnet build -c Debug
```

---

## 📂 Шаг 2: Копирование DLL в Unity

### 2.1 Создайте структуру папок в Unity проекте

```
Assets/
└── Plugins/
    └── WorkstationComms/
```

### 2.2 Скопируйте основные DLL модуля

Скопируйте следующие файлы в `Assets/Plugins/WorkstationComms/`:

```
✓ Workstation.Comms.Domain.dll
✓ Workstation.Comms.Application.dll
✓ Workstation.Comms.Infrastructure.SignalR.dll
✓ Workstation.Comms.UnityAdapter.dll
```

### 2.3 Скопируйте зависимости SignalR

**ВАЖНО**: SignalR клиент требует дополнительные зависимости. Установите их через NuGet Package Manager или скопируйте вручную:

#### Основные зависимости:
```
Microsoft.AspNetCore.SignalR.Client.dll
Microsoft.AspNetCore.SignalR.Client.Core.dll
Microsoft.AspNetCore.SignalR.Common.dll
Microsoft.AspNetCore.SignalR.Protocols.Json.dll
Microsoft.AspNetCore.Connections.Abstractions.dll
Microsoft.AspNetCore.Http.Connections.Client.dll
Microsoft.AspNetCore.Http.Connections.Common.dll
Microsoft.Extensions.Logging.Abstractions.dll
Microsoft.Extensions.DependencyInjection.Abstractions.dll
Microsoft.Extensions.Options.dll
System.Threading.Channels.dll
```

#### Альтернативный способ (NuGet for Unity):

1. Установите [NuGet for Unity](https://github.com/GlitchEnzo/NuGetForUnity)
2. Откройте NuGet Package Manager в Unity
3. Установите пакет: `Microsoft.AspNetCore.SignalR.Client` версии 8.0.0

---

## 🎮 Шаг 3: Настройка Unity сцены

### 3.1 Создайте GameObject для клиента

1. В Hierarchy создайте пустой GameObject
2. Назовите его `WorkstationClient`
3. Добавьте компонент `Workstation.Comms.UnityAdapter.Behaviours.WorkstationClientBehaviour`

### 3.2 Настройте параметры в Inspector

```yaml
Server Configuration:
  Server URL: "http://your-instructor-server:5000"
  Hub Path: "/ws/workstation"

Workstation Configuration:
  Workstation ID: "WS-GANGWAY-01"        # Уникальный ID
  Display Name: "Gangway Station 1"      # Человекочитаемое имя
  Workstation Type: Hybrid               # Touch / VR / Hybrid
  Subsystem Type: Gangway                # Gangway / Doors / HVAC и т.д.

Connection Settings:
  Auto Connect On Start: ✓               # Подключаться при старте
  Heartbeat Interval Seconds: 30         # Интервал heartbeat

Debug:
  Verbose Logging: ✓                     # Подробные логи
```

### 3.3 Сделайте GameObject персистентным (опционально)

Если нужно сохранить подключение между сценами:

```csharp
void Awake()
{
    DontDestroyOnLoad(gameObject);
}
```

---

## 📝 Шаг 4: Написание кода интеграции

### 4.1 Получение ссылки на клиент

```csharp
using UnityEngine;
using Workstation.Comms.UnityAdapter.Behaviours;
using Workstation.Comms.Domain.Entities;
using Workstation.Comms.Domain.Enums;

public class GameManager : MonoBehaviour
{
    private WorkstationClientBehaviour _workstationClient;

    void Start()
    {
        // Получить ссылку на клиент
        _workstationClient = FindObjectOfType<WorkstationClientBehaviour>();

        if (_workstationClient == null)
        {
            Debug.LogError("WorkstationClientBehaviour not found in scene!");
            return;
        }

        // Подписаться на события
        SubscribeToEvents();
    }

    void SubscribeToEvents()
    {
        _workstationClient.OnSessionCommandReceived += HandleSessionCommand;
        _workstationClient.OnConnectionStateChanged += HandleConnectionStateChanged;
    }
}
```

### 4.2 Обработка команд от инструктора

```csharp
void HandleSessionCommand(SessionCommand command)
{
    Debug.Log($"Received command: {command.CommandType}");

    switch (command.CommandType)
    {
        case SessionCommandType.StartSession:
            StartTrainingSession(command.ScenarioId, command.UserId);
            break;

        case SessionCommandType.StopSession:
            StopTrainingSession();
            break;

        case SessionCommandType.PauseSession:
            PauseTrainingSession();
            break;

        case SessionCommandType.ResumeSession:
            ResumeTrainingSession();
            break;

        case SessionCommandType.RequestReport:
            SendSessionReport();
            break;

        case SessionCommandType.SetSubsystem:
            if (command.TargetSubsystem.HasValue)
            {
                SwitchSubsystem(command.TargetSubsystem.Value);
            }
            break;

        default:
            Debug.LogWarning($"Unhandled command: {command.CommandType}");
            break;
    }
}

void StartTrainingSession(string scenarioId, string userId)
{
    Debug.Log($"Starting training session: {scenarioId} for user {userId}");

    // Ваша логика загрузки сценария
    // ScenarioLoader.LoadScenario(scenarioId);
}
```

### 4.3 Логирование действий пользователя

```csharp
public class UserInteractionTracker : MonoBehaviour
{
    private WorkstationClientBehaviour _client;

    void Start()
    {
        _client = FindObjectOfType<WorkstationClientBehaviour>();
    }

    // Вызывается при нажатии кнопки
    public void OnButtonClick(string buttonId)
    {
        _client?.LogUserAction(
            $"ButtonClick:{buttonId}",
            $"{{\"buttonId\": \"{buttonId}\", \"timestamp\": \"{System.DateTime.UtcNow:O}\"}}"
        );
    }

    // Вызывается при взаимодействии с инструментом
    public void OnToolUsed(string toolId, Vector3 position)
    {
        string payload = $"{{\"toolId\": \"{toolId}\", \"position\": {{\"x\": {position.x}, \"y\": {position.y}, \"z\": {position.z}}}}}";
        _client?.LogUserAction($"ToolUsed:{toolId}", payload);
    }

    // Вызывается при выполнении шага сценария
    public void OnStepCompleted(string stepId, bool success)
    {
        string payload = $"{{\"stepId\": \"{stepId}\", \"success\": {success.ToString().ToLower()}}}";
        _client?.LogEvent("ScenarioProgress", $"Step {stepId} completed", LogLevel.Info);
    }
}
```

### 4.4 Отправка отчета о сессии

```csharp
public class SessionReportGenerator : MonoBehaviour
{
    private WorkstationClientBehaviour _client;
    private string _currentSessionId;
    private DateTime _sessionStartTime;
    private List<ScenarioStep> _completedSteps = new List<ScenarioStep>();

    void Start()
    {
        _client = FindObjectOfType<WorkstationClientBehaviour>();
    }

    public void StartSession(string sessionId)
    {
        _currentSessionId = sessionId;
        _sessionStartTime = DateTime.UtcNow;
        _completedSteps.Clear();
    }

    public void AddCompletedStep(string stepId, string stepName, bool success, int attempts)
    {
        _completedSteps.Add(new ScenarioStep
        {
            StepId = stepId,
            StepName = stepName,
            IsSuccess = success,
            Attempts = attempts,
            StartTime = DateTime.UtcNow.AddMinutes(-5), // Примерно
            EndTime = DateTime.UtcNow
        });
    }

    public async void SendReport()
    {
        if (string.IsNullOrEmpty(_currentSessionId)) return;

        var report = new SessionReport
        {
            SessionId = _currentSessionId,
            WorkstationId = _client.WorkstationInfo?.WorkstationId ?? "Unknown",
            UserId = "current-user-id", // Получить из вашей системы
            ScenarioId = "current-scenario-id", // Получить из вашей системы
            Status = SessionStatus.Completed,
            StartTime = _sessionStartTime,
            EndTime = DateTime.UtcNow,
            DurationSeconds = (DateTime.UtcNow - _sessionStartTime).TotalSeconds,
            CompletedSteps = _completedSteps,
            Score = CalculateScore()
        };

        await _client.Service.SendSessionReportAsync(report);
    }

    double CalculateScore()
    {
        if (_completedSteps.Count == 0) return 0;

        int successCount = _completedSteps.Count(s => s.IsSuccess);
        return (successCount / (double)_completedSteps.Count) * 100.0;
    }
}
```

### 4.5 Обработка состояний соединения

```csharp
void HandleConnectionStateChanged(ConnectionState state)
{
    Debug.Log($"Connection state changed to: {state}");

    switch (state)
    {
        case ConnectionState.Connecting:
            ShowConnectionIndicator("Подключение...");
            break;

        case ConnectionState.Connected:
            ShowConnectionIndicator("Подключено");
            break;

        case ConnectionState.Registered:
            ShowConnectionIndicator("Готов к работе");
            EnableTraining(true);
            break;

        case ConnectionState.Reconnecting:
            ShowConnectionIndicator("Переподключение...");
            EnableTraining(false);
            break;

        case ConnectionState.Failed:
            ShowConnectionIndicator("Ошибка подключения");
            EnableTraining(false);
            break;

        case ConnectionState.Disconnected:
            ShowConnectionIndicator("Отключено");
            EnableTraining(false);
            break;
    }
}

void ShowConnectionIndicator(string message)
{
    // Показать UI индикатор
    // connectionStatusText.text = message;
}

void EnableTraining(bool enabled)
{
    // Включить/выключить возможность обучения
    // trainingPanel.interactable = enabled;
}
```

---

## 🔍 Шаг 5: Тестирование

### 5.1 Проверка в Unity Editor

1. Запустите сцену
2. Проверьте Console на наличие ошибок
3. Убедитесь, что видите логи подключения:
   ```
   [WorkstationClient] Connecting to server at http://localhost:5000
   [WorkstationClient] Successfully connected to server
   [WorkstationClient] Successfully registered workstation
   ```

### 5.2 Тестирование без сервера (Mock)

Если сервер ещё не готов, можно временно отключить автоподключение и тестировать локально:

```csharp
// В Inspector отключите "Auto Connect On Start"
// Затем в коде:

public class MockTesting : MonoBehaviour
{
    void Start()
    {
        // Не подключаемся к серверу
        // Тестируем только локальную логику
        TestLocalScenarioLogic();
    }
}
```

---

## 🚨 Troubleshooting

### Ошибка: "DllNotFoundException: Unable to load DLL 'System.IO.Pipelines'"

**Решение**: Убедитесь, что все зависимости SignalR скопированы в `Assets/Plugins/`

### Ошибка: "TypeLoadException: Could not load type"

**Решение**:
1. Проверьте версии DLL - они должны быть совместимы
2. Убедитесь, что используете .NET Standard 2.1
3. Очистите кеш Unity: `Assets → Reimport All`

### Ошибка: Unity зависает при подключении

**Решение**: Проверьте, что все асинхронные вызовы используют `async void` в MonoBehaviour и `await` вместо `.Result`

### WebSocket connection failed

**Решение**:
1. Проверьте URL сервера
2. Проверьте, что сервер запущен
3. Проверьте файрволл
4. Попробуйте отключить антивирус временно

---

## 📊 Пример полной интеграции

См. файл `WorkstationClientBehaviour.cs` для полного рабочего примера MonoBehaviour компонента.

Основные точки интеграции:
- ✅ Автоматическое подключение при старте
- ✅ Обработка всех типов команд
- ✅ Логирование действий пользователя
- ✅ Отправка отчетов о сессии
- ✅ Управление состоянием соединения
- ✅ Graceful shutdown при выходе

---

## 📞 Поддержка

При возникновении проблем:
1. Проверьте логи Unity Console
2. Проверьте логи сервера
3. Обратитесь к команде разработки

---

**Успешной интеграции!** 🚀
