using System;

namespace Workstation.Comms.Infrastructure.SignalR.Configuration
{
    /// <summary>
    /// Настройки SignalR транспорта
    /// </summary>
    public class SignalROptions
    {
        /// <summary>
        /// URL сервера (например, "https://instructor.example.com")
        /// </summary>
        public string ServerUrl { get; set; } = "http://localhost:5000";

        /// <summary>
        /// Путь к хабу (например, "/ws/workstation")
        /// </summary>
        public string HubPath { get; set; } = "/ws/workstation";

        /// <summary>
        /// Полный URL хаба
        /// </summary>
        public string HubUrl => $"{ServerUrl.TrimEnd('/')}/{HubPath.TrimStart('/')}";

        /// <summary>
        /// Таймаут handshake (секунды)
        /// </summary>
        public int HandshakeTimeoutSeconds { get; set; } = 15;

        /// <summary>
        /// Интервал KeepAlive (секунды)
        /// </summary>
        public int KeepAliveIntervalSeconds { get; set; } = 15;

        /// <summary>
        /// Таймаут сервера (секунды)
        /// </summary>
        public int ServerTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Автоматическое переподключение
        /// </summary>
        public bool EnableAutoReconnect { get; set; } = true;

        /// <summary>
        /// Максимальная задержка при переподключении (миллисекунды)
        /// </summary>
        public int MaxReconnectDelayMilliseconds { get; set; } = 60000;

        /// <summary>
        /// Токен авторизации (если требуется)
        /// </summary>
        public string? AccessToken { get; set; }
    }
}
