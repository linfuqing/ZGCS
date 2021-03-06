﻿using System.Collections.Generic;
using UnityEngine;

namespace ZG
{
    public class Delaunay
    {
        public struct Circle
        {
            public float sqrRadius;
            public Vector2 center;

            /*public Circle(Vector2 x, Vector2 y, Vector2 z)
            {
                center.x = ((y.y - x.y) * (z.y * z.y - x.y * x.y + z.x * z.x - x.x * x.x) - (z.y - x.y) * (y.y * y.y - x.y * x.y + y.x * y.x - x.x * x.x)) / (2.0f * (z.x - x.x) * (y.y - x.y) - 2.0f * ((y.x - x.x) * (z.y - x.y)));
                center.y = ((y.x - x.x) * (z.x * z.x - x.x * x.x + z.y * z.y - x.y * x.y) - (z.x - x.x) * (y.x * y.x - x.x * x.x + y.y * y.y - x.y * x.y)) / (2.0f * (z.y - x.y) * (y.x - x.x) - 2.0f * ((y.y - x.y) * (z.x - x.x)));

                sqrRadius = (x - center).sqrMagnitude;
            }*/

            public Circle(Vector2 x, Vector2 y, Vector2 z)
            {
                float m1, m2, mx1, mx2, my1, my2;
                
                if (Mathf.Approximately(x.y, y.y))
                {
                    if (Mathf.Approximately(y.y, z.y))
                    {
                        center = new Vector2(float.NaN, float.NaN);

                        sqrRadius = 0.0f;

                        return;
                    }

                    m2 = -(z.x - y.x) / (z.y - y.y);
                    mx2 = (y.x + z.x) / 2.0f;
                    my2 = (y.y + z.y) / 2.0f;
                    center.x = (y.x + x.x) / 2.0f;
                    center.y = m2 * (center.x - mx2) + my2;
                }
                else if (Mathf.Approximately(y.y, z.y))
                {
                    m1 = -(y.x - x.x) / (y.y - x.y);
                    mx1 = (x.x + y.x) / 2.0f;
                    my1 = (x.y + y.y) / 2.0f;
                    center.x = (z.x + y.x) / 2.0f;
                    center.y = m1 * (center.x - mx1) + my1;
                }
                else
                {
                    m1 = -(y.x - x.x) / (y.y - x.y);
                    m2 = -(z.x - y.x) / (z.y - y.y);

                    if (Mathf.Approximately(m1, m2))
                    {
                        center = new Vector2(float.NaN, float.NaN);

                        sqrRadius = 0.0f;

                        return;
                    }

                    mx1 = (x.x + y.x) / 2.0f;
                    mx2 = (y.x + z.x) / 2.0f;
                    my1 = (x.y + y.y) / 2.0f;
                    my2 = (y.y + z.y) / 2.0f;
                    center.x = (m1 * mx1 - m2 * mx2 + my2 - my1) / (m1 - m2);
                    center.y = Mathf.Abs(x.y - y.y) > Mathf.Abs(y.y - z.y) ? m1 * (center.x - mx1) + my1 : m2 * (center.x - mx2) + my2;
                }

                sqrRadius = (x - center).sqrMagnitude;
            }

            public bool Check(Vector2 point)
            {
                return sqrRadius > 0.0f && (point - center).sqrMagnitude <= sqrRadius;
            }
        }

        public struct Vertex
        {
            public int count;

            public Vector2 position;

            public Vertex(Vector2 position)
            {
                count = 0;

                this.position = position;
            }
        }

        public struct Edge
        {
            public int vertexIndexX;
            public int vertexIndexY;

            public int count;

            public Edge(int vertexIndexX, int vertexIndexY)
            {
                this.vertexIndexX = vertexIndexX;
                this.vertexIndexY = vertexIndexY;

                count = 1;
            }
        }

        public struct Triangle
        {
            public int vertexIndexX;
            public int vertexIndexY;
            public int vertexIndexZ;

            public int edgeIndexX;
            public int edgeIndexY;
            public int edgeIndexZ;

            public Circle circle;
        }

        [System.Serializable]
        public struct MapInfo : IMapInfo
        {
#if UNITY_EDITOR
            public string name;
#endif

            public Vector2 offset;
            public Vector2 scale;

