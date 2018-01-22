using System.Collections;
using System.Collections.Generic;

namespace ZG
{
    public struct IndexEnumerable : IEnumerable<int>
    {
        public class Enumerator : Object, IEnumerator<int>
        {
            private IList<int> __indices;
            private int __index;
            private int __count;

            public static Enumerator Create(IList<int> indices, int count)
            {
                Enumerator result = Create<Enumerator>();
                if (result == null)
                    return null;

                result.__indices = indices;
                result.__index = -1;
                result.__count = count;

                return result;
            }

            public int Current
            {
                get
                {
                    return __index < __count ? __indices[__index] : -1;
                }
            }

            public bool MoveNext()
            {
                ++__index;

                return __index < __count;
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

        private IList<int> __indices;
        private int __count;

        public IndexEnumerable(IList<int> indices, int count)
        {
            __indices = indices;
            __count = count;
        }
        
        public Enumerator GetEnumerator()
        {
            return Enumerator.Create(__indices, __count);
        }

        IEnumerator<int> IEnumerable<int>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public struct IndexEnumerable<T> : IEnumerable<T>
    {
        public class Enumerator : Object, IEnumerator<T>
        {
            private IList<T> __indices;
            private int __index;
            private int __count;

            public static Enumerator Create(IList<T> indices, int count)
            {
                Enumerator result = Create<Enumerator>();
                if (result == null)
                    return null;

                result.__indices = indices;
                result.__index = -1;
                result.__count = count;

                return result;
            }

            public T Current
            {
                get
                {
                    return __index < __count ? __indices[__index] : default(T);
                }
            }

            public bool MoveNext()
            {
                ++__index;

                return __index < __count;
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

        private IList<T> __indices;
        private int __count;

        public IndexEnumerable(IList<T> indices, int count)
        {
            __indices = indices;
            __count = count;
        }

        public Enumerator GetEnumerator()
        {
            return Enumerator.Create(__indices, __count);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
    
    public struct PairEnumerable<T> : IEnumerable<KeyValuePair<int, T>>
    {
        public class Enumerator : Object, IEnumerator<KeyValuePair<int, T>>
        {
            private IList<T> __indices;
            private int __index;
            private int __count;

            public static Enumerator Create(IList<T> indices, int count)
            {
                Enumerator result = Create<Enumerator>();
                if (result == null)
                    return null;

                result.__indices = indices;
                result.__index = -1;
                result.__count = count;

                return result;
            }

            public KeyValuePair<int, T> Current
            {
                get
                {
                    return new KeyValuePair<int, T>(__index, __index < __count ? __indices[__index] : default(T));
                }
            }

            public bool MoveNext()
            {
                ++__index;

                return __index < __count;
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

        private IList<T> __indices;
        private int __count;

        public PairEnumerable(IList<T> indices, int count)
        {
            __indices = indices;
            __count = count;
        }

        public Enumerator GetEnumerator()
        {
            return Enumerator.Create(__indices, __count);
        }

        IEnumerator<KeyValuePair<int, T>> IEnumerable<KeyValuePair<int, T>>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}