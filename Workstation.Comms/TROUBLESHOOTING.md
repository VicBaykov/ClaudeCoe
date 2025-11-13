# Troubleshooting Guide

Руководство по устранению частых проблем.

---

## 🔧 Проблемы сборки

### ❌ Ошибка: CS0518 "System.Runtime.CompilerServices.IsExternalInit" не определен

**Проблема:**
```
error CS0518: Предопределенный тип "System.Runtime.CompilerServices.IsExternalInit" не определен или не импортирован
```

**Причина:**
Использование C# 9.0 `record` типов в .NET Standard 2.1 требует полифил для `IsExternalInit`.

**Решение:**
✅ **УЖЕ ИСПРАВЛЕНО** - файл `IsExternalInit.cs` добавлен в проект Domain.

Если проблема всё ещё возникает:
1. Убедитесь, что файл `src/Workstation.Comms.Domain/IsExternalInit.cs` существует
2. Пересоберите проект: `dotnet clean && dotnet build`

---

### ❌ Ошибка: Не удается найти пакет Microsoft.AspNetCore.SignalR.Client

**Проблема:**
```
error NU1101: Unable to find package Microsoft.AspNetCore.SignalR.Client
```

**Решение:**
```bash
# Восстановить все NuGet пакеты
dotnet restore

# Если не помогло, очистить кеш NuGet
dotnet nuget locals all --clear
dotnet restore
```

---

### ❌ Ошибка: TargetFramework 'netstandard2.1' не найден

**Проблема:**
```
error MSB3644: The reference assemblies for .NETStandard,Version=v2.1 were not found
```

**Решение:**
Установите .NET SDK 5.0 или выше:
```bash
# Проверить установленные версии
dotnet --list-sdks

# Скачать .NET SDK с официального сайта:
# https://dotnet.microsoft.com/download
```

---

## 🎮 Проблемы Unity

### ❌ DllNotFoundException: Unable to load DLL

**Проблема:**
```
DllNotFoundException: Unable to load DLL 'System.IO.Pipelines': The specified module could not be found.
```

**Причина:**
Отсутствуют зависимости SignalR в Unity проекте.

**Решение:**
1. Установите NuGet for Unity: https://github.com/GlitchEnzo/NuGetForUnity
2. Установите пакет: `Microsoft.AspNetCore.SignalR.Client` версии 8.0.0
3. Альтернатива: скопируйте все зависимости вручную из `bin/` в `Assets/Plugins/`

Список необходимых DLL:
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

---

### ❌ TypeLoadException: Could not load type

**Проблема:**
```
TypeLoadException: Could not load type 'Workstation.Comms.Domain.Entities.WorkstationInfo'
```

**Причина:**
Несовместимые версии DLL или проблемы с кешем Unity.

**Решение:**
1. Закройте Unity
2. Удалите папки `Library/` и `Temp/` в Unity проекте
3. Удалите все DLL из `Assets/Plugins/WorkstationComms/`
4. Пересоберите модуль: `dotnet build -c Release`
5. Скопируйте DLL заново
6. Откройте Unity (он пересоздаст кеш)

---

### ❌ Unity зависает при подключении

**Проблема:**
Unity Editor зависает при вызове `ConnectAsync()`.

**Причина:**
Использование `.Result` или `.Wait()` блокирует главный поток Unity.

**Решение:**
✅ **УЖЕ ИСПРАВЛЕНО** - все методы используют `async void` в MonoBehaviour.

Если проблема всё ещё возникает:
```csharp
// ❌ НЕПРАВИЛЬНО
public void Start()
{
    client.ConnectAsync().Wait(); // Блокирует поток!
}

// ✅ ПРАВИЛЬНО
public async void Start()
{
    await client.ConnectAsync(); // Асинхронно
}
```

---

## 🌐 Проблемы подключения

### ❌ Не удается подключиться к серверу

**Проблема:**
```
[ERROR] Failed to connect to server: Connection refused
```

**Диагностика:**
```bash
# 1. Проверить, запущен ли сервер
curl http://localhost:5000/ws/workstation

# 2. Проверить доступность порта
telnet localhost 5000

# 3. Проверить логи сервера
```

**Возможные причины и решения:**

