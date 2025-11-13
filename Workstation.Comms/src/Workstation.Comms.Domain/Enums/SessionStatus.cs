namespace Workstation.Comms.Domain.Enums
{
    /// <summary>
    /// Статус обучающей сессии
    /// </summary>
    public enum SessionStatus
    {
        /// <summary>
        /// Сессия не запущена
        /// </summary>
        NotStarted = 0,

        /// <summary>
        /// Сессия активна
        /// </summary>
        Running = 1,

        /// <summary>
        /// Сессия на паузе
        /// </summary>
        Paused = 2,

        /// <summary>
        /// Сессия завершена успешно
        /// </summary>
        Completed = 3,

        /// <summary>
        /// Сессия прервана с ошибкой
        /// </summary>
        Failed = 4,

        /// <summary>
        /// Сессия отменена инструктором
        /// </summary>
        Cancelled = 5
    }
}
