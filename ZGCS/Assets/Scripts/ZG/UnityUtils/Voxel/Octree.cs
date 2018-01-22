using System;
using UnityEngine;
using ZG;

namespace ZG.Voxel
{
    public abstract class Octree : IDisposable
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

        public enum Axis
        {
            X,
            Y,
            Z, 

            Unkown
        }

        private enum Type
        {
            Internal,
            Psuedo,
            Leaf
        }

        public struct Info
        {
            public int corners;
            public Vector3Int offset;
            public Vector3 position;
            public Vector3 normal;
            public Qef.Data qef;
            //public Isosurface.QEFProper.QEFData qef;
        }

        private struct Node
        {
            public Type type;
            public int infoIndex;
            public int depth;
            public Object<Node>[] children;
        }

        private struct Node2
        {
            public Node x;
            public Node y;

            public bool isInternal
            {
                get
                {
                    return x.type == Type.Internal || y.type == Type.Internal;
                }
            }

            public Node this[int index]
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
            public Node x;
            public Node y;
            public Node z;
            public Node w;

            public bool isInternal
            {
                get
                {
                    return x.type == Type.Internal || y.type == Type.Internal || z.type == Type.Internal || w.type == Type.Internal;
                }
            }

            public Node this[int index]
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
        
        public delegate void TileProcessor(int x, int y, int z, int w, Axis axis, Vector3Int offset);

        private static readonly Vector3Int[] __childMinOffsets = new Vector3Int[]
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

        private static readonly Vector2Int[] __edgeToVertexIndices = new Vector2Int[]
        {
            new Vector2Int(0,4), new Vector2Int(1,5), new Vector2Int(2,6), new Vector2Int(3,7),	// x-axis 
	        new Vector2Int(0,2), new Vector2Int(1,3), new Vector2Int(4,6), new Vector2Int(5,7),	// y-axis
	        new Vector2Int(0,1), new Vector2Int(2,3), new Vector2Int(4,5), new Vector2Int(6,7)  // z-axis
        };


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

        private static readonly int[,] __processEdgeMask = {{3,2,1,0},{7,5,6,4},{11,10,9,8}} ;

        private static readonly int[,] __orders =
        {
            { 0, 0, 1, 1 },
            { 0, 1, 0, 1 },
        };

        public float increment = 1.0f / 8.0f;
        private int __depth;
        private Vector3 __scale;
        private Vector3 __offset;
        private Object<Node> __root;
        private Pool<Info> __infos;

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
        