1. **Сервер не запущен**
   - Запустите серверное приложение
   - Убедитесь, что хаб настроен: `app.MapHub<WorkstationHub>("/ws/workstation")`

2. **Неправильный URL**
   - Проверьте Server URL в конфигурации
   - Убедитесь, что Hub Path совпадает с сервером

3. **Файрволл/антивирус**
   - Временно отключите файрволл для тестирования
   - Добавьте исключение для приложения

4. **CORS проблемы** (если сервер на другом домене)
   ```csharp
   // На сервере добавьте CORS
   builder.Services.AddCors(options =>
   {
       options.AddDefaultPolicy(policy =>
       {
           policy.AllowAnyOrigin()
                 .AllowAnyHeader()
                 .AllowAnyMethod();
       });
   });
   ```

---

### ❌ WebSocket connection failed

**Проблема:**
```
[WARNING] WebSocket connection failed, falling back to long-polling
```

**Причина:**
Reverse proxy (nginx, IIS) не настроен для WebSocket.

**Решение для nginx:**
```nginx
location /ws/ {
    proxy_pass http://backend;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    proxy_set_header Host $host;
    proxy_cache_bypass $http_upgrade;
}
```

**Решение для IIS:**
1. Установите WebSocket Protocol: Server Manager → Add Features → WebSocket Protocol
2. Включите в web.config:
```xml
<system.webServer>
  <webSocket enabled="true" />
</system.webServer>
```

---

### ❌ Соединение обрывается каждые 30 секунд

**Проблема:**
Клиент постоянно переподключается.

**Причина:**
Таймауты SignalR слишком короткие или heartbeat не работает.

**Решение:**
Увеличьте таймауты в `SignalROptions`:
```csharp
new SignalROptions
{
    HandshakeTimeoutSeconds = 30,      // Было: 15
    KeepAliveIntervalSeconds = 30,     // Было: 15
    ServerTimeoutSeconds = 60          // Было: 30
}
```

На сервере:
```csharp
builder.Services.AddSignalR(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.KeepAliveInterval = TimeSpan.FromSeconds(30);
});
```

---

## 📤 Проблемы отправки данных

### ❌ Сообщения не доходят до сервера

**Диагностика:**
```csharp
// Проверить состояние соединения
Debug.Log($"Connection state: {client.ConnectionState}");

// Должно быть: Registered
```

**Причины:**

1. **Не зарегистрирован**
   ```csharp
   // Сначала зарегистрируйтесь
   await client.RegisterAsync(workstationInfo);

   // Потом отправляйте
   await client.SendLogAsync(logEntry);
   ```

2. **Сообщения буферизуются**
   - Это нормально при обрыве связи
   - Проверьте: `client.ConnectionState == ConnectionState.Reconnecting`
   - Сообщения отправятся автоматически после переподключения

3. **Размер сообщения слишком большой**
   ```csharp
   // ❌ Не отправляйте большие объекты
   var hugePayload = new string('x', 10_000_000); // 10 MB

   // ✅ Разбейте на части или сожмите
   ```

---

### ❌ Переполнен буфер сообщений

**Проблема:**
```
[WARNING] Message buffer is full (1000/1000)
```

**Причина:**
Слишком много сообщений отправлено при отсутствии соединения.

**Решение:**

1. **Увеличить размер буфера**
   ```csharp
   new WorkstationClientOptions
   {
       MaxBufferSize = 5000 // Было: 1000
   }
   ```

2. **Отправлять реже**
   ```csharp
   // Вместо каждого кадра
   void Update()
   {
       SendLog(); // ❌ 60 раз в секунду!
   }

   // Используйте таймер
   InvokeRepeating("SendLog", 0f, 1f); // ✅ Раз в секунду
   ```

3. **Batch отправка**
   ```csharp
   // Собирайте логи и отправляйте пачкой
   List<LogEntry> pendingLogs = new();

   void Update()
   {
       if (userAction)
           pendingLogs.Add(CreateLog());
   }

   async void FlushLogs()
   {
       foreach (var log in pendingLogs)
           await client.SendLogAsync(log);
       pendingLogs.Clear();
   }
   ```

---

## 🔐 Проблемы безопасности

