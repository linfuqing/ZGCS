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

        public static Bounds Multiply(this Matrix4x4 mat, Bounds bounds)
        {
            var absAxisX = Abs(mat.MultiplyVector(Vector3.right));
            var absAxisY = Abs(mat.MultiplyVector(Vector3.up));
            var absAxisZ = Abs(mat.MultiplyVector(Vector3.forward));
            var worldPosition = mat.MultiplyPoint(bounds.center);
            var worldSize = absAxisX * bounds.size.x + absAxisY * bounds.size.y + absAxisZ * bounds.size.z;
            return new Bounds(worldPosition, worldSize);
        }

    }
}