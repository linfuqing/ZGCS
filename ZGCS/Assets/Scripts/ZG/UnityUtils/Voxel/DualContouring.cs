using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZG.Voxel
{
    public abstract class DualContouring
    {
        public enum Axis
        {
            X,
            Y,
            Z,

            Unkown
        }
        
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

        public interface IBuilder
        {
            DualContouring parent { get; }

            bool Create(Vector3Int world, float increment);

            bool Set(BoundsInt bounds);

            bool Set(Vector3Int position);
        }

        public class VoxelBuilder : IBuilder
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

            public bool Create(Vector3Int world, float increment)
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

                                from = Vector3.Scale(world + local, __parent.__scale) + __parent.__offset;
                                x = __parent.GetDensity(from);
                                for (m = 0; m < (int)Axis.Unkown; ++m)
                                {
                                    to = from + Vector3.Scale(__axisOffsets[m], __parent.__scale);

                                    y = __parent.GetDensity(to);
                                    if (x < 0.0f == y < 0.0f)
                                        continue;

                                    point = __parent.ApproximateZeroCrossingPosition(from, to, increment);
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
                                                if (__parent.GetDensity(Vector3.Scale(position + __childMinOffsets[l], __parent.__scale) + __parent.__offset) < 0.0f)
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

        public class BoundsBuilder : IBuilder
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

            public bool Create(Vector3Int world, float increment)
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

                            from = Vector3.Scale(world + local, __parent.__scale) + __parent.__offset;
                            x = __parent.GetDensity(from);
                            for (m = 0; m < (int)Axis.Unkown; ++m)
                            {
                                to = from + Vector3.Scale(__axisOffsets[m], __parent.__scale);

                                y = __parent.GetDensity(to);
                                if (x < 0.0f == y < 0.0f)
                                    continue;

                                point = __parent.ApproximateZeroCrossingPosition(from, to, increment);
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
                                            if (__parent.GetDensity(Vector3.Scale(position + __childMinOffsets[l], __parent.__scale) + __parent.__offset) < 0.0f)
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
        
        public class Octree
        {
            [Flags]
            public enum Boundary
            {
                None = 0x00,

                Left = 0x01,
                Right = 0x02,

                Lower = 0x04,
                Upper = 0x08,

                Front = 0x10,
                Back = 0x20,

                LeftLowerFront = Left | Lower | Front,
                RightUpperBack = Right | Upper | Back,

                All = LeftLowerFront | RightUpperBack
            }

            public enum Type
            {
                Internal,
                Psuedo,
                Leaf
            }
            
            public struct Info
            {
                public int depth;

                public Vector3Int position;

                public Info(int depth, Vector3Int position)
                {
                    this.depth = depth;
                    this.position = position;
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

            public delegate int TileProcessor(Info x, Info y, Info z, Info w, Axis axis, Vector3Int offset);

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

            private static readonly int[,] __orders =
            {
                { 0, 0, 1, 1 },
                { 0, 1, 0, 1 },
            };

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

                if ((boundary & Boundary.LeftLowerFront) != 0)
                {
                    if (((boundary & Boundary.Left) == Boundary.Left && position.x == 0) ||
                        ((boundary & Boundary.Lower) == Boundary.Lower && position.y == 0) ||
                        ((boundary & Boundary.Front) == Boundary.Front && position.z == 0))
                        return true;
                }

                if ((boundary & Boundary.RightUpperBack) == 0)
                    return false;

                int max = 1 << __depth;
                return ((boundary & Boundary.Right) == Boundary.Right && position.x + size == max) ||
                    ((boundary & Boundary.Upper) == Boundary.Upper && position.y + size == max) ||
                    ((boundary & Boundary.Back) == Boundary.Back && position.z + size == max);
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
                __offset = Vector3.Scale(world * ((1 << __depth) - 1), parent.__scale) + parent.__offset;

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

                    foreach (KeyValuePair<Vector3Int, Node> pair in source)
                    {
                        size = 1 << (parent.__depth - depth - 2);

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

                    size = 1 << (parent.__depth - depth - 1);

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

            public void Build(TileProcessor tileProcessor)
            {
                __ContourCellProc(new NodeInfo(__root.type, __root.block.corners, new Info(0, Vector3Int.zero)), tileProcessor);
            }

            public bool Build(TileProcessor tileProcessor, out MeshData<Vector3> meshData)
            {
                List<MeshData<Vector3>.Vertex> vertices = null;
                List< MeshData<Vector3>.Triangle> triangles = null;
                Dictionary<Info, int> indices = null;
                Build((x, y, z, w, axis, offset) =>
                {
                    int indexX, indexY, indexZ, indexW;
                    MeshData<Vector3>.Vertex vertexX, vertexY, vertexZ, vertexW;
                    if (indices == null)
                        indices = new Dictionary<Info, int>(new InfoIntEqualityComparer());

                    if (indices.TryGetValue(x, out indexX))
                        vertexX = vertices[indexX];
                    else
                    {
                        if (!Get(x, out vertexX.position))
                            return 0;

                        Block block;
                        if (!Get(x, out block))
                            return 0;

                        vertexX.data = block.normal.normalized;

                        /*Vector3 min = Vector3.Scale(x.position, __scale) + __offset, max = min + __scale * (1 << (__depth - x.depth));
                        vertexX.data = new Bounds((min + max) * 0.5f, max - min);
                        */
                        if (vertices == null)
                            vertices = new List<MeshData<Vector3>.Vertex>();

                        indexX = vertices.Count;

                        vertices.Add(vertexX);

                        indices[x] = indexX;
                    }

                    if (indices.TryGetValue(y, out indexY))
                        vertexY = vertices[indexY];
                    else
                    {
                        if (!Get(y, out vertexY.position))
                            return 0;

                        Block block;
                        if (!Get(y, out block))
                            return 0;

                        vertexY.data = block.normal.normalized;

                        /*Vector3 min = Vector3.Scale(y.position, __scale) + __offset, max = min + __scale * (1 << (__depth - y.depth));
                        vertexY.data = new Bounds((min + max) * 0.5f, max - min);
                        */
                        if (vertices == null)
                            vertices = new List<MeshData<Vector3>.Vertex>();

                        indexY = vertices.Count;

                        vertices.Add(vertexY);

                        indices[y] = indexY;
                    }

                    if (indices.TryGetValue(z, out indexZ))
                        vertexZ = vertices[indexZ];
                    else
                    {
                        if (!Get(z, out vertexZ.position))
                            return 0;

                        Block block;
                        if (!Get(z, out block))
                            return 0;

                        vertexZ.data = block.normal.normalized;

                        /*Vector3 min = Vector3.Scale(z.position, __scale) + __offset, max = min + __scale * (1 << (__depth - z.depth));
                        vertexZ.data = new Bounds((min + max) * 0.5f, max - min);*/

                        if (vertices == null)
                            vertices = new List<MeshData<Vector3>.Vertex>();

                        indexZ = vertices.Count;

                        vertices.Add(vertexZ);

                        indices[z] = indexZ;
                    }

                    if (indices.TryGetValue(w, out indexW))
                        vertexW = vertices[indexW];
                    else
                    {
                        if (!Get(w, out vertexW.position))
                            return 0;

                        Block block;
                        if (!Get(w, out block))
                            return 0;

                        vertexW.data = block.normal.normalized;

                        /*Vector3 min = Vector3.Scale(w.position, __scale) + __offset, max = min + __scale * (1 << (__depth - w.depth));
                        vertexW.data = new Bounds((min + max) * 0.5f, max - min);
                        */
                        if (vertices == null)
                            vertices = new List<MeshData<Vector3>.Vertex>();

                        indexW = vertices.Count;

                        vertices.Add(vertexW);

                        indices[w] = indexW;
                    }

                    int index = tileProcessor == null ? 0 : tileProcessor(x, y, z, w, axis, offset);

                    if (triangles == null)
                        triangles = new List<MeshData<Vector3>.Triangle>();
                    
                    /*Qef qef = new Qef();
                    qef.Add(new Qef.Data(vertexX.position, vertexX.normal));
                    qef.Add(new Qef.Data(vertexY.position, vertexY.normal));
                    qef.Add(new Qef.Data(vertexZ.position, vertexZ.normal));
                    float source = Mathf.Abs(qef.GetError(qef.Solve(__sweeps)));
                    qef = new Qef();
                    qef.Add(new Qef.Data(vertexZ.position, vertexZ.normal));
                    qef.Add(new Qef.Data(vertexY.position, vertexY.normal));
                    qef.Add(new Qef.Data(vertexW.position, vertexW.normal));
                    source += Mathf.Abs(qef.GetError(qef.Solve(__sweeps)));

                    qef = new Qef();
                    qef.Add(new Qef.Data(vertexX.position, vertexX.normal));
                    qef.Add(new Qef.Data(vertexY.position, vertexY.normal));
                    qef.Add(new Qef.Data(vertexW.position, vertexW.normal));
                    float destination = Mathf.Abs(qef.GetError(qef.Solve(__sweeps)));
                    qef = new Qef();
                    qef.Add(new Qef.Data(vertexX.position, vertexX.normal));
                    qef.Add(new Qef.Data(vertexW.position, vertexW.normal));
                    qef.Add(new Qef.Data(vertexZ.position, vertexZ.normal));
                    destination += Mathf.Abs(qef.GetError(qef.Solve(__sweeps)));*/

                    if (Vector3.Dot(vertexY.data, vertexZ.data) > Vector3.Dot(vertexX.data, vertexW.data))
                    {
                        triangles.Add(new MeshData<Vector3>.Triangle(index, new Vector3Int(indexX, indexY, indexZ)));
                        triangles.Add(new MeshData<Vector3>.Triangle(index, new Vector3Int(indexZ, indexY, indexW)));
                    }
                    else
                    {
                        triangles.Add(new MeshData<Vector3>.Triangle(index, new Vector3Int(indexX, indexY, indexW)));
                        triangles.Add(new MeshData<Vector3>.Triangle(index, new Vector3Int(indexX, indexW, indexZ)));
                    }

                    return index;
                });

                meshData = new MeshData<Vector3>(vertices == null ? null : vertices.ToArray(), triangles == null ? null : triangles.ToArray());

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
                nodeInfo = new NodeInfo();
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
            
            private void __ContourProcessEdge(Node4 nodes, Axis axis, TileProcessor tileProcessor)
            {
                if (tileProcessor == null)
                    return;
                
                int depth = int.MinValue, index = -1;
                NodeInfo nodeInfo;
                for (int i = 0; i < 4; ++i)
                {
                    nodeInfo = nodes[i];
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

                Vector3Int offset;
                __Get(nodeInfo.info, isFlip ? new Vector2Int(vertexIndices.y, vertexIndices.x) : vertexIndices, out offset);// (nodeInfo.info.position + (isFlip ? __childMinOffsets[vertexIndices.y] : __childMinOffsets[vertexIndices.x])) * (1 << (__depth - depth));

                if (isFlip)
                    tileProcessor(
                        nodes.z.info,
                        nodes.x.info,
                        nodes.w.info,
                        nodes.y.info,
                        axis,
                        offset);
                else
                    tileProcessor(
                        nodes.z.info,
                        nodes.w.info,
                        nodes.x.info,
                        nodes.y.info,
                        axis,
                        offset);
            }

            private void __ContourEdgeProc(Node4 nodes, Axis axis, TileProcessor tileProcessor)
            {
                if (nodes.isInternal)
                {
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
                            __ContourEdgeProc(edgeNodes, (Axis)__edgeProcEdgeMask[(int)axis, i, 4], tileProcessor);
                    }
                }
                else
                    __ContourProcessEdge(nodes, axis, tileProcessor);
            }

            private void __ContourFaceProc(Node2 nodes, Axis axis, TileProcessor tileProcessor)
            {
                if (!nodes.isInternal)
                    return;

                bool result;
                int i, j, index;
                NodeInfo nodeInfo, temp;
                Node2 faceNodes = new Node2();
                Node4 edgeNodes = new Node4();
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
                        __ContourFaceProc(faceNodes, (Axis)__faceProcFaceMask[(int)axis, i, 2], tileProcessor);

                    result = true;

                    index = __faceProcEdgeMask[(int)axis, i, 0];
                    for (j = 0; j < 4; ++j)
                    {
                        nodeInfo = nodes[__orders[index, j]];
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
                        __ContourEdgeProc(edgeNodes, (Axis)__faceProcEdgeMask[(int)axis, i, 5], tileProcessor);
                }
            }

            private bool __ContourCellProc(NodeInfo nodeInfo, TileProcessor tileProcessor)
            {
                if (nodeInfo.type != Type.Internal)
                    return false;

                int i;
                NodeInfo temp;
                for (i = 0; i < 8; ++i)
                {
                    if(__Get(i, nodeInfo.info, out temp))
                        __ContourCellProc(temp, tileProcessor);
                }

                Node2 faceNodes;
                Vector3Int cellProcFaceMask;
                for (i = 0; i < 12; ++i)
                {
                    cellProcFaceMask = __cellProcFaceMask[i];
                    if(!__Get(cellProcFaceMask.x, nodeInfo.info, out faceNodes.x) ||
                       !__Get(cellProcFaceMask.y, nodeInfo.info, out faceNodes.y))
                        continue;
                    
                    __ContourFaceProc(faceNodes, (Axis)cellProcFaceMask.z, tileProcessor);
                }

                bool result;
                int j;
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

                    __ContourEdgeProc(edgeNodes, (Axis)__cellProcEdgeMask[i, 4], tileProcessor);
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
        {
            { new Vector3Int(0, 0, 0), new Vector3Int(0, 0, -1), new Vector3Int(0, -1, 0), new Vector3Int(0, -1, -1) }, 
            { new Vector3Int(0, 0, 0), new Vector3Int(0, 0, -1), new Vector3Int(-1, 0, 0), new Vector3Int(-1, 0, -1) },
            { new Vector3Int(0, 0, 0), new Vector3Int(0, -1, 0), new Vector3Int(-1, 0, 0), new Vector3Int(-1, -1, 0) },
        };

        private int __depth;
        private Vector3 __scale;
        private Vector3 __offset;
        private Dictionary<Vector3Int, Dictionary<Vector3Int, Block>> __blocks;

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

        public DualContouring(int depth, Vector3 scale, Vector3 offset)
        {
            __depth = depth;
            __scale = scale;
            __offset = offset;
        }

        public Vector3 ApproximateZeroCrossingPosition(Vector3 x, Vector3 y, float increment)
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

                t += increment;
            }

            return x + (y - x) * result;
        }

        public Vector3 CalculateSurfaceNormal(Vector3 point)
        {
            Vector3 x = new Vector3(__scale.x, 0.0f, 0.0f), y = new Vector3(0.0f, __scale.y, 0.0f), z = new Vector3(0.0f, 0.0f, __scale.z);
            //Vector3 x = new Vector3(0.001f, 0.0f, 0.0f), y = new Vector3(0.0f, 0.001f, 0.0f), z = new Vector3(0.0f, 0.0f, 0.001f);

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

        public void Create(Vector3Int world, float increment)
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

                            point = ApproximateZeroCrossingPosition(from, to, increment);
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
        }
        
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
        
        public abstract float GetDensity(Vector3 position);
    }
}