            public float Get(float x, float y)
            {
                return Mathf.PerlinNoise(x * scale.x + offset.x, y * scale.y + offset.y);
            }
        }

        [System.Serializable]
        public struct MeshInfo
        {
            [System.Serializable]
            public struct Item
            {
                public int index;
                public float min;
                public float max;
            }

#if UNITY_EDITOR
            public string name;
#endif
            public bool isOverlap;
            
            public float scale;
            public float offset;
            
            public Color color;

            public Item[] items;
        }

        public interface IMapInfo
        {
            float Get(float x, float y);
        }

        public class Builder
        {
            private struct Node
            {
                public Delaunay delaunay;

                public int[] leftVertexIndices;
                public int[] topVertexIndices;
                public int[] rightVertexIndices;
                public int[] bottomVertexIndices;
            }
            
            private int __framePointCount;
            private int __width;
            private Rect __rect;
            private Dictionary<int, Node> __nodes;

            public Builder(int framePointCount, int width, Rect rect)
            {
                __framePointCount = framePointCount;
                __width = width;
                __rect = rect;
            }

            public Delaunay Create(int x, int y, int pointCount, float bias)
            {
                if (x < 0 || x >= __width || y < 0)
                    return null;

                if (__nodes == null)
                    __nodes = new Dictionary<int, Node>();

                int index = x + y * __width;
                Vector2 size = __rect.size;
                Rect rect = new Rect(new Vector2(size.x * x, size.y * y), size);
                Node node;

                node.delaunay = new Delaunay(Rect.MinMaxRect(rect.xMin - bias, rect.yMin - bias, rect.xMax + bias, rect.yMax + bias), 0);

                for (int i = 0; i < pointCount; ++i)
                    node.delaunay.AddPoint(new Vector2(Random.Range(rect.xMin + bias, rect.xMax - bias), Random.Range(rect.yMin + bias, rect.yMax - bias)));

                node.delaunay.AddPoint(rect.min);
                node.delaunay.AddPoint(new Vector2(rect.xMin, rect.yMax));
                node.delaunay.AddPoint(rect.max);
                node.delaunay.AddPoint(new Vector2(rect.xMax, rect.yMin));
                Node temp;

                node.leftVertexIndices = new int[__framePointCount];
                if (x > 0 && __nodes.TryGetValue(x - 1 + y * __width, out temp))
                {
                    for (int i = 0; i < __framePointCount; ++i)
                        node.leftVertexIndices[i] = node.delaunay.AddPoint(temp.delaunay.__vertices[temp.rightVertexIndices[i]].position);
                }
                else
                {
                    for (int i = 0; i < __framePointCount; ++i)
                        node.leftVertexIndices[i] = node.delaunay.AddPoint(new Vector2(rect.xMin, Random.Range(rect.yMin, rect.yMax)));
                }

                node.topVertexIndices = new int[__framePointCount];
                if (__nodes.TryGetValue(x + (y + 1) * __width, out temp))
                {
                    for (int i = 0; i < __framePointCount; ++i)
                        node.topVertexIndices[i] = node.delaunay.AddPoint(temp.delaunay.__vertices[temp.bottomVertexIndices[i]].position);
                }
                else
                {
                    for (int i = 0; i < __framePointCount; ++i)
                        node.topVertexIndices[i] = node.delaunay.AddPoint(new Vector2(Random.Range(rect.xMin, rect.xMax), rect.yMax));
                }

                node.rightVertexIndices = new int[__framePointCount];
                if (x < (__width - 1) && __nodes.TryGetValue(x + 1 + y * __width, out temp))
                {
                    for (int i = 0; i < __framePointCount; ++i)
                        node.rightVertexIndices[i] = node.delaunay.AddPoint(temp.delaunay.__vertices[temp.leftVertexIndices[i]].position);
                }
                else
                {
                    for (int i = 0; i < __framePointCount; ++i)
                        node.rightVertexIndices[i] = node.delaunay.AddPoint(new Vector2(rect.xMax, Random.Range(rect.yMin, rect.yMax)));
                }

                node.bottomVertexIndices = new int[__framePointCount];
                if (y > 0 && __nodes.TryGetValue(x + (y - 1) * __width, out temp))
                {
                    for (int i = 0; i < __framePointCount; ++i)
                        node.bottomVertexIndices[i] = node.delaunay.AddPoint(temp.delaunay.__vertices[temp.topVertexIndices[i]].position);
                }
                else
                {
                    for (int i = 0; i < __framePointCount; ++i)
                        node.bottomVertexIndices[i] = node.delaunay.AddPoint(new Vector2(Random.Range(rect.xMin, rect.xMax), rect.yMin));
                }

                node.delaunay.DeleteFrames();

                __nodes[index] = node;

                return node.delaunay;
            }
        }

