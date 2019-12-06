using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZG.Voxel
{
    public class ManifoldDC : IEngine
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

        private struct Edges
        {
            public int e00;
            public int e01;
            public int e02;
            public int e03;

            public int e04;
            public int e05;
            public int e06;
            public int e07;

            public int e08;
            public int e09;
            public int e10;
            public int e11;

            public int this[int index]
            {
                get
                {
                    switch(index)
                    {
                        case 0:
                            return e00;
                        case 1:
                            return e01;
                        case 2:
                            return e02;
                        case 3:
                            return e03;

                        case 4:
                            return e04;
                        case 5:
                            return e05;
                        case 6:
                            return e06;
                        case 7:
                            return e07;

                        case 8:
                            return e08;
                        case 9:
                            return e09;
                        case 10:
                            return e10;
                        case 11:
                            return e11;
                    }

                    throw new IndexOutOfRangeException();
                }

                set
                {
                    switch(index)
                    {
                        case 0:
                            e00 = value;
                            break;
                        case 1:
                            e01 = value;
                            break;
                        case 2:
                            e02 = value;
                            break;
                        case 3:
                            e03 = value;
                            break;

                        case 4:
                            e04 = value;
                            break;
                        case 5:
                            e05 = value;
                            break;
                        case 6:
                            e06 = value;
                            break;
                        case 7:
                            e07 = value;
                            break;

                        case 8:
                            e08 = value;
                            break;
                        case 9:
                            e09 = value;
                            break;
                        case 10:
                            e10 = value;
                            break;
                        case 11:
                            e11 = value;
                            break;

                        default:
                            throw new IndexOutOfRangeException();
                    }
                }
            }
        }

        private struct Vertex
        {
            public Edges edges;
            public Vector3 normal;
            public Qef qef;
        }

        public struct Block
        {
            public int corners;
            public Tile<int> vertexIndices;
        }
        
        public class BoundsBuilder : IEngineBuilder
        {
            private ManifoldDC __parent;
            private Dictionary<Vector3Int, BoundsInt> __bounds;
            
            public BoundsBuilder(ManifoldDC parent)
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
                    __parent.__blocks = new Dictionary<Vector3Int, Dictionary<Vector3Int, Block>>();

                int i, j, k, l;
                Vector3Int local;
                Block block;
                Dictionary<Vector3Int, Block> blocks;
                if (!__parent.__blocks.TryGetValue(world, out blocks) || blocks == null)
                {
                    blocks = new Dictionary<Vector3Int, Block>(1 << (__parent.__depth * 3));

                    __parent.__blocks[world] = blocks;
                }
                else
                {
                    for (i = min.x; i < max.x; ++i)
                    {
                        for (j = min.y; j < max.y; ++j)
                        {
                            for (k = min.z; k < max.z; ++k)
                            {
                                local = new Vector3Int(i, j, k);
                                if (blocks.TryGetValue(local, out block))
                                {
                                    if (__parent.__vertices != null)
                                    {
                                        for (l = 0; l < 4; ++l)
                                        {
                                            if (!__parent.__vertices.RemoveAt(block.vertexIndices[l]))
                                                break;
                                        }
                                    }

                                    blocks.Remove(local);
                                }
                            }
                        }
                    }
                }

                bool isBlockDirty;
                int m, n, code, index, edgeIndex, vertexIndex, size = 1 << __parent.__depth;
                float x, y;
                Vector3Int offset, position;
                Vector3 from, to, point, normal;
                Vertex vertex;

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

                                point = __parent.ApproximateZeroCrossingPosition(from, to);
                                normal = __parent.CalculateSurfaceNormal(point);
                                for (n = 0; n < 4; ++n)
                                {
                                    offset = local + __edgeToBlockOffsets[m, n];
                                    if (offset.x == min.x - 1 || offset.y == min.y - 1 || offset.z == min.z - 1 || offset.x == max.x || offset.y == max.y || offset.z == max.z)
                                        continue;

                                    isBlockDirty = false;
                                    if (!blocks.TryGetValue(offset, out block))
                                    {
                                        position = world + offset;

                                        for (l = 0; l < 8; ++l)
                                        {
                                            if (__parent.GetDensity(Vector3.Scale(position + __childMinOffsets[l], __parent.__scale) + __parent.__offset) < 0.0f)
                                                block.corners |= 1 << l;
                                        }

                                        block.vertexIndices = new Tile<int>(-1, -1, -1, -1);

                                        isBlockDirty = true;
                                    }

                                    edgeIndex = __edgeIndices[m, n];

                                    index = 0;
                                    for(l = 0; l < 16; ++l)
                                    {
                                        code = __edgesTable[block.corners, l];
                                        if(code == -2)
                                        {
                                            ++index;

                                            break;
                                        }

                                        if(code == -1)
                                        {
                                            ++index;

                                            continue;
                                        }

                                        if(code == edgeIndex)
                                        {
                                            if (__parent.__vertices == null)
                                                __parent.__vertices = new Pool<Vertex>();

                                            vertexIndex = block.vertexIndices[index];
                                            if(!__parent.__vertices.TryGetValue(vertexIndex, out vertex))
                                            {
                                                vertexIndex = __parent.__vertices.nextIndex;

                                                block.vertexIndices[index] = vertexIndex;

                                                isBlockDirty = true;

                                                vertex = new Vertex();
                                            }

                                            vertex.edges[edgeIndex] += 1;

                                            vertex.normal += normal;
                                            vertex.qef.Add(new Qef.Data(point, normal));

                                            __parent.__vertices.Insert(vertexIndex, vertex);
                                        }
                                    }

                                    if(isBlockDirty)
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
                    __bounds = new Dictionary<Vector3Int, BoundsInt>();

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

        public class Octree : IEngineProcessor<ManifoldDC>
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

                public int childIndex
                {
                    get
                    {
                        Vector3Int offset = new Vector3Int(position.x >> 1, position.y >> 1, position.z >> 1);
                        offset = position - offset * 2;

                        return offset.x << 2 | offset.y << 1 | offset.z;
                    }
                }

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

            public struct Result
            {
                public float error;
                public Vector3 position;
            }
            
            private struct Point
            {
                public bool isFaceProp;

                public int surfaceIndex;

                public int parentIndex;

                public int euler;

                public Info info;

                public Vertex vertex;
            }

            private struct Node
            {
                public bool isMiddleCorner;
                public Type type;
                public int childMask;
                public int vertexIndexStart;
                public int vertexIndexCount;
                public Block block;
            }

            private struct NodeInfo : IEquatable<NodeInfo>
            {
                public Type type;

                public int vertexIndexStart;
                public int vertexIndexCount;

                public Block block;

                public Info info;

                public NodeInfo(Type type, int vertexIndexStart, int vertexIndexCount, Block block, Info info)
                {
                    this.type = type;
                    this.vertexIndexStart = vertexIndexStart;
                    this.vertexIndexCount = vertexIndexCount;
                    this.block = block;
                    this.info = info;
                }
                
                public bool Equals(NodeInfo other)
                {
                    return type == other.type && 
                        vertexIndexStart == other.vertexIndexStart && 
                        vertexIndexCount == other.vertexIndexCount && 
                        block.Equals(other.block) && 
                        info.Equals(other.info);
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

            private static readonly int[,] __faceOrders =
            {
                { 0, 0, 1, 1 },
                { 0, 1, 0, 1 },
            };
            #endregion
            
            private int __sweeps;
            private int __depth;
            private float __threshold;
            private Vector3 __scale;
            private Vector3 __offset;
            private Node __root;
            private List<Point> __points;
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
            
            public bool Create(Boundary boundary, int sweeps, float threshold, Vector3Int world, ManifoldDC parent)
            {
                if (parent == null || parent.__depth < 1 || parent.__blocks == null)
                    return false;

                Dictionary<Vector3Int, Block> blocks;
                if (!parent.__blocks.TryGetValue(world, out blocks) || blocks == null || blocks.Count < 1)
                    return false;

                __sweeps = sweeps;
                __depth = parent.__depth;
                __threshold = threshold;
                __scale = parent.__scale;
                __offset = Vector3.Scale(world * ((1 << __depth) - 1), parent.__scale) + parent.__offset;

                int depth = __nodes == null ? 0 : __nodes.Length;
                if (depth < __depth)
                    Array.Resize(ref __nodes, __depth);

                int i;
                Dictionary<Vector3Int, Node> source;
                for (i = 0; i < depth; ++i)
                {
                    source = __nodes[i];
                    if (source != null)
                        source.Clear();
                }

                depth = __depth;

                source = __nodes[--depth];

                if (source == null)
                {
                    source = new Dictionary<Vector3Int, Node>();

                    __nodes[depth] = source;
                }

                if (__points != null)
                    __points.Clear();

                int numPoints = 0, pointIndex, vertexIndex;
                Vector3Int position;
                Vertex vertex;
                Point point;
                Node node;
                Dictionary<int, int> pointIndices = null;
                foreach (KeyValuePair<Vector3Int, Block> pair in blocks)
                {
                    position = pair.Key;
                    
                    node.type = Type.Leaf;

                    node.isMiddleCorner = false;

                    node.childMask = 0;

                    node.vertexIndexStart = -1;
                    node.vertexIndexCount = 0;

                    node.block = pair.Value;
                    
                    for (i = 0; i < 4; ++i)
                    {
                        vertexIndex = node.block.vertexIndices[i];
                        if (parent.__vertices != null && parent.__vertices.TryGetValue(vertexIndex, out vertex))
                        {
                            if (pointIndices == null)
                                pointIndices = new Dictionary<int, int>();

                            if (!pointIndices.TryGetValue(vertexIndex, out pointIndex))
                            {
                                pointIndex = numPoints++;

                                point.isFaceProp = true;
                                point.surfaceIndex = -1;
                                point.parentIndex = -1;
                                point.euler = 1;

                                point.info = new Info(__depth, position);

                                point.vertex = vertex;

                                if (__points == null)
                                    __points = new List<Point>();

                                __points.Add(point);

                                pointIndices[vertexIndex] = pointIndex;
                            }

                            node.block.vertexIndices[i] = pointIndex;
                        }
                    }

                    source[position] = node;
                }
                
                int shift, size;
                Vector3Int offset, key;
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
                            destination = new Dictionary<Vector3Int, Node>(size * size * size);

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
                        if (temp.type == Type.Internal)
                        {
                            if (node.type != Type.Internal)
                            {
                                node.type = Type.Internal;

                                destination[offset] = node;
                            }

                            continue;
                        }
                        
                        key = position * size;
                        if (temp.type != Type.Leaf)
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

                        if (temp.isMiddleCorner && temp.childMask != 0)
                        {
                            temp.block.corners |= ~temp.childMask & 0xff;

                            if (nodes == null)
                                nodes = new List<KeyValuePair<Vector3Int, Node>>();

                            nodes.Add(new KeyValuePair<Vector3Int, Node>(position, temp));
                        }

                        //__DestroyChildren(depth + 2, position);
                        
                        if (node.type == Type.Internal)
                            continue;

                        position -= offset * 2;
                        shift = position.x << 2 | position.y << 1 | position.z;

                        node.isMiddleCorner = ((temp.block.corners >> (7 - shift)) & 1) != 0;
                        node.childMask |= 1 << shift;

                        node.block.corners |= ((temp.block.corners >> shift) & 1) << shift;
                        node.block.vertexIndices = new Tile<int>(-1, -1, -1, -1);

                        destination[offset] = node;
                    }

                    if (nodes != null)
                    {
                        foreach (KeyValuePair<Vector3Int, Node> pair in nodes)
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
                    
                    key = position * size;
                    if (temp.type != Type.Leaf)
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
                    
                    if (__root.type == Type.Internal)
                        continue;

                    position -= offset * 2;
                    shift = position.x << 2 | position.y << 1 | position.z;

                    __root.isMiddleCorner = ((temp.block.corners >> (7 - shift)) & 1) != 0;
                    __root.childMask |= 1 << shift;

                    __root.block.corners |= ((temp.block.corners >> shift) & 1) << shift;
                    __root.block.vertexIndices = new Tile<int>(-1, -1, -1, -1);
                }

                if (nodes != null)
                {
                    foreach (KeyValuePair<Vector3Int, Node> pair in nodes)
                        source[pair.Key] = pair.Value;
                }

                if (__root.type != Type.Internal)
                    return false;

                for (i = 0; i < 8; ++i)
                    __ClusterCell(__Get(i, new Info(0, Vector3Int.zero)));
                
                return true;
            }

            public void Build(Boundary boundary,
                IList<Voxel.Vertex> vertices,
                IDictionary<int, int> indices,
                IDictionary<int, Result> results,
                Action<Face> faces)
            {
                __ContourCellProc(
                    boundary,
                    new NodeInfo(__root.type, __root.vertexIndexStart, __root.vertexIndexCount, __root.block, new Info(0, Vector3Int.zero)),
                    vertices,
                    indices,
                    results,
                    faces);
            }

            public bool Build(Boundary boundary, Func<Face, IReadOnlyList<Voxel.Vertex>, int> subMeshHandler, out MeshData<Vector3> meshData)
            {
                List<Voxel.Vertex> vertices = new List<Voxel.Vertex>();
                Dictionary<int, int> indices = new Dictionary<int, int>();
                Dictionary<int, Result> results = new Dictionary<int, Result>();
                List<Face> faces = new List<Face>();
                Build(boundary, vertices, indices, results, faces.Add);

                List<MeshData<Vector3>.Triangle> triangles = null;
                foreach (Face face in faces)
                {
                    if (triangles == null)
                        triangles = new List<MeshData<Vector3>.Triangle>();

                    triangles.Add(new MeshData<Vector3>.Triangle(subMeshHandler == null ? 0 : subMeshHandler(face, vertices), face.indices));
                }

                List<MeshData<Vector3>.Vertex> result = null;
                foreach (Voxel.Vertex vertex in vertices)
                {
                    if (result == null)
                        result = new List<MeshData<Vector3>.Vertex>();

                    result.Add(vertex);
                }

                meshData = new MeshData<Vector3>(result == null ? null : result.ToArray(), triangles == null ? null : triangles.ToArray());

                return meshData.vertices != null && meshData.triangles != null;
            }

            private Info __Get(int childIndex, Info info)
            {
                return new Info(info.depth + 1, info.position * 2 + __childMinOffsets[childIndex]);
            }
            
            private bool __Get(Info info, out NodeInfo nodeInfo)
            {
                nodeInfo = default(NodeInfo);
                nodeInfo.info = info;

                if (info.depth < 1)
                {
                    nodeInfo.type = __root.type;
                    nodeInfo.vertexIndexStart = __root.vertexIndexStart;
                    nodeInfo.vertexIndexCount = __root.vertexIndexCount;
                    nodeInfo.block = __root.block;

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
                nodeInfo.vertexIndexStart = node.vertexIndexStart;
                nodeInfo.vertexIndexCount = node.vertexIndexCount;
                nodeInfo.block = node.block;

                return true;
            }

            private bool __Get(int childIndex, Info info, out NodeInfo node)
            {
                return __Get(__Get(childIndex, info), out node);
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
                
                //modify: 2019/12/6
                if ((node.block.corners & (1 << vertexIndices.y)) != 0)
                {
                    offset = __Get(info, vertexIndices.y);

                    return true;
                }
                else if (info.depth < __depth)
                {
                    if ((node.childMask & (1 << vertexIndices.y)) != 0 &&
                        __Get(new Info(info.depth + 1, info.position * 2 + __childMinOffsets[vertexIndices.y]), vertexIndices, out offset))
                        return true;

                    if ((node.childMask & (1 << vertexIndices.x)) != 0 &&
                        __Get(new Info(info.depth + 1, info.position * 2 + __childMinOffsets[vertexIndices.x]), vertexIndices, out offset))
                        return true;
                }
                
                offset = __Get(info, vertexIndices.x);

                return (node.block.corners & (1 << vertexIndices.x)) != 0;
            }
            
            private void __ContourProcessTile(
                Axis axis,
                int depth,
                Vector3Int offset,
                Tile<int> tile,
                IList<Voxel.Vertex> vertices,
                Action<Face> faces)
            {
                if (faces == null)
                    return;

                if (tile.IsAll(-1))
                    return;

                Face face;
                face.depth = depth;
                face.axis = axis;
                face.sizeDelta = offset;

                if (tile.IsAny(-1))
                {
                    if(tile.x == -1)
                    {
                        if (tile.z != -1 && tile.y != -1 && tile.w != -1 && tile.z != tile.y && tile.y != tile.w && tile.z != tile.w)
                        {
                            face.indices = new Vector3Int(tile.z, tile.y, tile.w);
                            faces(face);
                        }
                    }

                    if(tile.y == -1)
                    {
                        if (tile.x != -1 && tile.w != -1 && tile.z != -1 && tile.x != tile.w && tile.w != tile.z && tile.x != tile.z)
                        {
                            face.indices = new Vector3Int(tile.x, tile.w, tile.z);
                            faces(face);
                        }
                    }

                    if (tile.z == -1)
                    {
                        if (tile.x != -1 && tile.y != -1 && tile.w != -1 && tile.x != tile.y && tile.y != tile.w && tile.x != tile.w)
                        {
                            face.indices = new Vector3Int(tile.x, tile.y, tile.w);
                            faces(face);
                        }
                    }

                    if (tile.w == -1)
                    {
                        if (tile.x != -1 && tile.y != -1 && tile.z != -1 && tile.x != tile.y && tile.y != tile.z && tile.x != tile.z)
                        {
                            face.indices = new Vector3Int(tile.x, tile.y, tile.z);
                            faces(face);
                        }
                    }
                }
                else
                {
                    if (Vector3.Dot(vertices[tile.y].normal.normalized, vertices[tile.z].normal.normalized) > Vector3.Dot(vertices[tile.x].normal.normalized, vertices[tile.w].normal.normalized))
                    {
                        if (tile.x != tile.y && tile.y != tile.z && tile.x != tile.z)
                        {
                            face.indices = new Vector3Int(tile.x, tile.y, tile.z);
                            faces(face);
                        }

                        if (tile.z != tile.y && tile.y != tile.w && tile.z != tile.w)
                        {
                            face.indices = new Vector3Int(tile.z, tile.y, tile.w);
                            faces(face);
                        }
                    }
                    else
                    {
                        if (tile.x != tile.y && tile.y != tile.w && tile.x != tile.w)
                        {
                            face.indices = new Vector3Int(tile.x, tile.y, tile.w);
                            faces(face);
                        }

                        if (tile.x != tile.w && tile.w != tile.z && tile.x != tile.z)
                        {
                            face.indices = new Vector3Int(tile.x, tile.w, tile.z);
                            faces(face);
                        }
                    }
                }
            }

            private void __ContourProcessEdge(
                Boundary boundary,
                Axis axis,
                Node4 nodes,
                IList<Voxel.Vertex> vertices,
                IDictionary<int, int> indices,
                IDictionary<int, Result> results,
                Action<Face> faces)
            {
                int depth = int.MinValue, index = -1, i;
                NodeInfo nodeInfo;
                for (i = 0; i < 4; ++i)
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

                Vector2Int vertexIndices = __edgeToVertexIndices[__edgeIndices[(int)axis, index]];
                bool isFlip = ((nodeInfo.block.corners >> vertexIndices.x) & 1) == 0, isSign = isFlip == (((nodeInfo.block.corners >> vertexIndices.y) & 1) == 0);
                if (isSign)
                    return;

                Vector3Int offset;
                __Get(nodeInfo.info, isFlip ? new Vector2Int(vertexIndices.y, vertexIndices.x) : vertexIndices, out offset);// (nodeInfo.info.position + (isFlip ? __childMinOffsets[vertexIndices.y] : __childMinOffsets[vertexIndices.x])) * (1 << (__depth - depth));

                if (IsBoundary(offset, 1 << (__depth - depth), boundary))
                    return;

                bool isSkip;
                int edgeIndex, vertexIndex, pointIndex, code, j, numVertices = vertices == null ? 0 : vertices.Count, numPoints = __points == null ? 0 : __points.Count, size;
                Vector3Int key;
                Vector3 position, min, max;
                Point point;
                Result result;
                Tile<int> tile = new Tile<int>(-1, -1, -1, -1);
                for (i = 0; i < 4; ++i)
                {
                    nodeInfo = nodes[i];

                    isSkip = false;
                    edgeIndex = __edgeIndices[(int)axis, i];
                    vertexIndex = 0;
                    for(j = 0; j < 16; ++j)
                    {
                        code = __edgesTable[nodeInfo.block.corners, j];
                        if(code == -1)
                        {
                            ++vertexIndex;

                            continue;
                        }

                        if(code == -2)
                        {
                            isSkip = true;

                            break;
                        }

                        if (code == edgeIndex)
                            break;
                    }

                    if (isSkip)
                        continue;

                    pointIndex = nodeInfo.block.vertexIndices[vertexIndex];
                    if (pointIndex >= 0 && pointIndex < numPoints)
                    {
                        vertexIndex = pointIndex;

                        point = __points[pointIndex];

                        size = 1 << (__depth - point.info.depth);
                        key = point.info.position * size;
                        if (results == null || !results.TryGetValue(pointIndex, out result))
                        {
                            result.position = point.vertex.qef.Solve(__sweeps);
                            result.error = point.vertex.qef.GetError(result.position);

                            min = Vector3.Scale(key, __scale) + __offset;
                            max = min + __scale * size;
                            if(result.position.x < min.x || result.position.y < min.y || result.position.z < min.z || result.position.x > max.x || result.position.y > max.y || result.position.z > max.z)
                                result.position = point.vertex.qef.massPoint;

                            if (results != null)
                                results[pointIndex] = result;
                        }

                        position = result.position;

                        if (!IsBoundary(key, size, Boundary.All))
                        {
                            while (point.parentIndex >= 0 && point.parentIndex < numPoints)
                            {
                                pointIndex = point.parentIndex;

                                point = __points[pointIndex];
                                if (point.euler == 1 && point.isFaceProp)
                                {
                                    if (results == null || !results.TryGetValue(pointIndex, out result))
                                    {
                                        result.position = point.vertex.qef.Solve(__sweeps);
                                        result.error = point.vertex.qef.GetError(result.position);

                                        size = 1 << (__depth - point.info.depth);
                                        min = Vector3.Scale(point.info.position * size, __scale) + __offset;
                                        max = min + __scale * size;
                                        if (result.position.x < min.x || result.position.y < min.y || result.position.z < min.z || result.position.x > max.x || result.position.y > max.y || result.position.z > max.z)
                                            result.position = point.vertex.qef.massPoint;

                                        if (results != null)
                                            results[pointIndex] = result;
                                    }

                                    if (result.error < __threshold)
                                    {
                                        vertexIndex = pointIndex;

                                        position = result.position;
                                    }
                                }
                            }
                        }

                        if (!indices.TryGetValue(vertexIndex, out index))
                        {
                            index = numVertices++;
                            
                            if (vertices != null)
                            {
                                if(vertexIndex != pointIndex)
                                    point = __points[vertexIndex];

                                vertices.Add(new Voxel.Vertex(position, point.vertex.normal, point.vertex.qef));
                            }

                            indices[vertexIndex] = index;
                        }

                        tile[i] = index;
                    }
                }

                __ContourProcessTile(
                    axis,
                    depth,
                    offset,
                    isFlip ? new Tile<int>(tile.z, tile.x, tile.w, tile.y) : new Tile<int>(tile.z, tile.w, tile.x, tile.y),
                    vertices,
                    faces);
            }

            private void __ContourEdgeProc(
                Boundary boundary,
                Axis axis,
                Node4 nodes,
                IList<Voxel.Vertex> vertices,
                IDictionary<int, int> indices,
                IDictionary<int, Result> results,
                Action<Face> faces)
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
                            __ContourEdgeProc(boundary, (Axis)__edgeProcEdgeMask[(int)axis, i, 4], edgeNodes, vertices, indices, results, faces);
                    }
                }
                else
                    __ContourProcessEdge(boundary, axis, nodes, vertices, indices, results, faces);
            }

            private void __ContourFaceProc(
                Boundary boundary,
                Axis axis,
                Node2 nodes,
                IList<Voxel.Vertex> vertices,
                IDictionary<int, int> indices,
                IDictionary<int, Result> results,
                Action<Face> faces)
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
                        __ContourFaceProc(boundary, (Axis)__faceProcFaceMask[(int)axis, i, 2], faceNodes, vertices, indices, results, faces);

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
                        __ContourEdgeProc(boundary, (Axis)__faceProcEdgeMask[(int)axis, i, 5], edgeNodes, vertices, indices, results, faces);
                }
            }

            private bool __ContourCellProc(
                Boundary boundary,
                NodeInfo nodeInfo,
                IList<Voxel.Vertex> vertices,
                IDictionary<int, int> indices,
                IDictionary<int, Result> results,
                Action<Face> faces)
            {
                if (nodeInfo.type != Type.Internal)
                    return false;

                int i;
                NodeInfo temp;
                for (i = 0; i < 8; ++i)
                {
                    if (__Get(i, nodeInfo.info, out temp))
                        __ContourCellProc(boundary, temp, vertices, indices, results, faces);
                }
                
                int j;
                Node2 faceNodes;
                Vector3Int cellProcFaceMask;
                for (i = 0; i < 3; ++i)
                {
                    for (j = 0; j < 4; ++j)
                    {
                        cellProcFaceMask = __cellProcFaceMask[(i << 2) + j];
                        if (!__Get(cellProcFaceMask.x, nodeInfo.info, out faceNodes.x) ||
                           !__Get(cellProcFaceMask.y, nodeInfo.info, out faceNodes.y))
                            continue;
                        
                        __ContourFaceProc(boundary, (Axis)cellProcFaceMask.z, faceNodes, vertices, indices, results, faces);
                    }
                }

                bool result;
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
                    
                    __ContourEdgeProc(boundary, (Axis)__cellProcEdgeMask[i, 4], edgeNodes, vertices, indices, results, faces);
                }

                return true;
            }
            
            private void __ClusterIndexes(
                Axis axis,
                Node4 nodes,
                ICollection<int> pointIndices, 
                ref int maxSurfaceIndex)
            {
                int numPoints = __points == null ? 0 : __points.Count;
                if (numPoints < 1)
                    return;

                bool isSkip;
                int i, j, index, code, edgeIndex, surfaceIndex = -1, count = 0;
                Vector2Int edgeToVertexIndices;
                Point point;
                NodeInfo nodeInfo;
                Tile<int> tile = new Tile<int>(-1, -1, -1, -1);
                for (i = 0; i < 4; ++i)
                {
                    nodeInfo = nodes[i];

                    edgeIndex = __edgeIndices[(int)axis, i];

                    edgeToVertexIndices = __edgeToVertexIndices[edgeIndex];
                    
                    isSkip = false;
                    index = 0;
                    for (j = 0; j < 16; ++j)
                    {
                        code = __edgesTable[nodeInfo.block.corners, j];
                        if(code == -1)
                        {
                            ++index;
                            continue;
                        }

                        if(code == -2)
                        {
                            isSkip = ((nodeInfo.block.corners >> edgeToVertexIndices.x) & 1) == 0;
                            isSkip = isSkip == (((nodeInfo.block.corners >> edgeToVertexIndices.y) & 1) == 0);

                            break;
                        }

                        if (code == edgeIndex)
                            break;
                    }

                    if (isSkip)
                        continue;

                    index = nodeInfo.block.vertexIndices[index];
                    if (index >= 0 && index < numPoints)
                    {
                        point = __points[index];
                        while (point.parentIndex >= 0 && point.parentIndex < numPoints)
                        {
                            index = point.parentIndex;

                            point = __points[index];
                        }
                        
                        tile[i] = index;

                        index = point.surfaceIndex;
                        if (index != -1)
                        {
                            if (surfaceIndex == -1)
                                surfaceIndex = index;
                            else if(surfaceIndex != index && pointIndices != null)
                            {
                                foreach(int pointIndex in pointIndices)
                                {
                                    if (pointIndex < 0 || pointIndex >= numPoints)
                                        continue;

                                    point = __points[pointIndex];
                                    if (point.surfaceIndex != index)
                                        continue;

                                    point.surfaceIndex = surfaceIndex;

                                    __points[pointIndex] = point;
                                }
                            }
                        }

                        ++count;
                    }
                }

                if (count < 1)
                    return;

                if (surfaceIndex == -1)
                    surfaceIndex = maxSurfaceIndex++;

                for (i = 0; i < 4; ++i)
                {
                    index = tile[i];
                    if (index >= 0 && index < numPoints)
                    {
                        point = __points[index];
                        if (point.surfaceIndex == -1 && pointIndices != null)
                            pointIndices.Add(index);

                        point.surfaceIndex = surfaceIndex;

                        __points[index] = point;
                    }
                }
            }

            private void __ClusterEdge(
                Axis axis,
                Node4 nodes,
                ICollection<int> pointIndices,
                ref int maxSurfaceIndex)
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
                            __ClusterEdge((Axis)__edgeProcEdgeMask[(int)axis, i, 4], edgeNodes, pointIndices, ref maxSurfaceIndex);
                    }
                }
                else
                    __ClusterIndexes(axis, nodes, pointIndices, ref maxSurfaceIndex);
            }
            
            private void __ClusterFace(
                Axis axis,
                Node2 nodes,
                ICollection<int> pointIndices, 
                ref int maxSurfaceIndex)
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
                        __ClusterFace((Axis)__faceProcFaceMask[(int)axis, i, 2], faceNodes, pointIndices, ref maxSurfaceIndex);

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
                        __ClusterIndexes((Axis)__faceProcEdgeMask[(int)axis, i, 5], edgeNodes, pointIndices, ref maxSurfaceIndex);
                }
            }

            private static int[,] __externalEdgeIndices =
            {
                { 0, 8, 4 },
                { 1, 8, 5 },
                { 2, 9, 4 },
                { 3, 9, 5 },
                { 0, 10, 6 },
                { 10, 1, 7 },
                { 2, 11, 6 },
                { 11, 3, 7 }
            };

            private static int[,] __internalEdgeIndices =
            {
                { 1, 2, 3, 5, 6, 7, 9, 10, 11 },
                { 0, 2, 3, 4, 6, 7, 9, 10, 11 },
                { 0, 1, 3, 5, 6, 7, 8, 10, 11 },
                { 0, 1, 2, 4, 6, 7, 8, 10, 11 },
                { 1, 2, 3, 4, 5, 7, 8, 9, 11 },
                { 0, 2, 3, 4, 5, 6, 8, 9, 11 },
                { 0, 1, 3, 4, 5, 7, 8, 9, 10 },
                { 0, 1, 2, 4, 5, 6, 8, 9, 10 }
            };

            private static int[,] __faceEdgeIndices =
            {
                { 0, 6, 2, 4 },
                { 1, 7, 3, 5 },
                { 0, 10, 1, 8 },
                { 2, 11, 3, 9 },
                { 4, 8, 5, 9 },
                { 6, 10, 7, 11 }
            };

            private bool __ClusterCell(Info info)
            {
                Node node;
                Dictionary<Vector3Int, Node> nodes;
                if (info.depth < 1)
                {
                    node = __root;

                    nodes = null;
                }
                else
                {
                    if (__nodes == null || __nodes.Length < info.depth)
                        return false;

                    nodes = __nodes[info.depth - 1];
                    if (nodes == null)
                        return false;

                    if (!nodes.TryGetValue(info.position, out node))
                        return false;
                }

                if (node.type != Type.Internal)
                    return false;

                int i;
                for (i = 0; i < 8; ++i)
                    __ClusterCell(__Get(i, info));

                int surfaceIndex = 0;
                List<int> pointIndices = new List<int>();

                int j;
                Node2 faceNodes;
                Vector3Int cellProcFaceMask;
                for (i = 0; i < 3; ++i)
                {
                    for (j = 0; j < 4; ++j)
                    {
                        cellProcFaceMask = __cellProcFaceMask[(i << 2) + j];
                        if (!__Get(cellProcFaceMask.x, info, out faceNodes.x) ||
                           !__Get(cellProcFaceMask.y, info, out faceNodes.y))
                            continue;

                        __ClusterFace((Axis)cellProcFaceMask.z, faceNodes, pointIndices, ref surfaceIndex);
                    }
                }

                bool result;
                NodeInfo nodeInfo;
                Node4 edgeNodes = new Node4();
                for (i = 0; i < 6; ++i)
                {
                    result = true;

                    for (j = 0; j < 4; ++j)
                    {
                        if (!__Get(__cellProcEdgeMask[i, j], info, out nodeInfo))
                        {
                            result = false;

                            break;
                        }

                        edgeNodes[j] = nodeInfo;
                    }

                    if (!result)
                        continue;
                    
                    __ClusterEdge((Axis)__cellProcEdgeMask[i, 4], edgeNodes, pointIndices, ref surfaceIndex);
                }
                
                int numPoints = __points == null ? 0 : __points.Count, pointIndex;
                Point point;
                for (i = 0; i < 8; ++i)
                {
                    if (__Get(i, info, out nodeInfo))
                    {
                        for(j = 0; j < 4; ++j)
                        {
                            pointIndex = nodeInfo.block.vertexIndices[j];
                            if (pointIndex >= 0 && pointIndex < numPoints)
                            {
                                point = __points[pointIndex];
                                if (point.surfaceIndex == -1)
                                {
                                    point.surfaceIndex = surfaceIndex++;

                                    __points[pointIndex] = point;

                                    pointIndices.Add(pointIndex);
                                }
                            }
                            else
                                break;
                        }

                        for(j = 0; j < nodeInfo.vertexIndexCount; ++j)
                        {
                            pointIndex = nodeInfo.vertexIndexStart + j;
                            if (pointIndex >= 0 && pointIndex < numPoints)
                            {
                                point = __points[pointIndex];
                                if (point.surfaceIndex == -1)
                                {
                                    point.surfaceIndex = surfaceIndex++;

                                    __points[pointIndex] = point;

                                    pointIndices.Add(pointIndex);
                                }
                            }
                        }
                    }
                }

                node.vertexIndexStart = -1;
                node.vertexIndexCount = 0;
                node.block.vertexIndices = new Tile<int>(-1, -1, -1, -1);

                bool isContains;
                int tileIndex = 0, childIndex, edgeIndex, edgeCount, k;
                Point newPoint;
                for(i = 0; i < surfaceIndex; ++i)
                {
                    isContains = false;
                    edgeCount = 0;
                    newPoint = new Point();
                    foreach (int index in pointIndices)
                    {
                        if (index < 0 || index >= numPoints)
                            continue;

                        point = __points[index];
                        if (point.surfaceIndex != i)
                            continue;

                        childIndex = point.info.childIndex;

                        point.surfaceIndex = -1;

                        point.parentIndex = numPoints;
                        __points[index] = point;

                        for (j = 0; j < 3; ++j)
                        {
                            edgeIndex = __externalEdgeIndices[childIndex, j];
                            newPoint.vertex.edges[edgeIndex] += point.vertex.edges[edgeIndex];
                        }

                        for(j = 0; j < 9; ++j)
                        {
                            edgeIndex = __internalEdgeIndices[childIndex, j];
                            edgeCount += point.vertex.edges[edgeIndex];
                        }

                        newPoint.euler += point.euler;
                        newPoint.vertex.qef += point.vertex.qef;
                        newPoint.vertex.normal += point.vertex.normal;

                        isContains = true;
                    }

                    if (!isContains)
                        continue;

                    newPoint.isFaceProp = true;
                    newPoint.surfaceIndex = -1;
                    newPoint.parentIndex = -1;
                    newPoint.euler -= edgeCount >> 2;
                    newPoint.info = info;

                    for (j = 0; j < 6; ++j)
                    {
                        edgeCount = 0;
                        for(k = 0; k < 4; ++k)
                            edgeCount += newPoint.vertex.edges[__faceEdgeIndices[j, k]];

                        if(edgeCount != 0 && edgeCount != 2)
                        {
                            newPoint.isFaceProp = false;

                            break;
                        }
                    }

                    if (tileIndex < 4)
                        node.block.vertexIndices[tileIndex++] = numPoints;
                    else if (tileIndex > 4)
                        ++node.vertexIndexCount;
                    else
                    {
                        node.vertexIndexStart = numPoints;
                        node.vertexIndexCount = 1;
                    }

                    ++numPoints;

                    if(__points != null)
                        __points.Add(newPoint);
                }

                if (nodes == null)
                    __root = node;
                else
                    nodes[info.position] = node;

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
        
        private static readonly int[,] __edgeIndices = { { 3, 2, 1, 0 }, { 7, 5, 6, 4 }, { 11, 10, 9, 8 } };

#region MANIFOLD_TABLES

        private static int[] __verticesNumberTable =
        {
            0, 1, 1, 1, 1, 1, 2, 1, 1, 2, 1, 1, 1, 1, 1, 1,
            1, 1, 2, 1, 2, 1, 3, 1, 2, 2, 2, 1, 2, 1, 2, 1,
            1, 2, 1, 1, 2, 2, 2, 1, 2, 3, 1, 1, 2, 2, 1, 1,
            1, 1, 1, 1, 2, 1, 2, 1, 2, 2, 1, 1, 2, 1, 2, 1,
            1, 2, 2, 2, 1, 1, 2, 1, 2, 3, 2, 2, 1, 1, 1, 1,
            1, 1, 2, 1, 1, 1, 2, 1, 2, 2, 2, 1, 1, 1, 1, 1,
            2, 3, 2, 2, 2, 2, 2, 1, 3, 4, 2, 2, 2, 2, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 1, 1, 1, 1, 1, 1,
            1, 2, 2, 2, 2, 2, 3, 2, 1, 2, 1, 1, 1, 1, 1, 1,
            2, 2, 3, 2, 3, 2, 4, 2, 2, 2, 2, 1, 2, 1, 2, 1,
            1, 2, 1, 1, 2, 2, 2, 1, 1, 2, 1, 1, 1, 1, 1, 1,
            1, 1, 1, 1, 2, 1, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            1, 2, 2, 2, 1, 1, 2, 1, 1, 2, 1, 1, 1, 1 /*2*/, 1, 1,
            1, 1, 2, 1, 1, 1, 2, 1, 1, 1, 1, 1, 1, 1 /*2*/, 1, 1,
            1, 2, 1, 1, 1, 1, 1, 1, 1, 2, 1, 1, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0
        };

        private static int[,] __edgesTable = new int[256, 16]
        {
            { -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 4, 8, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 8, 5, 1, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 4, 5, 1, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 2, 9, 4, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 8, 2, 9, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 4, 2, 9, -1, 8, 5, 1, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 1, 2, 9, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 5, 9, 3, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 5, 9, 3, -1, 0, 8, 4, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 1, 8, 3, 9, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 1, 3, 9, 4, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 2, 3, 4, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 8, 2, 3, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 1, 8, 2, 3, 4, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 1, 2, 3, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 6, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 8, 4, 6, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 6, 0, -1, 1, 8, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 4, 6, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 6, -1, 4, 2, 9, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 8, 2, 9, 6, -2, -1, -1, 10, -1, -1, -1, -1, -1, -1, -1 },
            { 6, 0, 10, -1, 4, 2, 9, -1, 1, 8, 5, -2, -1, -1, -1, -1 },
            { 10, 1, 2, 9, 6, 5, -2, -1, -1, -1, -1, -2, -1, -1, -1, -1 },
            { 5, 9, 3, -1, 0, 6, 10, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 5, 9, 3, -1, 10, 8, 4, 6, -2, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 6, -1, 8, 1, 3, 9, -2, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 3, 9, 4, 6, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 6, -1, 4, 2, 5, 3, -2, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 8, 2, 3, 6, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 6, -1, 1, 8, 4, 2, 3, -2, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 2, 3, 6, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 4, 8, -1, 10, 1, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 8, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 4, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 7, -1, 4, 2, 9, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 7, -1, 0, 8, 9, 2, -2, -1, -1, -1, -1, -1, -1, -1 },
            { 8, 5, 7, 10, -1, 4, 9, 2, -2, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 2, 9, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 5, 9, 3, -1, 10, 1, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 5, 9, 3, -1, 10, 1, 7, -1, 0, 8, 4, -2, -1, -1, -1, -1 },
            { 10, 8, 3, 9, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 3, 9, 4, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 7, -1, 4, 2, 3, 5, -2, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 7, -1, 0, 8, 5, 3, 2, -2, -1, -1, -1, -1, -1, -1 },
            { 10, 8, 2, 3, 4, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 2, 3, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 6, 0, 1, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 7, 1, 8, 4, 6, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 8, 6, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 4, 6, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 1, 7, 6, -1, 4, 9, 2, -2, -1, -1, -1, -1, -1, -1, -1 },
            { 1, 8, 2, 9, 6, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 2, 9, 4, -1, 0, 8, 5, 7, 6, -2, -1, -1, -1, -1, -1, -1 },
            { 2, 9, 6, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 5, 9, 3, -1, 0, 6, 7, 1, -2, -1, -1, -1, -1, -1, -1, -1 },
            { 5, 9, 3, -1, 1, 8, 4, 6, 7, -2, -1, -1, -1, -1, -1, -1 },
            { 0, 8, 3, 9, 6, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 3, 9, 4, 6, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 6, 7, 1, -1, 4, 2, 5, 3, -2, -1, -1, -1, -1, -1, -1 },
            { 1, 8, 2, 3, 6, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 8, 2, 3, 4, 6, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 2, 3, 6, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 6, 11, 2, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 6, 11, 2, -1, 0, 4, 8, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 6, 11, 2, -1, 1, 8, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 6, 2, 11, -1, 0, 1, 5, 4, -2, -1, -1, -1, -1, -1, -1, -1 },
            { 11, 9, 4, 6, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 8, 11, 9, 6, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 1, 8, 5, -1, 11, 9, 4, 6, -2, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 1, 11, 9, 6, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 3, 9, 5, -1, 2, 11, 6, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 3, 9, 5, -1, 2, 11, 6, -1, 0, 8, 4, -2, -1, -1, -1, -1 },
            { 2, 11, 6, -1, 8, 1, 3, 9, -2, -1, -1, -1, -1, -1, -1, -1 },
            { 2, 11, 6, -1, 1, 0, 4, 9, 3, -2, -1, -1, -1, -1, -1, -1 },
            { 11, 3, 4, 6, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 8, 11, 3, 6, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 1, 8, 11, 3, 4, 6, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 1, 11, 3, 6, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 11, 2, 10, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 8, 2, 11, 4, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 8, 5, 1, -1, 0, 2, 11, 10, -2, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 2, 11, 4, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 11, 9, 4, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 11, 8, 9, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 1, 8, 5, -1, 0, 10, 11, 9, 4, -2, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 11, 9, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 5, 9, 3, -1, 10, 0, 11, 2, -2, -1, -1, -1, -1, -1, -1, -1 },
            { 5, 9, 3, -1, 4, 8, 10, 11, 2, -2, -1, -1, -1, -1, -1, -1 },
            { 8, 1, 3, 9, -1, 0, 10, 11, 2, -2, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 2, 11, 3, 9, 4, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 11, 3, 4, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 8, 11, 3, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 1, 8, 11, 3, 4, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 11, 3, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 7, -1, 6, 11, 2, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 8, 0, 4, -1, 10, 1, 7, -1, 2, 6, 11, -2, -1, -1, -1, -1 },
            { 8, 5, 7, 10, -1, 6, 2, 11, -2, -1, -1, -1, -1, -1, -1, -1 },
            { 2, 6, 11, -1, 0, 10, 4, 7, 5, -2, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 7, -1, 4, 9, 11, 6, -2, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 7, -1, 0, 8, 9, 11, 6, -2, -1, -1, -1, -1, -1, -1 },
            { 8, 5, 7, 10, -1, 4, 9, 11, 6, -2, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 11, 9, 6, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 5, 9, 3, -1, 2, 11, 6, -1, 10, 1, 7, -2, -1, -1, -1, -1 },
            { 5, 9, 3, -1, 2, 11, 6, -1, 10, 1, 7, -1, 0, 8, 4, -2 },
            { 6, 2, 11, -1, 10, 7, 3, 9, 8, -2, -1, -1, -1, -1, -1, -1 },
            { 6, 2, 11, -1, 3, 7, 10, 0, 4, 9, -2, -1, -1, -1, -1, -1 },
            { 10, 1, 7, -1, 6, 11, 3, 5, 4, -2, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 7, -1, 6, 0, 8, 5, 3, 11, -2, -1, -1, -1, -1, -1 },
            { 10, 8, 11, 3, 4, 6, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 11, 3, 6, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 1, 2, 11, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 1, 8, 2, 11, 4, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 8, 2, 11, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 11, 2, 4, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 1, 11, 9, 4, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 1, 8, 11, 9, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 8, 11, 9, 4, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 11, 9, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 5, 9, 3, -1, 0, 1, 7, 11, 2, -2, -1, -1, -1, -1, -1, -1 },
            { 5, 9, 3, -1, 4, 8, 1, 7, 11, 2, -2, -1, -1, -1, -1, -1 },
            { 0, 8, 2, 11, 3, 9, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 2, 11, 3, 9, 4, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 1, 11, 3, 4, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 1, 8, 11, 3, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 8, 11, 3, 4, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 11, 3, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 11, 3, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 8, 4, -1, 7, 11, 3, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 8, 5, 1, -1, 7, 3, 11, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 7, 11, 3, -1, 0, 4, 1, 5, -2, -1, -1, -1, -1, -1, -1, -1 },
            { 4, 9, 2, -1, 11, 3, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 11, 3, 7, -1, 0, 8, 2, 9, -2, -1, -1, -1, -1, -1, -1, -1 },
            { 8, 5, 1, -1, 11, 3, 7, -1, 4, 9, 2, -2, -1, -1, -1, -1 },
            { 7, 11, 3, -1, 0, 1, 5, 9, 2, -2, -1, -1, -1, -1, -1, -1 },
            { 11, 9, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 11, 9, 7, 5, -1, 0, 8, 4, -2, -1, -1, -1, -1, -1, -1, -1 },
            { 1, 8, 11, 9, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 1, 11, 9, 4, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 2, 11, 4, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 8, 2, 11, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 1, 8, 2, 11, 4, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 1, 2, 11, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 6, -1, 7, 11, 3, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 7, 11, 3, -1, 8, 4, 6, 10, -2, -1, -1, -1, -1, -1, -1, -1 },
            { 8, 5, 1, -1, 0, 10, 6, -1, 7, 11, 3, -2, -1, -1, -1, -1 },
            { 7, 11, 3, -1, 5, 1, 4, 6, 10, -2, -1, -1, -1, -1, -1, -1 },
            { 7, 11, 3, -1, 10, 6, 0, -1, 4, 9, 2, -2, -1, -1, -1, -1 },
            { 7, 11, 3, -1, 9, 2, 6, 10, 8, -2, -1, -1, -1, -1, -1, -1 },
            { 8, 5, 1, -1, 0, 10, 6, -1, 7, 11, 3, -1, 4, 9, 2, -2 },
            { 7, 11, 3, -1, 9, 2, 5, 1, 10, 6, -2, -1, -1, -1, -1, -1 },
            { 11, 9, 7, 5, -1, 0, 10, 6, -2, -1, -1, -1, -1, -1, -1, -1 },
            { 11, 9, 7, 5, -1, 8, 4, 6, 10, -2, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 6, -1, 9, 8, 1, 7, 11, -2, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 11, 9, 4, 6, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 6, -1, 4, 5, 7, 2, 11, -2, -1, -1, -1, -1, -1, -1 },
            { 10, 8, 2, 11, 6, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 6, -1, 4, 8, 1, 7, 11, 2, -2, -1, -1, -1, -1, -1 },
            { 10, 1, 2, 11, 6, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 11, 3, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 8, 4, -1, 10, 1, 3, 11, -2, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 8, 11, 3, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 11, 3, 4, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 4, 9, 2, -1, 10, 1, 3, 11, -2, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 8, 9, 2, -1, 10, 1, 3, 11, -2, -1, -1, -1, -1, -1, -1 },
            { 4, 9, 2, -1, 8, 5, 3, 11, 10, -2, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 2, 11, 3, 9, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 11, 9, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 11, 9, 5, -1, 0, 8, 4, -2, -1, -1, -1, -1, -1, -1 },
            { 10, 8, 11, 9, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 11, 9, 4, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 2, 11, 4, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 1, 8, 2, 11, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 8, 2, 11, 4, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 2, 11, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 1, 11, 3, 6, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 1, 8, 11, 3, 4, 6, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 8, 11, 3, 6, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 11, 3, 4, 6, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 4, 9, 2, -1, 0, 6, 1, 11, 3, -2, -1, -1, -1, -1, -1, -1 },
            { 1, 8, 2, 11, 3, 9, 6, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 4, 9, 2, -1, 3, 11, 6, 0, 5, 8, -2, -1, -1, -1, -1, -1 },
            { 2, 11, 3, 9, 6, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 1, 11, 9, 6, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 1, 8, 11, 9, 4, 6, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 8, 11, 9, 6, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 11, 9, 4, 6, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 1, 2, 11, 4, 6, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 1, 8, 2, 11, 6, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 8, 2, 11, 4, 6, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 2, 11, 6, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 2, 3, 6, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 8, 4, -1, 6, 7, 3, 2, -2, -1, -1, -1, -1, -1, -1, -1 },
            { 5, 1, 8, -1, 6, 2, 7, 3, -2, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 1, 5, 4, -1, 6, 2, 3, 7, -2, -1, -1, -1, -1, -1, -1 },
            { 3, 9, 4, 6, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 8, 3, 9, 6, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 8, 5, 1, -1, 7, 3, 6, 9, 4, -2, -1, -1, -1, -1, -1, -1 },
            { 0, 1, 3, 9, 6, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 2, 9, 6, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 8, 4, -1, 7, 6, 2, 9, 5, -2, -1, -1, -1, -1, -1, -1 },
            { 1, 8, 2, 9, 6, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 1, 2, 9, 4, 6, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 4, 6, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 8, 6, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 1, 8, 4, 6, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 1, 6, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 2, 3, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 8, 2, 3, 4, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 1, 8, 5, -1, 0, 10, 2, 3, 7, -2, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 2, 3, 4, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 3, 9, 4, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 8, 3, 9, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 8, 5, 1, -1, 4, 9, 0, 10, 7, 3, -2, -1, -1, -1, -1, -1 },
            { 10, 1, 3, 9, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 2, 9, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 8, 2, 9, 4, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 1, 8, 2, 9, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 2, 9, 4, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 4, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 8, 7, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 1, 8, 4, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 7, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 2, 3, 6, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 8, 4, -1, 10, 1, 2, 3, 6, -2, -1, -1, -1, -1, -1, -1 },
            { 10, 8, 2, 3, 6, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 2, 3, 4, 6, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 3, 9, 4, 6, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 1, 8, 3, 9, 6, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 8, 3, 9, 4, 6, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 3, 9, 6, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 2, 9, 6, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 2, 9, 6, 5, -1, 0, 8, 4, -2, -1, -1, -1, -1, -1 },
            { 10, 8, 2, 9, 6, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 2, 9, 4, 6, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 1, 4, 6, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 1, 8, 6, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 10, 8, 4, 6, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 10, 6, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 1, 2, 3, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 1, 8, 2, 3, 4, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 8, 2, 3, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 2, 3, 4, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 1, 3, 9, 4, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 1, 8, 3, 9, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 8, 3, 9, 4, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 3, 9, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 1, 2, 9, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 1, 8, 2, 9, 4, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 8, 2, 9, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 2, 9, 4, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 1, 4, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 1, 8, 5, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { 0, 8, 4, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
            { -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 }
        };
#endregion

        private int __depth;
        private float __increment;
        private IEngineSampler __sampler;
        private Vector3 __scale;
        private Vector3 __offset;
        private Pool<Vertex> __vertices;
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
        
        public ManifoldDC(int depth, float increment, Vector3 scale, IEngineSampler sampler)
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