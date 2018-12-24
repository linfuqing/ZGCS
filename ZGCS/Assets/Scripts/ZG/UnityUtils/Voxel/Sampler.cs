using UnityEngine;

namespace ZG.Voxel
{
    public struct Block
    {
        public int materialIndex;
        public float density;

        public Block(int materialIndex, float density)
        {
            this.materialIndex = materialIndex;
            this.density = density;
        }
    }

    public interface ISampler
    {
        bool Get(Vector3Int position, out Block block);

        bool Set(Vector3Int position, Block block);
    }

    public class Sampler : IEngineSampler
    {
        //private Dictionary<Vector3Int, ProcessorEx.Block> __blocks;
        private Vector3 __scale;
        private ISampler __instance;

        public Vector3 scale
        {
            get
            {
                return __scale;
            }
        }

        public ISampler instance
        {
            get
            {
                return __instance;
            }
        }

        public static float Cuboid(Vector3 position, Quaternion rotation, Bounds bounds)
        {
            Vector3 distance = bounds.extents - (rotation * (position - bounds.center)).Abs();
            if (distance.x < 0.0f)
            {
                if (distance.y < 0.0f)
                {
                    if (distance.z < 0.0f)
                        return -distance.magnitude;

                    return -Mathf.Sqrt(distance.x * distance.x + distance.y * distance.y);
                }
                else if (distance.z < 0.0f)
                    return -Mathf.Sqrt(distance.x * distance.x + distance.z * distance.z);

                return distance.x;
            }
            else if (distance.y < 0.0f)
            {
                if (distance.z < 0.0f)
                    return -Mathf.Sqrt(distance.y * distance.y + distance.z * distance.z);

                return distance.y;
            }
            else if (distance.z < 0.0f)
                return distance.z;

            return Mathf.Min(distance.x, Mathf.Min(distance.y, distance.z));
        }

        public Sampler(Vector3 scale, ISampler instance)
        {
            __scale = scale;

            __instance = instance;
        }

        public bool Get(Vector3Int position, out Block block)
        {
            if (__instance == null)
            {
                block = default(Block);

                return false;
            }

            return __instance.Get(position, out block);
        }

        public bool Set(Vector3Int position, Block block)
        {
            return __instance != null && __instance.Set(position, block);
        }

        public void Do(Bounds source, Quaternion rotation)
        {
            Bounds destination = rotation.Multiply(source);
            Vector3 temp = new Vector3(1.0f / __scale.x, 1.0f / __scale.y, 1.0f / __scale.z);
            Vector3Int min = Vector3Int.FloorToInt(Vector3.Scale(destination.min - __scale, temp)),
                max = Vector3Int.CeilToInt(Vector3.Scale(destination.max + __scale, temp)), key;

            Block block;
            int i, j, k;

            rotation = Quaternion.Inverse(rotation);
            for (i = min.x; i <= max.x; ++i)
            {
                for (j = min.y; j <= max.y; ++j)
                {
                    for (k = min.z; k <= max.z; ++k)
                    {
                        key = new Vector3Int(i, j, k);
                        if (Get(key, out block))
                        {
                            block.density = Mathf.Max(
                                block.density,
                                Mathf.Clamp(Cuboid(Vector3.Scale(key, __scale), rotation, source) / __scale.y, -1, 1));

                            Set(key, block);
                        }
                    }
                }
            }
        }

        public void Do(Bounds source, Quaternion rotation, int materialIndex)
        {
            Bounds destination = rotation.Multiply(source);
            Vector3 temp = new Vector3(1.0f / __scale.x, 1.0f / __scale.y, 1.0f / __scale.z);
            Vector3Int min = Vector3Int.FloorToInt(Vector3.Scale(destination.min - __scale, temp)),
                max = Vector3Int.CeilToInt(Vector3.Scale(destination.max + __scale, temp)), key;

            Block block;
            float density;
            int i, j, k;

            rotation = Quaternion.Inverse(rotation);
            for (i = min.x; i <= max.x; ++i)
            {
                for (j = min.y; j <= max.y; ++j)
                {
                    for (k = min.z; k <= max.z; ++k)
                    {
                        key = new Vector3Int(i, j, k);
                        if (Get(key, out block))
                        {
                            density = Cuboid(Vector3.Scale(key, __scale), rotation, source);// / scale.y;
                            if (density > -1.0f)
                                block.materialIndex = materialIndex;

                            block.density = Mathf.Max(
                                block.density,
                                Mathf.Clamp(density, -1, 1));

                            Set(key, block);
                        }
                    }
                }
            }
        }

        public void Do(Vector3 position, float radius)
        {
            Vector3 offset = new Vector3(position.x / __scale.x, position.y / __scale.y, position.z / __scale.z);
            Vector3Int min = Vector3Int.FloorToInt(new Vector3(offset.x - radius, offset.y - radius, offset.z - radius) - __scale),
                max = Vector3Int.CeilToInt(new Vector3(offset.x + radius, offset.y + radius, offset.z + radius) + __scale),
                temp;

            int i, j, k;
            float density;
            Block block;
            for (i = min.x; i <= max.x; ++i)
            {
                for (j = min.y; j <= max.y; ++j)
                {
                    for (k = min.z; k <= max.z; ++k)
                    {
                        density = radius - Vector3.Distance(new Vector3(i * __scale.x, j * __scale.y, k * __scale.z), position);
                        if (density > -radius)
                        {
                            temp = new Vector3Int(i, j, k);
                            if (Get(temp, out block))
                            {
                                block.density = Mathf.Max(
                                    block.density,
                                    density);

                                Set(temp, block);
                            }
                        }
                    }
                }
            }
        }

