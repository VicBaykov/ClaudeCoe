namespace Workstation.Comms.Domain.Enums
{
    /// <summary>
    /// Типы команд, которые инструктор может отправить на рабочее место
    /// </summary>
    public enum SessionCommandType
    {
        /// <summary>
        /// Запустить обучающую сессию с указанным сценарием
        /// </summary>
        StartSession = 1,

        /// <summary>
        /// Остановить текущую сессию
        /// </summary>
        StopSession = 2,

        /// <summary>
        /// Поставить сессию на паузу
        /// </summary>
        PauseSession = 3,

        /// <summary>
        /// Возобновить сессию после паузы
        /// </summary>
        ResumeSession = 4,

        /// <summary>
        /// Запросить полный отчёт о текущей сессии
        /// </summary>
        RequestReport = 5,

        /// <summary>
        /// Изменить активную подсистему для обучения
        /// </summary>
        SetSubsystem = 6,

        /// <summary>
        /// Загрузить новый сценарий
        /// </summary>
        LoadScenario = 7,

        /// <summary>
        /// Сбросить состояние рабочего места
        /// </summary>
        ResetWorkstation = 8,

        /// <summary>
        /// Запрос статуса рабочего места
        /// </summary>
        RequestStatus = 9
    }
}
