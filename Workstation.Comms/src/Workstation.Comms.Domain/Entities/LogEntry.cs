using System;
using Workstation.Comms.Domain.Enums;

namespace Workstation.Comms.Domain.Entities
{
    /// <summary>
    /// Запись лога/события с рабочего места
    /// </summary>
    public record LogEntry
    {
        /// <summary>
        /// ID рабочего места, с которого отправлен лог
        /// </summary>
        public string WorkstationId { get; init; } = string.Empty;

        /// <summary>
        /// ID активной сессии (если есть)
        /// </summary>
        public string? SessionId { get; init; }

        /// <summary>
        /// Уровень важности
        /// </summary>
        public LogLevel Level { get; init; }

        /// <summary>
        /// Категория события (например, "UserAction", "SystemEvent", "Error")
        /// </summary>
        public string Category { get; init; } = "General";

        /// <summary>
        /// Сообщение
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// Полезная нагрузка события в JSON-формате (детали действия пользователя, состояние и т.д.)
        /// </summary>
        public string? Payload { get; init; }

        /// <summary>
        /// Метка времени события
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Тег для группировки событий
        /// </summary>
        public string? Tag { get; init; }

        /// <summary>
        /// Стек вызовов (для ошибок)
        /// </summary>
        public string? StackTrace { get; init; }
    }
}
