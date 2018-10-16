using System;
using UnityEngine;

namespace ZG
{
    public struct Edge<T> : IEquatable<Edge<T>> where T : IEquatable<T>
    {
        public T x;
        public T y;

        public T this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0:
                        return x;
                    case 1:
                        return y;
                }

                throw new IndexOutOfRangeException();
            }

            set
            {
                switch (index)
                {
                    case 0:
                        x = value;
                        break;
                    case 1:
                        y = value;
                        break;
                    default:
                        throw new IndexOutOfRangeException();
                }
            }
        }

        public Edge(T x, T y)
        {
            this.x = x;
            this.y = y;
        }

        public bool Equals(Edge<T> other)
        {
            return x.Equals(other.x) && y.Equals(other.y);
        }
    }
}