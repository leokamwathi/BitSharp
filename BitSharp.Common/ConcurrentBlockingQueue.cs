﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace BitSharp.Common
{
    /// <summary>
    /// Represents a thread-safe blocking, first-in, first-out collection of objects.
    /// </summary>
    /// <typeparam name="T">Specifies the type of elements in the queue.</typeparam>
    public class ConcurrentBlockingQueue<T> : IDisposable
    {
        private readonly ConcurrentQueue<T> queue = new ConcurrentQueue<T>();
        private readonly AutoResetEvent workEvent = new AutoResetEvent(false);

        private bool isCompleteAdding;
        private bool isDisposed;

        /// <summary>
        /// Release all resources.
        /// </summary>
        public void Dispose()
        {
            if (isDisposed)
                return;

            this.workEvent.Dispose();
            this.isDisposed = true;
        }

        /// <summary>
        /// Gets the number of elements contained in the queue.
        /// </summary>
        public int Count
        {
            get
            {
                return this.queue.Count;
            }
        }

        /// <summary>
        /// Adds the item to the queue.
        /// </summary>
        /// <param name="item">The item to be added.</param>
        public void Add(T item)
        {
            CheckDisposed();

            // queue must not be marked as completed adding
            if (this.isCompleteAdding)
                throw new InvalidOperationException();

            // queue the item
            this.queue.Enqueue(item);

            // notify the consuming enumerable
            this.workEvent.Set();
        }

        /// <summary>
        /// Adds a collection of items to the queue.
        /// </summary>
        /// <param name="item">The items to be added.</param>
        public void AddRange(IEnumerable<T> items)
        {
            CheckDisposed();

            // queue must not be marked as completed adding
            if (this.isCompleteAdding)
                throw new InvalidOperationException();

            // queue the items
            foreach (var item in items)
            {
                this.queue.Enqueue(item);

                // notify the consuming enumerable
                this.workEvent.Set();
            }
        }

        /// <summary>
        /// Marks the queue as complete for adding, no additional items may be added.
        /// </summary>
        /// <remarks>
        /// After adding has been completed, any consuming enumerables will complete once the queue is empty.
        /// </remarks>
        public void CompleteAdding()
        {
            CheckDisposed();

            // mark complete
            this.isCompleteAdding = true;

            // notify the consuming enumerable
            this.workEvent.Set();
        }

        /// <summary>
        /// Provides a consuming enumerable of the items in the queue.
        /// </summary>
        /// <remarks>
        /// The consuming enumerable dequeues as many items as possible from the queue, and blocks
        /// when it is empty until additional items are added. The consuming enumerable will not
        /// return until the queue is complete for adding, and all items have been dequeued.
        /// </remarks>
        /// <returns>The consuming enumerable.</returns>
        public IEnumerable<T> GetConsumingEnumerable()
        {
            do
            {
                // dequeue and yield as many items as are available
                T value;
                while (this.queue.TryDequeue(out value))
                {
                    yield return value;
                }

                // once the queue is empty, check if adding is completed and return if so
                if (this.isCompleteAdding && this.queue.Count == 0)
                {
                    // ensure all other consuming enumerables are unblocked when complete
                    this.workEvent.Set();
                    yield break;
                }

                // otherwise, wait for additional items to be added and continue
            }
            while (this.workEvent.WaitOne());
        }

        private void CheckDisposed()
        {
            if (this.isDisposed)
                throw new ObjectDisposedException("ConcurrentBlockingQueue");
        }
    }
}
