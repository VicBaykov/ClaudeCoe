namespace Workstation.Comms.Domain.Enums
{
    /// <summary>
    /// Состояние соединения с сервером инструктора
    /// </summary>
    public enum ConnectionState
    {
        /// <summary>
        /// Отключено
        /// </summary>
        Disconnected = 0,

        /// <summary>
        /// Подключение в процессе
        /// </summary>
        Connecting = 1,

        /// <summary>
        /// Подключено
        /// </summary>
        Connected = 2,

        /// <summary>
        /// Зарегистрировано на сервере
        /// </summary>
        Registered = 3,

        /// <summary>
        /// Переподключение после обрыва
        /// </summary>
        Reconnecting = 4,

        /// <summary>
        /// Ошибка соединения
        /// </summary>
        Failed = 5
    }
}
