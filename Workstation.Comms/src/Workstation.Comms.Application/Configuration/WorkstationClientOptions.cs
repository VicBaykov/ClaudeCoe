using System;

namespace Workstation.Comms.Application.Configuration
{
    /// <summary>
    /// Настройки клиента рабочего места
    /// </summary>
    public class WorkstationClientOptions
    {
        /// <summary>
        /// URL сервера инструктора
        /// </summary>
        public string ServerUrl { get; set; } = "http://localhost:5000";

        /// <summary>
        /// Путь к хабу SignalR (например, "/ws/workstation")
        /// </summary>
        public string HubPath { get; set; } = "/ws/workstation";

        /// <summary>
        /// Интервал отправки heartbeat
        /// </summary>
        public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Таймаут подключения
        /// </summary>
        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Максимальное количество попыток переподключения
        /// </summary>
        public int MaxReconnectAttempts { get; set; } = 10;

        /// <summary>
        /// Базовая задержка для exponential backoff (секунды)
        /// </summary>
        public double ReconnectBaseDelaySeconds { get; set; } = 2.0;

        /// <summary>
        /// Максимальная задержка между попытками переподключения (секунды)
        /// </summary>
        public double ReconnectMaxDelaySeconds { get; set; } = 60.0;

        /// <summary>
        /// Максимальный размер буфера для логов/отчётов при обрыве связи
        /// </summary>
        public int MaxBufferSize { get; set; } = 1000;

        /// <summary>
        /// Автоматически переподключаться при обрыве
        /// </summary>
        public bool AutoReconnect { get; set; } = true;

        /// <summary>
        /// Автоматически начинать heartbeat после регистрации
        /// </summary>
        public bool AutoStartHeartbeat { get; set; } = true;
    }
}
