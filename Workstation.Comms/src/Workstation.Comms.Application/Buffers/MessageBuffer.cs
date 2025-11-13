using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Workstation.Comms.Application.Buffers
{
    /// <summary>
    /// Буфер для временного хранения сообщений при обрыве связи
    /// </summary>
    /// <typeparam name="T">Тип буферизуемого сообщения</typeparam>
    public class MessageBuffer<T> where T : class
    {
        private readonly ConcurrentQueue<BufferedMessage<T>> _queue;
        private readonly int _maxSize;

        public MessageBuffer(int maxSize = 1000)
        {
            _maxSize = maxSize;
            _queue = new ConcurrentQueue<BufferedMessage<T>>();
        }

        /// <summary>
        /// Количество сообщений в буфере
        /// </summary>
        public int Count => _queue.Count;

        /// <summary>
        /// Буфер заполнен?
        /// </summary>
        public bool IsFull => _queue.Count >= _maxSize;

        /// <summary>
        /// Добавить сообщение в буфер
        /// </summary>
        public bool Enqueue(T message, int priority = 0)
        {
            if (IsFull)
            {
                // При переполнении удаляем самое старое сообщение с наименьшим приоритетом
                TryDequeueOldest();
            }

            var bufferedMessage = new BufferedMessage<T>
            {
                Message = message,
                Timestamp = DateTime.UtcNow,
                Priority = priority
            };

            _queue.Enqueue(bufferedMessage);
            return true;
        }

        /// <summary>
        /// Извлечь сообщение из буфера
        /// </summary>
        public bool TryDequeue(out T? message)
        {
            if (_queue.TryDequeue(out var bufferedMessage))
            {
                message = bufferedMessage.Message;
                return true;
            }

            message = null;
            return false;
        }

        /// <summary>
        /// Извлечь все сообщения из буфера
        /// </summary>
        public IEnumerable<T> DequeueAll()
        {
            var messages = new List<T>();
            while (TryDequeue(out var message))
            {
                if (message != null)
                {
                    messages.Add(message);
                }
            }
            return messages;
        }

        /// <summary>
        /// Получить все сообщения без извлечения (для просмотра)
        /// </summary>
        public IEnumerable<T> Peek()
        {
            return _queue
                .OrderByDescending(m => m.Priority)
                .ThenBy(m => m.Timestamp)
                .Select(m => m.Message)
                .ToList();
        }

        /// <summary>
        /// Очистить буфер
        /// </summary>
        public void Clear()
        {
            _queue.Clear();
        }

        private bool TryDequeueOldest()
        {
            return _queue.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Буферизованное сообщение с метаданными
    /// </summary>
    internal class BufferedMessage<T> where T : class
    {
        public T Message { get; init; } = null!;
        public DateTime Timestamp { get; init; }
        public int Priority { get; init; }
    }
}
