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
    }
}