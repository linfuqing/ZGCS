using UnityEngine;

namespace ZG
{
    public static class MathUtility
    {
        private static readonly int[] __MAXINUM_BIT_TABLE = new int[256]
        {
            0, 1, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
            6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
            7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
            7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
            8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
            8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
            8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
            8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8
        };
        
        public static int GetHighestBit(this byte value)
        {
            return __MAXINUM_BIT_TABLE[value];
        }

        public static int GetHighestBit(this ushort value)
        {
            int mask = value >> 8;
            return mask == 0 ? __MAXINUM_BIT_TABLE[value] : 8 + __MAXINUM_BIT_TABLE[mask];
        }

        public static int GetHighestBit(this uint value)
        {
            uint mask = value >> 24;
            int result;
            if (mask == 0)
            {
                mask = value >> 16;
                if (mask == 0)
                {
                    mask = value >> 8;

                    result = mask == 0 ? __MAXINUM_BIT_TABLE[value] : 8 + __MAXINUM_BIT_TABLE[mask];
                }
                else
                    result = 16 + __MAXINUM_BIT_TABLE[mask];
            }
            else
                result = 24 + __MAXINUM_BIT_TABLE[mask];

            return result;
        }

        public static int GetLowerstBit(this byte value)
        {
            return __MAXINUM_BIT_TABLE[(value - 1) ^ value];
        }

        public static int GetLowerstBit(this ushort value)
        {
            return GetHighestBit((ushort)((value - 1) ^ value));
        }

        public static int GetLowerstBit(this uint value)
        {
            return GetHighestBit((value - 1) ^ value);
        }

        public static float Cross(this Vector2 x, Vector2 y)
        {
            return x.x * y.y - y.x * x.y;
        }
        
        public static bool IsIntersect(Vector2 x, Vector2 y, Vector2 z, Vector2 w)
        {
            float delta = Cross(new Vector2(y.x - x.x, z.x - w.x), new Vector2(y.y - x.y, z.y - w.y));
            if (Mathf.Approximately(delta, 0.0f))
                return false;

            float namenda = Cross(new Vector2(z.x - x.x, z.x - w.x), new Vector2(z.y - x.y, z.y - w.y)) / delta;
            if (namenda > 1.0f || namenda < 0.0f)
                return false;

            float miu = Cross(new Vector2(y.x - x.x, z.x - x.x), new Vector2(y.y - x.y, z.y - x.y)) / delta;
            if (miu > 1.0f || miu < 0.0f)
                return false;
            
            return true;
        }
        
        public static Vector3 Abs(this Vector3 v)
        {
            return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        }

        public static Bounds Multiply(this Matrix4x4 matrix, Bounds bounds)
        {
            Vector3 absAxisX = Abs(matrix.MultiplyVector(Vector3.right)), 
                    absAxisY = Abs(matrix.MultiplyVector(Vector3.up)), 
                    absAxisZ = Abs(matrix.MultiplyVector(Vector3.forward)), 
                    size = bounds.size;
            return new Bounds(
                matrix.MultiplyPoint(bounds.center), 
                absAxisX * size.x + absAxisY * size.y + absAxisZ * size.z);
        }
        
        public static Bounds Multiply(this Quaternion rotation, Bounds bounds)
        {
            Vector3 absAxisX = Abs(rotation * Vector3.right), 
                    absAxisY = Abs(rotation * Vector3.up), 
                    absAxisZ = Abs(rotation * Vector3.forward), 
                    size = bounds.size;
            return new Bounds(bounds.center, absAxisX * size.x + absAxisY * size.y + absAxisZ * size.z);
        }

        public static void GetCorners(
            this Bounds bounds,
            Matrix4x4 matrix,
            out Vector3 leftUpForward,
            out Vector3 leftDownForward,
            out Vector3 rightUpForward,
            out Vector3 rightDownForward,

            out Vector3 leftUpBackward,
            out Vector3 leftDownBackward,
            out Vector3 rightUpBackward,
            out Vector3 rightDownBackward)
        {
            Vector3 center = matrix.MultiplyPoint(bounds.center),
                extents = bounds.extents,
                right = matrix.MultiplyVector(Vector3.right) * extents.x,
                up = matrix.MultiplyVector(Vector3.up) * extents.y,
                forward = matrix.MultiplyVector(Vector3.forward) * extents.z;
            leftUpForward = center - right + up + forward;
            leftDownForward = center - right - up + forward;
            rightUpForward = center + right + up + forward;
            rightDownForward = center + right - up + forward;

            leftUpBackward = center - right + up - forward;
            leftDownBackward = center - right - up - forward;
            rightUpBackward = center + right + up - forward;
            rightDownBackward = center + right - up - forward;
        }

        public static bool Intersect(this Ray ray, Vector3 center, float radiusSquare, out float t0, out float t1)
        {
            t0 = 0.0f;
            t1 = 0.0f;

            Vector3 oc = ray.origin - center; float dotOCD = Vector3.Dot(ray.direction, oc);
            if (dotOCD > 0)
                return false;

            float dotOC = Vector3.Dot(oc, oc);
            float discriminant = dotOCD * dotOCD - dotOC + radiusSquare;

            if (discriminant < 0.0f)
                return false;
            
            discriminant = Mathf.Sqrt(discriminant);
            t0 = -dotOCD - discriminant;
            t1 = -dotOCD + discriminant;
            if (t0 < 0.0f)
                t0 = t1;

            return true;
        }
    }
}