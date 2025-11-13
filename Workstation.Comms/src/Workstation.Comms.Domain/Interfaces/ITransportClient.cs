using System;
using System.Threading;
using System.Threading.Tasks;

namespace Workstation.Comms.Domain.Interfaces
{
    /// <summary>
    /// Абстракция транспортного уровня (SignalR, WebSocket и т.д.)
    /// </summary>
    public interface ITransportClient : IDisposable
    {
        /// <summary>
        /// Состояние соединения
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Событие изменения состояния соединения
        /// </summary>
        event EventHandler<TransportConnectionStateChangedEventArgs>? ConnectionStateChanged;

        /// <summary>
        /// Событие получения сообщения
        /// </summary>
        event EventHandler<MessageReceivedEventArgs>? MessageReceived;

        /// <summary>
        /// Открыть соединение
        /// </summary>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Закрыть соединение
        /// </summary>
        Task StopAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Отправить сообщение на сервер
        /// </summary>
        Task SendAsync<TMessage>(string methodName, TMessage message, CancellationToken cancellationToken = default)
            where TMessage : class;

        /// <summary>
        /// Зарегистрировать обработчик для входящего метода
        /// </summary>
        void On<TMessage>(string methodName, Action<TMessage> handler)
            where TMessage : class;
    }

    /// <summary>
    /// Аргументы события изменения состояния соединения
    /// </summary>
    public class TransportConnectionStateChangedEventArgs : EventArgs
    {
        public bool IsConnected { get; }
        public string? Error { get; }

        public TransportConnectionStateChangedEventArgs(bool isConnected, string? error = null)
        {
            IsConnected = isConnected;
            Error = error;
        }
    }

    /// <summary>
    /// Аргументы события получения сообщения
    /// </summary>
    public class MessageReceivedEventArgs : EventArgs
    {
        public string MethodName { get; }
        public object? Message { get; }

        public MessageReceivedEventArgs(string methodName, object? message)
        {
            MethodName = methodName;
            Message = message;
        }
    }
}