        private HashSet<int> __frameVertexIndices;
        private HashSet<int> __frameEdgeIndices;
        private Pool<Vertex> __vertices;
        private Pool<Edge> __edges;
        private Pool<Triangle> __triangles;

        public Delaunay(Vector2 leftBottom, Vector2 leftTop, Vector2 rightTop, Vector2 rightBottom)
        {
            __frameVertexIndices = new HashSet<int>();

            __vertices = new Pool<Vertex>();

            Vertex vertex;
            vertex.count = 2;

            vertex.position = leftBottom;
            __frameVertexIndices.Add(__vertices.Add(vertex));
            vertex.position = leftTop;
            __frameVertexIndices.Add(__vertices.Add(vertex));
            vertex.position = rightTop;
            __frameVertexIndices.Add(__vertices.Add(vertex));
            vertex.position = rightBottom;
            __frameVertexIndices.Add(__vertices.Add(vertex));

            __frameEdgeIndices = new HashSet<int>();

            __edges = new Pool<Edge>();

            Edge edge;
            edge.count = 1;

            edge.vertexIndexX = 0;
            edge.vertexIndexY = 1;
            __frameEdgeIndices.Add(__edges.Add(edge));

            edge.vertexIndexX = 1;
            edge.vertexIndexY = 2;
            __frameEdgeIndices.Add(__edges.Add(edge));

            edge.vertexIndexX = 2;
            edge.vertexIndexY = 3;
            __frameEdgeIndices.Add(__edges.Add(edge));

            edge.vertexIndexX = 0;
            edge.vertexIndexY = 3;
            __frameEdgeIndices.Add(__edges.Add(edge));

            __MakeTriangle(0, 1, 2);
            __MakeTriangle(0, 2, 3);
        }

        public Delaunay(Rect rect, int pointCount) : this(rect.min, new Vector2(rect.xMin, rect.yMax), rect.max, new Vector2(rect.xMax, rect.yMin))
        {
            for (int i = 0; i < pointCount; ++i)
                AddPoint(new Vector2(Random.Range(rect.xMin, rect.xMax), Random.Range(rect.yMin, rect.yMax)));
        }
        
        public bool Get(int index, out Vector2 point)
        {
            if(__vertices != null)
            {
                Vertex vertex;
                if (__vertices.TryGetValue(index, out vertex))
                {
                    point = vertex.position;

                    return true;
                }
            }

            point = default(Vector2);

            return false;
        }

        public int AddPoint(Vector2 point)
        {
            if (__vertices == null)
                __vertices = new Pool<Vertex>();

            int result = __vertices.Add(new Vertex(point));

            if (__triangles != null)
            {
                bool isIntersect;
                Vector2 x, y, z, u, v;
                Edge edge;
                Triangle triangle;
                HashSet<int> edgeIndices = null;
                foreach (KeyValuePair<int, Triangle> pair in (IEnumerable<KeyValuePair<int, Triangle>>)__triangles)
                {
                    triangle = pair.Value;
                    if (!triangle.circle.Check(point))
                        continue;

                    x = __vertices[triangle.vertexIndexX].position;
                    y = __vertices[triangle.vertexIndexY].position;
                    z = __vertices[triangle.vertexIndexZ].position;

                    if (x == point || y == point || z == point)
                        continue;

                    isIntersect = false;
                    foreach (int frameEdgeIndex in __frameEdgeIndices)
                    {
                        edge = __edges[frameEdgeIndex];

                        u = __vertices[edge.vertexIndexX].position;
                        v = __vertices[edge.vertexIndexY].position;

                        if ((edge.vertexIndexX != triangle.vertexIndexX &&
                            edge.vertexIndexY != triangle.vertexIndexX &&
                            MathUtility.IsIntersect(point, x, u, v)) ||
                            (edge.vertexIndexX != triangle.vertexIndexY &&
                            edge.vertexIndexY != triangle.vertexIndexY &&
                            MathUtility.IsIntersect(point, y, u, v)) ||
                            (edge.vertexIndexX != triangle.vertexIndexZ &&
                            edge.vertexIndexY != triangle.vertexIndexZ &&
                            MathUtility.IsIntersect(point, z, u, v)))
                        {
                            isIntersect = true;

                            break;
                        }
                    }

                    if (isIntersect)
                        continue;
                    
                    __DeleteTriangle(pair.Key, ref edgeIndices);
                }

                if (edgeIndices != null)
                {
                    foreach (int edgeIndex in edgeIndices)
                    {
                        edge = __edges[edgeIndex];

                        x = __vertices[edge.vertexIndexX].position;
                        y = __vertices[edge.vertexIndexY].position;
                        if ((point - x).Cross(y - x) > 0.0f)
                            __MakeTriangle(edge.vertexIndexX, edge.vertexIndexY, result);
                        else
                            __MakeTriangle(edge.vertexIndexX, result, edge.vertexIndexY);
                    }
                }
            }

            return result;
        }

