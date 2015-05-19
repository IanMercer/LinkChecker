using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;

namespace  LinkChecker.WebSpider
{
    /// <summary>
    /// A queue that tosses any items queue'd more than once - maintains a history of all items queue'd
    /// </summary>
    public class QueueWithDeduping<T>
    {
        private Queue<T> queue = new Queue<T>();
        private HashSet<T> allItems = new HashSet<T>();

        public void Enqueue(T item)
        {
            lock (allItems)
            {
                if (!allItems.Contains(item))
                {
                    allItems.Add(item);
                    queue.Enqueue(item);
                }
            }
        }

        public T Dequeue()
        {
            lock (allItems)
            {
                return queue.Dequeue();
            }
        }

        public int Count
        {
            get
            {
                lock (allItems)
                    return queue.Count;
            }
        }

    }
}