        ~Octree()
        {
            Dispose();
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

        public bool Get(int index, out Info info)
        {
            if(__infos == null)
            {
                info = default(Info);

                return false;
            }

            return __infos.TryGetValue(index, out info);
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

        public bool Create(
            Boundary boundary,
            int depth, 
            int qefSweeps,
            //float qefError,
            float threshold,
            Vector3 scale,
            Vector3 offset)
        {
            __Dispose(__root);

            __scale = scale;
            __offset = offset;

            const int MAX_DEPTH = (sizeof(int) << 3) / 3;

            __depth = Mathf.Clamp(depth, 0, MAX_DEPTH);

            __root = __Create(
                boundary, 
                __depth,
                qefSweeps,
                //qefError,
                threshold,
                Vector3Int.zero);

            return __root != null;
        }

        public void Dispose()
        {
            __Dispose(__root);

            __root = null;
        }

        public void Build(TileProcessor tileProcessor)
        {
            if (__root == null)
                return;

            __ContourCellProc(__root.value, tileProcessor);
        }
        
        public abstract float GetDensity(Vector3 position);

        /*private Vector3 __CalculateSurfaceNormal(float bias, Vector3 point)
        {
            return new Vector3(
                GetDensity(point + new Vector3(bias, 0.0f, 0.0f)) - GetDensity(point - new Vector3(bias, 0.0f, 0.0f)),
                GetDensity(point + new Vector3(0.0f, bias, 0.0f)) - GetDensity(point - new Vector3(0.0f, bias, 0.0f)),
                GetDensity(point + new Vector3(0.0f, 0.0f, bias)) - GetDensity(point - new Vector3(0.0f, 0.0f, bias))).normalized;
        }*/

        private void __Dispose(Object<Node> node)
        {
            if (node == null)
                return;

            int numChildren = node.value.children == null ? 0 : node.value.children.Length;

            for (int i = 0; i < numChildren; ++i)
            {
                __Dispose(node.value.children[i]);

                node.value.children[i] = null;
            }

            if (__infos != null)
                __infos.RemoveAt(node.value.infoIndex);

            node.value.infoIndex = -1;

            node.Dispose();
        }

        private Object<Node> __Create(
            Boundary boundary, 
            int depth,
            int qefSweeps,
            //float qefError,
            float threshold,
            //float bias,
            Vector3Int position)
        {
            int i, corners = 0, edgeCount = 0, size = 1 << depth;
            Vector2Int vertexIndices;
            Vector3 averageNormal = Vector3.zero, temp;
            Qef qef = new Qef();
            Object<Node> result = Object<Node>.Create();
            result.value.depth = depth;

            Info info;
            if (depth > 0)
            {
                bool isHasChildren = false;
                bool isCollapsible = true;
                int halfSize = size >> 1, midSign = -1;

                --depth;

                Array.Resize(ref result.value.children, 8);
                Object<Node> child;
                for (i = 0; i < 8; ++i)
                {
                    child = __Create(
                        boundary, 
                        depth, 
                        qefSweeps, 
                        //qefError, 
                        threshold, 
                        //bias, 
                        position + (__childMinOffsets[i] * halfSize));
                    if (child != null)
                    {
                        isHasChildren = true;

                        if (__infos != null && __infos.TryGetValue(child.value.infoIndex, out info))
                        {
                            midSign = (info.corners >> (7 - i)) & 1;

                            corners |= ((info.corners >> i) & 1) << i;

                            averageNormal += info.normal;

                            qef.Add(info.qef);
                            //qef.Add(ref info.qef);

                            result.value.children[i] = child;

                            edgeCount++;
                        }
                        else
                            isCollapsible = false;
                    }

                    result.value.children[i] = child;
                }
                
                if(!isHasChildren)
                {
                    result.Dispose();

                    return null;
                }

                if(!isCollapsible || IsBoundary(position, size, boundary))
                {
                    result.value.type = Type.Internal;

                    result.value.infoIndex = -1;

                    return result;
                }
                
                temp = qef.Solve(/*qefError, */qefSweeps/*, qefError*/);
                if (qef.GetError(temp) > threshold)
                {
                    result.value.type = Type.Internal;

                    result.value.infoIndex = -1;

                    return result;
                }
                
                result.value.type = Type.Psuedo;

                for (i = 0; i < 8; ++i)
                {
                    child = result.value.children[i];
                    if (child == null)
                        corners |= midSign << i;
                    else
                    {
                        __Dispose(child);

                        result.value.children[i] = null;
                    }
                }
            }
            else
            {
                Vector3 point, normal;
                for (i = 0; i < 8; ++i)
                {
                    if (GetDensity(Vector3.Scale(position + __childMinOffsets[i], __scale) + __offset) < 0.0f)
                        corners |= 1 << i;
                }

                if (corners == 0 || corners == 255)
                {
                    result.Dispose();

                    return null;
                }
                
                for (i = 0; i < 12 && edgeCount < 6; ++i)
                {
                    vertexIndices = __edgeToVertexIndices[i];
                    if ((((corners >> vertexIndices.x) & 1) == 0) == (((corners >> vertexIndices.y) & 1) == 0))
                        continue;

                    point = ApproximateZeroCrossingPosition(
                        Vector3.Scale(position + __childMinOffsets[vertexIndices.x], __scale) + __offset,
                        Vector3.Scale(position + __childMinOffsets[vertexIndices.y], __scale) + __offset);

                    normal = CalculateSurfaceNormal(point);
                    //normal = __CalculateSurfaceNormal(bias, point);

                    qef.Add(new Qef.Data(point, normal));
                    //qef.Add(point, normal);

                    averageNormal += normal;

                    ++edgeCount;
                }
                
                result.value.type = Type.Leaf;

                temp = qef.Solve(/*qefError, */qefSweeps/*, qefError*/);
            }
            
            info.corners = corners;
            info.offset = position;

            Vector3 min = Vector3.Scale(position, __scale) + __offset, max = min + new Vector3(size, size, size);
            if (temp.x < min.x || temp.y < min.y || temp.z < min.z || temp.x > max.x || temp.y > max.y || temp.z > max.z)
                info.position = qef.massPoint;
            else
                info.position = temp;

            /*info.position = new Bounds(
                Vector3.Scale(position + new Vector3(0.5f, 0.5f, 0.5f), __scale) + __offset,
                Vector3.Scale(new Vector3(size, size, size), __scale)).Contains(temp) ? temp : qef.massPoint;*/

            info.normal = (averageNormal / edgeCount).normalized;
            info.qef = qef.data;

            if (__infos == null)
                __infos = new Pool<Info>();

            result.value.infoIndex = __infos.Add(info);
            
            return result;
        }

        private void __ContourProcessEdge(Node4 nodes, Axis axis, TileProcessor tileProcessor)
        {
            if (tileProcessor == null || __infos == null)
                return;
            
            bool isSign = false, isFlip = false;
            int depth = int.MaxValue;
            Vector2Int vertexIndices;
            Vector3Int offset = new Vector3Int();
            Info info;
            Node node;
            for(int i = 0; i < 4; ++i)
            {
                node = nodes[i];
                if (node.depth < depth && __infos.TryGetValue(node.infoIndex, out info))
                {
                    depth = node.depth;

                    vertexIndices = __edgeToVertexIndices[__processEdgeMask[(int)axis, i]];

                    isFlip = ((info.corners >> vertexIndices.x) & 1) == 0;

                    isSign = isFlip == (((info.corners >> vertexIndices.y) & 1) == 0);
                    
                    offset = info.offset + (isFlip ? __childMinOffsets[vertexIndices.y] : __childMinOffsets[vertexIndices.x]);
                }
            }

            if (isSign)
                return;
            
            if (isFlip)
                tileProcessor(
                    nodes.z.infoIndex, 
                    nodes.x.infoIndex, 
                    nodes.w.infoIndex, 
                    nodes.y.infoIndex, 
                    axis,
                    offset);
            else
                tileProcessor(
                    nodes.z.infoIndex, 
                    nodes.w.infoIndex, 
                    nodes.x.infoIndex, 
                    nodes.y.infoIndex, 
                    axis,
                    offset);

            /*if(isFlip)
            {
                indexHandler(nodes[0].infoIndex);
                indexHandler(nodes[1].infoIndex);
                indexHandler(nodes[3].infoIndex);

                indexHandler(nodes[0].infoIndex);
                indexHandler(nodes[3].infoIndex);
                indexHandler(nodes[2].infoIndex);
            }
            else
            {
                indexHandler(nodes[0].infoIndex);
                indexHandler(nodes[3].infoIndex);
                indexHandler(nodes[1].infoIndex);

                indexHandler(nodes[0].infoIndex);
                indexHandler(nodes[2].infoIndex);
                indexHandler(nodes[3].infoIndex);
            }*/
        }

        private void __ContourEdgeProc(Node4 nodes, Axis axis, TileProcessor tileProcessor)
        {
            if (nodes.isInternal)
            {
                bool result;
                int i, j;
                Node node;
                Node4 edgeNodes = new Node4();
                Object<Node> temp;
                for (i = 0; i < 2; ++i)
                {
                    result = true;

                    for (j = 0; j < 4; ++j)
                    {
                        node = nodes[j];
                        if (node.type == Type.Internal)
                        { 
                            temp = node.children[__edgeProcEdgeMask[(int)axis, i, j]];
                            if (temp == null)
                            {
                                result = false;

                                break;
                            }

                            edgeNodes[j] = temp.value;
                        }
                        else
                            edgeNodes[j] = node;
                    }

                    if(result)
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
            Node node;
            Node2 faceNodes = new Node2();
            Node4 edgeNodes = new Node4();
            Object<Node> temp;
            for(i = 0; i < 4; ++i)
            {
                result = true;
                for (j = 0; j < 2; ++j)
                {
                    node = nodes[j];

                    if (node.type == Type.Internal)
                    {
                        temp = node.children[__faceProcFaceMask[(int)axis, i, j]];
                        if(temp == null)
                        {
                            result = false;

                            break;
                        }

                        faceNodes[j] = temp.value;
                    }
                    else
                        faceNodes[j] = node;
                }

                if(result)
                    __ContourFaceProc(faceNodes, (Axis)__faceProcFaceMask[(int)axis, i, 2], tileProcessor);

                result = true;

                index = __faceProcEdgeMask[(int)axis, i, 0];
                for (j = 0; j < 4; ++j)
                {
                    node = nodes[__orders[index, j]];
                    if (node.type == Type.Internal)
                    {
                        temp = node.children[__faceProcEdgeMask[(int)axis, i, j + 1]];
                        if (temp == null)
                        {
                            result = false;

                            break;
                        }

                        edgeNodes[j] = temp.value;
                    }
                    else
                        edgeNodes[j] = node;
                }

                if (result)
                    __ContourEdgeProc(edgeNodes, (Axis)__faceProcEdgeMask[(int)axis, i, 5], tileProcessor);
            }
        }

        private void __ContourCellProc(Node node, TileProcessor tileProcessor)
        {
            if (node.type != Type.Internal)
                return;

            int i;
            Object<Node> temp;
            for (i = 0; i < 8; ++i)
            {
                temp = node.children[i];
                if (temp == null)
                    continue;

                __ContourCellProc(temp.value, tileProcessor);
            }

            Node2 faceNodes;
            Vector3Int cellProcFaceMask;
            for(i = 0; i < 12; ++i)
            {
                cellProcFaceMask = __cellProcFaceMask[i];

                temp = node.children[cellProcFaceMask.x];
                if (temp == null)
                    continue;

                faceNodes.x = temp.value;

                temp = node.children[cellProcFaceMask.y];
                if (temp == null)
                    continue;

                faceNodes.y = temp.value;

                __ContourFaceProc(faceNodes, (Axis)cellProcFaceMask.z, tileProcessor);
            }

            int j;
            Node4 edgeNodes = new Node4();
            for(i = 0; i < 6; ++i)
            {
                temp = null;

                for (j = 0; j < 4; ++j)
                {
                    temp = node.children[__cellProcEdgeMask[i, j]];
                    if (temp == null)
                        break;

                    edgeNodes[j] = temp.value;
                }

                if (temp == null)
                    continue;

                __ContourEdgeProc(edgeNodes, (Axis)__cellProcEdgeMask[i, 4], tileProcessor);
            }
        }
    }
}