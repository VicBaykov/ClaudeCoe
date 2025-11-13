namespace Workstation.Comms.Domain.Enums
{
    /// <summary>
    /// Уровень важности лог-события
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// Отладочная информация
        /// </summary>
        Debug = 0,

        /// <summary>
        /// Информационное сообщение
        /// </summary>
        Info = 1,

        /// <summary>
        /// Предупреждение
        /// </summary>
        Warning = 2,

        /// <summary>
        /// Ошибка
        /// </summary>
        Error = 3,

        /// <summary>
        /// Критическая ошибка
        /// </summary>
        Critical = 4
    }
}