        public void Do(Vector3 position, float radius, int materialIndex)
        {
            Vector3 offset = new Vector3(position.x / __scale.x, position.y / __scale.y, position.z / __scale.z);
            Vector3Int min = Vector3Int.FloorToInt(new Vector3(offset.x - radius, offset.y - radius, offset.z - radius) - __scale),
                max = Vector3Int.CeilToInt(new Vector3(offset.x + radius, offset.y + radius, offset.z + radius) + __scale),
                temp;

            int i, j, k;
            float density;
            Block block;
            for (i = min.x; i <= max.x; ++i)
            {
                for (j = min.y; j <= max.y; ++j)
                {
                    for (k = min.z; k <= max.z; ++k)
                    {
                        density = radius - Vector3.Distance(new Vector3(i * __scale.x, j * __scale.y, k * __scale.z), position);
                        if (density > -radius)
                        {
                            temp = new Vector3Int(i, j, k);
                            if (Get(temp, out block))
                            {
                                block.materialIndex = materialIndex;

                                block.density = Mathf.Max(block.density, density);

                                Set(temp, block);
                            }
                        }
                    }
                }
            }
        }
        
        public float GetDensity(Vector3 position)
        {
            position.x /= __scale.x;
            position.y /= __scale.y;
            position.z /= __scale.z;

            Block block;
            Vector3Int min = Vector3Int.FloorToInt(position), max = Vector3Int.CeilToInt(position);
            if (min.x == max.x || min.y == max.y || min.z == max.z)
            {
                float x = Get(min, out block) ? block.density : 1.0f;
                if (min == max)
                    return x;

                float y = Get(max, out block) ? block.density : 1.0f;
                if ((min.x == max.x && min.y == max.y) || (min.x == max.x && min.z == max.z) || (min.y == max.y && min.z == max.z))
                    return Mathf.Lerp(x, y, position.x - min.x + position.y - min.y + position.z - min.z);

                float z, w, u, v;
                if (min.x == max.x)
                {
                    z = Get(new Vector3Int(min.x, max.y, min.z), out block) ? block.density : 1.0f;
                    w = Get(new Vector3Int(min.x, min.y, max.z), out block) ? block.density : 1.0f;

                    u = position.y - min.y;
                    v = position.z - min.z;
                }
                else if (min.y == max.y)
                {
                    z = Get(new Vector3Int(max.x, min.y, min.z), out block) ? block.density : 1.0f;
                    w = Get(new Vector3Int(min.x, min.y, max.z), out block) ? block.density : 1.0f;

                    u = position.x - min.x;
                    v = position.z - min.z;
                }
                else
                {
                    z = Get(new Vector3Int(max.x, min.y, min.z), out block) ? block.density : 1.0f;
                    w = Get(new Vector3Int(min.x, max.y, min.z), out block) ? block.density : 1.0f;

                    u = position.x - min.x;
                    v = position.y - min.y;
                }

                float invertU = 1.0f - u, invertV = 1.0f - v;

                return x * invertU * invertV + y * u * v + z * u * invertV + w * v * invertU;
            }

            float leftLowerBack = Get(min, out block) ? block.density : 1.0f,
                leftLowerFront = Get(new Vector3Int(min.x, min.y, max.z), out block) ? block.density : 1.0f,
                leftUpperBack = Get(new Vector3Int(min.x, max.y, min.z), out block) ? block.density : 1.0f,
                leftUpperFront = Get(new Vector3Int(min.x, max.y, max.z), out block) ? block.density : 1.0f,
                rightLowerBack = Get(new Vector3Int(max.x, min.y, min.z), out block) ? block.density : 1.0f,
                rightLowerFront = Get(new Vector3Int(max.x, min.y, max.z), out block) ? block.density : 1.0f,
                rightUpperBack = Get(new Vector3Int(max.x, max.y, min.z), out block) ? block.density : 1.0f,
                rightUpperFront = Get(max, out block) ? block.density : 1.0f;

            position -= min;

            Vector3 inverse = new Vector3(1.0f - position.x, 1.0f - position.y, 1.0f - position.z);

            return leftLowerBack * inverse.x * inverse.y * inverse.z +
                leftLowerFront * inverse.x * inverse.y * position.z +
                leftUpperBack * inverse.x * position.y * inverse.z +
                leftUpperFront * inverse.x * position.y * position.z +
                rightLowerBack * position.x * inverse.y * inverse.z +
                rightLowerFront * position.x * inverse.y * position.z +
                rightUpperBack * position.x * position.y * inverse.z +
                rightUpperFront * position.x * position.y * position.z;
        }
    }
}