        public void DeleteFrames(System.Predicate<Vector3Int> predicate)
        {
            if (predicate == null)
                return;

            if (__frameEdgeIndices != null)
            {
                Edge edge;
                foreach (int frameEdgeIndex in __frameEdgeIndices)
                {
                    edge = __edges[frameEdgeIndex];
                    --edge.count;
                    __edges[frameEdgeIndex] = edge;
                }
            }

            if (__frameVertexIndices != null)
            {
                HashSet<int> edgeIndices = null;
                Triangle triangle;
                foreach (KeyValuePair<int, Triangle> pair in (IEnumerable<KeyValuePair<int, Triangle>>)__triangles)
                {
                    triangle = pair.Value;
                    if (predicate(new Vector3Int(triangle.vertexIndexX, triangle.vertexIndexY, triangle.vertexIndexZ)))
                        __DeleteTriangle(pair.Key, ref edgeIndices);
                }

                if (edgeIndices != null)
                    __frameEdgeIndices.UnionWith(edgeIndices);
                
                __frameVertexIndices.Clear();
                Edge edge;
                foreach (int frameEdgeIndex in __frameEdgeIndices)
                {
                    edge = __edges[frameEdgeIndex];
                    ++edge.count;
                    __edges[frameEdgeIndex] = edge;

                    __frameVertexIndices.Add(edge.vertexIndexX);
                    __frameVertexIndices.Add(edge.vertexIndexY);
                }
            }
        }
        
        public void DeleteFrames()
        {
            DeleteFrames(x =>
            {
                foreach (int frameVertexIndex in __frameVertexIndices)
                {
                    if (x.x == frameVertexIndex ||
                        x.y == frameVertexIndex ||
                        x.z == frameVertexIndex)
                        return true;
                }

                return false;
            });
        }
        
        public bool ToMesh(System.Func<int, float> heightGetter, out MeshData<int> meshData)
        {
            int count = __vertices.Count, i;
            Vertex vertex;
            List<MeshData<int>.Vertex> vertices = null;
            Dictionary<int, int> indices = null;
            foreach (KeyValuePair<int, Vertex> pair in (IEnumerable<KeyValuePair<int, Vertex>>)__vertices)
            {
                vertex = pair.Value;
                if (vertex.count < 1)
                    continue;

                if (vertices == null)
                    vertices = new List<MeshData<int>.Vertex>();

                if (indices == null)
                    indices = new Dictionary<int, int>();

                i = pair.Key;

                indices[i] = vertices.Count;
                
                vertices.Add(new MeshData<int>.Vertex(new Vector3(
                    vertex.position.x,
                    heightGetter == null ? 0.0f : heightGetter(i),
                    vertex.position.y), i));
            }

            count = __triangles.count;
            count *= 3;

            int indexX, indexY, indexZ;
            MeshData<int>.Vertex vertexX, vertexY, vertexZ;
            List<MeshData<int>.Triangle> triangles = null;
            foreach (Triangle triangle in (IEnumerable<Triangle>)__triangles)
            {
                indexX = indices[triangle.vertexIndexX];
                indexY = indices[triangle.vertexIndexY];
                indexZ = indices[triangle.vertexIndexZ];

                vertexX = vertices[indexX];
                vertexY = vertices[indexY];
                vertexZ = vertices[indexZ];

                if (vertexX.position == vertexY.position || vertexY.position == vertexZ.position || vertexX.position == vertexZ.position)
                    continue;

                if (triangles == null)
                    triangles = new List<MeshData<int>.Triangle>();

                triangles.Add(new MeshData<int>.Triangle(0, new Vector3Int(indices[triangle.vertexIndexX], indices[triangle.vertexIndexY], indices[triangle.vertexIndexZ])));
            }

            meshData = new MeshData<int>(vertices == null ? null : vertices.ToArray(), triangles == null ? null : triangles.ToArray());

            return true;
        }
        
