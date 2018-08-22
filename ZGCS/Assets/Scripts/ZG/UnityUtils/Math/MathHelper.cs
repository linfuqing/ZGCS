using UnityEngine;

namespace ZG
{
    public static class MathHelper
    {
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

    }
}