### ❌ Unauthorized (401)

**Проблема:**
```
[ERROR] Failed to connect: Unauthorized (401)
```

**Причина:**
Сервер требует аутентификацию, но токен не предоставлен.

**Решение:**
```csharp
new SignalROptions
{
    ServerUrl = "https://server.com",
    HubPath = "/ws/workstation",
    AccessToken = "your-jwt-token-here" // Добавить токен
}
```

Получение токена (пример):
```csharp
// Запросить токен у API
var response = await httpClient.PostAsync("/api/auth/login", credentials);
var token = await response.Content.ReadAsStringAsync();

// Использовать в опциях
options.AccessToken = token;
```

---

## 🐛 Общие проблемы

### ❌ NullReferenceException в Unity

**Проблема:**
```
NullReferenceException: Object reference not set to an instance of an object
```

**Частые причины:**

1. **Клиент не инициализирован**
   ```csharp
   // Проверяйте перед использованием
   if (_workstationClient == null)
   {
       Debug.LogError("Client not initialized!");
       return;
   }
   ```

2. **Сервис не найден**
   ```csharp
   // FindObjectOfType может вернуть null
   _client = FindObjectOfType<WorkstationClientBehaviour>();
   if (_client == null)
   {
       Debug.LogError("WorkstationClientBehaviour not found in scene!");
   }
   ```

3. **Доступ к полям до подключения**
   ```csharp
   // Подождите регистрации
   await client.RegisterAsync(info);

   // Теперь можно использовать
   var id = client.WorkstationInfo.WorkstationId; // ✅
   ```

---

### ❌ Memory leak / утечка памяти

**Проблема:**
Память постоянно растёт со временем.

**Причина:**
Не вызывается Dispose у клиента.

**Решение:**
```csharp
// В Unity MonoBehaviour
private async void OnDestroy()
{
    if (_workstationClient?.Service != null)
    {
        await _workstationClient.Service.ShutdownAsync();
    }
}

private async void OnApplicationQuit()
{
    if (_workstationClient?.Service != null)
    {
        await _workstationClient.Service.ShutdownAsync();
    }
}
```

---

## 📊 Диагностика проблем

### Включение verbose logging

```csharp
// В Unity
public bool verboseLogging = true; // В Inspector

// В консольном приложении
loggerFactory.SetMinimumLevel(LogLevel.Debug);
```

### Проверка версий

```bash
# Проверить .NET SDK
dotnet --version

# Проверить версии NuGet пакетов
dotnet list package

# Проверить Unity версию
# Help → About Unity
```

### Тестирование без сервера

```bash
# Запустить консольное демо
cd src/Workstation.Comms.ConsoleDemo
dotnet run

# Оно покажет все ошибки подключения
# и позволит проверить клиентскую логику
```

---

## 🔍 Checklist для отладки

При возникновении проблемы проверьте:

- [ ] Сервер запущен и доступен
- [ ] URL и Hub Path настроены правильно
- [ ] Все DLL скопированы (для Unity)
- [ ] Не используется `.Result` или `.Wait()`
- [ ] Клиент зарегистрирован (`ConnectionState == Registered`)
- [ ] Логирование включено для диагностики
- [ ] Нет исключений в консоли/логах
- [ ] Файрволл не блокирует соединение
- [ ] CORS настроен (если кросс-доменный запрос)
- [ ] WebSocket поддерживается (reverse proxy)

---

## 📞 Дальнейшая помощь

Если проблема не решена:

1. Проверьте логи клиента (Unity Console / Application logs)
2. Проверьте логи сервера
3. Запустите консольное демо для изоляции проблемы
4. Создайте минимальный воспроизводящий пример
5. Обратитесь к команде разработки с:
   - Описанием проблемы
   - Логами
   - Версиями ПО
   - Конфигурацией

---

## 📚 Связанная документация

- [README.md](README.md) - общая документация
- [UNITY_INTEGRATION.md](UNITY_INTEGRATION.md) - Unity специфика
- [SERVER_HUB_SPEC.md](SERVER_HUB_SPEC.md) - серверная часть
- [QUICKSTART.md](QUICKSTART.md) - быстрый старт

---

**Успехов с отладкой!** 🔧✨