        private int __MakeTriangle(int vertexIndexX, int vertexIndexY, int vertexIndexZ)
        {
            Vertex vertexX = __vertices[vertexIndexX], vertexY = __vertices[vertexIndexY], vertexZ = __vertices[vertexIndexZ];
            //if (vertexX.position == vertexY.position || vertexY.position == vertexZ.position || vertexX.position == vertexZ.position)
            //    return -1;
            
            Triangle triangle;
            triangle.vertexIndexX = vertexIndexX;
            triangle.vertexIndexY = vertexIndexY;
            triangle.vertexIndexZ = vertexIndexZ;

            triangle.edgeIndexX = -1;
            triangle.edgeIndexY = -1;
            triangle.edgeIndexZ = -1;

            triangle.circle = new Circle(vertexX.position, vertexY.position, vertexZ.position);

            if (__edges != null)
            {
                Edge edge;
                foreach (KeyValuePair<int, Edge> pair in (IEnumerable<KeyValuePair<int, Edge>>)__edges)
                {
                    edge = pair.Value;

                    if (edge.count > 0)
                    {
                        if ((edge.vertexIndexX == vertexIndexX && edge.vertexIndexY == vertexIndexY) || (edge.vertexIndexY == vertexIndexX && edge.vertexIndexX == vertexIndexY))
                        {
                            triangle.edgeIndexX = pair.Key;

                            ++edge.count;

                            __edges.Insert(triangle.edgeIndexX, edge);
                        }

                        if ((edge.vertexIndexX == vertexIndexY && edge.vertexIndexY == vertexIndexZ) || (edge.vertexIndexY == vertexIndexY && edge.vertexIndexX == vertexIndexZ))
                        {
                            triangle.edgeIndexY = pair.Key;

                            ++edge.count;

                            __edges.Insert(triangle.edgeIndexY, edge);
                        }

                        if ((edge.vertexIndexX == vertexIndexX && edge.vertexIndexY == vertexIndexZ) || (edge.vertexIndexY == vertexIndexX && edge.vertexIndexX == vertexIndexZ))
                        {
                            triangle.edgeIndexZ = pair.Key;

                            ++edge.count;

                            __edges.Insert(triangle.edgeIndexZ, edge);
                        }
                    }
                    else
                    {
                        if ((edge.vertexIndexX == vertexIndexX && edge.vertexIndexY == vertexIndexY) || (edge.vertexIndexY == vertexIndexX && edge.vertexIndexX == vertexIndexY))
                            triangle.edgeIndexX = pair.Key;

                        if ((edge.vertexIndexX == vertexIndexY && edge.vertexIndexY == vertexIndexZ) || (edge.vertexIndexY == vertexIndexY && edge.vertexIndexX == vertexIndexZ))
                            triangle.edgeIndexY = pair.Key;

                        if ((edge.vertexIndexX == vertexIndexX && edge.vertexIndexY == vertexIndexZ) || (edge.vertexIndexY == vertexIndexX && edge.vertexIndexX == vertexIndexZ))
                            triangle.edgeIndexZ = pair.Key;
                    }

                    if (triangle.edgeIndexX >= 0 && triangle.edgeIndexY >= 0 && triangle.edgeIndexZ >= 0)
                        break;
                }
            }

            if(triangle.edgeIndexX < 0)
            {
                ++vertexX.count;
                __vertices[vertexIndexX] = vertexX;

                ++vertexY.count;
                __vertices[vertexIndexY] = vertexY;

                if (__edges == null)
                    __edges = new Pool<Edge>();

                triangle.edgeIndexX = __edges.Add(new Edge(vertexIndexX, vertexIndexY));
            }

            if (triangle.edgeIndexY < 0)
            {
                ++vertexY.count;
                __vertices[vertexIndexY] = vertexY;

                ++vertexZ.count;
                __vertices[vertexIndexZ] = vertexZ;

                if (__edges == null)
                    __edges = new Pool<Edge>();

                triangle.edgeIndexY = __edges.Add(new Edge(vertexIndexY, vertexIndexZ));
            }

            if (triangle.edgeIndexZ < 0)
            {
                ++vertexX.count;
                __vertices[vertexIndexX] = vertexX;

                ++vertexZ.count;
                __vertices[vertexIndexZ] = vertexZ;

                if (__edges == null)
                    __edges = new Pool<Edge>();

                triangle.edgeIndexZ = __edges.Add(new Edge(vertexIndexX, vertexIndexZ));
            }

            if (__triangles == null)
                __triangles = new Pool<Triangle>();

            return __triangles.Add(triangle);
        }

