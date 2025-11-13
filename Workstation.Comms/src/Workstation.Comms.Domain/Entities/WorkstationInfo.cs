using System;
using Workstation.Comms.Domain.Enums;

namespace Workstation.Comms.Domain.Entities
{
    /// <summary>
    /// Информация о рабочем месте для регистрации на сервере инструктора
    /// </summary>
    public record WorkstationInfo
    {
        /// <summary>
        /// Уникальный идентификатор рабочего места
        /// </summary>
        public string WorkstationId { get; init; } = string.Empty;

        /// <summary>
        /// Тип рабочего места (Touch/VR/Hybrid)
        /// </summary>
        public WorkstationType WorkstationType { get; init; }

        /// <summary>
        /// Подсистема, закреплённая за рабочим местом
        /// </summary>
        public SubsystemType SubsystemType { get; init; }

        /// <summary>
        /// Дополнительное описание/название рабочего места
        /// </summary>
        public string? DisplayName { get; init; }

        /// <summary>
        /// Версия клиентского ПО
        /// </summary>
        public string ClientVersion { get; init; } = "1.0.0";

        /// <summary>
        /// Дополнительные метаданные (например, информация об оборудовании)
        /// </summary>
        public string? Metadata { get; init; }

        /// <summary>
        /// Время регистрации
        /// </summary>
        public DateTime RegisteredAt { get; init; } = DateTime.UtcNow;
    }
}
