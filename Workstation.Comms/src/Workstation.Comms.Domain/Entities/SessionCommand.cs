using System;
using System.Collections.Generic;
using Workstation.Comms.Domain.Enums;

namespace Workstation.Comms.Domain.Entities
{
    /// <summary>
    /// Команда от инструктора к рабочему месту
    /// </summary>
    public record SessionCommand
    {
        /// <summary>
        /// Уникальный идентификатор команды
        /// </summary>
        public string CommandId { get; init; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Тип команды
        /// </summary>
        public SessionCommandType CommandType { get; init; }

        /// <summary>
        /// ID сессии (если применимо)
        /// </summary>
        public string? SessionId { get; init; }

        /// <summary>
        /// ID сценария для запуска
        /// </summary>
        public string? ScenarioId { get; init; }

        /// <summary>
        /// ID обучаемого
        /// </summary>
        public string? UserId { get; init; }

        /// <summary>
        /// Подсистема для переключения (для SetSubsystem)
        /// </summary>
        public SubsystemType? TargetSubsystem { get; init; }

        /// <summary>
        /// Произвольные параметры команды
        /// </summary>
        public Dictionary<string, object>? Parameters { get; init; }

        /// <summary>
        /// Время создания команды
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Приоритет команды (для очереди обработки)
        /// </summary>
        public int Priority { get; init; } = 0;
    }
}