        private void __DeleteTriangle(int triangleIndex, ref HashSet<int> edgeIndices)
        {
            Triangle triangle = __triangles[triangleIndex];

            Edge edge = __edges[triangle.edgeIndexX];
            
            switch (edge.count)
            {
                case 0:
                    if (edgeIndices == null)
                        edgeIndices = new HashSet<int>();

                    edgeIndices.Add(triangle.edgeIndexX);
                    break;
                case 1:
                    Vertex vertex = __vertices[edge.vertexIndexX];
                    --vertex.count;
                    
                    __vertices[edge.vertexIndexX] = vertex;

                    vertex = __vertices[edge.vertexIndexY];
                    --vertex.count;

                    __vertices[edge.vertexIndexY] = vertex;

                    __edges.RemoveAt(triangle.edgeIndexX);

                    __frameEdgeIndices.Remove(triangle.edgeIndexX);

                    if (edgeIndices != null)
                        edgeIndices.Remove(triangle.edgeIndexX);
                    break;
                default:
                    --edge.count;
                    __edges[triangle.edgeIndexX] = edge;

                    if (edgeIndices == null)
                        edgeIndices = new HashSet<int>();

                    edgeIndices.Add(triangle.edgeIndexX);
                    break;
            }

            edge = __edges[triangle.edgeIndexY];
            switch (edge.count)
            {
                case 0:
                    if (edgeIndices == null)
                        edgeIndices = new HashSet<int>();

                    edgeIndices.Add(triangle.edgeIndexY);
                    break;
                case 1:
                    Vertex vertex = __vertices[edge.vertexIndexX];
                    --vertex.count;
                    
                    __vertices[edge.vertexIndexX] = vertex;

                    vertex = __vertices[edge.vertexIndexY];
                    --vertex.count;
                    
                    __vertices[edge.vertexIndexY] = vertex;

                    __edges.RemoveAt(triangle.edgeIndexY);

                    __frameEdgeIndices.Remove(triangle.edgeIndexY);

                    if (edgeIndices != null)
                        edgeIndices.Remove(triangle.edgeIndexY);
                    break;
                default:
                    --edge.count;
                    __edges[triangle.edgeIndexY] = edge;

                    if (edgeIndices == null)
                        edgeIndices = new HashSet<int>();

                    edgeIndices.Add(triangle.edgeIndexY);
                    break;
            }

            edge = __edges[triangle.edgeIndexZ];
            switch (edge.count)
            {
                case 0:
                    if (edgeIndices == null)
                        edgeIndices = new HashSet<int>();

                    edgeIndices.Add(triangle.edgeIndexZ);
                    break;
                case 1:
                    Vertex vertex = __vertices[edge.vertexIndexX];
                    --vertex.count;
                    
                    __vertices[edge.vertexIndexX] = vertex;

                    vertex = __vertices[edge.vertexIndexY];
                    --vertex.count;

                    __vertices[edge.vertexIndexY] = vertex;

                    __edges.RemoveAt(triangle.edgeIndexZ);

                    __frameEdgeIndices.Remove(triangle.edgeIndexZ);

                    if (edgeIndices != null)
                        edgeIndices.Remove(triangle.edgeIndexZ);
                    break;
                default:
                    --edge.count;
                    __edges[triangle.edgeIndexZ] = edge;

                    if (edgeIndices == null)
                        edgeIndices = new HashSet<int>();

                    edgeIndices.Add(triangle.edgeIndexZ);
                    break;
            }

            __triangles.RemoveAt(triangleIndex);
        }
    }
}