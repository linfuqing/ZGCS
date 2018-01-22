using System;

namespace ZG
{
    public class Object<T> : IDisposable
    {
        private static Pool<Object<T>> __pool = null;
        private int __index;

        public T value;
        
        public static Object<T> Create()
        {
            if (__pool == null)
                __pool = new Pool<Object<T>>();

            int index = __pool.nextIndex;
            Object<T> target;
            __pool.TryGetValue(index, out target);
            if (target == null)
                target = new Object<T>(index);
            else
                target.__index = index;

            __pool.Insert(index, target);

            return target;
        }

        public static Object<T> Create(T value)
        {
            Object<T> result = Create();
            if (result != null)
                result.value = value;

            return result;
        }

        private Object(int index)
        {
            __index = index;
        }

        public void Dispose()
        {
            if (__pool != null)
                __pool.RemoveAt(__index);
        }
    }

    public class Object : IDisposable
    {
        private Func<int, bool> __dispose;
        private int __index;

        private static class Creator<T> where T : Object, new()
        {
            private static Pool<T> __pool = null;
            private static Func<int, bool> __dispose;

            public static T Create()
            {
                if (__pool == null)
                    __pool = new Pool<T>();

                int index = __pool.nextIndex;
                T target;
                __pool.TryGetValue(index, out target);
                if (target == null)
                    target = new T();

                if (__dispose == null)
                    __dispose = __pool.RemoveAt;

                target.__dispose = __dispose;
                target.__index = index;

                __pool.Insert(index, target);

                return target;
            }
        }

        public static T Create<T>() where T : Object, new()
        {
            return Creator<T>.Create();
        }

        public bool Dispose()
        {
            return __dispose != null && __dispose(__index);
        }

        void IDisposable.Dispose()
        {
            Dispose();
        }
    }
}