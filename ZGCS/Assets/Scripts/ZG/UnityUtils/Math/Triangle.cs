using System;
using UnityEngine;

namespace ZG
{
    public struct Triangle<T>
    {
        public T x;
        public T y;
        public T z;


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
                    case 2:
                        return z;
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
                    case 2:
                        z = value;
                        break;
                    default:
                        throw new IndexOutOfRangeException();
                }
            }
        }

        public Triangle(T x, T y, T z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }

    [Serializable]
    public struct Triangle
    {
        public Vector3 x;
        public Vector3 y;
        public Vector3 z;

        public float area
        {
            get
            {
                float a = (x - y).magnitude, b = (y - z).magnitude, c = (z - x).magnitude, p = (a + b + c) * 0.5f;

                return Mathf.Sqrt(p * (p - a) * (p - b) * (p - c));
            }
        }

        public Vector3 normal
        {
            get
            {
                return Vector3.Cross(y - x, z - y);
            }
        }

        public Plane plane
        {
            get
            {
                return new Plane(normal, x);
            }
        }

        public Triangle(Vector3 x, Vector3 y, Vector3 z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public Vector3 this[int index]
        {
            get
            {
                switch(index)
                {
                    case 0:
                        return x;
                    case 1:
                        return y;
                    case 2:
                        return z;
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
                    case 2:
                        z = value;
                        break;
                    default:
                        throw new IndexOutOfRangeException();
                }
            }
        }

        public Vector3 GetPoint(float r1, float r2)
        {
            r1 = Mathf.Sqrt(r1);
            
            return (1 - r1) * x + (r1 * (1 - r2)) * y + (r1 * r2) * z;
        }

        public bool IsSeparating(Vector3 axis, Vector3 x, Vector3 y)
        {
            axis.Normalize();

            float minX = Vector3.Dot(axis, this.x), maxX = minX, temp;
            for(int i = 1; i < 3; ++i)
            {
                temp = Vector3.Dot(axis, this[i]);
                if (temp < minX)
                    minX = temp;
                else if (temp > maxX)
                    maxX = temp;
            }

            float minY = Vector3.Dot(axis, x), maxY = Vector3.Dot(axis, y);
            if(minY > maxY)
            {
                temp = minY;
                minY = maxY;
                maxY = temp;
            }

            return minX > maxY || minY > maxX;
        }

        public bool IsSeparating(Vector3 axis, Triangle triangle)
        {
            //axis.Normalize();

            float minX = Vector3.Dot(axis, x), maxX = minX;
            float minY = Vector3.Dot(axis, triangle.x), maxY = minY;

            float temp;
            for (int i = 1; i < 3; i++)
            {
                temp = Vector3.Dot(axis, this[i]);
                if (temp < minX)
                    minX = temp;
                else if (temp > maxX)
                    maxX = temp;

                temp = Vector3.Dot(axis, triangle[i]);
                if (temp < minY)
                    minY = temp;
                else if (temp > maxY)
                    maxY = temp;
            }

            return minX >= maxY || minY >= maxX;
        }
    }
}
