using System;
using System.Threading;
using System.Threading.Tasks;
using Workstation.Comms.Domain.Entities;
using Workstation.Comms.Domain.Enums;

namespace Workstation.Comms.Domain.Interfaces
{
    /// <summary>
    /// Высокоуровневый клиент для взаимодействия рабочего места с сервером инструктора
    /// </summary>
    public interface IWorkstationClient : IDisposable
    {
        /// <summary>
        /// Текущее состояние соединения
        /// </summary>
        ConnectionState ConnectionState { get; }

        /// <summary>
        /// Информация о зарегистрированном рабочем месте
        /// </summary>
        WorkstationInfo? WorkstationInfo { get; }

        /// <summary>
        /// Событие изменения состояния соединения
        /// </summary>
        event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

        /// <summary>
        /// Событие получения команды от инструктора
        /// </summary>
        event EventHandler<SessionCommandReceivedEventArgs>? SessionCommandReceived;

        /// <summary>
        /// Событие ошибки
        /// </summary>
        event EventHandler<ErrorEventArgs>? ErrorOccurred;

        /// <summary>
        /// Подключиться к серверу инструктора
        /// </summary>
        Task ConnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Зарегистрировать рабочее место на сервере
        /// </summary>
        Task RegisterAsync(WorkstationInfo workstationInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Отправить лог-запись на сервер
        /// </summary>
        Task SendLogAsync(LogEntry logEntry, CancellationToken cancellationToken = default);

        /// <summary>
        /// Отправить отчёт о сессии
        /// </summary>
        Task SendSessionReportAsync(SessionReport report, CancellationToken cancellationToken = default);

        /// <summary>
        /// Отправить heartbeat
        /// </summary>
        Task SendHeartbeatAsync(HeartbeatMessage heartbeat, CancellationToken cancellationToken = default);

        /// <summary>
        /// Отключиться от сервера
        /// </summary>
        Task DisconnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Начать автоматическую отправку heartbeat
        /// </summary>
        void StartHeartbeat(TimeSpan interval);

        /// <summary>
        /// Остановить автоматическую отправку heartbeat
        /// </summary>
        void StopHeartbeat();
    }

    /// <summary>
    /// Аргументы события изменения состояния соединения
    /// </summary>
    public class ConnectionStateChangedEventArgs : EventArgs
    {
        public ConnectionState OldState { get; }
        public ConnectionState NewState { get; }
        public string? Message { get; }

        public ConnectionStateChangedEventArgs(ConnectionState oldState, ConnectionState newState, string? message = null)
        {
            OldState = oldState;
            NewState = newState;
            Message = message;
        }
    }

    /// <summary>
    /// Аргументы события получения команды
    /// </summary>
    public class SessionCommandReceivedEventArgs : EventArgs
    {
        public SessionCommand Command { get; }

        public SessionCommandReceivedEventArgs(SessionCommand command)
        {
            Command = command;
        }
    }

    /// <summary>
    /// Аргументы события ошибки
    /// </summary>
    public class ErrorEventArgs : EventArgs
    {
        public string Message { get; }
        public Exception? Exception { get; }

        public ErrorEventArgs(string message, Exception? exception = null)
        {
            Message = message;
            Exception = exception;
        }
    }
}
