using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;



namespace Fizzler
{
    public class LRUCache<TInput, TResult> : IDisposable
    {

#if SALTARELLE
        private readonly JsDictionary<TInput, TResult> data;
#else
        private readonly Dictionary<TInput, TResult> data;
#endif
        private readonly IndexedLinkedList<TInput> lruList = new IndexedLinkedList<TInput>();
        private readonly Func<TInput, TResult> evalutor;
#if !SALTARELLE
        private ReaderWriterLockSlim rwl = new ReaderWriterLockSlim();
#endif
        private int capacity;

        public LRUCache(Func<TInput, TResult> evalutor, int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException();

#if SALTARELLE
            this.data = new JsDictionary<TInput, TResult>();
#else
            this.data = new Dictionary<TInput, TResult>(capacity);
#endif
            this.capacity = capacity;
            this.evalutor = evalutor;
        }

#if !SALTARELLE
        private bool Remove(TInput key)
        {
            bool existed = data.Remove(key);
            lruList.Remove(key);
            return existed;
        }
#endif

        public TResult GetValue(TInput key)
        {
            TResult value;
            bool found;

#if !SALTARELLE
            rwl.EnterReadLock();
            try
            {
                found = data.TryGetValue(key, out value);
            }
            finally
            {
                rwl.ExitReadLock();
            }
#else
            value = data[key];
            found = !Script.IsNullOrUndefined(value);
#endif


            if (!found) value = evalutor(key);

#if !SALTARELLE
            rwl.EnterWriteLock();
#endif
            try
            {
                if (found)
                {
                    lruList.Remove(key);
                    lruList.Add(key);
                }
                else
                {
                    data[key] = value;
                    lruList.Add(key);

                    if (data.Count > capacity)
                    {
                        data.Remove(lruList.First);
                        lruList.RemoveFirst();
                    }
                }

            }
            finally
            {
#if !SALTARELLE
                rwl.ExitWriteLock();
#endif
            }


            return value;
        }

        public int Capacity
        {
            get
            {
                return capacity;
            }

            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException();

#if !SALTARELLE
                rwl.EnterWriteLock();
#endif
                try
                {
                    capacity = value;
                    while (data.Count > capacity)
                    {
                        data.Remove(lruList.First);
                        lruList.RemoveFirst();
                    }
                }
                finally
                {
#if !SALTARELLE
                    rwl.ExitWriteLock();
#endif
                }

            }
        }




        private class IndexedLinkedList<T>
        {
#if SALTARELLE
            private List<T> data = new List<T>();
            private JsDictionary<T, int> index = new JsDictionary<T, int>();
#else
            private LinkedList<T> data = new LinkedList<T>();
            private Dictionary<T, LinkedListNode<T>> index = new Dictionary<T, LinkedListNode<T>>();
#endif

            public void Add(T value)
            {
#if SALTARELLE
                data.Add(value);
                index[value] = data.Count;
#else
                index[value] = data.AddLast(value);
#endif
            }

            public void RemoveFirst()
            {
#if SALTARELLE
                index.Remove(data[0]);
                data.RemoveAt(0);
#else
                index.Remove(data.First.Value);
                data.RemoveFirst();
#endif
            }

            public void Remove(T value)
            {
#if SALTARELLE
                var idx = index[value];
                if (!Script.IsNullOrUndefined(idx))
                {
                    data.RemoveAt(idx);
                    index.Remove(value);
                }
#else
                LinkedListNode<T> node;
                if (index.TryGetValue(value, out node))
                {
                    data.Remove(node);
                    index.Remove(value);
                }
#endif
            }

            public void Clear()
            {
                data.Clear();
                index.Clear();
            }

            public T First
            {
                get
                {
#if SALTARELLE
                    return data[0];
#else
                    return data.First.Value;
#endif
                }
            }
        }


        public void Dispose()
        {
#if !SALTARELLE
            if (rwl == null) return;
            try
            {
                rwl.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // It should ignore duplicate calls to Dispose(), but it doesn't.
            }
            rwl = null;
#endif
        }
    }



}
