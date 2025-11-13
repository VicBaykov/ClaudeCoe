using System;
using System.Collections.Generic;
using Workstation.Comms.Domain.Enums;

namespace Workstation.Comms.Domain.Entities
{
    /// <summary>
    /// Отчёт о выполненной сессии обучения
    /// </summary>
    public record SessionReport
    {
        /// <summary>
        /// ID сессии
        /// </summary>
        public string SessionId { get; init; } = string.Empty;

        /// <summary>
        /// ID рабочего места
        /// </summary>
        public string WorkstationId { get; init; } = string.Empty;

        /// <summary>
        /// ID обучаемого
        /// </summary>
        public string UserId { get; init; } = string.Empty;

        /// <summary>
        /// ID сценария
        /// </summary>
        public string ScenarioId { get; init; } = string.Empty;

        /// <summary>
        /// Статус сессии
        /// </summary>
        public SessionStatus Status { get; init; }

        /// <summary>
        /// Время начала сессии
        /// </summary>
        public DateTime StartTime { get; init; }

        /// <summary>
        /// Время окончания сессии
        /// </summary>
        public DateTime? EndTime { get; init; }

        /// <summary>
        /// Общая продолжительность (секунды)
        /// </summary>
        public double DurationSeconds { get; init; }

        /// <summary>
        /// Список выполненных шагов сценария
        /// </summary>
        public List<ScenarioStep> CompletedSteps { get; init; } = new();

        /// <summary>
        /// Список ошибок, допущенных во время сессии
        /// </summary>
        public List<SessionError> Errors { get; init; } = new();

        /// <summary>
        /// Оценка/результат (процент правильности, баллы и т.д.)
        /// </summary>
        public double? Score { get; init; }

        /// <summary>
        /// Дополнительные метрики производительности
        /// </summary>
        public Dictionary<string, object>? Metrics { get; init; }

        /// <summary>
        /// Комментарии/замечания
        /// </summary>
        public string? Notes { get; init; }
    }

    /// <summary>
    /// Информация о выполненном шаге сценария
    /// </summary>
    public record ScenarioStep
    {
        /// <summary>
        /// ID шага в сценарии
        /// </summary>
        public string StepId { get; init; } = string.Empty;

        /// <summary>
        /// Название шага
        /// </summary>
        public string StepName { get; init; } = string.Empty;

        /// <summary>
        /// Время начала выполнения шага
        /// </summary>
        public DateTime StartTime { get; init; }

        /// <summary>
        /// Время завершения шага
        /// </summary>
        public DateTime? EndTime { get; init; }

        /// <summary>
        /// Успешность выполнения
        /// </summary>
        public bool IsSuccess { get; init; }

        /// <summary>
        /// Количество попыток
        /// </summary>
        public int Attempts { get; init; } = 1;

        /// <summary>
        /// Дополнительные данные о выполнении шага
        /// </summary>
        public string? Data { get; init; }
    }

    /// <summary>
    /// Информация об ошибке в сессии
    /// </summary>
    public record SessionError
    {
        /// <summary>
        /// Тип ошибки
        /// </summary>
        public string ErrorType { get; init; } = string.Empty;

        /// <summary>
        /// Описание ошибки
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// Время возникновения
        /// </summary>
        public DateTime Timestamp { get; init; }

        /// <summary>
        /// ID шага, на котором произошла ошибка
        /// </summary>
        public string? StepId { get; init; }

        /// <summary>
        /// Критичность ошибки
        /// </summary>
        public bool IsCritical { get; init; }
    }
}
