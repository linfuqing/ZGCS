using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZG.Voxel
{
    public class DualContouring : IEngine
    {
        private struct Edge
        {
            public bool isInside;
            public Vector3 position;
            public Vector3 normal;

            public Edge(bool isInside, Vector3 position, Vector3 normal)
            {
                this.isInside = isInside;
                this.position = position;
                this.normal = normal;
            }
        }

        public struct Block
        {
            public int corners;
            public Vector3 normal;
            public Qef qef;
        }
        
        public struct Vector3IntEqualityComparer : IEqualityComparer<Vector3Int>
        {
            bool IEqualityComparer<Vector3Int>.Equals(Vector3Int x, Vector3Int y)
            {
                return x == y;
            }

            int IEqualityComparer<Vector3Int>.GetHashCode(Vector3Int obj)
            {
                return obj.GetHashCode();
            }
        }
        
        public class VoxelBuilder : IEngineBuilder
        {
            private DualContouring __parent;
            private Dictionary<Vector3Int, HashSet<Vector3Int>> __voxels;

            public DualContouring parent
            {
                get
                {
                    return __parent;
                }
            }

            public VoxelBuilder(DualContouring parent)
            {
                __parent = parent;
            }

            public bool Check(Vector3Int world)
            {
                if (__parent == null)
                    return false;

                if (__voxels == null)
                    return false;

                return __voxels.ContainsKey(world);
            }

            public bool Create(Vector3Int world)
            {
                if (__parent == null)
                    return false;

                if (__voxels == null)
                    return false;

                HashSet<Vector3Int> voxels;
                if (!__voxels.TryGetValue(world, out voxels) || voxels == null || voxels.Count < 1)
                    return false;

                if (__parent.__blocks == null)
                    __parent.__blocks = new Dictionary<Vector3Int, Dictionary<Vector3Int, Block>>(new Vector3IntEqualityComparer());

                int i;
                Dictionary<Vector3Int, Block> blocks;
                if (!__parent.__blocks.TryGetValue(world, out blocks) || blocks == null)
                {
                    blocks = new Dictionary<Vector3Int, Block>(1 << (__parent.__depth * 3), new Vector3IntEqualityComparer());

                    __parent.__blocks[world] = blocks;
                }

                int size = 1 << __parent.__depth, j, k, l, m, n;
                float x, y;
                Vector3Int local, min, max, offset, position;
                Vector3 from, to, point, normal;
                Block block;

                world *= size - 1;
                foreach (Vector3Int voxel in voxels)
                {
                    for (i = 0; i < 8; ++i)
                    {
                        local = voxel - __childMinOffsets[i];
                        if (local.x == -1 || local.y == -1 || local.z == -1 || local.x == size || local.y == size || local.z == size)
                            continue;

                        blocks.Remove(local);
                    }

                    min = Vector3Int.Max(voxel - Vector3Int.one, Vector3Int.zero);
                    max = Vector3Int.Min(voxel + Vector3Int.one, new Vector3Int(size, size, size));
                    for (i = min.x; i <= max.x; ++i)
                    {
                        for (j = min.y; j <= max.y; ++j)
                        {
                            for (k = min.z; k <= max.z; ++k)
                            {
                                if (i == max.x && j == max.y && k == max.z)
                                    break;

                                local = new Vector3Int(i, j, k);

                                from = Vector3.Scale(world + local, __parent.__scale);
                                x = __parent.GetDensity(from);
                                for (m = 0; m < (int)Axis.Unkown; ++m)
                                {
                                    to = from + Vector3.Scale(__axisOffsets[m], __parent.__scale);

                                    y = __parent.GetDensity(to);
                                    if (x < 0.0f == y < 0.0f)
                                        continue;

                                    point = __parent.ApproximateZeroCrossingPosition(from, to);
                                    normal = __parent.CalculateSurfaceNormal(point);
                                    for (n = 0; n < 4; ++n)
                                    {
                                        offset = local + __edgeToBlockOffsets[m, n];
                                        if (offset.x == min.x - 1 || offset.y == min.y - 1 || offset.z == min.z - 1 || offset.x == max.x || offset.y == max.y || offset.z == max.z)
                                            continue;

                                        if (!blocks.TryGetValue(offset, out block))
                                        {
                                            block = new Block();

                                            position = world + offset;

                                            for (l = 0; l < 8; ++l)
                                            {
                                                if (__parent.GetDensity(Vector3.Scale(position + __childMinOffsets[l], __parent.__scale)) < 0.0f)
                                                    block.corners |= 1 << l;
                                            }
                                        }

                                        block.normal += normal;
                                        block.qef.Add(new Qef.Data(point, normal));

                                        blocks[offset] = block;
                                    }
                                }
                            }
                        }
                    }
                }

                voxels.Clear();

                return true;
            }

            public bool Set(BoundsInt bounds)
            {
                bool result = false;
                int i, j, k;
                Vector3Int min = bounds.min, max = bounds.max;
                for (i = min.x; i <= max.x; ++i)
                {
                    for (j = min.y; j <= max.y; ++j)
                    {
                        for (k = min.z; k <= max.z; ++k)
                            result = Set(new Vector3Int(i, j, k)) || result;
                    }
                }

                return result;
            }

            public bool Set(Vector3Int position)
            {
                if (__parent == null)
                    return false;

                int mask = (1 << __parent.__depth) - 1;

                Vector3Int offset = new Vector3Int(position.x / mask, position.y / mask, position.z / mask);
                position -= offset * mask;
                if (position.x < 0)
                {
                    position.x += mask;

                    --offset.x;
                }

                if (position.y < 0)
                {
                    position.y += mask;

                    --offset.y;
                }

                if (position.z < 0)
                {
                    position.z += mask;

                    --offset.z;
                }

                return __Set(offset, position);
            }
            
            private bool __Set(Vector3Int world, Vector3Int local)
            {
                if (__parent == null)
                    return false;

                if (__voxels == null)
                    __voxels = new Dictionary<Vector3Int, HashSet<Vector3Int>>(new Vector3IntEqualityComparer());

                HashSet<Vector3Int> voxels;
                if (!__voxels.TryGetValue(world, out voxels))
                {
                    voxels = new HashSet<Vector3Int>();

                    __voxels[world] = voxels;
                }

                if (!voxels.Add(local))
                    return false;

                int size = 1 << __parent.__depth, mask = size - 1;
                if (local.x == 0)
                    __Set(new Vector3Int(world.x - 1, world.y, world.z), new Vector3Int(mask, local.y, local.z));

                if (local.y == 0)
                    __Set(new Vector3Int(world.x, world.y - 1, world.z), new Vector3Int(local.x, mask, local.z));

                if (local.z == 0)
                    __Set(new Vector3Int(world.x, world.y, world.z - 1), new Vector3Int(local.x, local.y, mask));

                if (local.x == 0 && local.y == 0)
                    __Set(new Vector3Int(world.x - 1, world.y - 1, world.z), new Vector3Int(mask, mask, local.z));

                if (local.y == 0 && local.z == 0)
                    __Set(new Vector3Int(world.x, world.y - 1, world.z - 1), new Vector3Int(local.x, mask, mask));

                if (local.x == 0 && local.z == 0)
                    __Set(new Vector3Int(world.x - 1, world.y, world.z - 1), new Vector3Int(mask, local.y, mask));

                if (local.x == 0 && local.y == 0 && local.z == 0)
                    __Set(new Vector3Int(world.x - 1, world.y - 1, world.z - 1), new Vector3Int(mask, mask, mask));

                if (local.x == 1)
                    __Set(new Vector3Int(world.x - 1, world.y, world.z), new Vector3Int(size, local.y, local.z));

                if (local.y == 1)
                    __Set(new Vector3Int(world.x, world.y - 1, world.z), new Vector3Int(local.x, size, local.z));

                if (local.z == 1)
                    __Set(new Vector3Int(world.x, world.y, world.z - 1), new Vector3Int(local.x, local.y, size));

                if (local.x == 1 && local.y == 1)
                    __Set(new Vector3Int(world.x - 1, world.y - 1, world.z), new Vector3Int(size, size, local.z));

                if (local.y == 1 && local.z == 1)
                    __Set(new Vector3Int(world.x, world.y - 1, world.z - 1), new Vector3Int(local.x, size, size));

                if (local.x == 1 && local.z == 1)
                    __Set(new Vector3Int(world.x - 1, world.y, world.z - 1), new Vector3Int(size, local.y, size));

                if (local.x == 1 && local.y == 1 && local.z == 1)
                    __Set(new Vector3Int(world.x - 1, world.y - 1, world.z - 1), new Vector3Int(size, size, size));

                return true;
            }
        }

        public class BoundsBuilder : IEngineBuilder
        {
            private DualContouring __parent;
            private Dictionary<Vector3Int, BoundsInt> __bounds;

            public DualContouring parent
            {
                get
                {
                    return __parent;
                }
            }

            public BoundsBuilder(DualContouring parent)
            {
                __parent = parent;
            }

            public bool Check(Vector3Int world)
            {
                if (__parent == null)
                    return false;

                if (__bounds == null)
                    return false;

                return __bounds.ContainsKey(world);
            }

            public bool Create(Vector3Int world)
            {
                if (__parent == null)
                    return false;

                if (__bounds == null)
                    return false;

                BoundsInt bounds;
                if (!__bounds.TryGetValue(world, out bounds) || !__bounds.Remove(world))
                    return false;

                Vector3Int min = bounds.min, max = bounds.max;

                if (__parent.__blocks == null)
                    __parent.__blocks = new Dictionary<Vector3Int, Dictionary<Vector3Int, Block>>(new Vector3IntEqualityComparer());

                int i, j, k;
                Dictionary<Vector3Int, Block> blocks;
                if (!__parent.__blocks.TryGetValue(world, out blocks) || blocks == null)
                {
                    blocks = new Dictionary<Vector3Int, Block>(1 << (__parent.__depth * 3), new Vector3IntEqualityComparer());

                    __parent.__blocks[world] = blocks;
                }
                else
                {
                    for (i = min.x; i < max.x; ++i)
                    {
                        for (j = min.y; j < max.y; ++j)
                        {
                            for (k = min.z; k < max.z; ++k)
                                blocks.Remove(new Vector3Int(i, j, k));
                        }
                    }
                }

                int l, m, n, size = 1 << __parent.__depth;
                float x, y;
                Vector3Int local, offset, position;
                Vector3 from, to, point, normal;
                Block block;

                world *= size - 1;

                for (i = min.x; i <= max.x; ++i)
                {
                    for (j = min.y; j <= max.y; ++j)
                    {
                        for (k = min.z; k <= max.z; ++k)
                        {
                            if (i == max.x && j == max.y && k == max.z)
                                break;

                            local = new Vector3Int(i, j, k);

                            from = Vector3.Scale(world + local, __parent.__scale);
                            x = __parent.GetDensity(from);
                            for (m = 0; m < (int)Axis.Unkown; ++m)
                            {
                                to = from + Vector3.Scale(__axisOffsets[m], __parent.__scale);

                                y = __parent.GetDensity(to);
                                if (x < 0.0f == y < 0.0f)
                                    continue;

                                point = __parent.ApproximateZeroCrossingPosition(from, to);
                                normal = __parent.CalculateSurfaceNormal(point);
                                for (n = 0; n < 4; ++n)
                                {
                                    offset = local + __edgeToBlockOffsets[m, n];
                                    if (offset.x == min.x - 1 || offset.y == min.y - 1 || offset.z == min.z - 1 || offset.x == max.x || offset.y == max.y || offset.z == max.z)
                                        continue;

                                    if (!blocks.TryGetValue(offset, out block))
                                    {
                                        block = new Block();

                                        position = world + offset;

                                        for (l = 0; l < 8; ++l)
                                        {
                                            if (__parent.GetDensity(Vector3.Scale(position + __childMinOffsets[l], __parent.__scale)) < 0.0f)
                                                block.corners |= 1 << l;
                                        }
                                    }

                                    block.normal += normal;
                                    block.qef.Add(new Qef.Data(point, normal));

                                    blocks[offset] = block;
                                }
                            }
                        }
                    }
                }

                return true;
            }

            public bool Set(BoundsInt bounds)
            {
                if (__parent == null)
                    return false;

                int size = (1 << __parent.__depth) + 1, mask = size - 2;
                Vector3Int min = bounds.min, max = bounds.max, world, local;

                world = new Vector3Int(min.x / mask, min.y / mask, min.z / mask);
                local = min - world * mask;
                if (local.x < 2)
                    --world.x;

                if (local.y < 2)
                    --world.y;

                if (local.z < 2)
                    --world.z;

                min = world;
                max = new Vector3Int(max.x / mask, max.y / mask, max.z / mask);

                int i, j, k;
                Vector3Int extends = new Vector3Int(size, size, size);
                BoundsInt source, destination;
                for (i = min.x; i <= max.x; ++i)
                {
                    for (j = min.y; j <= max.y; ++j)
                    {
                        for (k = min.z; k <= max.z; ++k)
                        {
                            world = new Vector3Int(i, j, k);
                            local = world * mask;
                            destination = new BoundsInt(local, extends);
                            destination.ClampToBounds(bounds);
                            destination.position -= local;

                            local = destination.min;
                            if (local.x > 0)
                                --local.x;

                            if (local.y > 0)
                                --local.y;

                            if (local.z > 0)
                                --local.z;

                            destination.min = local;

                            local = destination.max;
                            if (local.x <= mask)
                                ++local.x;

                            if (local.y <= mask)
                                ++local.y;

                            if (local.z <= mask)
                                ++local.z;

                            destination = new BoundsInt(destination.position, local - destination.min);

                            if (__bounds == null)
                                __bounds = new Dictionary<Vector3Int, BoundsInt>();

                            if (__bounds.TryGetValue(world, out source))
                            {
                                local = Vector3Int.Min(destination.min, source.min);
                                destination = new BoundsInt(local, Vector3Int.Max(destination.max, source.max) - local);
                            }

                            __bounds[world] = destination;
                        }
                    }
                }

                return true;
            }

            public bool Set(Vector3Int position)
            {
                if (__parent == null)
                    return false;

                int mask = (1 << __parent.__depth) - 1;

                Vector3Int offset = new Vector3Int(position.x / mask, position.y / mask, position.z / mask);
                position -= offset * mask;
                if (position.x < 0)
                {
                    position.x += mask;

                    --offset.x;
                }

                if (position.y < 0)
                {
                    position.y += mask;

                    --offset.y;
                }

                if (position.z < 0)
                {
                    position.z += mask;

                    --offset.z;
                }

                return __Set(offset, position);
            }

            private bool __Set(Vector3Int world, Vector3Int local)
            {
                if (__parent == null)
                    return false;

                int size = 1 << __parent.__depth;
                Vector3Int min = Vector3Int.Max(local - Vector3Int.one, Vector3Int.zero);
                Vector3Int max = Vector3Int.Min(local + Vector3Int.one, new Vector3Int(size, size, size));

                if (__bounds == null)
                    __bounds = new Dictionary<Vector3Int, BoundsInt>(new Vector3IntEqualityComparer());

                BoundsInt bounds;
                if (__bounds.TryGetValue(world, out bounds))
                {
                    min = Vector3Int.Min(min, bounds.min);
                    max = Vector3Int.Max(max, bounds.max);
                }

                __bounds[world] = new BoundsInt(min, max - min);

                int mask = size - 1;
                if (local.x == 0)
                    __Set(new Vector3Int(world.x - 1, world.y, world.z), new Vector3Int(mask, local.y, local.z));

                if (local.y == 0)
                    __Set(new Vector3Int(world.x, world.y - 1, world.z), new Vector3Int(local.x, mask, local.z));

                if (local.z == 0)
                    __Set(new Vector3Int(world.x, world.y, world.z - 1), new Vector3Int(local.x, local.y, mask));

                if (local.x == 0 && local.y == 0)
                    __Set(new Vector3Int(world.x - 1, world.y - 1, world.z), new Vector3Int(mask, mask, local.z));

                if (local.y == 0 && local.z == 0)
                    __Set(new Vector3Int(world.x, world.y - 1, world.z - 1), new Vector3Int(local.x, mask, mask));

                if (local.x == 0 && local.z == 0)
                    __Set(new Vector3Int(world.x - 1, world.y, world.z - 1), new Vector3Int(mask, local.y, mask));

                if (local.x == 0 && local.y == 0 && local.z == 0)
                    __Set(new Vector3Int(world.x - 1, world.y - 1, world.z - 1), new Vector3Int(mask, mask, mask));

                if (local.x == 1)
                    __Set(new Vector3Int(world.x - 1, world.y, world.z), new Vector3Int(size, local.y, local.z));

                if (local.y == 1)
                    __Set(new Vector3Int(world.x, world.y - 1, world.z), new Vector3Int(local.x, size, local.z));

                if (local.z == 1)
                    __Set(new Vector3Int(world.x, world.y, world.z - 1), new Vector3Int(local.x, local.y, size));

                if (local.x == 1 && local.y == 1)
                    __Set(new Vector3Int(world.x - 1, world.y - 1, world.z), new Vector3Int(size, size, local.z));

                if (local.y == 1 && local.z == 1)
                    __Set(new Vector3Int(world.x, world.y - 1, world.z - 1), new Vector3Int(local.x, size, size));

                if (local.x == 1 && local.z == 1)
                    __Set(new Vector3Int(world.x - 1, world.y, world.z - 1), new Vector3Int(size, local.y, size));

                if (local.x == 1 && local.y == 1 && local.z == 1)
                    __Set(new Vector3Int(world.x - 1, world.y - 1, world.z - 1), new Vector3Int(size, size, size));

                return true;
            }
        }
        
        public class Octree : IEngineProcessor<DualContouring>
        {
            public enum Type
            {
                Internal,
                Psuedo,
                Leaf
            }

            public struct Info : IEquatable<Info>
            {
                public int depth;

                public Vector3Int position;

                public Info(int depth, Vector3Int position)
                {
                    this.depth = depth;
                    this.position = position;
                }

                public bool Equals(Info info)
                {
                    return depth == info.depth && position == info.position;
                }
            }

            private struct Tile<T>
            {
                public T x;
                public T y;
                public T z;
                public T w;

                public T this[int index]
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
                            case 3:
                                return w;
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
                            case 3:
                                w = value;
                                break;
                            default:
                                throw new IndexOutOfRangeException();
                        }
                    }
                }

                public Tile(T x, T y, T z, T w)
                {
                    this.x = x;
                    this.y = y;
                    this.z = z;
                    this.w = w;
                }
            }

            private struct Node
            {
                public bool isMiddleCorner;
                public Type type;
                public int childMask;
                public Block block;
            }

            private struct NodeInfo
            {
                public Type type;

                public int corners;

                public Info info;

                public NodeInfo(Type type, int corners, Info info)
                {
                    this.type = type;
                    this.corners = corners;
                    this.info = info;
                }
            }

            private struct Node2
            {
                public Vector3Int sizeDelta;

                public NodeInfo x;
                public NodeInfo y;

                public bool isInternal
                {
                    get
                    {
                        return x.type == Type.Internal || y.type == Type.Internal;
                    }
                }

                public NodeInfo this[int index]
                {
                    get
                    {
                        switch (index)
                        {
                            case 0:
                                return x;
                            case 1:
                                return y;
                            default:
                                throw new IndexOutOfRangeException();
                        }
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
            }

            private struct Node4
            {
                public Vector3Int sizeDelta;

                public NodeInfo x;
                public NodeInfo y;
                public NodeInfo z;
                public NodeInfo w;

                public bool isInternal
                {
                    get
                    {
                        return x.type == Type.Internal || y.type == Type.Internal || z.type == Type.Internal || w.type == Type.Internal;
                    }
                }

                public bool IsLeaf
                {
                    get
                    {
                        return x.type == Type.Leaf && y.type == Type.Leaf && z.type == Type.Leaf && w.type == Type.Leaf;
                    }
                }

                public NodeInfo this[int index]
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
                            case 3:
                                return w;
                            default:
                                throw new IndexOutOfRangeException();
                        }
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
                            case 3:
                                w = value;
                                break;
                            default:
                                throw new IndexOutOfRangeException();
                        }
                    }
                }
            }

            private struct InfoIntEqualityComparer : IEqualityComparer<Info>
            {
                bool IEqualityComparer<Info>.Equals(Info x, Info y)
                {
                    return x.depth == y.depth && x.position == y.position;
                }

                int IEqualityComparer<Info>.GetHashCode(Info obj)
                {
                    return obj.GetHashCode();
                }
            }

            #region BUILD_PROC_TABLES
            private static readonly Vector3Int[] __cellProcFaceMask =
            {
                new Vector3Int(0, 4, 0),
                new Vector3Int(1, 5, 0),
                new Vector3Int(2, 6, 0),
                new Vector3Int(3, 7, 0),
                new Vector3Int(0, 2, 1),
                new Vector3Int(4, 6, 1),
                new Vector3Int(1, 3, 1),
                new Vector3Int(5, 7, 1),
                new Vector3Int(0, 1, 2),
                new Vector3Int(2, 3, 2),
                new Vector3Int(4, 5, 2),
                new Vector3Int(6, 7, 2)
            };

            private static readonly int[,] __cellProcEdgeMask = { { 0, 1, 2, 3, 0 }, { 4, 5, 6, 7, 0 }, { 0, 4, 1, 5, 1 }, { 2, 6, 3, 7, 1 }, { 0, 2, 4, 6, 2 }, { 1, 3, 5, 7, 2 } };

            private static readonly int[,,] __faceProcFaceMask =
            {
                {{4,0,0},{5,1,0},{6,2,0},{7,3,0}},
                {{2,0,1},{6,4,1},{3,1,1},{7,5,1}},
                {{1,0,2},{3,2,2},{5,4,2},{7,6,2}}
            };

            private static readonly int[,,] __faceProcEdgeMask =
            {
                {{1,4,0,5,1,1},{1,6,2,7,3,1},{0,4,6,0,2,2},{0,5,7,1,3,2}},
                {{0,2,3,0,1,0},{0,6,7,4,5,0},{1,2,0,6,4,2},{1,3,1,7,5,2}},
                {{1,1,0,3,2,0},{1,5,4,7,6,0},{0,1,5,0,4,1},{0,3,7,2,6,1}}
            };

            private static readonly int[,,] __edgeProcEdgeMask =
            {
                {{3,2,1,0,0},{7,6,5,4,0}},
                {{5,1,4,0,1},{7,3,6,2,1}},
                {{6,4,2,0,2},{7,5,3,1,2}},
            };

            private static readonly int[,] __processEdgeMask = { { 3, 2, 1, 0 }, { 7, 5, 6, 4 }, { 11, 10, 9, 8 } };

            private static readonly int[,] __faceOrders =
            {
                { 0, 0, 1, 1 },
                { 0, 1, 0, 1 },
            };
            #endregion

            #region INTER_FREE_TABLES
            private static readonly int[,,] __triangleIndices = { { { 0, 1, 3, 2 }, { 3, 2, 0, 1 } }, { { 2, 0, 1, 3 }, { 1, 3, 2, 0 } } };
            private static readonly Vector2Int[] __neighborNodeIndices = { new Vector2Int(0, 1), new Vector2Int(1, 3), new Vector2Int(2, 3), new Vector2Int(0, 2) };
            private static readonly Vector2Int[,] __faceAxisOffsets =
            {
                { new Vector2Int(1, -1), new Vector2Int(2, 0), new Vector2Int(1, 0), new Vector2Int(2, -1) },
                { new Vector2Int(2, -1), new Vector2Int(0, 0), new Vector2Int(2, 0), new Vector2Int(0, -1) },
                { new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 0), new Vector2Int(1, -1) }
            };

            private static readonly int[,] __faceAxes =
            {
                {2,1,2,1},
                {0,2,0,2},
                {1,0,1,0}
            };
            #endregion
            
            private int __sweeps;
            private int __depth;
            private Vector3 __scale;
            private Vector3 __offset;
            private Node __root;
            private Dictionary<Vector3Int, Vector3> __points;
            private Dictionary<Vector3Int, Node>[] __nodes;

            public int depth
            {
                get
                {
                    return __depth;
                }
            }

            public Vector3 scale
            {
                get
                {
                    return __scale;
                }
            }

            public Vector3 offset
            {
                get
                {
                    return __offset;
                }
            }

            public bool IsBoundary(Vector3Int position, int size, Boundary boundary)
            {
                if ((boundary & Boundary.All) == 0)
                    return false;

                if ((boundary & Boundary.LeftLowerBack) != 0)
                {
                    if (((boundary & Boundary.Left) == Boundary.Left && position.x == 0) ||
                        ((boundary & Boundary.Lower) == Boundary.Lower && position.y == 0) ||
                        ((boundary & Boundary.Back) == Boundary.Back && position.z == 0))
                        return true;
                }

                if ((boundary & Boundary.RightUpperFront) == 0)
                    return false;

                int max = 1 << __depth;
                return ((boundary & Boundary.Right) == Boundary.Right && position.x + size == max) ||
                    ((boundary & Boundary.Upper) == Boundary.Upper && position.y + size == max) ||
                    ((boundary & Boundary.Front) == Boundary.Front && position.z + size == max);
            }

            public bool Get(Info info, out Vector3 point)
            {
                if(__points == null)
                {
                    point = default(Vector3);

                    return false;
                }

                return __points.TryGetValue(info.position * (1 << (__depth - info.depth)), out point);
            }

            public bool Get(Info info, out Block block)
            {
                if (info.depth < 1)
                {
                    block = __root.block;

                    return true;
                }

                if (__nodes == null || __nodes.Length < info.depth)
                {
                    block = default(Block);

                    return false;
                }

                Dictionary<Vector3Int, Node> nodes = __nodes[info.depth - 1];
                if (nodes == null)
                {
                    block = default(Block);

                    return false;
                }

                Node node;
                if (!nodes.TryGetValue(info.position, out node))
                {
                    block = default(Block);

                    return false;
                }

                block = node.block;

                return true;
            }

            public bool Create(Boundary boundary, int sweeps, float threshold, Vector3Int world, DualContouring parent)
            {
                if (parent == null || parent.__depth < 1 || parent.__blocks == null)
                    return false;

                Dictionary<Vector3Int, Block> blocks;
                if (!parent.__blocks.TryGetValue(world, out blocks) || blocks == null || blocks.Count < 1)
                    return false;

                __sweeps = sweeps;
                __depth = parent.__depth;
                __scale = parent.__scale;
                __offset = Vector3.Scale(world * ((1 << __depth) - 1), parent.__scale);

                int depth = __nodes == null ? 0 : __nodes.Length;
                if (depth < __depth)
                    Array.Resize(ref __nodes, __depth);
                
                Dictionary<Vector3Int, Node> source;
                for (int i = 0; i < depth; ++i)
                {
                    source = __nodes[i];
                    if (source != null)
                        source.Clear();
                }

                depth = __depth;

                source = __nodes[--depth];

                if (source == null)
                {
                    source = new Dictionary<Vector3Int, Node>(new Vector3IntEqualityComparer());

                    __nodes[depth] = source;
                }

                Vector3Int position;
                Node node;
                foreach (KeyValuePair<Vector3Int, Block> pair in blocks)
                {
                    position = pair.Key;
                    
                    node.type = Type.Leaf;

                    node.isMiddleCorner = false;

                    node.block = pair.Value;

                    node.childMask = 0;

                    source[position] = node;
                }

                if (__points != null)
                    __points.Clear();

                int size, shift;
                Vector3Int offset, key;
                Vector3 point, min, max;
                Node temp;
                Dictionary<Vector3Int, Node> destination;
                List<KeyValuePair<Vector3Int, Node>> nodes = null;
                while (depth > 0)
                {
                    if (source == null)
                        return false;

                    destination = __nodes[--depth];

                    size = 1 << (__depth - depth - 2);

                    foreach (KeyValuePair<Vector3Int, Node> pair in source)
                    {
                        if (destination == null)
                        {
                            destination = new Dictionary<Vector3Int, Node>(size * size * size, new Vector3IntEqualityComparer());

                            __nodes[depth] = destination;
                        }

                        position = pair.Key;

                        offset = new Vector3Int(position.x >> 1, position.y >> 1, position.z >> 1);
                        if (!destination.TryGetValue(offset, out node))
                        {
                            node = new Node();
                            node.type = Type.Psuedo;
                            node.block = new Block();
                        }
                        
                        temp = pair.Value;
                        if(temp.type == Type.Internal)
                        {
                            if (node.type != Type.Internal)
                            {
                                node.type = Type.Internal;

                                destination[offset] = node;
                            }

                            continue;
                        }

                        point = temp.block.qef.Solve(sweeps);

                        key = position * size;
                        if (temp.type != Type.Leaf && (IsBoundary(key, size, boundary) || temp.block.qef.GetError(point) > threshold))
                        {
                            temp.type = Type.Internal;

                            if (nodes == null)
                                nodes = new List<KeyValuePair<Vector3Int, Node>>();

                            nodes.Add(new KeyValuePair<Vector3Int, Node>(position, temp));

                            if (node.type != Type.Internal)
                            {
                                node.type = Type.Internal;

                                destination[offset] = node;
                            }

                            continue;
                        }

                        if(temp.isMiddleCorner && temp.childMask != 0)
                        {
                            temp.block.corners |= ~temp.childMask & 0xff;

                            if (nodes == null)
                                nodes = new List<KeyValuePair<Vector3Int, Node>>();

                            nodes.Add(new KeyValuePair<Vector3Int, Node>(position, temp));
                        }

                        //__DestroyChildren(depth + 2, position);

                        min = Vector3.Scale(key, __scale) + __offset;
                        max = min + parent.__scale * size;
                        
                        if (__points == null)
                            __points = new Dictionary<Vector3Int, Vector3>(1 << (__depth * 3), new Vector3IntEqualityComparer());

                        __points[key] = point.x < min.x || point.y < min.y || point.z < min.z || point.x > max.x || point.y > max.y || point.z > max.z ?
                            temp.block.qef.massPoint : point;

                        if (node.type == Type.Internal)
                            continue;

                        position -= offset * 2;
                        shift = position.x << 2 | position.y << 1 | position.z;
                        
                        node.isMiddleCorner = ((temp.block.corners >> (7 - shift)) & 1) != 0;
                        node.childMask |= 1 << shift;

                        node.block.corners |= ((temp.block.corners >> shift) & 1) << shift;
                        node.block.normal += temp.block.normal;
                        node.block.qef.Add(temp.block.qef.data);

                        destination[offset] = node;
                    }

                    if(nodes != null)
                    {
                        foreach(KeyValuePair<Vector3Int, Node> pair in nodes)
                            source[pair.Key] = pair.Value;

                        nodes.Clear();
                    }

                    source = destination;
                }

                if (source == null)
                    return false;

                __root = new Node();
                __root.type = Type.Psuedo;
                __root.block = new Block();

                size = 1 << (__depth - depth - 1);

                foreach (KeyValuePair<Vector3Int, Node> pair in source)
                {
                    position = pair.Key;

                    offset = new Vector3Int(position.x >> 1, position.y >> 1, position.z >> 1);

                    temp = pair.Value;

                    if (temp.type == Type.Internal)
                    {
                        __root.type = Type.Internal;

                        continue;
                    }

                    point = temp.block.qef.Solve(sweeps);

                    key = position * size;
                    if (temp.type != Type.Leaf && (IsBoundary(key, size, boundary) || temp.block.qef.GetError(point) > threshold))
                    {
                        temp.type = Type.Internal;

                        if (nodes == null)
                            nodes = new List<KeyValuePair<Vector3Int, Node>>();

                        nodes.Add(new KeyValuePair<Vector3Int, Node>(position, temp));

                        __root.type = Type.Internal;

                        continue;
                    }

                    if (temp.isMiddleCorner && temp.childMask != 0)
                    {
                        temp.block.corners |= ~temp.childMask & 0xff;

                        if (nodes == null)
                            nodes = new List<KeyValuePair<Vector3Int, Node>>();

                        nodes.Add(new KeyValuePair<Vector3Int, Node>(position, temp));
                    }

                    //__DestroyChildren(depth + 1, position);
                    
                    min = Vector3.Scale(key, __scale) + __offset;
                    max = min + parent.__scale * size;

                    if (__points == null)
                        __points = new Dictionary<Vector3Int, Vector3>();

                    __points[key] = point.x < min.x || point.y < min.y || point.z < min.z || point.x > max.x || point.y > max.y || point.z > max.z ?
                        temp.block.qef.massPoint : point;

                    if (__root.type == Type.Internal)
                        continue;

                    position -= offset * 2;
                    shift = position.x << 2 | position.y << 1 | position.z;

                    __root.isMiddleCorner = ((temp.block.corners >> (7 - shift)) & 1) != 0;
                    __root.childMask |= 1 << shift;

                    __root.block.corners |= ((temp.block.corners >> shift) & 1) << shift;
                    __root.block.normal += temp.block.normal;
                    __root.block.qef.Add(temp.block.qef.data);
                }

                if (nodes != null)
                {
                    foreach (KeyValuePair<Vector3Int, Node> pair in nodes)
                        source[pair.Key] = pair.Value;
                }
                
                if (__root.type == Type.Internal)
                    return true;

                if (__root.type == Type.Leaf)
                    return false;

                point = __root.block.qef.Solve(sweeps);
                if (__root.block.qef.GetError(point) > threshold)
                {
                    __root.type = Type.Internal;

                    return true;
                }

                return false;
            }

            public void Build(Boundary boundary, 
                IList<Vertex> vertices,
                IDictionary<Edge<Info>, int> edgeIndices,
                IDictionary<Info, int> infoIndices,
                Action<Face> faces)
            {
                __ContourCellProc(
                    boundary, 
                    Vector3Int.zero, 
                    new NodeInfo(__root.type, __root.block.corners, new Info(0, Vector3Int.zero)),
                    vertices,
                    edgeIndices, 
                    infoIndices, 
                    faces);
            }

            public bool Build(Boundary boundary, Func<Face, IReadOnlyList<Vertex>, int> subMeshHandler, out MeshData<Vector3> meshData)
            {
                List<Vertex> vertices = new List<Vertex>();
                Dictionary<Edge<Info>, int> edgeIndices = new Dictionary<Edge<Info>, int>();
                Dictionary<Info, int> infoIndices = new Dictionary<Info, int>();
                List<Face> faces = new List<Face>();
                Build(boundary, vertices, edgeIndices, infoIndices, faces.Add);
                
                List<MeshData<Vector3>.Triangle> triangles = null;
                foreach(Face face in faces)
                {
                    if (triangles == null)
                        triangles = new List<MeshData<Vector3>.Triangle>();

                    triangles.Add(new MeshData<Vector3>.Triangle(subMeshHandler == null ? 0 : subMeshHandler(face, vertices), face.indices));
                }
                
                List<MeshData<Vector3>.Vertex> result = null;
                foreach(Vertex vertex in vertices)
                {
                    if (result == null)
                        result = new List<MeshData<Vector3>.Vertex>();

                    result.Add(vertex);
                }

                meshData = new MeshData<Vector3>(result == null ? null : result.ToArray(), triangles == null ? null : triangles.ToArray());

                return meshData.vertices != null && meshData.triangles != null;
            }
            
            private bool __Destroy(int depth, Vector3Int position)
            {
                if (__nodes == null || __nodes.Length < depth)
                    return false;

                Dictionary<Vector3Int, Node> nodes = __nodes[depth - 1];
                if (nodes == null)
                    return false;

                Node node;
                if (!nodes.TryGetValue(position, out node))
                    return false;

                if (!nodes.Remove(position))
                    return false;

                if (node.type != Type.Internal)
                {
                    if (__points != null)
                        __points.Remove(position * (1 << (__depth - depth)));
                }

                ++depth;
                position *= 2;
                for (int i = 0; i < 8; ++i)
                    __Destroy(depth, position + __childMinOffsets[i]);

                return true;
            }

            private bool __DestroyChildren(int depth, Vector3Int position)
            {
                bool result = false;

                ++depth;
                position *= 2;
                for (int i = 0; i < 8; ++i)
                    result = __Destroy(depth, position + __childMinOffsets[i]) || result;

                return result;
            }

            private bool __Get(Info info, out NodeInfo nodeInfo)
            {
                nodeInfo = default(NodeInfo);
                nodeInfo.info = info;

                if (info.depth < 1)
                {
                    nodeInfo.type = __root.type;

                    return true;
                }

                if (__nodes == null || __nodes.Length < info.depth)
                    return false;

                Dictionary<Vector3Int, Node> nodes = __nodes[info.depth - 1];
                if (nodes == null)
                    return false;

                Node node;
                if (!nodes.TryGetValue(info.position, out node))
                    return false;

                nodeInfo.type = node.type;
                nodeInfo.corners = node.block.corners;

                return true;
            }

            private bool __Get(int childIndex, Info info, out NodeInfo node)
            {
                return __Get(new Info(info.depth + 1, info.position * 2 + __childMinOffsets[childIndex]), out node);
            }

            private Vector3Int __Get(Info info, int childIndex)
            {
                return (info.position + __childMinOffsets[childIndex]) * (1 << (__depth - info.depth));
            }

            private bool __Get(Info info, Vector2Int vertexIndices, out Vector3Int offset)
            {
                Node node;
                if (info.depth < 1)
                    node = __root;
                else if (__nodes == null || __nodes.Length < info.depth)
                {
                    offset = __Get(info, vertexIndices.x);

                    return false;
                }
                else
                {
                    Dictionary<Vector3Int, Node> nodes = __nodes[info.depth - 1];
                    if (nodes == null || !nodes.TryGetValue(info.position, out node))
                    {
                        offset = __Get(info, vertexIndices.x);

                        return false;
                    }
                }

                /*if ((node.block.corners & (1 << vertexIndices.y)) != 0)
                {
                    if (node.isMiddleCorner && (node.childMask & (1 << vertexIndices.y)) == 0)
                        offset = __Get(new Info(info.depth + 1, info.position * 2 + __childMinOffsets[7 - vertexIndices.y]), vertexIndices.y);
                    else
                        offset = __Get(info, vertexIndices.y);

                    return true;
                }*/

                bool result = true;
                if (info.depth < __depth)
                {
                    if ((node.childMask & (1 << vertexIndices.y)) != 0 &&
                        __Get(new Info(info.depth + 1, info.position * 2 + __childMinOffsets[vertexIndices.y]), vertexIndices, out offset))
                        return true;

                    if ((node.childMask & (1 << vertexIndices.x)) != 0 &&
                        __Get(new Info(info.depth + 1, info.position * 2 + __childMinOffsets[vertexIndices.x]), vertexIndices, out offset))
                        return true;

                    result = false;
                }
                else if ((node.block.corners & (1 << vertexIndices.y)) != 0)
                {
                    offset = __Get(info, vertexIndices.y);

                    return true;
                }
                else if ((node.block.corners & (1 << vertexIndices.x)) == 0)
                {
                    /*int shift = 7 - vertexIndices.x;
                    if ((node.block.corners & (1 << shift)) != 0)
                    {
                        offset = __Get(info, shift);

                        return true;
                    }*/

                    result = false;
                }
                
                offset = __Get(info, vertexIndices.x);

                return result;
            }

            private bool __TestFace(Axis axis, int length, Vector3Int sizeDelta, Vector3 x, Vector3 y)
            {
                int indexX = ((int)axis + 1) % 3, indexY = ((int)axis + 2) % 3;
                Vector3 distance = y - x, axisX = Vector3.zero, axisY = Vector3.zero;
                axisX[indexX] = 1.0f;
                axisY[indexY] = 1.0f;

                axisX = Vector3.Cross(axisX, distance);
                axisY = Vector3.Cross(axisY, distance);

                float size = length * __scale[(int)axis];
                Vector3 delta = Vector3.Scale(sizeDelta, __scale) + __offset;
                Triangle triangleX = new Triangle(x, y, y), triangleY = new Triangle(delta, delta, delta);
                triangleY.y[indexX] += size;
                triangleY.z[indexY] += size;

                return !triangleX.IsSeparating(axisX, triangleY) && !triangleX.IsSeparating(axisY, triangleY);
            }

            private bool __TestEdge(Axis axis, int length, Vector3Int sizeDelta, Tile<Vertex> tile)
            {
                Vector3 x = Vector3.Scale(sizeDelta, __scale) + __offset, y = x;
                y[(int)axis] += length * __scale[(int)axis];
                
                int i, j;
                Vector3 axisX, axisY, axisZ, temp;
                Triangle triangleX, triangleY;
                for(i = 0; i < 2; ++i)
                {
                    for (j = 0; j < 2; ++j)
                    {
                        triangleX.x = tile[__triangleIndices[i, j, 0]].position;
                        triangleX.y = tile[__triangleIndices[i, j, 1]].position;
                        triangleX.z = tile[__triangleIndices[i, j, 2]].position;

                        if (triangleX.IsSeparating(triangleX.normalVector, x, y))
                            continue;

                        temp = tile[__triangleIndices[i, j, 3]].position;

                        triangleY = new Triangle(x, y, temp);

                        axisX = y - x;
                        axisY = temp - y;
                        axisZ = x - temp;
                        temp = triangleX.z - triangleX.x;

                        axisX = Vector3.Cross(axisX, temp);
                        axisY = Vector3.Cross(axisY, temp);
                        axisZ = Vector3.Cross(axisZ, temp);

                        if (triangleY.IsSeparating(axisX, triangleX.x, triangleX.z) ||
                            triangleY.IsSeparating(axisY, triangleX.x, triangleX.z) ||
                            triangleY.IsSeparating(axisZ, triangleX.x, triangleX.z))
                            continue;

                        return true;
                    }
                }

                return false;
            }

            private Vertex __MakeFaceVertex(Axis axis, int length, Vector3Int sizeDelta, Edge<Vertex> edge)
            {
                Vertex result = new Vertex(Vector3.Scale(sizeDelta, __scale) + __offset, edge.x.normal + edge.y.normal, edge.x.qef + edge.y.qef);
                
                Axis axisX, axisY;
                Vector2 atb;
                Edge<Vector2> ata;
                switch (axis)
                {
                    case Axis.X:
                        axisX = Axis.Y;
                        axisY = Axis.Z;

                        ata.x.x = result.qef.data.ata.m11;
                        ata.x.y = result.qef.data.ata.m12;
                        ata.y.x = result.qef.data.ata.m12;
                        ata.y.y = result.qef.data.ata.m22;

                        atb.x = result.qef.data.atb.y - result.position.x * result.qef.data.ata.m01;
                        atb.y = result.qef.data.atb.z - result.position.x * result.qef.data.ata.m02;
                        break;
                    case Axis.Y:
                        axisX = Axis.X;
                        axisY = Axis.Z;

                        ata.x.x = result.qef.data.ata.m00;
                        ata.x.y = result.qef.data.ata.m02;
                        ata.y.x = result.qef.data.ata.m02;
                        ata.y.y = result.qef.data.ata.m22;

                        atb.x = result.qef.data.atb.x - result.position.y * result.qef.data.ata.m01;
                        atb.y = result.qef.data.atb.z - result.position.y * result.qef.data.ata.m12;
                        break;
                    case Axis.Z:
                        axisX = Axis.X;
                        axisY = Axis.Y;

                        ata.x.x = result.qef.data.ata.m00;
                        ata.x.y = result.qef.data.ata.m01;
                        ata.y.x = result.qef.data.ata.m01;
                        ata.y.y = result.qef.data.ata.m11;

                        atb.x = result.qef.data.atb.x - result.position.z * result.qef.data.ata.m02;
                        atb.y = result.qef.data.atb.y - result.position.z * result.qef.data.ata.m12;
                        break;
                    default:
                        return result;
                }

                float determinant = ata.x.x * ata.y.y - ata.x.y * ata.y.x,
                    sizeX = __scale[(int)axisX] * length, 
                    sourceX = result.position[(int)axisX],
                    destinationX = sourceX + sizeX,
                    sizeY = __scale[(int)axisY] * length,
                    sourceY = result.position[(int)axisY],
                    destinationY = sourceY + sizeY;
                if(Mathf.Approximately(determinant, 0.0f))
                {
                    int count = 0, i, j;
                    float x, y, z;
                    Vector2 temp, point = Vector2.zero;
                    for(i = 0; i < 2; ++i)
                    {
                        temp = ata[i];
                        z = atb[i];

                        if (!Mathf.Approximately(temp.x, 0.0f))
                        {
                            for(j = 0; j < 2; ++j)
                            {
                                y = sourceY + sizeY * j;
                                x = (z - temp.y * y) / temp.x;
                                if(x >= sourceX && x <= destinationX)
                                {
                                    point.x += x;
                                    point.y += y;

                                    ++count;
                                }
                            }
                        }

                        if(!Mathf.Approximately(temp.y, 0.0f))
                        {
                            for(j = 0; j < 2; ++j)
                            {
                                x = sourceX + sizeX * j;
                                y = (z - temp.x * x) / temp.y;
                                if (y >= sourceY && y <= destinationY)
                                {
                                    point.x += x;
                                    point.y += y;

                                    ++count;
                                }
                            }
                        }
                    }

                    if (count > 0)
                    {
                        result.position[(int)axisX] = point.x / count;
                        result.position[(int)axisY] = point.y / count;
                    }
                    else
                    {
                        result.position[(int)axisX] = (sourceX + destinationX) * 0.5f;
                        result.position[(int)axisY] = (sourceY + destinationY) * 0.5f;
                    }

                    return result;
                }

                Vector4 invert = new Vector4(ata.y.y / determinant, -ata.x.y / determinant, -ata.x.y / determinant, ata.x.x / determinant);

                result.position[(int)axisX] = Mathf.Clamp(Vector2.Dot(new Vector2(invert.x, invert.y), atb), sourceX, destinationX);
                result.position[(int)axisY] = Mathf.Clamp(Vector2.Dot(new Vector2(invert.z, invert.w), atb), sourceY, destinationY);

                return result;
            }

            private Vertex __MakeEdgeVertex(Axis axis, int length, Vector3Int sizeDelta, Tile<Vertex> tile)
            {
                Vertex result = new Vertex(
                    Vector3.Scale(sizeDelta, __scale) + __offset, 
                    tile.x.normal + tile.y.normal + tile.z.normal + tile.w.normal, 
                    tile.x.qef + tile.y.qef + tile.z.qef + tile.w.qef);
                float source = result.position[(int)axis], destination = source + __scale[(int)axis] * length;
                //result.position[(int)axis] = destination;
                /*switch(axis)
                {
                    case Axis.X:
                        a = result.qef.data.ata.m00;
                        b = result.qef.data.atb.x - result.qef.data.ata.m01 * sizeDelta.y - result.qef.data.ata.m02 * sizeDelta.z;
                        break;
                    case Axis.Y:
                        a = result.qef.data.ata.m11;
                        b = result.qef.data.atb.y - result.qef.data.ata.m01 * sizeDelta.x - result.qef.data.ata.m12 * sizeDelta.z;
                        break;
                    case Axis.Z:
                        a = result.qef.data.ata.m22;
                        b = result.qef.data.atb.z - result.qef.data.ata.m02 * sizeDelta.x - result.qef.data.ata.m12 * sizeDelta.y;
                        break;
                    default:
                        return result;
                }

                result.position[(int)axis] = Mathf.Clamp(b / a, source, destination);
                return result;*/

                int indexX = ((int)axis + 1) % 3, indexY = ((int)axis + 2) % 3, i;
                Vector2 vertex = new Vector2(result.position[indexX], result.position[indexY]);
                Vector4 point;
                Tile<Vector4> points = new Tile<Vector4>(tile.x.position, tile.y.position, tile.w.position, tile.z.position);
                for (i = 0; i < 4; ++i)
                {
                    point = points[i];
                    point = new Vector3(point[indexX], point[indexY], point[(int)axis]);

                    point.x -= vertex.x;
                    point.y -= vertex.y;
                    point.w = point.x * point.x + point.y * point.y;

                    if (Mathf.Approximately(point.w, 0.0f))
                    {
                        result.position[(int)axis] = Mathf.Clamp(point.z, source, destination);

                        return result;
                    }

                    point.w = Mathf.Sqrt(point.w);

                    points[i] = point;
                }

                int index;
                float temp, sine, cosine, tan, total = 0.0f;
                Vector4 x, y, z = Vector4.zero;
                for(i = 0; i < 4; ++i)
                {
                    index = (i + 1) & 3;
                    x = points[i];
                    y = points[index];
                    temp = x.w * y.w;
                    sine = (x.x * y.y - x.y * y.x) / temp;
                    cosine = (x.x * y.x + x.y * y.y) / temp + 1.0f;
                    
                    if(Mathf.Approximately(cosine, 0.0f))
                    {
                        result.position[(int)axis] = Mathf.Clamp((x.z * y.w + y.z * x.w) / (x.w + y.w), source, destination);

                        return result;
                    }

                    tan = sine / cosine;
                    temp = tan / x.w;
                    tan /= y.w;

                    z[i] += temp;
                    z[index] += tan;

                    total += temp + tan;
                }

                if (Mathf.Approximately(total, 0.0f))
                    result.position[(int)axis] = destination;
                else
                {
                    temp = 0.0f;
                    for (i = 0; i < 4; ++i)
                        temp += z[i] * points[i].z;

                    result.position[(int)axis] = Mathf.Clamp(temp / total, source, destination);
                }

                return result;
            }

            private bool __ContourProcessNoInter(
                bool isFlip, 
                Axis axis, 
                int depth, 
                Vector3Int sizeDelta, 
                Tile<Vertex> tile, 
                Node4 nodes, 
                IList<Vertex> vertices, 
                IDictionary<Edge<Info>, int> edgeIndices,
                IDictionary<Info, int> infoIndices,
                Action<Face> faces)
            {
                const int FLAG_CONTAINS = 0x01, FLAG_NEW = 0x03;

                bool isNeedTess = false;
                int numVertices = vertices == null ? 0 : vertices.Count, length = 1 << (__depth - depth), flag, index, delta, i;
                NodeInfo x, y;
                Vector2Int indices, temp;
                Vector3Int faceSizeDelta = default(Vector3Int);
                Edge<Info> edgeInfos;
                Edge<Vertex> edgeVertices;
                Tile<int> vertexIndices = default(Tile<int>), flags = default(Tile<int>);
                for (i = 0; i < 4; ++i)
                {
                    indices = __neighborNodeIndices[i];
                    x = nodes[indices.x];
                    y = nodes[indices.y];
                    if (x.info.depth == y.info.depth)
                        continue;

                    edgeInfos = new Edge<Info>(x.info, y.info);
                    if (edgeIndices != null && edgeIndices.TryGetValue(edgeInfos, out index))
                    {
                        vertexIndices[i] = index;
                        
                        flags[i] = FLAG_CONTAINS;
                        
                        isNeedTess = true;
                    }
                    else
                    {
                        flag = 1 << (__depth - Mathf.Max(x.info.depth, y.info.depth));

                        index = __faceAxes[(int)axis, i];
                        faceSizeDelta[index] = sizeDelta[index];

                        temp = __faceAxisOffsets[(int)axis, i];

                        faceSizeDelta[temp.x] = sizeDelta[temp.x] + temp.y * flag;

                        delta = sizeDelta[(int)axis];
                        faceSizeDelta[(int)axis] = delta - (delta & (flag - 1));

                        edgeVertices = new Edge<Vertex>(tile[indices.x], tile[indices.y]);
                        if (__TestFace((Axis)index, flag, faceSizeDelta, edgeVertices.x.position, edgeVertices.y.position))
                            continue;

                        index = numVertices++;

                        if (vertices != null)
                            vertices.Add(default(Vertex)/*__MakeFaceVertex((Axis)index, flag, faceSizeDelta, edgeVertices)*/);

                        edgeIndices[edgeInfos] = index;

                        vertexIndices[i] = index;

                        flags[i] = FLAG_NEW;

                        isNeedTess = true;
                    }
                }
                
                if (!isNeedTess && !__TestEdge(axis, length, sizeDelta, tile))
                    isNeedTess = true;

                Vector3Int flipped = isFlip ? new Vector3Int(0, 1, 2) : new Vector3Int(2, 1, 0);
                if (isNeedTess)
                {
                    Vertex vertex = __MakeEdgeVertex(axis, length, sizeDelta, tile);

                    delta = numVertices++;

                    if (vertices != null)
                        vertices.Add(vertex);

                    if (faces != null && infoIndices != null)
                    {
                        Face face = default(Face);
                        face.depth = depth;
                        face.axis = axis;
                        face.sizeDelta = sizeDelta;
                        for (i = 0; i < 4; ++i)
                        {
                            indices = __neighborNodeIndices[i];
                            if ((i & 0x2) != 0)
                            {
                                index = indices.x;
                                indices.x = indices.y;
                                indices.y = index;
                            }

                            x = nodes[indices.x];
                            y = nodes[indices.y];

                            flag = flags[i];
                            if (flag == 0)
                            {
                                if (x.info.Equals(y.info))
                                    continue;

                                if (!infoIndices.TryGetValue(x.info, out index))
                                {
                                    index = numVertices++;

                                    infoIndices[x.info] = index;

                                    if (vertices != null)
                                        vertices.Add(tile[indices.x]);
                                }

                                face.indices[flipped[0]] = index;

                                if (!infoIndices.TryGetValue(y.info, out index))
                                {
                                    index = numVertices++;

                                    infoIndices[y.info] = index;

                                    if (vertices != null)
                                        vertices.Add(tile[indices.y]);
                                }

                                face.indices[flipped[1]] = index;

                                face.indices[flipped[2]] = delta;

                                faces(face);
                            }
                            else
                            {
                                index = vertexIndices[i];

                                if (vertices != null)
                                    vertices[index] = flag == FLAG_NEW ? vertex : (vertices[index] + vertex);

                                face.indices[flipped[1]] = index;

                                if (!infoIndices.TryGetValue(x.info, out index))
                                {
                                    index = numVertices++;

                                    infoIndices[x.info] = index;

                                    if (vertices != null)
                                        vertices.Add(tile[indices.x]);
                                }

                                face.indices[flipped[0]] = index;
                                face.indices[flipped[2]] = delta;
                                faces(face);

                                if (!infoIndices.TryGetValue(y.info, out index))
                                {
                                    index = numVertices++;

                                    infoIndices[y.info] = index;

                                    if (vertices != null)
                                        vertices.Add(tile[indices.y]);
                                }

                                face.indices[flipped[2]] = index;
                                face.indices[flipped[0]] = delta;
                                faces(face);
                            }
                        }
                    }

                }
                else
                {
                    if (!nodes.x.info.Equals(nodes.y.info) && !nodes.y.info.Equals(nodes.w.info))
                    {
                        Vector3Int tileIndices = new Vector3Int(0, 1, 3);

                        Face face = default(Face);
                        face.depth = depth;
                        face.axis = axis;
                        face.sizeDelta = sizeDelta;
                        for (i = 0; i < 3; ++i)
                        {
                            delta = tileIndices[i];
                            x = nodes[delta];
                            if (!infoIndices.TryGetValue(x.info, out index))
                            {
                                index = numVertices++;

                                infoIndices[x.info] = index;

                                if (vertices != null)
                                    vertices.Add(tile[delta]);
                            }

                            face.indices[flipped[i]] = index;
                        }

                        if (faces != null)
                            faces(face);
                    }

                    if (!nodes.w.info.Equals(nodes.z.info) && !nodes.z.info.Equals(nodes.x.info))
                    {
                        Vector3Int tileIndices = new Vector3Int(3, 2, 0);

                        Face face = default(Face);
                        face.depth = depth;
                        face.axis = axis;
                        face.sizeDelta = sizeDelta;
                        for (i = 0; i < 3; ++i)
                        {
                            delta = tileIndices[i];
                            x = nodes[delta];
                            if (!infoIndices.TryGetValue(x.info, out index))
                            {
                                index = numVertices++;

                                infoIndices[x.info] = index;

                                if (vertices != null)
                                    vertices.Add(tile[delta]);
                            }

                            face.indices[flipped[i]] = index;
                        }

                        if (faces != null)
                            faces(face);
                    }
                }

                return isNeedTess;
            }

            private void __ContourProcessTile(
                Axis axis, 
                int depth,
                Vector3Int sizeDelta,
                Tile<int> tile,
                IList<Vertex> vertices,
                Action<Face> faces)
            {
                if (faces == null)
                    return;

                int x = tile[0], y = tile[1], z = tile[2], w = tile[3];
                Face face;
                face.depth = depth;
                face.axis = axis;
                face.sizeDelta = sizeDelta;
                if (Vector3.Dot(vertices[y].normal.normalized, vertices[z].normal.normalized) > Vector3.Dot(vertices[x].normal.normalized, vertices[w].normal.normalized))
                {
                    face.indices = new Vector3Int(x, y, z);
                    faces(face);

                    face.indices = new Vector3Int(z, y, w);
                    faces(face);
                }
                else
                {
                    face.indices = new Vector3Int(x, y, w);
                    faces(face);

                    face.indices = new Vector3Int(x, w, z);
                    faces(face);
                }
            }

            private void __ContourProcessEdge(
                Boundary boundary, 
                Axis axis,
                Node4 nodes,
                IList<Vertex> vertices,
                IDictionary<Edge<Info>, int> edgeIndices,
                IDictionary<Info, int> infoIndices,
                Action<Face> faces)
            {
                int depth = int.MinValue, index = -1, i;
                NodeInfo nodeInfo;
                Vector3 position;
                Block block;
                Tile<Vertex> tile = default(Tile<Vertex>);
                for (i = 0; i < 4; ++i)
                {
                    nodeInfo = nodes[i];
                    if (Get(nodeInfo.info, out position) && Get(nodeInfo.info, out block))
                        tile[i] = new Vertex(position, block.normal, block.qef);
                    else
                        Debug.LogWarning("Failed To Get Point.");

                    if (nodeInfo.info.depth > depth)
                    {
                        depth = nodeInfo.info.depth;

                        index = i;
                    }
                }

                if (index == -1)
                    return;

                nodeInfo = nodes[index];

                Vector2Int vertexIndices = __edgeToVertexIndices[__processEdgeMask[(int)axis, index]];
                bool isFlip = ((nodeInfo.corners >> vertexIndices.x) & 1) == 0, isSign = isFlip == (((nodeInfo.corners >> vertexIndices.y) & 1) == 0);
                if (isSign)
                    return;

                Vector3Int sizeDelta;
                __Get(nodeInfo.info, isFlip ? new Vector2Int(vertexIndices.y, vertexIndices.x) : vertexIndices, out sizeDelta);// (nodeInfo.info.position + (isFlip ? __childMinOffsets[vertexIndices.y] : __childMinOffsets[vertexIndices.x])) * (1 << (__depth - depth));

                if (IsBoundary(nodes.sizeDelta, 1 << (__depth - depth), boundary))
                    return;

                /*if (__ContourProcessNoInter(isFlip, axis, depth, nodes.sizeDelta, tile, nodes, vertices, edgeIndices, infoIndices, faces))
                    return;

                return;*/
                int numVertices = vertices == null ? 0 : vertices.Count;
                Tile<int> indices = default(Tile<int>);
                for (i = 0; i < 4; ++i)
                {
                    nodeInfo = nodes[i];
                    if (!infoIndices.TryGetValue(nodeInfo.info, out index))
                    {
                        index = numVertices++;

                        infoIndices[nodeInfo.info] = index;

                        if (vertices != null)
                            vertices.Add(tile[i]);
                    }

                    indices[i] = index;
                }

                __ContourProcessTile(
                    axis, 
                    depth,
                    sizeDelta,
                    isFlip ? new Tile<int>(indices.z, indices.x, indices.w, indices.y) : new Tile<int>(indices.z, indices.w, indices.x, indices.y), 
                    vertices, 
                    faces);
            }

            private void __ContourEdgeProc(
                Boundary boundary, 
                Axis axis, 
                int length, 
                Node4 nodes,
                IList<Vertex> vertices,
                IDictionary<Edge<Info>, int> edgeIndices,
                IDictionary<Info, int> infoIndices,
                Action<Face> faces)
            {
                if (nodes.isInternal)
                {
                    length >>= 1;

                    bool result;
                    int i, j;
                    NodeInfo nodeInfo, temp;
                    Node4 edgeNodes = new Node4();
                    for (i = 0; i < 2; ++i)
                    {
                        result = true;

                        for (j = 0; j < 4; ++j)
                        {
                            nodeInfo = nodes[j];
                            if (nodeInfo.type == Type.Internal)
                            {
                                if (!__Get(__edgeProcEdgeMask[(int)axis, i, j], nodeInfo.info, out temp))
                                {
                                    result = false;

                                    break;
                                }

                                edgeNodes[j] = temp;
                            }
                            else
                                edgeNodes[j] = nodeInfo;
                        }

                        if (result)
                        {
                            edgeNodes.sizeDelta = nodes.sizeDelta;
                            edgeNodes.sizeDelta[(int)axis] += length * i;

                            __ContourEdgeProc(boundary, (Axis)__edgeProcEdgeMask[(int)axis, i, 4], length, edgeNodes, vertices, edgeIndices, infoIndices, faces);
                        }
                    }
                }
                else
                    __ContourProcessEdge(boundary, axis, nodes, vertices, edgeIndices, infoIndices, faces);
            }

            private void __ContourFaceProc(
                Boundary boundary, 
                Axis axis, 
                int length, 
                Node2 nodes,
                IList<Vertex> vertices,
                IDictionary<Edge<Info>, int> edgeIndices,
                IDictionary<Info, int> infoIndices,
                Action<Face> faces)
            {
                if (!nodes.isInternal)
                    return;

                length >>= 1;

                bool result;
                int i, j, index;
                Axis edgeAxis;
                Vector3Int offset = __childMinOffsets[__faceProcFaceMask[(int)axis, 0, 0]], sizeDelta = nodes.sizeDelta + new Vector3Int(length, length, length);
                NodeInfo nodeInfo, temp;
                Node2 faceNodes = new Node2();
                Node4 edgeNodes = new Node4();

                sizeDelta[(int)axis] -= length;
                for (i = 0; i < 4; ++i)
                {
                    result = true;
                    for (j = 0; j < 2; ++j)
                    {
                        nodeInfo = nodes[j];

                        if (nodeInfo.type == Type.Internal)
                        {
                            if (!__Get(__faceProcFaceMask[(int)axis, i, j], nodeInfo.info, out temp))
                            {
                                result = false;

                                break;
                            }

                            faceNodes[j] = temp;
                        }
                        else
                            faceNodes[j] = nodeInfo;
                    }

                    if (result)
                    {
                        faceNodes.sizeDelta = nodes.sizeDelta + (__childMinOffsets[__faceProcFaceMask[(int)axis, i, 0]] - offset) * length;

                        __ContourFaceProc(boundary, (Axis)__faceProcFaceMask[(int)axis, i, 2], length, faceNodes, vertices, edgeIndices, infoIndices, faces);
                    }

                    result = true;

                    index = __faceProcEdgeMask[(int)axis, i, 0];
                    for (j = 0; j < 4; ++j)
                    {
                        nodeInfo = nodes[__faceOrders[index, j]];
                        if (nodeInfo.type == Type.Internal)
                        {
                            if (!__Get(__faceProcEdgeMask[(int)axis, i, j + 1], nodeInfo.info, out temp))
                            {
                                result = false;

                                break;
                            }

                            edgeNodes[j] = temp;
                        }
                        else
                            edgeNodes[j] = nodeInfo;
                    }

                    if (result)
                    {
                        edgeNodes.sizeDelta = sizeDelta;

                        edgeAxis = (Axis)__faceProcEdgeMask[(int)axis, i, 5];
                        if ((i & 1) == 0)
                            edgeNodes.sizeDelta[(int)edgeAxis] -= length;

                        __ContourEdgeProc(boundary, edgeAxis, length, edgeNodes, vertices, edgeIndices, infoIndices, faces);
                    }
                }
            }

            private bool __ContourCellProc(
                Boundary boundary,
                Vector3Int sizeDelta, 
                NodeInfo nodeInfo, 
                IList<Vertex> vertices,
                IDictionary<Edge<Info>, int> edgeIndices,
                IDictionary<Info, int> infoIndices,
                Action<Face> faces)
            {
                if (nodeInfo.type != Type.Internal)
                    return false;
                
                int length = 1 << (__depth - nodeInfo.info.depth - 1), i;
                NodeInfo temp;
                for (i = 0; i < 8; ++i)
                {
                    if (__Get(i, nodeInfo.info, out temp))
                        __ContourCellProc(boundary, sizeDelta + __childMinOffsets[i] * length, temp, vertices, edgeIndices, infoIndices, faces);
                }

                sizeDelta += new Vector3Int(length, length, length);

                int j;
                Node2 faceNodes;
                Vector3Int cellProcFaceMask;
                for(i = 0; i < 3; ++i)
                {
                    for (j = 0; j < 4; ++j)
                    {
                        cellProcFaceMask = __cellProcFaceMask[(i << 2) + j];
                        if (!__Get(cellProcFaceMask.x, nodeInfo.info, out faceNodes.x) ||
                           !__Get(cellProcFaceMask.y, nodeInfo.info, out faceNodes.y))
                            continue;

                        faceNodes.sizeDelta = sizeDelta + __edgeToBlockOffsets[i, j] * length;

                        __ContourFaceProc(boundary, (Axis)cellProcFaceMask.z, length, faceNodes, vertices, edgeIndices, infoIndices, faces);
                    }
                }

                bool result;
                Axis axis;
                Node4 edgeNodes = new Node4();
                for (i = 0; i < 6; ++i)
                {
                    result = true;

                    for (j = 0; j < 4; ++j)
                    {
                        if (!__Get(__cellProcEdgeMask[i, j], nodeInfo.info, out temp))
                        {
                            result = false;

                            break;
                        }

                        edgeNodes[j] = temp;
                    }

                    if (!result)
                        continue;

                    axis = (Axis)__cellProcEdgeMask[i, 4];

                    edgeNodes.sizeDelta = sizeDelta;
                    if((i & 1) == 0)
                        edgeNodes.sizeDelta[(int)axis] -= length;

                    __ContourEdgeProc(boundary, axis, length, edgeNodes, vertices, edgeIndices, infoIndices, faces);
                }

                return true;
            }
        }

        private static Vector3Int[] __axisOffsets = new Vector3Int[]
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, 0, 1)
        };

        private static readonly Vector3Int[] __childMinOffsets =
        {
	        // needs to match the vertMap from Dual Contouring impl
	        new Vector3Int( 0, 0, 0 ),
            new Vector3Int( 0, 0, 1 ),
            new Vector3Int( 0, 1, 0 ),
            new Vector3Int( 0, 1, 1 ),
            new Vector3Int( 1, 0, 0 ),
            new Vector3Int( 1, 0, 1 ),
            new Vector3Int( 1, 1, 0 ),
            new Vector3Int( 1, 1, 1 )
        };

        private static readonly Vector2Int[] __edgeToVertexIndices =
        {
            new Vector2Int(0,4), new Vector2Int(1,5), new Vector2Int(2,6), new Vector2Int(3,7),	// x-axis 
	        new Vector2Int(0,2), new Vector2Int(1,3), new Vector2Int(4,6), new Vector2Int(5,7),	// y-axis
	        new Vector2Int(0,1), new Vector2Int(2,3), new Vector2Int(4,5), new Vector2Int(6,7)  // z-axis
        };

        private static Vector3Int[,] __edgeToBlockOffsets =
        /*{
            { new Vector3Int(0, 0, 0), new Vector3Int(0, 0, -1), new Vector3Int(0, -1, 0), new Vector3Int(0, -1, -1) }, 
            { new Vector3Int(0, 0, 0), new Vector3Int(0, 0, -1), new Vector3Int(-1, 0, 0), new Vector3Int(-1, 0, -1) },
            { new Vector3Int(0, 0, 0), new Vector3Int(0, -1, 0), new Vector3Int(-1, 0, 0), new Vector3Int(-1, -1, 0) },
        };*/
        {
            { new Vector3Int(0, -1, -1), new Vector3Int(0, -1, 0), new Vector3Int(0, 0, -1), new Vector3Int(0, 0, 0) },
            { new Vector3Int(-1, 0, -1), new Vector3Int(0, 0, -1), new Vector3Int(-1, 0, 0), new Vector3Int(0, 0, 0) },
            { new Vector3Int(-1, -1, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, -1, 0), new Vector3Int(0, 0, 0) },
        };

        private int __depth;
        private float __increment;
        private Vector3 __scale;
        private IEngineSampler __sampler;
        private Dictionary<Vector3Int, Dictionary<Vector3Int, Block>> __blocks;

        public int depth
        {
            get
            {
                return __depth;
            }
        }

        public float increment
        {
            get
            {
                return __increment;
            }
        }

        public Vector3 scale
        {
            get
            {
                return __scale;
            }
        }
        
        public DualContouring(int depth, float increment, Vector3 scale, IEngineSampler sampler)
        {
            __depth = depth;
            __increment = increment;
            __scale = scale;
            __sampler = sampler;
        }

        public Vector3 ApproximateZeroCrossingPosition(Vector3 x, Vector3 y)
        {
            // approximate the zero crossing by finding the min value along the edge
            float density, minValue = int.MaxValue, result = 0.0f, t = 0.0f;
            while (t <= 1.0f)
            {
                density = Mathf.Abs(GetDensity(x + (y - x) * t));
                if (density < minValue)
                {
                    minValue = density;

                    result = t;
                }

                t += __increment;
            }

            return x + (y - x) * result;
        }

        public Vector3 CalculateSurfaceNormal(Vector3 point)
        {
            //Vector3 x = new Vector3(__scale.x, 0.0f, 0.0f), y = new Vector3(0.0f, __scale.y, 0.0f), z = new Vector3(0.0f, 0.0f, __scale.z);
            Vector3 x = new Vector3(__increment * __scale.x, 0.0f, 0.0f), y = new Vector3(0.0f, __increment * __scale.y, 0.0f), z = new Vector3(0.0f, 0.0f, __increment * __scale.z);

            return new Vector3(
                GetDensity(point + x) - GetDensity(point - x),
                GetDensity(point + y) - GetDensity(point - y),
                GetDensity(point + z) - GetDensity(point - z)).normalized;
        }

        public bool Check(Vector3Int world)
        {
            if (__blocks == null)
                return false;

            Dictionary<Vector3Int, Block> blocks;
            if (!__blocks.TryGetValue(world, out blocks) || blocks == null)
                return false;

            return blocks.Count > 0;
        }

        /*public void Create(Vector3Int world, float increment)
        {
            if (__blocks == null)
                __blocks = new Dictionary<Vector3Int, Dictionary<Vector3Int, Block>>(new Vector3IntEqualityComparer());

            Dictionary<Vector3Int, Block> blocks;
            if (__blocks.TryGetValue(world, out blocks) && blocks != null)
                blocks.Clear();
            else
            {
                blocks = new Dictionary<Vector3Int, Block>(1 << (__depth * 3), new Vector3IntEqualityComparer());

                __blocks[world] = blocks;
            }
            
            int size = (1 << __depth), i, j, k, l, m, n;
            float x, y;
            Vector3Int local, offset, position;
            Vector3 from, to, point, normal;
            Block block;

            world *= size - 1;
            for (i = 0; i <= size; ++i)
            {
                for (j = 0; j <= size; ++j)
                {
                    for (k = 0; k <= size; ++k)
                    {
                        if (i == size && j == size && k == size)
                            break;

                        local = new Vector3Int(i, j, k);
                        from = Vector3.Scale(world + local, __scale) + __offset;
                        x = GetDensity(from);
                        for (m = 0; m < (int)Axis.Unkown; ++m)
                        {
                            to = from + Vector3.Scale(__axisOffsets[m], __scale);

                            y = GetDensity(to);
                            if (x < 0.0f == y < 0.0f)
                                continue;

                            point = ApproximateZeroCrossingPosition(from, to);
                            normal = CalculateSurfaceNormal(point);
                            for (n = 0; n < 4; ++n)
                            {
                                offset = local + __edgeToBlockOffsets[m, n];
                                if (offset.x == -1 || offset.y == -1 || offset.z == -1 || offset.x == size || offset.y == size || offset.z == size)
                                    continue;

                                if (!blocks.TryGetValue(offset, out block))
                                {
                                    block = new Block();

                                    position = world + offset;

                                    for (l = 0; l < 8; ++l)
                                    {
                                        if (GetDensity(Vector3.Scale(position + __childMinOffsets[l], __scale) + __offset) < 0.0f)
                                            block.corners |= 1 << l;
                                    }
                                }

                                block.normal += normal;
                                block.qef.Add(new Qef.Data(point, normal));

                                blocks[offset] = block;
                            }
                        }
                    }
                }
            }
        }*/
        
        public bool Destroy(Vector3Int world)
        {
            if (__blocks == null)
                return false;

            Dictionary<Vector3Int, Block> blocks;
            if (!__blocks.TryGetValue(world, out blocks) || blocks == null)
                return false;
            
            blocks.Clear();

            return true;
        }
        
        public float GetDensity(Vector3 position)
        {
            return __sampler == null ? 0.0f : __sampler.GetDensity(position);
        }
    }
}