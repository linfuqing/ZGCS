using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZG
{
    public struct MeshData<T>
    {
        public struct Vertex
        {
            public Vector3 position;
            
            public T data;

            public Vertex(Vector3 position, T data)
            {
                this.position = position;
                this.data = data;
            }
        }

        public struct Triangle
        {
            public int subMeshIndex;
            public Vector3Int instance;

            public Triangle(int subMeshIndex, Vector3Int instance)
            {
                this.subMeshIndex = subMeshIndex;
                this.instance = instance;
            }
        }

        public Vertex[] vertices;
        public Triangle[] triangles;

        public static void Split(IList<KeyValuePair<Vector3Int, MeshData<T>>> result, Func<float, T, T, T> lerp, Bounds bounds, Vector3Int segments)
        {
            int count = result == null ? 0 : result.Count;
            if (count < 1)
                throw new InvalidOperationException();

            int i, j;
            Vector3Int position;
            Vector3 center = bounds.center, size = bounds.size;
            Plane plane;
            MeshData<T> x, y;
            KeyValuePair<Vector3Int, MeshData<T>> pair;
            for (i = 1; i < segments.x; ++i)
            {
                plane = new Plane(Vector3.right, -(center.x + size.x * (i * 1.0f / segments.x - 0.5f)));
                for (j = 0; j < count; ++j)
                {
                    pair = result[j];
                    pair.Value.Split(lerp, plane, out x, out y);
                    if (y.isValuable)
                        result[j] = new KeyValuePair<Vector3Int, MeshData<T>>(pair.Key, y);

                    if (x.isValuable)
                    {
                        position = pair.Key;
                        ++position.x;

                        result.Add(new KeyValuePair<Vector3Int, MeshData<T>>(position, x));
                    }
                    else
                        break;
                }
            }

            count = result.Count;
            for (i = 1; i < segments.y; ++i)
            {
                plane = new Plane(Vector3.up, -(center.y + size.y * (i * 1.0f / segments.y - 0.5f)));
                for (j = 0; j < count; ++j)
                {
                    pair = result[j];
                    pair.Value.Split(lerp, plane, out x, out y);

                    if (y.isValuable)
                        result[j] = new KeyValuePair<Vector3Int, MeshData<T>>(pair.Key, y);

                    if (x.isValuable)
                    {
                        position = pair.Key;
                        ++position.y;

                        result.Add(new KeyValuePair<Vector3Int, MeshData<T>>(position, x));
                    }
                    else
                        break;
                }
            }

            count = result.Count;
            for (i = 1; i < segments.z; ++i)
            {
                plane = new Plane(Vector3.forward, -(center.z + size.z * (i * 1.0f / segments.z - 0.5f)));
                for (j = 0; j < count; ++j)
                {
                    pair = result[j];
                    pair.Value.Split(lerp, plane, out x, out y);

                    if (y.isValuable)
                        result[j] = new KeyValuePair<Vector3Int, MeshData<T>>(pair.Key, y);

                    if (x.isValuable)
                    {
                        position = pair.Key;
                        ++position.z;

                        result.Add(new KeyValuePair<Vector3Int, MeshData<T>>(position, x));
                    }
                    else
                        break;
                }
            }
        }

        public bool isValuable
        {
            get
            {
                return vertices != null && vertices.Length > 0 && triangles != null && triangles.Length > 0;
            }
        }

        public MeshData(Vertex[] vertices, Triangle[] triangles)
        {
            this.vertices = vertices;
            this.triangles = triangles;
        }
        
        public void Split(Func<float, T, T, T> lerp, Plane plane, out MeshData<T> x, out MeshData<T> y)
        {
            if (lerp == null)
                throw new InvalidOperationException();

            //plane = plane.flipped;

            List<Vertex> verticesX = null, verticesY = null;
            List<Triangle> triangleX = null, triangleY = null;
            
            int vertexCount = vertices == null ? 0 : vertices.Length;
            
            bool[] above = new bool[vertexCount];
            int[] indices = new int[vertexCount];

            Vertex vertex;
            for (int i = 0; i < vertexCount; ++i)
            {
                vertex = vertices[i];
                if (above[i] = plane.GetSide(vertex.position))
                {
                    if (verticesX == null)
                        verticesX = new List<Vertex>();

                    indices[i] = verticesX.Count;

                    verticesX.Add(vertex);
                }
                else
                {
                    if (verticesY == null)
                        verticesY = new List<Vertex>();

                    indices[i] = verticesY.Count;

                    verticesY.Add(vertex);
                }
            }
            
            int triangleCount = triangles == null ? 0 : triangles.Length, index, indexXU, indexYU, indexXV, indexYV;
            bool aboveX, aboveY, aboveZ, result;
            float enter, invMagnitude;
            Vertex vertexX, vertexY, vertexZ;
            Vector3 position;
            Triangle triangle;
            for (int i = 0; i < triangleCount; ++i)
            {
                triangle = triangles[i];

                aboveX = above[triangle.instance.x];
                aboveY = above[triangle.instance.y];
                aboveZ = above[triangle.instance.z];

                if (aboveX && aboveY && aboveZ)
                {
                    if (triangleX == null)
                        triangleX = new List<Triangle>();

                    triangleX.Add(new Triangle(triangle.subMeshIndex, new Vector3Int(indices[triangle.instance.x], indices[triangle.instance.y], indices[triangle.instance.z])));
                }
                else if (!aboveX && !aboveY && !aboveZ)
                {
                    if (triangleY == null)
                        triangleY = new List<Triangle>();

                    triangleY.Add(new Triangle(triangle.subMeshIndex, new Vector3Int(indices[triangle.instance.x], indices[triangle.instance.y], indices[triangle.instance.z])));
                }
                else
                {
                    if (aboveX == aboveY)
                    {
                        result = aboveZ;

                        triangle.instance = new Vector3Int(triangle.instance.x, triangle.instance.y, triangle.instance.z);
                    }
                    else if (aboveY == aboveZ)
                    {
                        result = aboveX;

                        triangle.instance = new Vector3Int(triangle.instance.y, triangle.instance.z, triangle.instance.x);
                    }
                    else
                    {
                        result = aboveY;

                        triangle.instance = new Vector3Int(triangle.instance.z, triangle.instance.x, triangle.instance.y);
                    }
                    
                    vertexX = vertices[triangle.instance.x];
                    vertexY = vertices[triangle.instance.y];
                    vertexZ = vertices[triangle.instance.z];

                    indexXU = indexXV = indexYU = indexYV = -1;

                    position = vertexX.position - vertexZ.position;
                    invMagnitude = position.magnitude;
                    invMagnitude = 1.0f / invMagnitude;
                    position *= invMagnitude;
                    plane.Raycast(new Ray(vertexZ.position, position), out enter);

                    position *= enter;
                    position += vertexZ.position;

                    enter *= invMagnitude;
                    
                    if (verticesX == null)
                        verticesX = new List<Vertex>();

                    indexXU = verticesX.Count;

                    verticesX.Add(new Vertex(position, lerp(enter, vertexZ.data, vertexX.data)));

                    if (verticesY == null)
                        verticesY = new List<Vertex>();

                    indexYU = verticesY.Count;

                    verticesY.Add(new Vertex(position, lerp(enter, vertexZ.data, vertexX.data)));

                    position = vertexY.position - vertexZ.position;
                    invMagnitude = position.magnitude;
                    invMagnitude = 1.0f / invMagnitude;
                    position *= invMagnitude;
                    plane.Raycast(new Ray(vertexZ.position, position), out enter);

                    position *= enter;
                    position += vertexZ.position;

                    enter *= invMagnitude;

                    if (verticesX == null)
                        verticesX = new List<Vertex>();

                    indexXV = verticesX.Count;

                    verticesX.Add(new Vertex(position, lerp(enter, vertexZ.data, vertexY.data)));

                    if (verticesY == null)
                        verticesY = new List<Vertex>();

                    indexYV = verticesY.Count;

                    verticesY.Add(new Vertex(position, lerp(enter, vertexZ.data, vertexY.data)));

                    if (result)
                    {
                        if (triangleX == null)
                            triangleX = new List<Triangle>();

                        triangleX.Add(new Triangle(triangle.subMeshIndex, new Vector3Int(indices[triangle.instance.z], indexXU, indexXV)));

                        if (triangleY == null)
                            triangleY = new List<Triangle>();

                        index = indices[triangle.instance.y];
                        triangleY.Add(new Triangle(triangle.subMeshIndex, new Vector3Int(indices[triangle.instance.x], index, indexYU)));
                        triangleY.Add(new Triangle(triangle.subMeshIndex, new Vector3Int(index, indexYV, indexYU)));
                    }
                    else
                    {
                        if (triangleY == null)
                            triangleY = new List<Triangle>();

                        triangleY.Add(new Triangle(triangle.subMeshIndex, new Vector3Int(indices[triangle.instance.z], indexYU, indexYV)));

                        if (triangleX == null)
                            triangleX = new List<Triangle>();

                        index = indices[triangle.instance.y];
                        triangleX.Add(new Triangle(triangle.subMeshIndex, new Vector3Int(indices[triangle.instance.x], index, indexXU)));
                        triangleX.Add(new Triangle(triangle.subMeshIndex, new Vector3Int(index, indexXV, indexXU)));
                    }
                }
            }
            
            if (verticesX != null && triangleX != null)
            {
                x = new MeshData<T>();
                x.vertices = verticesX.ToArray();
                x.triangles = triangleX.ToArray();
            }
            else
                x = default(MeshData<T>);

            if (verticesY != null && triangleY != null)
            {
                y = new MeshData<T>();
                y.vertices = verticesY.ToArray();
                y.triangles = triangleY.ToArray();
            }
            else
                y = default(MeshData<T>);
        }

        public Mesh ToMesh(Mesh mesh, ref Dictionary<int, int> subMeshIndices)
        {
            int numVertices = vertices == null ? 0 : vertices.Length;
            if (numVertices < 1)
                return null;

            int numTriangles = triangles == null ? 0 : triangles.Length;
            if (numTriangles < 1)
                return null;

            int i;
            MeshData<T>.Vertex vertex;
            Vector3[] positions = new Vector3[numVertices];
            for (i = 0; i < numVertices; ++i)
            {
                vertex = vertices[i];
                positions[i] = vertex.position;
            }

            if (subMeshIndices != null)
                subMeshIndices.Clear();

            int index;
            MeshData<T>.Triangle triangle;
            List<int> indices;
            List<List<int>> indexMap = null;
            for (i = 0; i < numTriangles; ++i)
            {
                triangle = triangles[i];

                if (subMeshIndices == null)
                    subMeshIndices = new Dictionary<int, int>();

                if (subMeshIndices.TryGetValue(triangle.subMeshIndex, out index))
                    indices = indexMap[index];
                else
                {
                    if (indexMap == null)
                        indexMap = new List<List<int>>();

                    subMeshIndices[triangle.subMeshIndex] = indexMap.Count;

                    indices = new List<int>();

                    indexMap.Add(indices);
                }

                indices.Add(triangle.instance.x);
                indices.Add(triangle.instance.y);
                indices.Add(triangle.instance.z);
            }

            int numSubMeshes = indexMap == null ? 0 : indexMap.Count;
            if (numSubMeshes < 1)
                return null;

            if (mesh == null)
                mesh = new Mesh();
            else
                mesh.Clear();

            mesh.vertices = positions;

            mesh.subMeshCount = numSubMeshes;
            for (i = 0; i < numSubMeshes; ++i)
                mesh.SetTriangles(indexMap[i].ToArray(), i);

            return mesh;
        }

        public Mesh ToFlatMesh(Mesh mesh, ref Dictionary<int, int> subMeshIndices)
        {
            int numVertices = vertices == null ? 0 : vertices.Length;
            if (numVertices < 1)
                return null;

            int numTriangles = triangles == null ? 0 : triangles.Length;
            if (numTriangles < 1)
                return null;
            
            if (subMeshIndices != null)
                subMeshIndices.Clear();

            int i, temp, index = 0;
            Triangle triangle;
            Vector3[] positions = new Vector3[numTriangles * 3];
            List<int> indices;
            List<List<int>> indexMap = null;
            for (i = 0; i < numTriangles; ++i)
            {
                triangle = triangles[i];

                if (subMeshIndices == null)
                    subMeshIndices = new Dictionary<int, int>();

                if (subMeshIndices.TryGetValue(triangle.subMeshIndex, out temp))
                    indices = indexMap[temp];
                else
                {
                    if (indexMap == null)
                        indexMap = new List<List<int>>();

                    subMeshIndices[triangle.subMeshIndex] = indexMap.Count;

                    indices = new List<int>();

                    indexMap.Add(indices);
                }

                positions[index] = vertices[triangle.instance.x].position;
                indices.Add(index++);
                positions[index] = vertices[triangle.instance.y].position;
                indices.Add(index++);
                positions[index] = vertices[triangle.instance.z].position;
                indices.Add(index++);
            }

            int numSubMeshes = indexMap == null ? 0 : indexMap.Count;
            if (numSubMeshes < 1)
                return null;

            if (mesh == null)
                mesh = new Mesh();
            else
                mesh.Clear();

            mesh.vertices = positions;

            mesh.subMeshCount = numSubMeshes;
            for (i = 0; i < numSubMeshes; ++i)
                mesh.SetTriangles(indexMap[i].ToArray(), i);

            mesh.RecalculateNormals();

            return mesh;
        }
    }

    public static class MeshData
    {
        private struct CollapseEdge
        {
            public int index;

            public float error;

            public CollapseEdge(int index, float error)
            {
                this.index = index;
                this.error = error;
            }
        }

        public static int Comparsion(Vector2Int x, Vector2Int y)
        {
            return x.y == y.y ? x.x - y.x : x.y - y.y;
        }

        public static Vector3 Lerp(float t, Vector3 x, Vector3 y)
        {
            return (y - x) * t + x;
        }

        public static void Split(this IList<KeyValuePair<Vector3Int, MeshData<Vector3>>> result, Bounds bounds, Vector3Int segments)
        {
            MeshData<Vector3>.Split(result, Lerp, bounds, segments);
        }
        
        public static Mesh ToMesh(this MeshData<Vector3> data, Mesh mesh, ref Dictionary<int, int> subMeshIndices)
        {
            int numVertices = data.vertices == null ? 0 : data.vertices.Length;
            if (numVertices < 1)
                return null;

            int numTriangles = data.triangles == null ? 0 : data.triangles.Length;
            if (numTriangles < 1)
                return null;

            int i;
            MeshData<Vector3>.Vertex vertex;
            Vector3[] positions = new Vector3[numVertices];
            Vector3[] normals = new Vector3[numVertices];
            for (i = 0; i < numVertices; ++i)
            {
                vertex = data.vertices[i];
                positions[i] = vertex.position;
                normals[i] = vertex.data;
            }

            if (subMeshIndices != null)
                subMeshIndices.Clear();

            int index;
            MeshData<Vector3>.Triangle triangle;
            List<int> indices;
            List<List<int>> indexMap = null;
            for (i = 0; i < numTriangles; ++i)
            {
                triangle = data.triangles[i];

                if (subMeshIndices == null)
                    subMeshIndices = new Dictionary<int, int>();

                if (subMeshIndices.TryGetValue(triangle.subMeshIndex, out index))
                    indices = indexMap[index];
                else
                {
                    if (indexMap == null)
                        indexMap = new List<List<int>>();

                    subMeshIndices[triangle.subMeshIndex] = indexMap.Count;

                    indices = new List<int>();

                    indexMap.Add(indices);
                }

                indices.Add(triangle.instance.x);
                indices.Add(triangle.instance.y);
                indices.Add(triangle.instance.z);
            }

            int numSubMeshes = indexMap == null ? 0 : indexMap.Count;
            if (numSubMeshes < 1)
                return null;

            if (mesh == null)
                mesh = new Mesh();
            else
                mesh.Clear();

            mesh.vertices = positions;
            mesh.normals = normals;

            mesh.subMeshCount = numSubMeshes;
            for (i = 0; i < numSubMeshes; ++i)
                mesh.SetTriangles(indexMap[i].ToArray(), i);

            return mesh;
        }

        public static MeshData<Vector3> Simplify(
            this MeshData<Vector3> meshData, 
            int sweeps,
            int minCollapseDegree,
            int maxCollapseDegree,
            int maxIterations,
            float targetPecentage,
            float edgeFraction,
            float minAngleCosine,
            float maxEdgeSize,
            float maxError)
        {
            int numTriangles = meshData.triangles == null ? 0 : meshData.triangles.Length;
            if (numTriangles < 1)
                return meshData;

            int i, numEdges = numTriangles * 3;
            Vector2Int[] edges = new Vector2Int[numEdges];
            MeshData<Vector3>.Triangle triangle;
            for (i = 0; i < numTriangles; ++i)
            {
                triangle = meshData.triangles[i];

                edges[i * 3 + 0] = new Vector2Int(Mathf.Min(triangle.instance.x, triangle.instance.y), Mathf.Max(triangle.instance.x, triangle.instance.y));
                edges[i * 3 + 1] = new Vector2Int(Mathf.Min(triangle.instance.y, triangle.instance.z), Mathf.Max(triangle.instance.y, triangle.instance.z));
                edges[i * 3 + 2] = new Vector2Int(Mathf.Min(triangle.instance.x, triangle.instance.z), Mathf.Max(triangle.instance.x, triangle.instance.z));
            }

            Array.Sort(edges, Comparsion);

            int count = 1;
            Vector2Int previous = edges[0], current;
            HashSet<int> boundaryIndices = null;
            List<Vector2Int> edgeBuffer = null;
            for (i = 1; i < numEdges; ++i)
            {
                current = edges[i];
                if (current == previous)
                    ++count;
                else if (count == 1)
                {
                    if (boundaryIndices == null)
                        boundaryIndices = new HashSet<int>();

                    boundaryIndices.Add(previous.x);
                    boundaryIndices.Add(previous.y);
                }
                else
                {
                    if (edgeBuffer == null)
                        edgeBuffer = new List<Vector2Int>();

                    edgeBuffer.Add(previous);

                    count = 1;
                }

                previous = current;
            }

            if (edgeBuffer == null)
                return meshData;

            numEdges = 0;
            foreach (Vector2Int temp in edgeBuffer)
            {
                if (boundaryIndices == null || (!boundaryIndices.Contains(temp.x) && !boundaryIndices.Contains(temp.y)))
                    edges[numEdges++] = temp;
            }

            int numVertices = meshData.vertices == null ? 0 : meshData.vertices.Length;
            int[] vertexTriangleCounts = new int[numVertices];
            for (i = 0; i < numTriangles; ++i)
            {
                triangle = meshData.triangles[i];

                ++vertexTriangleCounts[triangle.instance.x];
                ++vertexTriangleCounts[triangle.instance.y];
                ++vertexTriangleCounts[triangle.instance.z];
            }

            int targetTriangleCount = Mathf.FloorToInt(numTriangles * targetPecentage),
                iterations = 0,
                numRandomEdges,
                index,
                degree,
                j;
            float error;
            Vector2Int edge;
            Vector3 point, min, max;
            MeshData<Vector3>.Vertex x, y, collapseVertex;
            Qef qef;
            System.Random random = null;
            MeshData<Vector3>.Vertex[] vertices = null;
            MeshData<Vector3>.Triangle[] triangles = null;
            List<MeshData<Vector3>.Triangle> triangleBuffer = null;
            MeshData<Vector3>.Vertex[] collapseVertices = null;
            CollapseEdge[] collapseEdges = null;
            int[] collapseTargets = null;
            List<int> collapseValidEdgeIndices = null;
            while (numTriangles > targetTriangleCount && iterations++ < maxIterations)
            {
                numRandomEdges = Mathf.FloorToInt(numEdges * edgeFraction);
                if (numRandomEdges > 0)
                {
                    for (i = 1; i < numRandomEdges; ++i)
                    {
                        if (random == null)
                            random = new System.Random();

                        index = random.Next(i, numEdges);
                        edge = edges[index];
                        edges[index] = edges[i - 1];
                        edges[i - 1] = edge;
                    }

                    if (collapseValidEdgeIndices != null)
                        collapseValidEdgeIndices.Clear();

                    if (collapseEdges != null)
                    {
                        for (i = 0; i < numVertices; ++i)
                            collapseEdges[i] = new CollapseEdge(-1, float.MaxValue);
                    }

                    for (i = 0; i < numRandomEdges; ++i)
                    {
                        edge = edges[i];

                        if (vertices == null)
                            vertices = meshData.vertices.Clone() as MeshData<Vector3>.Vertex[];

                        x = vertices[edge.x];
                        y = vertices[edge.y];

                        if (Vector3.Dot(x.data, y.data) < minAngleCosine)
                            continue;

                        if (Vector3.Distance(x.position, y.position) > maxEdgeSize)
                            continue;

                        degree = vertexTriangleCounts[edge.x] + vertexTriangleCounts[edge.y];
                        if (degree > maxCollapseDegree)
                            continue;

                        qef = new Qef();
                        qef.Add(new Qef.Data(x.position, x.data));
                        qef.Add(new Qef.Data(y.position, y.data));

                        point = qef.Solve(sweeps);

                        error = qef.GetError(point);
                        if (error > 0.0f)
                            error = 1.0f / error;

                        error += Mathf.Max(0, degree - minCollapseDegree) * maxError / minCollapseDegree;
                        if (error > maxError)
                            continue;

                        if (collapseValidEdgeIndices == null)
                            collapseValidEdgeIndices = new List<int>();

                        collapseValidEdgeIndices.Add(i);
                        
                        min = Vector3.Min(x.position, y.position);
                        max = Vector3.Max(x.position, y.position);

                        if (collapseVertices == null)
                            collapseVertices = new MeshData<Vector3>.Vertex[numRandomEdges];

                        collapseVertices[i] = new MeshData<Vector3>.Vertex(
                            point.x < min.x || point.y < min.y || point.z < min.z || point.x > max.x || point.y > max.y || point.z > max.z ?
                            qef.massPoint : point,
                            (x.data + y.data).normalized);

                        if (collapseEdges == null)
                        {
                            collapseEdges = new CollapseEdge[numVertices];
                            for (j = 0; j < numVertices; ++j)
                                collapseEdges[j] = new CollapseEdge(-1, float.MaxValue);
                        }

                        if (error < collapseEdges[edge.x].error)
                            collapseEdges[edge.x] = new CollapseEdge(i, error);

                        if (error < collapseEdges[edge.y].error)
                            collapseEdges[edge.y] = new CollapseEdge(i, error);
                    }
                }

                if (collapseValidEdgeIndices == null || collapseValidEdgeIndices.Count < 1)
                    break;

                if (collapseTargets != null)
                {
                    for (i = 0; i < numVertices; ++i)
                        collapseTargets[i] = -1;
                }

                foreach (int collapseValidEdgeIndex in collapseValidEdgeIndices)
                {
                    edge = edges[collapseValidEdgeIndex];
                    if (collapseEdges[edge.x].index == collapseValidEdgeIndex && collapseEdges[edge.y].index == collapseValidEdgeIndex)
                    {
                        if (collapseTargets == null)
                        {
                            collapseTargets = new int[numVertices];

                            for (i = 0; i < numVertices; ++i)
                                collapseTargets[i] = -1;
                        }

                        collapseTargets[edge.y] = edge.x;

                        collapseVertex = collapseVertices[collapseValidEdgeIndex];

                        vertices[edge.x] = new MeshData<Vector3>.Vertex(collapseVertex.position, collapseVertex.data);
                    }
                }

                if (collapseTargets == null)
                    break;

                if (triangles == null)
                {
                    triangles = new MeshData<Vector3>.Triangle[numTriangles];
                    Array.Copy(meshData.triangles, triangles, numTriangles);
                }

                if (triangleBuffer != null)
                    triangleBuffer.Clear();

                Array.Clear(vertexTriangleCounts, 0, numVertices);
                for (i = 0; i < numTriangles; ++i)
                {
                    triangle = triangles[i];

                    index = collapseTargets[triangle.instance.x];
                    if (index >= 0 && index < numVertices)
                        triangle.instance.x = index;

                    index = collapseTargets[triangle.instance.y];
                    if (index >= 0 && index < numVertices)
                        triangle.instance.y = index;

                    index = collapseTargets[triangle.instance.z];
                    if (index >= 0 && index < numVertices)
                        triangle.instance.z = index;

                    if (triangle.instance.x == triangle.instance.y || triangle.instance.x == triangle.instance.z || triangle.instance.y == triangle.instance.z)
                        continue;

                    vertexTriangleCounts[triangle.instance.x] += 1;
                    vertexTriangleCounts[triangle.instance.y] += 1;
                    vertexTriangleCounts[triangle.instance.z] += 1;

                    if (triangleBuffer == null)
                        triangleBuffer = new List<MeshData<Vector3>.Triangle>();

                    triangleBuffer.Add(triangle);
                }

                numTriangles = triangleBuffer == null ? 0 : triangleBuffer.Count;
                if (numTriangles > 0)
                    triangleBuffer.CopyTo(triangles);

                edgeBuffer.Clear();
                for (i = 0; i < numEdges; ++i)
                {
                    edge = edges[i];

                    index = collapseTargets[edge.x];
                    if (index >= 0 && index < numVertices)
                        edge.x = index;

                    index = collapseTargets[edge.y];
                    if (index >= 0 && index < numVertices)
                        edge.y = index;

                    if (edge.x == edge.y)
                        continue;

                    edgeBuffer.Add(edge);
                }

                numEdges = edgeBuffer.Count;
                if (numEdges > 0)
                    edgeBuffer.CopyTo(edges);
            }
            
            if (triangleBuffer != null)
                triangleBuffer.Clear();

            List<MeshData<Vector3>.Vertex> vertexBuffer = null;
            Dictionary<int, int> indices = null;
            for (i = 0; i < numTriangles; ++i)
            {
                if (triangles == null)
                    triangles = meshData.triangles;

                triangle = triangles[i];

                if (indices == null)
                    indices = new Dictionary<int, int>();

                if (!indices.TryGetValue(triangle.instance.x, out index))
                {
                    if (vertexBuffer == null)
                        vertexBuffer = new List<MeshData<Vector3>.Vertex>();

                    index = vertexBuffer.Count;

                    if (vertices == null)
                        vertices = meshData.vertices;

                    vertexBuffer.Add(vertices[triangle.instance.x]);

                    indices[triangle.instance.x] = index;
                }

                triangle.instance.x = index;

                if (!indices.TryGetValue(triangle.instance.y, out index))
                {
                    if (vertexBuffer == null)
                        vertexBuffer = new List<MeshData<Vector3>.Vertex>();

                    index = vertexBuffer.Count;

                    vertexBuffer.Add(vertices[triangle.instance.y]);

                    indices[triangle.instance.y] = index;
                }

                triangle.instance.y = index;

                if (!indices.TryGetValue(triangle.instance.z, out index))
                {
                    if (vertexBuffer == null)
                        vertexBuffer = new List<MeshData<Vector3>.Vertex>();

                    index = vertexBuffer.Count;

                    vertexBuffer.Add(vertices[triangle.instance.z]);

                    indices[triangle.instance.z] = index;
                }

                triangle.instance.z = index;

                if (triangleBuffer == null)
                    triangleBuffer = new List<MeshData<Vector3>.Triangle>();

                triangleBuffer.Add(triangle);
            }

            return new MeshData<Vector3>(vertexBuffer == null ? null : vertexBuffer.ToArray(), triangleBuffer == null ? null : triangleBuffer.ToArray());
        }
    }
}