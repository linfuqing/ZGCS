using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ZG
{
    /// <summary>
    /// Represents a list that can be added/deleted faster by recycling index.
    /// </summary>
    /// <typeparam name="T">
    /// The type of elements in the pool.
    /// </typeparam>
    public interface IPool<T> : IList<T>, IEnumerable<KeyValuePair<int, T>>
    {
        /// <summary>
        /// Get next index of the element be added.
        /// </summary>
        /// <returns>
        /// The index of elements in the <see cref="IPool{T}"/>.
        /// </returns>
        int nextIndex { get; }

        /// <summary>
        /// Determines whether the <see cref="IPool{T}"/> contains an element with the index.
        /// </summary>
        /// <param name="index">
        /// The index to locate in the <see cref="IPool{T}"/>.
        /// </param>
        /// <returns>
        /// <code>true</code> if the <see cref="IPool{T}"/> contains an element with the index; otherwise, <code>false</code>.
        /// </returns>
        bool ContainsKey(int index);

        /// <summary>
        /// Determines whether the <see cref="IPool{T}"/> contains an element.
        /// </summary>
        /// <param name="data">
        /// The element to locate in the <see cref="IPool{T}"/>.
        /// </param>
        /// <returns>
        /// <code>true</code> if the <see cref="IPool{T}"/> contains an element; otherwise, <code>false</code>.
        /// </returns>
        bool ContainsValue(T data);

        /// <summary>
        /// Gets the value associated with the index.
        /// </summary>
        /// <param name="index">
        /// The index of the value to get.
        /// </param>
        /// <param name="data">
        /// The value associated with the index, if the index is found; 
        /// the old value with the index, if the index has been used; 
        /// otherwise, the default value for the type of the value parameter. 
        /// </param>
        /// <returns>
        /// <code>true</code> if the <see cref="IPool{T}"/> contains an element with the index; otherwise, <code>false</code>.
        /// </returns>
        bool TryGetValue(int index, out T data);

        /// <summary>
        /// Adds an element with the value into the <see cref="IPool{T}"/>.
        /// </summary>
        /// <param name="data">
        /// The value of the element to be added. The value can be null for reference types.
        /// 
        /// The value will be set to the index of the last element to be removed in the <see cref="IPool{T}"/>, if there is not a continous index of element in <see cref="IPool{T}"/>;
        /// otherwise, the value will be added to end of <see cref="IPool{T}"/>.
        /// </param>
        /// <returns>
        /// The index of element which after be added to the <see cref="IPool{T}"/>.
        /// </returns>
        new int Add(T data);

        /// <summary>
        /// Removes an element at the index.
        /// </summary>
        /// <param name="index">
        /// The index to locate in the <see cref="IPool{T}"/>.
        /// </param>
        /// <returns>
        /// <code>true</code> if the <see cref="IPool{T}"/> contains an element with the index; otherwise, <code>false</code>.
        /// </returns>
        new bool RemoveAt(int index);
    }

    /// <summary>
    /// A list that can be added/deleted faster by recycling index.
    /// </summary>
    /// <typeparam name="T">
    /// The type of elements in the pool.
    /// </typeparam>
    [Serializable]
    public class Pool<T> : IPool<T>, ISerializable
    {
        internal struct DataItem
        {
            public T data;
            public int index;
        }

        public class ValueEnumerator : Object, IEnumerator<T>
        {
            private List<DataItem>.Enumerator __instance;

            public T Current
            {
                get
                {
                    return __instance.Current.data;
                }
            }

            internal static ValueEnumerator Create(List<DataItem>.Enumerator instance)
            {
                ValueEnumerator result = Create<ValueEnumerator>();

                result.__instance = instance;

                return result;
            }

            public bool MoveNext()
            {
                while (__instance.MoveNext())
                {
                    if (__instance.Current.index == -1)
                        return true;
                }

                return false;
            }

            void IEnumerator.Reset()
            {
                ((IEnumerator<DataItem>)__instance).Reset();
            }

            object IEnumerator.Current
            {
                get
                {
                    return Current;
                }
            }
        }

        public class PairEnumerator : Object, IEnumerator<KeyValuePair<int, T>>
        {
            private List<DataItem> __list;
            private int __index;

            public KeyValuePair<int, T> Current
            {
                get
                {
                    return new KeyValuePair<int, T>(__index, __list[__index].data);
                }
            }

            internal static PairEnumerator Create(List<DataItem> list)
            {
                PairEnumerator result = Create<PairEnumerator>();

                result.__list = list;
                result.__index = -1;

                return result;
            }

            public bool MoveNext()
            {
                int count = __list == null ? 0 : __list.Count;
                while (++__index < count)
                {
                    if (__list[__index].index == -1)
                        return true;
                }

                return false;
            }

            void IEnumerator.Reset()
            {
                __index = -1;
            }

            object IEnumerator.Current
            {
                get
                {
                    return Current;
                }
            }
        }

        private List<DataItem> __items;
        private List<int> __indices;
        private int __numIndices;
        private int __count;

        /// <summary>
        /// Gets a value indicating whether the <see cref="Pool{T}"/> is read-only.
        /// </summary>
        /// <return>
        /// Always <code>false</code>.
        /// </return>
        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the number of elements actually contained in the <see cref="Pool{T}"/>.
        /// </summary>
        /// <returns>
        /// The number of elements actually contained in the <see cref="Pool{T}"/>.
        /// </returns>
        public int Count
        {
            get
            {
                return __count;
            }
        }

        /// <summary>
        /// Gets the number of elements actually contained in the <see cref="Pool{T}"/>.
        /// </summary>
        /// <returns>
        /// The number of elements actually contained in the  <see cref="Pool{T}"/>.
        /// </returns>
        public int count
        {
            get
            {
                return __count;
            }
        }

        /// <summary>
        /// Gets or sets the total number of elements the internal data structure can hold without resizing.
        /// </summary>
        /// <value>
        /// The number of elements that the <see cref="Pool{T}"/> can contain before resizing is required.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Capacity is set to a value that is less than <see cref="count"/>. 
        /// </exception>
        public int capacity
        {
            get
            {
                return __items == null ? 0 : __items.Count;
            }

            set
            {
                if (__items == null)
                {
                    if (value == 0)
                        return;

                    __items = new List<DataItem>();
                }
                else if (value < __items.Count)
                    throw new ArgumentOutOfRangeException();

                DataItem item;
                item.data = default(T);
                for (int i = __items.Count; i < value; ++i)
                {
                    if (__indices == null)
                        __indices = new List<int>();

                    item.index = __numIndices;
                    if (__numIndices < __indices.Count)
                        __indices[__numIndices++] = __items.Count;
                    else
                    {
                        __indices.Add(__items.Count);

                        __numIndices = __indices.Count;
                    }

                    __items.Add(item);
                }
            }
        }

        /// <summary>
        /// Get next index of the element be added.
        /// </summary>
        /// <returns>
        /// The index of elements in the <see cref="Pool{T}"/>.
        /// </returns>
        public int nextIndex
        {
            get
            {
                int numItems = __items == null ? 0 : __items.Count;
                if (numItems <= 0)
                    return numItems;

                __numIndices = Math.Min(__numIndices, __indices == null ? 0 : __indices.Count);
                if (__numIndices > 0)
                {
                    int index = __indices[__numIndices - 1];
                    if (index >= 0 && index < __items.Count)
                        return index;
                }

                return numItems;
            }
        }

        public IndexEnumerable indices
        {
            get
            {
                return new IndexEnumerable(__indices, __numIndices);
            }
        }

        /// <summary>
        /// Gets or sets the element at the specified index.
        /// </summary>
        /// <param name="index">
        /// The zero-based index of the element to get or set.
        /// </param>
        /// <returns>
        /// The element at the specified index.
        /// </returns>
        public T this[int index]
        {
            get
            {
                DataItem item = __items[index];
                if (item.index != -1)
                    throw new IndexOutOfRangeException();

                return item.data;
            }

            set
            {
                DataItem item = __items[index];
                if (item.index != -1)
                    throw new IndexOutOfRangeException();

                item.data = value;
                __items[index] = item;
            }
        }

        public Pool()
        {

        }

        protected Pool(SerializationInfo info, StreamingContext context)
        {
            SerializationInfoEnumerator enumerator = info == null ? null : info.GetEnumerator();
            if (enumerator != null)
            {
                while (enumerator.MoveNext())
                    Insert(int.Parse(enumerator.Name), (T)enumerator.Value);
            }
        }

        /// <summary>
        /// Determines whether the <see cref="Pool{T}"/> contains an element with the index.
        /// </summary>
        /// <param name="index">
        /// The index to locate in the <see cref="Pool{T}"/>.
        /// </param>
        /// <returns>
        /// <code>true</code> if the <see cref="Pool{T}"/> contains an element with the index; otherwise, <code>false</code>.
        /// </returns>
        public bool ContainsKey(int index)
        {
            if (__items == null || index < 0 || index >= __items.Count)
                return false;

            DataItem item = __items[index];
            if (item.index != -1)
                return false;

            return true;
        }

        /// <summary>
        /// Determines whether the <see cref="Pool{T}"/> contains an element.
        /// </summary>
        /// <param name="data">
        /// The element to locate in the <see cref="Pool{T}"/>.
        /// </param>
        /// <returns>
        /// <code>true</code> if the <see cref="Pool{T}"/> contains an element; otherwise, <code>false</code>.
        /// </returns>
        public bool ContainsValue(T data)
        {
            return IndexOf(data) != -1;
        }

        /// <summary>
        /// Searches for the specified object and returns the zero-based index of the first occurrence within the entire <see cref="Pool{T}"/>.
        /// </summary>
        /// <param name="data">
        /// The object to locate in the <see cref="Pool{T}"/>. The value can be <code>null</code> for reference types.
        /// </param>
        /// <returns>
        /// The zero-based index of the first occurrence of item within the entire <see cref="Pool{T}"/>, if found; otherwise, <code>-1</code>.
        /// </returns>
        public int IndexOf(T data)
        {
            if (__items == null)
                return -1;

            foreach (KeyValuePair<int, T> pair in (IEnumerable<KeyValuePair<int, T>>)this)
            {
                if (Equals(pair.Value, data))
                    return pair.Key;
            }

            return -1;
        }

        /// <summary>
        /// Searches for an element that matches the conditions defined by a specified predicate, and returns the zero-based index of the first occurrence within the <see cref="Pool{T}"/> or a portion of it.
        /// </summary>
        /// <param name="predicate">
        /// The <seealso cref="Predicate{T}"/> delegate that defines the conditions of the element to search for.
        /// </param>
        /// <returns>
        /// The zero-based index of the first occurrence of an element that matches the conditions defined by match, if found; otherwise, <code>–1</code>.
        /// </returns>
        public int FindIndex(Predicate<T> predicate)
        {
            if (__items != null)
                return __items.FindIndex(item => item.index == -1 && predicate(item.data));

            return -1;
        }

        /// <summary>
        /// Searches for an element that matches the conditions defined by the specified predicate, and returns the first occurrence within the entire <see cref="Pool{T}"/>.
        /// </summary>
        /// <param name="predicate">
        /// The <seealso cref="Predicate{T}"/> delegate that defines the conditions of the element to search for.
        /// </param>
        /// <returns>
        /// The first element that matches the conditions defined by the specified predicate, if found; otherwise, the default value for type T.
        /// </returns>
        public T Find(Predicate<T> predicate)
        {
            if (__items != null)
                return __items.Find(item => item.index != -1 && predicate(item.data)).data;

            return default(T);
        }

        /// <summary>
        /// Gets the value associated with the index.
        /// </summary>
        /// <param name="index">
        /// The index of the value to get.
        /// </param>
        /// <param name="data">
        /// The value associated with the index, if the index is found; 
        /// the old value with the index, if the index has been used; 
        /// otherwise, the default value for the type of the value parameter. 
        /// </param>
        /// <returns>
        /// <code>true</code> if the <see cref="Pool{T}"/> contains an element with the index; otherwise, <code>false</code>.
        /// </returns>
        public bool TryGetValue(int index, out T data)
        {
            if (__items == null || index < 0 || index >= __items.Count)
            {
                data = default(T);

                return false;
            }

            DataItem item = __items[index];
            data = item.data;

            return item.index == -1;
        }

        /// <summary>
        /// Inserts an element into the <see cref="Pool{T}"/> at the specified index.
        /// </summary>
        /// <param name="index">
        /// The zero-based index at which item should be inserted.
        /// If the index is greater than <see cref="capacity"/>, the <see cref="capacity"/> auto to be set as <code>index + 1</code>.
        /// </param>
        /// <param name="data">
        /// The object to insert. The value can be <code>null</code> for reference types.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="index"/> is less than <code>0</code>.
        /// </exception>
        public void Insert(int index, T data)
        {
            if (index < 0)
                throw new IndexOutOfRangeException();

            if (index >= capacity)
                capacity = index + 1;

            DataItem item = __items[index];
            item.data = data;
            if (item.index >= 0)
            {
                __numIndices = Math.Min(__numIndices, __indices == null ? 0 : __indices.Count);
                if (__numIndices > 0 && item.index < __numIndices)
                {
                    int currentIndex = __indices[--__numIndices];
                    if (currentIndex >= 0 && currentIndex < __items.Count)
                    {
                        DataItem currentItem = __items[currentIndex];

                        currentItem.index = item.index;

                        __items[currentIndex] = currentItem;
                    }

                    __indices[item.index] = currentIndex;
                }

                item.index = -1;

                ++__count;
            }

            __items[index] = item;
        }

        /// <summary>
        /// Adds an element with the value into the <see cref="Pool{T}"/>.
        /// </summary>
        /// <param name="data">
        /// The value of the element to be added. The value can be <code>null</code> for reference types.
        /// 
        /// The value will be set to the index of the last element to be removed in the <see cref="Pool{T}"/>, if there is not a continous index of element in <see cref="Pool{T}"/>;
        /// otherwise, the value will be added to end of <see cref="Pool{T}"/>.
        /// </param>
        /// <returns>
        /// The index of element which after be added to the <see cref="Pool{T}"/>.
        /// </returns>
        public int Add(T data)
        {
            if (__items == null)
                __items = new List<DataItem>();

            DataItem item;
            item.data = data;
            item.index = -1;

            __numIndices = Math.Min(__numIndices, __indices == null ? 0 : __indices.Count);
            if (__numIndices > 0)
            {
                int index = __indices[--__numIndices];
                if (index >= 0 && index < __items.Count)
                {
                    __items[index] = item;

                    ++__count;

                    return index;
                }
            }

            __items.Add(item);

            ++__count;

            return __items.Count - 1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="items">
        /// </param>
        public void AddRange(IEnumerable<T> items)
        {
            if (items != null)
            {
                foreach (T item in items)
                    Add(item);
            }
        }

        /// <summary>
        /// Removes an element at the index.
        /// </summary>
        /// <param name="index">
        /// The index to locate in the <see cref="Pool{T}"/>.
        /// </param>
        /// <returns>
        /// <code>true</code> if the <see cref="Pool{T}"/> contains an element with the index; otherwise, <code>false</code>.
        /// </returns>
        public bool RemoveAt(int index)
        {
            if (index < 0 || __items == null || __items.Count <= index)
                return false;

            DataItem item = __items[index];
            int numIndices = __indices == null ? 0 : __indices.Count;
            if (item.index >= 0 && item.index < numIndices)
                return false;

            if (__indices == null)
                __indices = new List<int>();

            if (__numIndices < numIndices)
                __indices[__numIndices++] = index;
            else
            {
                __indices.Add(index);

                __numIndices = __indices.Count;
            }

            item.index = __numIndices - 1;
            __items[index] = item;

            --__count;

            return true;
        }

        /// <summary>
        /// Removes the first occurrence of a specific object from the <see cref="Pool{T}"/>.
        /// </summary>
        /// <param name="data">
        /// The object to remove from the <see cref="Pool{T}"/>. The value can be <code>null</code> for reference types.
        /// </param>
        /// <returns>
        /// <code>true</code> if item is successfully removed; otherwise, <code>false</code>. 
        /// This method also returns <code>false</code> if item was not found in the <see cref="Pool{T}"/>.
        /// </returns>
        public bool Remove(T data)
        {
            return RemoveAt(IndexOf(data));
        }

        public int RemoveAll(Predicate<T> predicate)
        {
            int count = 0;
            while (RemoveAt(FindIndex(predicate)))
                ++count;

            return count;
        }

        /// <summary>
        /// Copies the entire <see cref="Pool{T}"/> to a compatible one-dimensional array, starting at the specified index of the target array.
        /// </summary>
        /// <param name="array">
        /// The one-dimensional Array that is the destination of the elements copied from <see cref="Pool{T}"/>. The Array must have zero-based indexing.
        /// </param>
        /// <param name="arrayIndex">
        /// The zero-based index in array at which copying begins.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="array"/> must not be <code>null</code> or empty.
        /// </exception>
        public void CopyTo(T[] array, int arrayIndex)
        {
            int numItems = array == null ? 0 : array.Length;
            if (arrayIndex < 0 || arrayIndex >= numItems)
                throw new ArgumentNullException();

            foreach (T data in (IEnumerable<T>)this)
            {
                array[arrayIndex++] = data;

                if (arrayIndex >= numItems)
                    break;
            }
        }

        public void CopyTo(KeyValuePair<int, T>[] array, int arrayIndex)
        {
            int numItems = array == null ? 0 : array.Length;
            if (arrayIndex < 0 || arrayIndex >= numItems)
                throw new ArgumentNullException();

            foreach (KeyValuePair<int, T> pair in (IEnumerable<KeyValuePair<int,T>>)this)
            {
                array[arrayIndex++] = pair;

                if (arrayIndex >= numItems)
                    break;
            }
        }

        /// <summary>
        /// Copies the elements of the <see cref="Pool{T}"/> to a new array.
        /// </summary>
        /// <returns>
        /// An array containing copies of the elements of the <see cref="Pool{T}"/>.
        /// </returns>
        public T[] ToArray()
        {
            if (__count <= 0)
                return null;

            T[] array = new T[__count];
            CopyTo(array, 0);

            return array;
        }

        /// <summary>
        /// Removes all elements from the <see cref="Pool{T}"/>.
        /// </summary>
        public void Clear()
        {
            foreach (KeyValuePair<int, T> pair in (IEnumerable<KeyValuePair<int, T>>)this)
                RemoveAt(pair.Key);

            __count = 0;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the <see cref="Pool{T}"/>.
        /// </summary>
        /// <returns>
        /// An enumerator that iterates through the <see cref="Pool{T}"/>.
        /// </returns>
        public ValueEnumerator GetValueEnumerator()
        {
            if (__items == null)
                __items = new List<DataItem>();

            return ValueEnumerator.Create(__items.GetEnumerator());
        }

        public PairEnumerator GetPairEnumerator()
        {
            return PairEnumerator.Create(__items);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return GetValueEnumerator();
        }

        IEnumerator<KeyValuePair<int, T>> IEnumerable<KeyValuePair<int, T>>.GetEnumerator()
        {
            return GetPairEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<T>)this).GetEnumerator();
        }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info != null)
            {
                foreach (KeyValuePair<int, T> pair in (IEnumerable<KeyValuePair<int, T>>)this)
                    info.AddValue(pair.Key.ToString(), pair.Value);
            }
        }

        bool ICollection<T>.Contains(T item)
        {
            return ContainsValue(item);
        }

        void ICollection<T>.Add(T item)
        {
            Add(item);
        }

        void IList<T>.RemoveAt(int index)
        {
            RemoveAt(index);
        }
    }
}