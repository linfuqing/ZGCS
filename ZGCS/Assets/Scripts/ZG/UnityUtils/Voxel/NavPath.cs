using System.Collections.Generic;
using UnityEngine;

namespace ZG.Voxel
{
    public class NavPath : NavPathEx
    {
        private float __minDentity;
        private float __maxDentity;
        private Vector3Int __minExtends;
        private Vector3Int __maxExtends;
        private Vector3Int __position;
        private ISampler __sampler;
        private HashSet<Vector3Int> __points;

        public NavPath(
            Vector3Int size) : base(size)
        {
        }

        public void Clear()
        {
            if (__points != null)
                __points.Clear();
        }

        public int Search(
            int maxDistance,
            int maxDepth,
            float minDentity,
            float maxDentity,
            Vector3Int minExtends,
            Vector3Int maxExtends,
            Vector3Int position,
            Vector3Int from,
            Vector3Int to,
            ISampler sampler)
        {
            __minDentity = minDentity;
            __maxDentity = maxDentity;
            __minExtends = minExtends;
            __maxExtends = maxExtends;
            __position = position;
            __sampler = sampler;

            if (Voluate(to, from) < int.MaxValue)
            {
                if (__points != null)
                {
                    int i, j, k;
                    Vector3Int min = from - __minExtends, max = new Vector3Int(from.x + minExtends.x, from.y - 1, from.z + minExtends.z);
                    for (i = min.x; i <= max.x; ++i)
                    {
                        for (j = min.y; j <= max.y; ++j)
                        {
                            for (k = min.z; k <= max.z; ++k)
                                __points.Remove(position + new Vector3Int(i, j, k));
                        }
                    }

                    min = new Vector3Int(from.x - maxExtends.x, from.y, from.z - maxExtends.z);
                    max = from + __maxExtends;
                    for (i = min.x; i <= max.x; ++i)
                    {
                        for (j = min.y; j <= max.y; ++j)
                        {
                            for (k = min.z; k <= max.z; ++k)
                            {
                                if (i == from.x && j == from.y && k == from.z)
                                    continue;

                                __points.Remove(position + new Vector3Int(i, j, k));
                            }
                        }
                    }
                }

                int depth = Search(Type.Min, maxDistance, maxDepth, from, to);
                if (depth > 0)
                {
                    if (__points == null)
                        __points = new HashSet<Vector3Int>();

                    __points.Add(position + from);

                    foreach (Vector3Int point in this)
                        __points.Add(position + point);
                }

                return depth;
            }

            return 0;
        }

        public override int Voluate(Vector3Int from, Vector3Int to)
        {
            if (from == to)
                return 0;

            if (new Vector3Int(from.x - to.x, Mathf.Abs(from.y - to.y), from.z - to.z) == new Vector3Int(0, 1, 0))
                return int.MaxValue;

            if (__sampler == null)
                return int.MaxValue;

            Block block;
            if (__sampler.Get(__position + from, out block) && block.density <= 0.0f)
                return int.MaxValue;

            //float source = block.density;

            if (!__sampler.Get(__position + new Vector3Int(from.x, from.y - 1, from.z), out block) || block.density > 0.0f)
                return int.MaxValue;

            Vector3Int min = from - __minExtends;
            if (min.x < 0 || min.y < 0 || min.z < 0)
                return int.MaxValue;

            Vector3Int max = new Vector3Int(from.x + __minExtends.x, from.y - 1, from.z + __minExtends.z), size = base.size;
            if (max.x >= size.x || max.y >= size.y || max.z >= size.z)
                return int.MaxValue;

            int i, j, k;
            Vector3Int position;
            for (i = min.x; i <= max.x; ++i)
            {
                for (j = min.y; j <= max.y; ++j)
                {
                    for (k = min.z; k <= max.z; ++k)
                    {
                        position = __position + new Vector3Int(i, j, k);

                        if (__sampler.Get(position, out block) ? block.density > __minDentity : __minDentity < 1.0f)
                            return int.MaxValue;

                        if (__points != null && __points.Contains(position))
                            return int.MaxValue;
                    }
                }
            }

            min = new Vector3Int(from.x - __maxExtends.x, from.y, from.z - __maxExtends.z);
            if (min.x < 0 || min.y < 0 || min.z < 0)
                return int.MaxValue;

            max = from + __maxExtends;
            if (max.x >= size.x || max.y >= size.y || max.z >= size.z)
                return int.MaxValue;

            for (i = min.x; i <= max.x; ++i)
            {
                for (j = min.y; j <= max.y; ++j)
                {
                    for (k = min.z; k <= max.z; ++k)
                    {
                        if (i == from.x && j == from.y && k == from.z)
                            continue;

                        position = __position + new Vector3Int(i, j, k);

                        if (__sampler.Get(position, out block) && block.density < __maxDentity)
                            return int.MaxValue;

                        if (__points != null && __points.Contains(position))
                            return int.MaxValue;
                    }
                }
            }

            return base.Voluate(from, to);
        }
    }

}