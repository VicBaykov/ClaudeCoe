using System;

namespace Workstation.Comms.Domain.Entities
{
    /// <summary>
    /// Heartbeat сообщение для поддержания соединения
    /// </summary>
    public record HeartbeatMessage
    {
        /// <summary>
        /// ID рабочего места
        /// </summary>
        public string WorkstationId { get; init; } = string.Empty;

        /// <summary>
        /// Время отправки heartbeat
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// ID активной сессии (если есть)
        /// </summary>
        public string? ActiveSessionId { get; init; }

        /// <summary>
        /// Статус рабочего места (например, "Idle", "InSession", "Maintenance")
        /// </summary>
        public string Status { get; init; } = "Idle";

        /// <summary>
        /// Метрики системы (CPU, RAM и т.д.)
        /// </summary>
        public string? SystemMetrics { get; init; }
    }
}
