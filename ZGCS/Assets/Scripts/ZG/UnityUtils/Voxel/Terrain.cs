﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZG.Voxel
{
    public struct Node
    {
        public int index;
        public GameObject gameObject;

        public Node(int index, GameObject gameObject)
        {
            this.index = index;
            this.gameObject = gameObject;
        }
    }

    [Serializable]
    public struct MaterialFilter
    {
        [Index("materialInfos", relativePropertyPath = "index", pathLevel = 2, uniqueLevel = 1)]
        public string name;

        [Index("materialInfos", pathLevel = 2, uniqueLevel = 1)]
        public int index;

        public float chanceOffset;
        
        public LayerFilter[] layerFilters;
    }

    [Serializable]
    public struct MapFilter
    {
        public bool isInverce;

        [Index("mapInfos", pathLevel = 2, uniqueLevel = 1)]
        public int index;
        public float min;
        public float max;
        public float center;
        public float scale;
        public float offset;
    }

    [Serializable]
    public struct MapInfo
    {
#if UNITY_EDITOR
        public string name;
#endif

        public int octaveCount;
        public float frequency;
        public float persistence;

        public Vector2 offset;
        public Vector2 scale;

        public float Get(float x, float y)
        {
            x *= scale.x;
            y *= scale.y;

            x += offset.x;
            y += offset.y;

            if (octaveCount > 0)
            {
                float result = 0.0f, amplitude = 1.0f, frequency = 1.0f;
                for (int i = 0; i < octaveCount; ++i)
                {
                    if (i > 0)
                    {
                        frequency *= this.frequency;
                        amplitude *= persistence;
                    }

                    result += Unity.Mathematics.noise.cnoise(new Unity.Mathematics.float2(x * frequency, y * frequency)) * amplitude;
                }

                return ((result / octaveCount) + 1.0f) * 0.5f;
            }

            return Unity.Mathematics.noise.srnoise(new Unity.Mathematics.float2(x, y)) * 0.5f + 0.5f;
        }
    }

    [Serializable]
    public struct VolumeInfo
    {
#if UNITY_EDITOR
        public string name;
#endif

        public Vector3 offset;
        public Vector3 scale;

        public float Get(float x, float y, float z)
        {
            return Unity.Mathematics.noise.cnoise(new Unity.Mathematics.float3(x * scale.x + offset.x, y * scale.y + offset.y, z * scale.z + offset.z));// noise == null ? 0.0f : noise.Get(x * scale.x + offset.x, y * scale.y + offset.y, z * scale.z + offset.z);
        }
    }

    [Serializable]
    public struct LayerFilter
    {
#if UNITY_EDITOR
        [Index("layerInfos", relativePropertyPath = "index", pathLevel = 3)]
        public string name;
#endif

        [Index("layerInfos", pathLevel = 3, uniqueLevel = 1)]
        public int index;
        [Range(0.0f, 1.0f)]
        public float min;
        [Range(0.0f, 1.0f)]
        public float max;
        public float offset;
    }

    [Serializable]
    public struct MaterialInfo
    {
#if UNITY_EDITOR
        public string name;
#endif
        [Index("materials", pathLevel = 1)]
        [UnityEngine.Serialization.FormerlySerializedAs("materialIndex")]
        public int index;
    }

    [Serializable]
    public struct LayerInfo
    {
#if UNITY_EDITOR
        public string name;

        [Index("materialInfos", relativePropertyPath = "materialIndex", pathLevel = 1)]
        public string materialName;
#endif
        [Index("materialInfos", pathLevel = 1)]
        public int materialIndex;

        [Index("volumeInfos", emptyName = "无", pathLevel = 1)]
        public int volumeIndex;

#if UNITY_EDITOR
        [Index("layerInfos", emptyName = "无", relativePropertyPath = "layerIndex", pathLevel = 1)]
        public string layerName;
#endif

        [Index("layerInfos", emptyName = "无", pathLevel = 1)]
        [UnityEngine.Serialization.FormerlySerializedAs("layer")]
        public int layerIndex;

        public int depth;

        public float power;
        public float scale;
        public float offset;

        public float max;

        public MapFilter[] filters;
    }

    [Serializable]
    public struct ObjectInfo
    {
        public enum Rotation
        {
            None,
            AixY,
            NormalUp,
            NormalAixY,
            All
        }
        
#if UNITY_EDITOR
        public string name;

        public string guid;
#endif
        [Index("gameObjects", relativePropertyPath = "index", pathLevel = 1)]
        public string objectName;

        [Index("gameObjects", pathLevel = 1)]
        public int index;
        
        public LayerMask ignoreMask;
        public LayerMask collideLayers;
        public LayerMask raycastLayers;

        public Rotation rotation;

        public float chance;

        public float dot;

        public float offset;

        [UnityEngine.Serialization.FormerlySerializedAs("extent")]
        public float top;

        [UnityEngine.Serialization.FormerlySerializedAs("height")]
        public float bottom;

        public float distance;

        public float minScale;
        public float maxScale;

        public Vector3 normal;

        public MaterialFilter[] materialFilters;

        public MapFilter[] mapFilters;
    }

    [Serializable]
    public struct LineInfo
    {
#if UNITY_EDITOR
        public string name;
#endif
        public float halfWidth;

        public float radius;

        public float countPerUnit;

        public float chance;

        public float minDentity;
        public float maxDentity;

        public Vector3Int minExtends;
        public Vector3Int maxExtends;

        public GameObject gameObject;

        public MapFilter[] mapFilters;
    }

    [Serializable]
    public struct DrawInfo
    {
#if UNITY_EDITOR
        public string name;
#endif
        public float countPerUnit;

        public GameObject gameObject;

        public MapFilter[] mapFilters;
    }

    //[Serializable]
    public abstract class FlatTerrain<T, U> : Processor<T, U>, IEnumerable<Node>
        where T : IEngine
        where U : IEngineProcessor<T>, new()
    {
        private struct Comparer : IComparer<Vector3>
        {
            public Vector3 forward;

            public int Compare(Vector3 x, Vector3 y)
            {
                return Vector3.Dot(x, forward).CompareTo(Vector3.Dot(y, forward));
            }
        }

        private class Enumerator : Object, IEnumerator<Node>
        {
            private List<Node>.Enumerator? __leaf;
            private Dictionary<NodeIndex, List<Node>>.Enumerator __root;

            public static Enumerator Create(Dictionary<NodeIndex, List<Node>>.Enumerator enumerator)
            {
                Enumerator result = Create<Enumerator>();
                result.__leaf = null;
                result.__root = enumerator;
                return result;
            }

            public Node Current => __leaf.Value.Current;

            public bool MoveNext()
            {
                do
                {
                    if (__leaf != null)
                    {
                        List<Node>.Enumerator leaf = __leaf.Value;
                        bool result = leaf.MoveNext();
                        __leaf = leaf;

                        if (result)
                            return true;
                    }

                    if (!__root.MoveNext())
                        return false;

                    __leaf = __root.Current.Value?.GetEnumerator();
                } while (true);
            }

            void IEnumerator.Reset()
            {
                throw new InvalidOperationException();
            }

            object IEnumerator.Current => Current;
        }

        private struct NodeIndex : IEquatable<NodeIndex>
        {
            public Axis axis;
            public Vector3Int position;

            public bool Equals(NodeIndex other)
            {
                return axis == other.axis && position.Equals(other);
            }

            public override int GetHashCode()
            {
                return position.GetHashCode();
            }
        }

        private struct ObjectIndex
        {
            public int value;
            public float chance;
        }

        private class Sampler : ISampler
        {
            public struct Chunk
            {
                public int index;
                public int count;

                public float height;
            }

            public class Drawer
            {
                private Vector2 __scale;
                private System.Random __random;
                private MapInfo[] __mapInfos;
                private DrawInfo[] __drawInfos;
                private HashSet<Vector2Int>[] __drawers;
                private List<Vector3Int> __triangles;

                public Drawer(System.Random random)
                {
                    __random = random;
                }

                public void Create(Vector2 scale, DrawInfo[] drawInfos, MapInfo[] mapInfos)
                {
                    int numDrawInfos = drawInfos == null ? 0 : drawInfos.Length;
                    if (__drawers == null || __drawers.Length < numDrawInfos)
                        Array.Resize(ref __drawers, numDrawInfos);

                    HashSet<Vector2Int> drawer;
                    for (int i = 0; i < numDrawInfos; ++i)
                    {
                        drawer = __drawers[i];
                        if (drawer != null)
                            drawer.Clear();
                    }

                    __scale = scale;

                    __drawInfos = drawInfos;
                    __mapInfos = mapInfos;
                }

                public void Set(int index, Vector2Int point)
                {
                    int numDrawInfos = __drawInfos == null ? 0 : __drawInfos.Length;
                    float result;
                    HashSet<Vector2Int> drawer;
                    for (int i = 0; i < numDrawInfos; ++i)
                    {
                        if (__Get(point.x * __scale.x, point.y * __scale.y, __mapInfos, __drawInfos[i].mapFilters, out result))
                        {
                            drawer = __drawers[i];
                            if (drawer == null)
                            {
                                drawer = new HashSet<Vector2Int>();
                                __drawers[i] = drawer;
                            }

                            drawer.Add(point);
                        }
                    }
                }

                public void Do(Predicate<Instance> instantiate)
                {
                    if (instantiate == null)
                        return;

                    bool result;
                    int count = Mathf.Min(__drawInfos == null ? 0 : __drawInfos.Length, __drawers == null ? 0 : __drawers.Length), numPoints, i, j, k;
                    Vector2 point, min, max, size, x, y, z, temp;
                    Delaunay delaunay;
                    HashSet<Vector2Int> drawer;
                    Func<int, float> heightGetter;
                    for (i = 0; i < count; ++i)
                    {
                        drawer = __drawers[i];
                        numPoints = drawer == null ? 0 : drawer.Count;
                        if (numPoints < 1)
                            continue;

                        min = new Vector2(float.MaxValue, float.MaxValue);
                        max = new Vector2(float.MinValue, float.MinValue);
                        foreach (Vector2Int pointToDraw in drawer)
                        {
                            point = Vector2.Scale(pointToDraw, __scale);

                            min = Vector2.Min(min, point);
                            max = Vector2.Max(max, point);
                        }

                        min -= __scale;
                        max += __scale;

                        size = max - min;
                        delaunay = new Delaunay(new Rect(min, size), 0);
                        foreach (Vector2Int pointToDraw in drawer)
                        {
                            result = false;
                            for (j = -1; j < 2; ++j)
                            {
                                for (k = -1; k < 2; ++k)
                                {
                                    if (j == 0 && k == 0)
                                        continue;

                                    if (!drawer.Contains(pointToDraw + new Vector2Int(j, k)))
                                    {
                                        result = true;

                                        break;
                                    }
                                }
                            }

                            if (!result)
                                continue;

                            delaunay.AddPoint(Vector2.Scale(pointToDraw, __scale));
                        }

                        if (__triangles != null)
                            __triangles.Clear();

                        DrawInfo drawInfo = __drawInfos[i];

                        delaunay.DeleteFrames(triangle =>
                        {
                            if (triangle.x < 4 || triangle.y < 4 || triangle.z < 4)
                                return true;

                            if (!delaunay.Get(triangle.x, out x))
                                return true;

                            if (!delaunay.Get(triangle.y, out y))
                                return true;

                            if (!delaunay.Get(triangle.z, out z))
                                return true;

                            float value;

                            point = (x + y) * 0.5f;
                            if (!__Get(point.x, point.y, __mapInfos, drawInfo.mapFilters, out value))
                                return true;

                            point = (y + z) * 0.5f;
                            if (!__Get(point.x, point.y, __mapInfos, drawInfo.mapFilters, out value))
                                return true;

                            point = (z + x) * 0.5f;
                            if (!__Get(point.x, point.y, __mapInfos, drawInfo.mapFilters, out value))
                                return true;

                            if (__triangles == null)
                                __triangles = new List<Vector3Int>();

                            __triangles.Add(triangle);

                            return false;
                        });

                        heightGetter = pointIndex =>
                        {
                            if (!delaunay.Get(pointIndex, out point))
                                return 0.0f;

                            float value;
                            if (!__Get(point.x, point.y, __mapInfos, drawInfo.mapFilters, out value))
                                return 0.0f;

                            return value;
                        };

                        MeshData<int> meshForCollide;
                        result = delaunay.ToMesh(heightGetter, out meshForCollide);

                        if (__triangles != null)
                        {
                            lock (__random)
                            {
                                numPoints = Mathf.RoundToInt((size.x * size.y) * drawInfo.countPerUnit);
                                for (j = 0; j < numPoints; ++j)
                                {
                                    temp = new Vector2((float)(__random.NextDouble() * size.x + min.x), (float)(__random.NextDouble() * size.y + min.y));
                                    foreach (Vector3Int triangle in __triangles)
                                    {
                                        if (!delaunay.Get(triangle.x, out x))
                                            continue;

                                        if (!delaunay.Get(triangle.y, out y))
                                            continue;

                                        if (!delaunay.Get(triangle.z, out z))
                                            continue;

                                        if ((temp - x).Cross(y - x) > 0.0f && (temp - y).Cross(z - y) > 0.0f && (temp - z).Cross(x - z) > 0.0f)
                                        {
                                            delaunay.AddPoint(temp);

                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        MeshData<int> meshForRender;
                        if (delaunay.ToMesh(heightGetter, out meshForRender))
                        {
                            instantiate(new Instance(drawInfo.gameObject, null, target =>
                            {
                                GameObject instance = target as GameObject;
                                if (instance == null)
                                    return;

                                Dictionary<int, int> subMeshIndices = null;

                                MeshFilter meshFilter = instance.AddComponent<MeshFilter>();
                                if (meshFilter != null)
                                    meshFilter.sharedMesh = meshForRender.ToFlatMesh(null, ref subMeshIndices);

                                if (result)
                                {
                                    MeshCollider meshCollider = instance.AddComponent<MeshCollider>();
                                    if (meshCollider != null)
                                    {
                                        subMeshIndices = null;

                                        meshCollider.sharedMesh = meshForCollide.ToMesh(null, ref subMeshIndices);
                                    }
                                }
                            }));
                        }
                    }
                }
            }

            public class Liner
            {
                private int __increment;
                private System.Random __random;
                private Voxel.Sampler __sampler;
                private NavPath __path;
                private LineInfo[] __lineInfos;
                private List<Vector3> __points;
                private List<Vector3Int> __triangles;
                private List<int>[] __lines;

                public Liner(int increment, System.Random random, Voxel.Sampler sampler)
                {
                    __increment = increment;
                    __random = random;
                    __sampler = sampler;
                }

                public void Create(LineInfo[] lineInfos)
                {
                    return;
                    int numLineInfos = lineInfos == null ? 0 : lineInfos.Length;
                    if (__lines == null || __lines.Length < numLineInfos)
                        Array.Resize(ref __lines, numLineInfos);

                    List<int> line;
                    for (int i = 0; i < numLineInfos; ++i)
                    {
                        line = __lines[i];
                        if (line != null)
                            line.Clear();
                    }

                    __lineInfos = lineInfos;
                }

                public bool Set(int index, float x, float y, MapInfo[] mapInfos)
                {
                    int lineIndex = __GetLineIndex(x, y, __lineInfos, mapInfos);
                    if (lineIndex == -1)
                        return false;

                    List<int> line = __lines[lineIndex];
                    if (line == null)
                    {
                        line = new List<int>();

                        __lines[lineIndex] = line;
                    }

                    line.Add(index);

                    return true;
                }

                public void Do(Vector3Int position, Vector3Int size, Chunk[] chunks, Predicate<Instance> instantiate)
                {
                    if (__sampler == null)
                        return;

                    if (__path != null)
                        __path.Clear();

                    int numLineInfos = __lineInfos == null ? 0 : __lineInfos.Length, numPoints, fromPointIndex, toPointIndex, depth, i, j;

                    float fromHeight, toHeight;
                    Vector2Int fromPoint, toPoint;
                    Vector3Int source, destination;
                    Vector3 offset, scale = __sampler.scale, temp;
                    LineInfo lineInfo;
                    List<int> line;
                    for (i = 0; i < numLineInfos; ++i)
                    {
                        line = __lines[i];
                        numPoints = line == null ? 0 : line.Count;
                        if (numPoints < 2)
                            continue;

                        for (j = 1; j < numPoints; ++j)
                        {
                            fromPointIndex = __random.Next(j, numPoints);
                            toPointIndex = line[fromPointIndex];
                            line[fromPointIndex] = line[j - 1];
                            line[j - 1] = toPointIndex;
                        }

                        lineInfo = __lineInfos[i];
                        lineInfo.minDentity *= size.y;
                        lineInfo.maxDentity *= size.y;

                        fromPointIndex = line[0];
                        fromPoint = new Vector2Int(fromPointIndex % size.x, fromPointIndex / size.x);
                        fromHeight = chunks[fromPointIndex].height;

                        temp = new Vector3(
                            (fromPoint.x + position.x) * scale.x,
                            fromHeight,
                            (fromPoint.y + position.z) * scale.z);

                        for (j = 1; j < numPoints; ++j)
                        {
                            if (__path == null)
                                __path = new NavPath(size);
                            else
                            {
                                source = __path.size;
                                destination = size;// Vector3Int.Max(source, size);
                                if (source != destination)
                                    __path = new NavPath(destination);
                            }

                            toPointIndex = line[j];
                            toPoint = new Vector2Int(toPointIndex % size.x, toPointIndex / size.x);
                            toHeight = chunks[toPointIndex].height;

                            depth = __path.Search(
                                int.MaxValue,
                                0,
                                lineInfo.minDentity,
                                lineInfo.maxDentity,
                                lineInfo.minExtends,
                                lineInfo.maxExtends,
                                position,
                                new Vector3Int(fromPoint.x, Mathf.CeilToInt(fromHeight / scale.y) - position.y, fromPoint.y),
                                new Vector3Int(toPoint.x, Mathf.CeilToInt(toHeight / scale.y) - position.y, toPoint.y),
                                __sampler == null ? null : __sampler.instance);

                            fromPointIndex = toPointIndex;
                            fromPoint = toPoint;
                            fromHeight = toHeight;

                            if (depth > 0)
                            {
                                foreach (Vector3Int pathPoint in __path)
                                {
                                    offset = new Vector3(
                                        (pathPoint.x + position.x) * scale.x,
                                        (pathPoint.y + position.y) * scale.y,
                                        (pathPoint.z + position.z) * scale.z);

                                    offset = __sampler.ApproximateZeroCrossingPosition(offset, new Vector3(offset.x, offset.y - scale.y, offset.z), __increment);

                                    if (__points == null)
                                        __points = new List<Vector3>();

                                    __points.Add(offset);
                                }
                            }
                            else
                            {
                                __Do(
                                    lineInfo.halfWidth,
                                    lineInfo.radius,
                                    lineInfo.countPerUnit,
                                    temp,
                                    lineInfo.gameObject,
                                    instantiate);

                                temp = new Vector3(
                                    (fromPoint.x + position.x) * scale.x,
                                    fromHeight,
                                    (fromPoint.y + position.z) * scale.z);
                            }
                        }

                        __Do(
                            lineInfo.halfWidth,
                            lineInfo.radius,
                            lineInfo.countPerUnit,
                            temp,
                            lineInfo.gameObject,
                            instantiate);
                    }
                }

                private void __Do(
                    float halfWidth,
                    float radius,
                    float pointCount,
                    Vector3 position,
                    GameObject gameObject,
                    Predicate<Instance> instantiate)
                {
                    int numPoints = __points == null ? 0 : __points.Count;
                    if (numPoints < 2 || instantiate == null)
                        return;

                    int segment = 10;
                    float orginRadius = 2.0f;
                    float doRadius = 1.0f;

                    __sampler.Do(position, doRadius);

                    int i, count = numPoints - 1;
                    Vector2 min = new Vector2(position.x - orginRadius, position.z - orginRadius), max = new Vector2(position.x + orginRadius, position.z + orginRadius), temp;
                    Vector3 point;
                    //Quaternion rotation;
                    for (i = 1; i < count; ++i)
                    {
                        point = __points[i];

                        //rotation = Quaternion.FromToRotation(Vector3.forward, to - from);

                        __sampler.Do(point, radius);

                        //from = to;

                        temp = new Vector2(point.x, point.z);

                        min = Vector2.Min(min, temp);
                        max = Vector2.Max(max, temp);
                    }

                    point = __points[count];
                    temp = new Vector2(point.x, point.z);

                    min = Vector2.Min(min, temp);
                    max = Vector2.Max(max, temp);

                    point = __points[0];
                    temp = new Vector2(point.x, point.z);

                    min = Vector2.Min(min, temp);
                    max = Vector2.Max(max, temp);

                    float width = halfWidth * 2.0f;
                    temp = new Vector2(width, width);
                    min -= temp;
                    max += temp;
                    Vector3 size = max - min;
                    Delaunay delaunay = new Delaunay(new Rect(min, size), 0);

                    Vector2 direction,
                        current,
                        previous = Vector2.zero,
                        x = new Vector2(position.x, position.z), y;
                    for (i = 0; i < numPoints; ++i)
                    {
                        point = __points[i];
                        y = new Vector2(point.x, point.z);

                        current = y - x;
                        direction = (current + previous).normalized;
                        direction = new Vector2(direction.y, -direction.x);

                        x = y;
                        previous = current;

                        delaunay.AddPoint(x + direction * halfWidth);
                        delaunay.AddPoint(x - direction * halfWidth);

                        if (i + 1 == numPoints)
                        {
                            delaunay.AddPoint(y + direction * halfWidth);
                            delaunay.AddPoint(y - direction * halfWidth);
                        }
                    }

                    if (segment > 0)
                    {
                        float anglePerSegment = Mathf.PI * 2.0f / segment, angle = 0.0f;
                        for (i = 0; i < segment; ++i)
                        {
                            delaunay.AddPoint(new Vector2(position.x + orginRadius * Mathf.Cos(angle), position.z + orginRadius * Mathf.Sin(angle)));

                            angle += anglePerSegment;
                        }
                    }

                    if (__triangles != null)
                        __triangles.Clear();

                    float length = orginRadius + halfWidth;
                    length *= length;
                    width += halfWidth;

                    Vector2 orgin = new Vector2(position.x, position.z);
                    delaunay.DeleteFrames(triangle =>
                    {
                        if (triangle.x < 4 || triangle.y < 4 || triangle.z < 4)
                            return true;

                        Vector2 vertexX, vertexY, vertexZ;
                        if (!delaunay.Get(triangle.x, out vertexX))
                            return true;

                        if (!delaunay.Get(triangle.y, out vertexY))
                            return true;

                        if (!delaunay.Get(triangle.z, out vertexZ))
                            return true;

                        if (
                            (vertexX - orgin).sqrMagnitude > length ||
                            (vertexY - orgin).sqrMagnitude > length ||
                            (vertexZ - orgin).sqrMagnitude > length)
                        {
                            bool result = true;
                            int index = (numPoints + 1) << 1;
                            if (triangle.x < index && triangle.y < index && triangle.z < index)
                            {
                                int indexX = triangle.x & 1, indexY = triangle.y & 1, indexZ = triangle.z & 1;
                                result = indexX == indexY && indexY == indexZ;
                            }
                            else
                            {
                                if (triangle.x < index && triangle.y < index)
                                    result = (triangle.x & 1) == (triangle.y & 1);

                                if (triangle.y < index && triangle.z < index)
                                    result = (triangle.y & 1) == (triangle.z & 1);

                                if (triangle.z < index && triangle.x < index)
                                    result = (triangle.z & 1) == (triangle.x & 1);
                            }

                            if (result)
                            {
                                float distance;

                                for (i = 0; i < numPoints; ++i)
                                {
                                    point = __points[i];

                                    x = new Vector2(point.x, point.z);

                                    distance = (x - vertexX).magnitude + (x - vertexY).magnitude + (x - vertexZ).magnitude;
                                    if (distance < width)
                                    {
                                        result = false;

                                        break;
                                    }
                                }

                                if (result)
                                    return true;
                            }
                        }

                        if (__triangles == null)
                            __triangles = new List<Vector3Int>();

                        __triangles.Add(triangle);

                        return false;
                    });

                    if (__triangles != null)
                    {
                        Vector2 z;
                        pointCount = Mathf.RoundToInt(pointCount * (size.x * size.y));
                        for (i = 0; i < pointCount; ++i)
                        {
                            temp = new Vector2((float)(__random.NextDouble() * size.x + min.x), (float)(__random.NextDouble() * size.y + min.y));
                            foreach (Vector3Int triangle in __triangles)
                            {
                                if (!delaunay.Get(triangle.x, out x))
                                    continue;

                                if (!delaunay.Get(triangle.y, out y))
                                    continue;

                                if (!delaunay.Get(triangle.z, out z))
                                    continue;

                                if ((temp - x).Cross(y - x) > 0.0f && (temp - y).Cross(z - y) > 0.0f && (temp - z).Cross(x - z) > 0.0f)
                                {
                                    delaunay.AddPoint(temp);

                                    break;
                                }
                            }
                        }
                    }

                    float height = position.y;
                    for (i = 0; i < numPoints; ++i)
                    {
                        point = __points[i];
                        if ((new Vector2(point.x, point.z) - orgin).sqrMagnitude < length && point.y < height)
                            height = point.y;
                    }

                    MeshData<int> meshData;
                    if (delaunay.ToMesh(pointIndex =>
                    {
                        if (!delaunay.Get(pointIndex, out x))
                            return 0.0f;

                        if ((x - orgin).sqrMagnitude < length)
                            return height;

                        Sampler sampler = __sampler == null ? null : __sampler.instance as Sampler;
                        if (sampler == null || sampler.__chunks == null)
                            return 0.0f;

                        Vector3 scale = sampler.__scale;
                        Vector2Int
                        offset = new Vector2Int(Mathf.RoundToInt(x.x / scale.x), Mathf.RoundToInt(x.y / scale.z)),
                        world = new Vector2Int(offset.x / sampler.__mapSize.x, offset.y / sampler.__mapSize.y),
                        local = Vector2Int.Scale(world, sampler.__mapSize);

                        local.x = offset.x - local.x;
                        local.y = offset.y - local.y;

                        if (local.x < 0)
                        {
                            local.x += sampler.__mapSize.x;

                            --world.x;
                        }

                        if (local.y < 0)
                        {
                            local.y += sampler.__mapSize.y;

                            --world.y;
                        }

                        Chunk[] chunks;
                        if (!sampler.__chunks.TryGetValue(world, out chunks))
                            return 0.0f;

                        int index = local.x + local.y * sampler.__mapSize.x, numChunks = chunks == null ? 0 : chunks.Length;
                        if (index >= numChunks)
                            return 0.0f;

                        return chunks[index].height;

                    }, out meshData))
                    {
                        instantiate(new Instance(gameObject, null, target =>
                        {
                            GameObject instance = target as GameObject;
                            if (instance == null)
                                return;

                            Dictionary<int, int> subMeshIndices = null;
                            Mesh mesh = meshData.ToFlatMesh(null, ref subMeshIndices);
                            if (mesh == null)
                                return;

                            MeshFilter meshFilter = instance.AddComponent<MeshFilter>();
                            if (meshFilter != null)
                                meshFilter.sharedMesh = mesh;

                            MeshCollider meshCollider = instance.AddComponent<MeshCollider>();
                            if (meshCollider != null)
                                meshCollider.sharedMesh = mesh;
                        }));
                    }
                    if (__points != null)
                        __points.Clear();
                }

                private float __GetLineHeight(Vector2 pointToBuild, Vector3 position)
                {
                    int numPoints = __points == null ? 0 : __points.Count, source = numPoints, destination;
                    float minDistance = (new Vector2(position.x, position.z) - pointToBuild).sqrMagnitude, distance;
                    Vector3 pointToCheck;
                    for (int i = 0; i < numPoints; ++i)
                    {
                        pointToCheck = __points[i];

                        distance = (new Vector2(pointToCheck.x, pointToCheck.z) - pointToBuild).sqrMagnitude;
                        if (distance < minDistance)
                        {
                            minDistance = distance;

                            source = i;
                        }
                    }

                    if (source < numPoints)
                    {
                        destination = source;

                        minDistance = int.MaxValue;
                        if (source > 0)
                        {
                            destination = source - 1;

                            pointToCheck = __points[destination];

                            minDistance = (new Vector2(pointToCheck.x, pointToCheck.z) - pointToBuild).sqrMagnitude;
                        }

                        if (source < numPoints - 1)
                        {
                            pointToCheck = __points[source + 1];

                            distance = (new Vector2(pointToCheck.x, pointToCheck.z) - pointToBuild).sqrMagnitude;
                            if (distance < minDistance)
                                destination = source + 1;
                        }

                        Vector3 start = __points[source], end = __points[destination], normal = end - start;

                        return Mathf.Lerp(start.y, end.y, Vector3.Project(new Vector3(pointToBuild.x - start.x, 0.0f, pointToBuild.y), normal).magnitude / normal.magnitude);
                    }

                    return position.y;
                }

                public int __GetLineIndex(float x, float y, LineInfo[] lineInfos, MapInfo[] mapInfos)
                {
                    int numLines = lineInfos == null ? 0 : lineInfos.Length;
                    float chance = 0.0f, random = (float)__random.NextDouble(), temp;
                    LineInfo lineInfo;
                    for (int i = 0; i < numLines; ++i)
                    {
                        lineInfo = lineInfos[i];

                        chance += lineInfo.chance;

                        if (chance > random && __Check(x, y, mapInfos, lineInfo.mapFilters, out temp) && temp > (float)__random.NextDouble())
                            return i;

                        if (chance >= 1.0f)
                        {
                            chance = 0.0f;

                            random = (float)__random.NextDouble();
                        }
                    }

                    return -1;
                }
            }

            public IEngineBuilder builder;

            private int __noiseSize;
            
            private Vector2Int __mapSize;

            private Vector3 __scale;
            private System.Random __random;
            private Drawer __drawer;
            //private float[] __results;
            private float[] __layers;
            private List<Block> __blocks;
            private Dictionary<Vector2Int, Chunk[]> __chunks = new Dictionary<Vector2Int, Chunk[]>();
            
            public Sampler(int noiseSize, Vector3 scale, Vector2Int mapSize, System.Random random)
            {
                __noiseSize = noiseSize;

                __mapSize = mapSize;

                __scale = scale;

                __random = random;
            }

            public float Get(int volumeIndex, float result, Vector3 point, VolumeInfo[] volumeInfos)
            {
                if (volumeIndex < 0)
                    return result;

                int numVolumeInfos = volumeInfos == null ? 0 : volumeInfos.Length;
                if (volumeIndex >= numVolumeInfos)
                    return result;
                
                VolumeInfo volumeInfo = volumeInfos[volumeIndex];
                return Mathf.Max(volumeInfo.Get(point.x, point.y, point.z) / volumeInfo.scale.magnitude, result);// Mathf.Clamp(volumeInfo.Get(__noise, point.x, point.y, point.z) / (volumeInfo.scale.y * scale.y), -1.0f, 1.0f);
            }

            public bool Create(
                Vector2Int position,
                MapInfo[] mapInfos,
                VolumeInfo[] volumeInfos,
                LayerInfo[] layerInfos,
                LineInfo[] lineInfos,
                DrawInfo[] drawInfos,
                Liner liner,
                Predicate<Instance> instantiate)
            {
                if (__chunks.ContainsKey(position))
                    return false;

                bool result;
                int size = __mapSize.x * __mapSize.y, index = 0, min = int.MaxValue, max = int.MinValue, i, j, k, l;
                float previous, current, next, middle, temp;
                Vector3 point;
                LayerInfo layerInfo;
                Chunk chunk;
                Block block;
                Chunk[] chunks = new Chunk[size];

                __chunks[position] = chunks;

                int numLayerInfos = layerInfos == null ? 0 : layerInfos.Length;
                if (__layers == null || __layers.Length < numLayerInfos)
                    __layers = new float[numLayerInfos];

                if (liner != null)
                    liner.Create(lineInfos);

                if (__drawer == null)
                    __drawer = new Drawer(__random);

                __drawer.Create(new Vector2(__scale.x, __scale.z), drawInfos, mapInfos);

                Vector2Int offset = position;
                position = Vector2Int.Scale(position, __mapSize);

                float x = position.x * __scale.x, y, length;
                point.z = position.y * __scale.z;
                for (i = 0; i < __mapSize.y; ++i)
                {
                    point.x = x;
                    for (j = 0; j < __mapSize.x; ++j)
                    {
                        if (__blocks == null)
                            __blocks = new List<Block>();// (size * (1 << depth));

                        chunk.index = __blocks.Count;
                        chunk.count = 0;

                        point.y = 0.0f;

                        previous = 0.0f;
                        for (k = 0; k < numLayerInfos; ++k)
                        {
                            layerInfo = layerInfos[k];
                            if (__Check(point.x, point.z, mapInfos, layerInfo.filters, out current))
                            {
                                current = (layerInfo.power > 0.0f ? Mathf.Pow(current, layerInfo.power) : current) * layerInfo.scale + layerInfo.offset;
                                current = current + (layerInfo.layerIndex >= 0 ? __layers[layerInfo.layerIndex] : 0.0f);

                                if (layerInfo.max > 0.0f)
                                    current = Mathf.Min(current, layerInfo.max);

                                if (layerInfo.depth > 0)
                                {
                                    next = current - __scale.y * layerInfo.depth;
                                    if (point.y > next)
                                    {
                                        //temp += scale.y;

                                        l = Mathf.FloorToInt((point.y - next) / __scale.y);
                                        if (l > chunk.count)
                                        {
                                            next += (l - chunk.count) * __scale.y;

                                            l = chunk.count;
                                        }

                                        if (current < previous)
                                        {
                                            temp = next;
                                            l = chunk.index + chunk.count - l;
                                            while (temp < current)
                                            {
                                                if (temp > point.y)
                                                    break;

                                                block = __blocks[l];
                                                block.materialIndex = layerInfo.materialIndex;
                                                __blocks[l] = block;

                                                ++l;

                                                temp += __scale.y;
                                            }
                                        }
                                        else
                                        {
                                            point.y -= l * __scale.y;

                                            chunk.count -= l;

                                            previous = next;
                                        }
                                    }
                                    else
                                    {
                                        length = next - previous;

                                        y = length * 0.5f;

                                        middle = previous + y;

                                        result = false;
                                        while (point.y <= next)
                                        {
                                            block = new Block(
                                                layerInfo.materialIndex,
                                                Get(layerInfo.volumeIndex, y - Mathf.Abs(point.y - middle), point, volumeInfos));

                                            l = chunk.index + chunk.count;
                                            if (l < __blocks.Count)
                                            {
                                                if (!result)
                                                {
                                                    temp = __blocks[l].density;
                                                    if (temp < block.density)
                                                        block.density = temp;
                                                    else
                                                        result = true;
                                                }

                                                __blocks[l] = block;
                                            }
                                            else
                                                __blocks.Add(block);

                                            ++chunk.count;

                                            point.y += __scale.y;
                                        }

                                        previous = next;
                                    }
                                }

                                if (current >= previous)
                                {
                                    length = current - previous;
                                    if (previous > 0.0f)
                                    {
                                        temp = point.y - __scale.y;
                                        /*if (current - temp < point.y - current)
                                        {
                                            point.y = temp;

                                            --chunk.count;
                                        }*/

                                        y = length * 0.5f;

                                        middle = previous + y;

                                        l = chunk.index + chunk.count;
                                        previous = previous - y;
                                        do
                                        {
                                            --l;
                                            point.y -= __scale.y;

                                            temp = Get(layerInfo.volumeIndex, Mathf.Abs(point.y - middle) - y, point, volumeInfos);

                                            block = __blocks[l];

                                            if (block.density > temp)
                                            {
                                                block.density = temp;

                                                __blocks[l] = block;
                                            }
                                            else
                                                break;
                                        } while (l > chunk.index && point.y > previous);

                                        point.y = __scale.y * chunk.count;
                                    }
                                    else
                                    {
                                        y = length;

                                        middle = 0.0f;
                                    }

                                    result = false;
                                    while (point.y <= current)
                                    {
                                        block = new Block(
                                            layerInfo.materialIndex,
                                            Get(layerInfo.volumeIndex, Mathf.Abs(point.y - middle) - y, point, volumeInfos));

                                        if (block.density > -1.0f && min > chunk.count)
                                            min = chunk.count;

                                        l = chunk.index + chunk.count;
                                        if (l < __blocks.Count)
                                        {
                                            if (!result)
                                            {
                                                temp = __blocks[l].density;
                                                if (temp < block.density)
                                                    block.density = temp;
                                                else
                                                    result = true;
                                            }

                                            __blocks[l] = block;
                                        }
                                        else
                                            __blocks.Add(block);

                                        ++chunk.count;

                                        point.y += __scale.y;
                                    }

                                    l = chunk.index + chunk.count;

                                    previous = current + y;
                                    while (point.y < previous)
                                    {
                                        block = new Block(
                                            layerInfo.materialIndex,
                                            Get(layerInfo.volumeIndex, point.y - middle - y, point, volumeInfos));

                                        if (l < __blocks.Count)
                                        {
                                            if (!result)
                                            {
                                                temp = __blocks[l].density;
                                                if (temp < block.density)
                                                    block.density = temp;
                                                else
                                                    result = true;
                                            }

                                            __blocks[l] = block;
                                        }
                                        else
                                            __blocks.Add(block);

                                        ++l;

                                        block.density += __scale.y;

                                        point.y += __scale.y;
                                    }

                                    if (result)
                                    {
                                        while (l < __blocks.Count)
                                        {
                                            block = new Block(
                                                   layerInfo.materialIndex,
                                                   Get(layerInfo.volumeIndex, point.y - middle - y, point, volumeInfos));

                                            __blocks[l] = block;

                                            ++l;

                                            point.y += __scale.y;
                                        }
                                    }

                                    point.y = __scale.y * chunk.count;

                                    previous = current;
                                }
                            }

                            __layers[k] = previous;
                        }

                        if (liner != null)
                            liner.Set(index, point.x, point.z, mapInfos);

                        __drawer.Set(index, new Vector2Int(position.x + i, position.y + j));

                        chunk.count = __blocks.Count - chunk.index;

                        max = Mathf.Max(max, chunk.count);

                        chunk.height = previous;

                        chunks[index++] = chunk;

                        point.x += __scale.x;
                    }

                    point.z += __scale.z;
                }

                if (__chunks.ContainsKey(new Vector2Int(offset.x, offset.y - 1)))
                {
                    Vector2Int target = new Vector2Int(position.x, position.y - 1);
                    for (i = 0; i < __mapSize.x; ++i)
                    {
                        __drawer.Set(index, target);

                        ++target.x;
                    }
                }

                if (__chunks.ContainsKey(new Vector2Int(offset.x, offset.y + 1)))
                {
                    Vector2Int target = new Vector2Int(position.x, position.y + __mapSize.y);
                    for (i = 0; i < __mapSize.x; ++i)
                    {
                        __drawer.Set(index, target);

                        ++target.x;
                    }
                }

                if (__chunks.ContainsKey(new Vector2Int(offset.x - 1, offset.y)))
                {
                    Vector2Int target = new Vector2Int(position.x - 1, position.y);
                    for (i = 0; i < __mapSize.y; ++i)
                    {
                        __drawer.Set(index, target);

                        ++target.y;
                    }
                }

                if (__chunks.ContainsKey(new Vector2Int(offset.x + 1, offset.y)))
                {
                    Vector2Int target = new Vector2Int(position.x + __mapSize.x, position.y);
                    for (i = 0; i < __mapSize.y; ++i)
                    {
                        __drawer.Set(index, target);

                        ++target.y;
                    }
                }

                if (__chunks.ContainsKey(new Vector2Int(offset.x - 1, offset.y - 1)))
                    __drawer.Set(index, new Vector2Int(position.x - 1, position.y - 1));

                if (__chunks.ContainsKey(new Vector2Int(offset.x + 1, offset.y - 1)))
                    __drawer.Set(index, new Vector2Int(position.x + __mapSize.x, position.y - 1));

                if (__chunks.ContainsKey(new Vector2Int(offset.x - 1, offset.y + 1)))
                    __drawer.Set(index, new Vector2Int(position.x - 1, position.y + __mapSize.y));

                if (__chunks.ContainsKey(new Vector2Int(offset.x + 1, offset.y + 1)))
                    __drawer.Set(index, new Vector2Int(position.x + __mapSize.x, position.y + __mapSize.y));

                __drawer.Do(instantiate);

                if (min <= max)
                {
                    BoundsInt bounds = new BoundsInt(new Vector3Int(position.x, min, position.y), new Vector3Int(__mapSize.x, max - min, __mapSize.y));

                    if (liner != null)
                        liner.Do(bounds.position, bounds.size, chunks, instantiate);

                    if (builder != null)
                        builder.Set(bounds);
                }

                return true;
            }

            public bool Get(Vector3Int position, out Block block)
            {
                Vector2Int world = new Vector2Int(position.x / __mapSize.x, position.z / __mapSize.y), 
                    local = Vector2Int.Scale(world, __mapSize);

                local.x = position.x - local.x;
                local.y = position.z - local.y;

                if(local.x < 0)
                {
                    local.x += __mapSize.x;

                    --world.x;
                }

                if(local.y < 0)
                {
                    local.y += __mapSize.y;

                    --world.y;
                }

                Chunk[] chunks;
                if (__chunks.TryGetValue(world, out chunks))
                {
                    int index = local.x + local.y * __mapSize.x, numChunks = chunks == null ? 0 : chunks.Length;
                    if(index < numChunks)
                    {
                        Chunk chunk = chunks[index];
                        if (position.y >= 0 && position.y < chunk.count)
                            block = __blocks[chunk.index + position.y];
                        else
                        {
                            block = __blocks[chunk.index + Mathf.Clamp(position.y, 0, chunk.count - 1)];

                            block.density = position.y * __scale.y - (position.y < 0 ? 0.0f : chunk.height);
                        }
                            
                        return true;
                    }
                }

                block = new Block(0, position.y * __scale.y);

                return true;
            }

            public bool Set(Vector3Int position, Block block)
            {
                if (position.y < 0)
                    return false;

                Vector2Int world = new Vector2Int(position.x / __mapSize.x, position.z / __mapSize.y),
                    local = Vector2Int.Scale(world, __mapSize);

                local.x = position.x - local.x;
                local.y = position.z - local.y;

                if (local.x < 0)
                {
                    local.x += __mapSize.x;

                    --world.x;
                }

                if (local.y < 0)
                {
                    local.y += __mapSize.y;

                    --world.y;
                }

                Chunk[] chunks;
                if (__chunks.TryGetValue(world, out chunks))
                {
                    int index = local.x + local.y * __mapSize.x, numChunks = chunks == null ? 0 : chunks.Length;
                    if (index < numChunks)
                    {
                        Chunk chunk = chunks[index];
                        if (chunk.count > position.y)
                        {
                            index = chunk.index + position.y;
                            if (builder != null && !Mathf.Approximately(__blocks[index].density, block.density))
                                builder.Set(position);

                            __blocks[index] = block;

                            return true;
                        }
                    }
                }

                return false;
            }
        }

        public int seed = 186;

        public int noiseSize;

        public float objectCountPerArea = 1.0f;

        public Vector2Int mapSize;
        
        [HideInInspector]
        public Transform root;

        public MapInfo[] mapInfos;
        public VolumeInfo[] volumeInfos;
        public MaterialInfo[] materialInfos;
        public LayerInfo[] layerInfos;
        public ObjectInfo[] objectInfos;
        public LineInfo[] lineInfos;
        public DrawInfo[] drawInfos;
        public Material[] materials;
        public GameObject[] gameObjects;
        
        private System.Random __random;
        private Voxel.Sampler __sampler;
        private List<ObjectIndex> __objectIndices = new List<ObjectIndex>();
        private Dictionary<NodeIndex, List<Node>> __nodes = new Dictionary<NodeIndex, List<Node>>();

        public abstract T engine { get; }

        public Voxel.Sampler sampler
        {
            get
            {
                return __sampler;
            }
        }

        /*public IEnumerable<KeyValuePair<Vector3Int, Node>> nodes
        {
            get
            {
                return __nodes;
            }
        }*/

        public FlatTerrain()
        {
            subMeshHandler = __GetMaterialIndex;
        }

        public void Do(Vector3 position, float radius)
        {
            if(__sampler != null)
                __sampler.Do(position, radius);

            float size = radius * 2.0f;
            Rebuild(new Bounds(position, new Vector3(size, size, size) + _size));
        }

        public void Do(Bounds bounds, Quaternion rotation)
        {
            if (__sampler != null)
                __sampler.Do(bounds, rotation);

            bounds = rotation.Multiply(bounds);
            Vector3 scale = base.scale, min = bounds.min - scale, max = bounds.max + scale;

            Rebuild(new Bounds((min + max) * 0.5f, max - min));
        }

#if UNITY_EDITOR
        public void OnValidate()
        {
            int i, j, 
                numMapInfos = mapInfos == null ? 0 : mapInfos.Length,
                numVolumeInfos = volumeInfos == null ? 0 : volumeInfos.Length,
                numMaterialInfos = materialInfos == null ? 0 : materialInfos.Length,
                numMaterials = materials == null ? 0 : materials.Length,
                numGameObjects = gameObjects == null ? 0 : gameObjects.Length;

            MaterialInfo materialInfo;
            Material material;
            for (i = 0; i < numMaterialInfos; ++i)
            {
                materialInfo = materialInfos[i];
                if(materialInfo.index < 0 || materialInfo.index >= numMaterials)
                {
                    if (string.IsNullOrEmpty(materialInfo.name))
                        continue;
                }
                else
                {
                    material = materials[materialInfo.index];
                    if (material == null || material.name == materialInfo.name)
                        continue;
                }

                if (string.IsNullOrEmpty(materialInfo.name))
                    materialInfo.index = -1;
                else
                {
                    for(j = 0; j < numMaterials; ++j)
                    {
                        material = materials[j];
                        if (material != null && material.name == materialInfo.name)
                        {
                            materialInfo.index = j;

                            break;
                        }
                    }
                }

                materialInfos[i] = materialInfo;
            }

            bool isContains;
            int numLayerInfos = layerInfos == null ? 0 : layerInfos.Length;
            LayerInfo layerInfo, temp;
            for (i = 0; i < numLayerInfos; ++i)
            {
                layerInfo = layerInfos[i];

                isContains = false;
                if (layerInfo.materialIndex < 0 || layerInfo.materialIndex >= numMaterialInfos)
                    isContains = !string.IsNullOrEmpty(layerInfo.materialName);
                else
                    isContains = materialInfos[layerInfo.materialIndex].name != layerInfo.materialName;

                if (isContains)
                {
                    if (string.IsNullOrEmpty(layerInfo.materialName))
                        layerInfo.materialName = materialInfos[layerInfo.materialIndex].name;
                    else
                    {
                        for (j = 0; j < numMaterialInfos; ++j)
                        {
                            materialInfo = materialInfos[j];
                            if (materialInfo.name == layerInfo.materialName)
                            {
                                layerInfo.materialIndex = j;

                                break;
                            }
                        }
                    }
                }

                isContains = false;
                if (layerInfo.layerIndex < 0)
                {
                    isContains = false;

                    layerInfo.layerName = string.Empty;
                }
                else if (layerInfo.layerIndex >= numLayerInfos)
                    isContains = !string.IsNullOrEmpty(layerInfo.layerName);
                else
                    isContains = layerInfos[layerInfo.layerIndex].name != layerInfo.layerName;

                if (isContains)
                {
                    if (string.IsNullOrEmpty(layerInfo.layerName))
                        layerInfo.layerName = layerInfos[layerInfo.layerIndex].name;
                    else
                    {
                        for (j = 0; j < numLayerInfos; ++j)
                        {
                            temp = layerInfos[j];
                            if (temp.name == layerInfo.layerName)
                            {
                                layerInfo.layerIndex = j;

                                break;
                            }
                        }
                    }
                }

                layerInfos[i] = layerInfo;
            }
            
            int numObjectInfos = objectInfos == null ? 0 : objectInfos.Length;
            int length, k, l;
            MaterialFilter materialFilter;
            LayerFilter layerFilter;
            ObjectInfo objectInfo;
            GameObject gameObject;
            for (i = 0; i < numObjectInfos; ++i)
            {
                objectInfo = objectInfos[i];
                
                if (objectInfo.index < 0 || objectInfo.index >= numGameObjects)
                {
                    if (string.IsNullOrEmpty(objectInfo.objectName))
                        continue;
                }
                else
                {
                    gameObject = gameObjects[objectInfo.index];
                    if (gameObject == null || gameObject.name == objectInfo.objectName)
                        continue;
                }

                if (string.IsNullOrEmpty(objectInfo.objectName))
                    objectInfo.index = -1;
                else
                {
                    for (j = 0; j < numGameObjects; ++j)
                    {
                        gameObject = gameObjects[j];
                        if (gameObject != null && gameObject.name == objectInfo.objectName)
                        {
                            objectInfo.index = j;

                            break;
                        }
                    }
                }

                length = objectInfo.materialFilters == null ? 0 : objectInfo.materialFilters.Length;
                for(j = 0; j < length; ++j)
                {
                    materialFilter = objectInfo.materialFilters[j];

                    if (materialFilter.index < 0 || materialFilter.index >= numMaterialInfos)
                    {
                        if (string.IsNullOrEmpty(materialFilter.name))
                            continue;
                    }
                    else
                    {
                        materialInfo = materialInfos[materialFilter.index];
                        if (materialInfo.name == materialFilter.name)
                            continue;
                    }

                    if (string.IsNullOrEmpty(materialFilter.name))
                        materialFilter.index = -1;
                    else
                    {
                        for (k = 0; k < numMaterialInfos; ++k)
                        {
                            materialInfo = materialInfos[k];
                            if (materialInfo.name == materialFilter.name)
                            {
                                materialFilter.index = k;

                                break;
                            }
                        }
                    }

                    length = materialFilter.layerFilters == null ? 0 : materialFilter.layerFilters.Length;
                    for (k = 0; k < length; ++k)
                    {
                        isContains = false;
                        layerFilter = materialFilter.layerFilters[k];
                        if (layerFilter.index < 0)
                        {
                            isContains = false;

                            layerFilter.name = string.Empty;
                        }
                        else if (layerFilter.index >= numLayerInfos)
                            isContains = !string.IsNullOrEmpty(layerFilter.name);
                        else
                            isContains = layerInfos[layerFilter.index].name != layerFilter.name;
                        
                        if (isContains)
                        {
                            if (string.IsNullOrEmpty(layerFilter.name))
                                layerFilter.name = layerInfos[layerFilter.index].name;
                            else
                            {
                                for (l = 0; l < numLayerInfos; ++l)
                                {
                                    layerInfo = layerInfos[l];
                                    if (layerInfo.name == layerFilter.name)
                                    {
                                        layerFilter.index = l;

                                        break;
                                    }
                                }
                            }
                        }
                        
                        materialFilter.layerFilters[k] = layerFilter;
                    }

                    objectInfo.materialFilters[j] = materialFilter;
                }

                objectInfos[i] = objectInfo;
            }
        }
#endif

        public IEnumerator<Node> GetEnumerator()
        {
            if (__nodes == null)
                return null;

            return Enumerator.Create(__nodes.GetEnumerator());
        }

        public override GameObject Convert(MeshData<Vector3> meshData, Level level)
        {
            /*meshData = meshData.Simplify(
                   5,
                   10,
                   16,
                   100,
                   0.05f,
                   0.125f,
                   0.8f,
                   2.5f,
                   1.0f);*/

            Dictionary<int, int> materialMap = null;

            Mesh mesh = meshData.ToFlatMesh(null, ref materialMap);
            if (mesh == null)
                return null;

            GameObject gameObject = new GameObject();

            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
            if (meshFilter != null)
                meshFilter.sharedMesh = mesh;

            if (materialMap != null)
            {
                Material[] materials = new Material[materialMap.Count];
                foreach (KeyValuePair<int, int> pair in materialMap)
                    materials[pair.Value] = this.materials[pair.Key];

                MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
                if (meshRenderer != null)
                    meshRenderer.sharedMaterials = materials;
            }

            return gameObject;
        }

        public override T CreateOrUpdate(Vector3Int world)
        {
            Sampler instance = __sampler == null ? null : __sampler.instance as Sampler;
            if(instance != null)
            {
                int i, j, size = (1 << depth), mask = size - 1;
                Vector2Int min = new Vector2Int(world.x * mask, world.z * mask), max = min + new Vector2Int(size, size);//, position, offset;

                /*offset = new Vector2Int(min.x / mapSize.x, min.y / mapSize.y);
                position = min - Vector2Int.Scale(offset, mapSize);
                if(position.x < 0)
                    --offset.x;

                if (position.y < 0)
                    --offset.y;

                min = offset;
                max = new Vector2Int(max.x / mapSize.x, max.y / mapSize.y);*/

                min = Vector2Int.FloorToInt(new Vector2(min.x * 1.0f / mapSize.x, min.y * 1.0f / mapSize.y)) - Vector2Int.one;
                max = Vector2Int.CeilToInt(new Vector2(max.x * 1.0f / mapSize.x, max.y * 1.0f / mapSize.y)) + Vector2Int.one;

                /*offset = new Vector2Int(max.x / mapSize.x, max.y / mapSize.y);
                position = max - Vector2Int.Scale(offset, mapSize);
                if (position.x > 0)
                    ++offset.x;

                if (position.y > 0)
                    ++offset.y;

                max = offset;*/

                for (i = min.x; i <= max.x; ++i)
                {
                    for (j = min.y; j <= max.y; ++j)
                        instance.Create(
                            new Vector2Int(i, j),
                            mapInfos,
                            volumeInfos,
                            layerInfos,
                            lineInfos,
                            drawInfos, 
                            null, 
                            Instantiate);
                }
            }
            
            return engine;
        }

        public override IEngineBuilder Create(int depth, float increment, Vector3 scale)
        {
            __random = new System.Random(seed);

            Sampler sampler = new Sampler(noiseSize, scale, mapSize, __random);
            __sampler = new Voxel.Sampler(scale, sampler);

            IEngineBuilder builder = Create(depth, increment, scale, __sampler);

            sampler.builder = builder;

            return builder;
        }

        public abstract IEngineBuilder Create(int depth, float increment, Vector3 scale, IEngineSampler sampler);
        
        private int __GetMaterialIndex(
            int level,
            Face face,
            IReadOnlyList<Vertex> vertices,
            U processor)
        {
            if (processor == null)
                return -1;
            
            Sampler instance = __sampler == null ? null : __sampler.instance as Sampler;
            if (instance == null)
                return -1;

            Vector3 scale = base.scale,
                position = processor.offset + Vector3.Scale(face.sizeDelta, scale);
            Vector3Int offset = Vector3Int.RoundToInt(new Vector3(position.x / scale.x, position.y / scale.y, position.z / scale.z));
            Block block;
            if (!instance.Get(offset, out block))
            {
                Debug.Log("wtf?");

                return -1;
            }

            int numMaterials = materialInfos == null ? 0 : materialInfos.Length;
            int result = block.materialIndex >= 0 && block.materialIndex < numMaterials ? materialInfos[block.materialIndex].index : -1;
            if (level > 0)
                return result;

            NodeIndex nodeIndex;
            nodeIndex.axis = face.axis;
            nodeIndex.position = offset;
            lock (__nodes)
            {
                if (__nodes.ContainsKey(nodeIndex))
                    return result;

                __nodes[nodeIndex] = null;
            }

            if (objectInfos != null)
            {
                lock (__random)
                {
                    __objectIndices.Clear();

                    Triangle triangle = new Triangle(vertices[face.indices.x].position, vertices[face.indices.y].position, vertices[face.indices.z].position);
                    float area = triangle.area / (scale.x * scale.y * scale.z) * Mathf.Min(scale.x, scale.y, scale.z) * 2.0f;

                    bool isContains;
                    int numObjectInfos = objectInfos == null ? 0 : objectInfos.Length, numGameObjects = gameObjects == null ? 0 : gameObjects.Length, i;
                    float total = 0.0f, chance, temp, height;
                    Vector3 normal = triangle.normal;
                    ObjectInfo objectInfo;
                    ObjectIndex objectIndex;
                    for (i = 0; i < numObjectInfos; ++i)
                    {
                        objectInfo = objectInfos[i];
                        if (Vector3.Dot(normal, objectInfo.normal) < objectInfo.dot)
                            continue;
                        
                        if (!__Check(position.x, position.z, mapInfos, objectInfo.mapFilters, out temp) || temp < (float)__random.NextDouble())
                            continue;

                        chance = objectInfo.chance;
                        if (objectInfo.materialFilters != null && objectInfo.materialFilters.Length > 0)
                        {
                            isContains = false;
                            foreach (MaterialFilter materialFilter in objectInfo.materialFilters)
                            {
                                if (materialFilter.index == block.materialIndex)
                                {
                                    chance += materialFilter.chanceOffset;

                                    isContains = true;

                                    if (materialFilter.layerFilters != null && materialFilter.layerFilters.Length > 0)
                                    {
                                        foreach (var layerFilter in materialFilter.layerFilters)
                                        {
                                            height = __GetHeight(layerFilter.index, position.x, position.z, mapInfos, layerInfos, out temp) + layerFilter.offset;

                                            height -= temp;
                                            if (position.y < height + temp * layerFilter.min || position.y > height + temp * layerFilter.max)
                                            {
                                                isContains = false;

                                                break;
                                            }
                                        }
                                    }

                                    break;
                                }
                            }

                            if (!isContains)
                                continue;
                        }

                        //chance = 1.0f - Mathf.Pow((1.0f - chance), area);
                        /*if ((1.0f - Mathf.Pow((1.0f - chance), area)) < (float)__random.NextDouble())
                            continue;*/

                        total += chance;

                        objectIndex.value = i;
                        objectIndex.chance = chance;

                        __objectIndices.Add(objectIndex);
                    }

                    int numObjectIndices = __objectIndices.Count;
                    if (numObjectIndices > 0)
                    {
                        GameObject gameObject;
                        float random;
                        int j, numObjects = Mathf.RoundToInt(area * objectCountPerArea);
                        for (i = 0; i < numObjects; ++i)
                        {
                            random = (float)__random.NextDouble();
                            for(j = 0; j < numObjectIndices; ++j)
                            {
                                objectIndex = __objectIndices[j];
                                objectInfo = objectInfos[objectIndex.value];

                                gameObject = objectInfo.index >= 0 && objectInfo.index < numGameObjects ? gameObjects[objectInfo.index] : null;
                                if (gameObject == null)
                                    continue;
                                
                                chance = total > 1.0f ? objectIndex.chance / total : objectIndex.chance;
                                if (chance < random)
                                {
                                    random -= chance;

                                    continue;
                                }

                                //temp = objectInfo.range * 2.0f;
                                Vector3 finalPosition = triangle.GetPoint((float)__random.NextDouble(), (float)__random.NextDouble()) /*plane.ClosestPointOnPlane(
                                    position + new Vector3(
                                        (float)(__random.NextDouble() * temp - objectInfo.range),
                                        (float)(__random.NextDouble() * temp - objectInfo.range),
                                        (float)(__random.NextDouble() * temp - objectInfo.range)))*/
                                    + normal * objectInfo.offset;

                                Quaternion rotation;
                                switch (objectInfo.rotation)
                                {
                                    case ObjectInfo.Rotation.AixY:
                                        rotation = Quaternion.Euler(0.0f, (float)(__random.NextDouble() * 360.0), 0.0f);
                                        break;
                                    case ObjectInfo.Rotation.NormalUp:
                                        rotation = Quaternion.FromToRotation(Vector3.up, normal);
                                        break;
                                    case ObjectInfo.Rotation.NormalAixY:
                                        rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0.0f, (float)(__random.NextDouble() * 360.0), 0.0f);
                                        break;
                                    case ObjectInfo.Rotation.All:
                                        rotation = Quaternion.Euler((float)(__random.NextDouble() * 360.0), (float)(__random.NextDouble() * 360.0), (float)(__random.NextDouble() * 360.0));
                                        break;
                                    default:
                                        rotation = Quaternion.identity;
                                        break;
                                }

                                if (objectInfo.minScale < objectInfo.maxScale)
                                {
                                    random = (float)__random.NextDouble();
                                    random *= objectInfo.maxScale - objectInfo.minScale;
                                    random += objectInfo.minScale;
                                }
                                else
                                    random = 1.0f;

                                LayerMask collideLayers = objectInfo.collideLayers, raycastLayers = objectInfo.raycastLayers;
                                int index = objectIndex.value, layerMask = ~objectInfo.ignoreMask;
                                float top = objectInfo.top, bottom = objectInfo.bottom, maxDistance = objectInfo.distance;
                                Vector3 down = -objectInfo.normal, finalScale = new Vector3(random, random, random);
                                Matrix4x4 matrix = Matrix4x4.TRS(finalPosition, rotation, finalScale);
                                Instantiate(new Instance(gameObject, x =>
                                {
                                    GameObject target = x as GameObject;
                                    if (target == null)
                                        return false;

                                    bool isCollide = false, isRaycast = false;
                                    int layer;
                                    Bounds collideBounds = default, raycastBounds = default, bounds;
                                    Transform transform;
                                    Mesh mesh;
                                    MeshFilter[] meshFilters = target.GetComponentsInChildren<MeshFilter>(true);
                                    foreach (MeshFilter meshFilter in meshFilters)
                                    {
                                        mesh = meshFilter.sharedMesh;
                                        if (mesh != null)
                                        {
                                            transform = meshFilter.transform;
                                            bounds = mesh.bounds;
                                            layer = 1 <<  meshFilter.gameObject.layer;
                                            if ((layer & collideLayers) != 0)
                                            {
                                                if (isCollide)
                                                    collideBounds.Encapsulate(transform.localToWorldMatrix.Multiply(bounds));
                                                else
                                                {
                                                    isCollide = true;

                                                    collideBounds = transform.localToWorldMatrix.Multiply(bounds);
                                                }
                                            }

                                            if ((layer & raycastLayers) != 0)
                                            {
                                                if (isRaycast)
                                                    raycastBounds.Encapsulate(transform.localToWorldMatrix.Multiply(bounds));
                                                else
                                                {
                                                    isRaycast = true;

                                                    raycastBounds = transform.localToWorldMatrix.Multiply(bounds);
                                                }
                                            }
                                        }
                                    }

                                    SkinnedMeshRenderer[] skinnedMeshRenderers = target.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                                    foreach (SkinnedMeshRenderer skinnedMeshRenderer in skinnedMeshRenderers)
                                    {
                                        transform = skinnedMeshRenderer.rootBone;
                                        if (transform != null)
                                        {
                                            bounds = skinnedMeshRenderer.bounds;
                                            layer = 1 << skinnedMeshRenderer.gameObject.layer;
                                            if ((layer & collideLayers) != 0)
                                            {
                                                if (isCollide)
                                                    collideBounds.Encapsulate(transform.localToWorldMatrix.Multiply(bounds));
                                                else
                                                {
                                                    isCollide = true;

                                                    collideBounds = transform.localToWorldMatrix.Multiply(bounds);
                                                }
                                            }

                                            if ((layer & raycastLayers) != 0)
                                            {
                                                if (isRaycast)
                                                    raycastBounds.Encapsulate(transform.localToWorldMatrix.Multiply(bounds));
                                                else
                                                {
                                                    isRaycast = true;

                                                    raycastBounds = transform.localToWorldMatrix.Multiply(bounds);
                                                }
                                            }
                                        }
                                    }

                                    BoxCollider[] colliders = target.GetComponentsInChildren<BoxCollider>(true);
                                    foreach (BoxCollider collider in colliders)
                                    {
                                        transform = collider.transform;
                                        bounds = new Bounds(collider.center, collider.size);
                                        layer = 1 << collider.gameObject.layer;
                                        if ((layer & collideLayers) != 0)
                                        {
                                            if (isCollide)
                                                collideBounds.Encapsulate(transform.localToWorldMatrix.Multiply(bounds));
                                            else
                                            {
                                                isCollide = true;

                                                collideBounds = transform.localToWorldMatrix.Multiply(bounds);
                                            }
                                        }

                                        if ((layer & raycastLayers) != 0)
                                        {
                                            if (isRaycast)
                                                raycastBounds.Encapsulate(transform.localToWorldMatrix.Multiply(bounds));
                                            else
                                            {
                                                isRaycast = true;

                                                raycastBounds = transform.localToWorldMatrix.Multiply(bounds);
                                            }
                                        }
                                    }

                                    Matrix4x4 worldToLocalMatrix = matrix.inverse;
                                    Vector3 distance = worldToLocalMatrix.MultiplyVector(down * -bottom), min = collideBounds.min, max = collideBounds.max;
                                    min = Vector3.Max(min, min + distance);
                                    max = Vector3.Min(max, max + distance);
                                    collideBounds.SetMinMax(Vector3.Min(min, max), Vector3.Max(min, max));
                                    collideBounds.Encapsulate(new Bounds(collideBounds.center + worldToLocalMatrix.MultiplyVector(down * -top), collideBounds.size));

                                    if (Physics.CheckBox(
                                            matrix.MultiplyPoint(collideBounds.center),
                                            Vector3.Scale(collideBounds.extents, matrix.lossyScale),
                                            matrix.rotation,
                                            layerMask))
                                        return false;

                                    if (maxDistance > 0.0f)
                                    {
                                        Vector3[] corners = new Vector3[8];
                                        raycastBounds.GetCorners(matrix,
                                            out corners[0],
                                            out corners[1],
                                            out corners[2],
                                            out corners[3],
                                            out corners[4],
                                            out corners[5],
                                            out corners[6],
                                            out corners[7]);

                                        Comparer comparer;
                                        comparer.forward = -down;
                                        Array.Sort(corners, comparer);

                                        if (!Physics.Raycast(corners[0], down, maxDistance, layerMask) ||
                                            !Physics.Raycast(corners[1], down, maxDistance, layerMask) ||
                                            !Physics.Raycast(corners[2], down, maxDistance, layerMask) ||
                                            !Physics.Raycast(corners[3], down, maxDistance, layerMask))
                                            return false;
                                    }

                                    return true;
                                }, x =>
                                {
                                    List<Node> nodes;
                                    lock (__nodes)
                                    {
                                        if(!__nodes.TryGetValue(nodeIndex, out nodes) || nodes == null)
                                        {
                                            nodes = new List<Node>();

                                            __nodes[nodeIndex] = nodes;
                                        }
                                    }

                                    Node node = new Node(index, x as GameObject);

                                    Transform transform = node.gameObject == null ? null : node.gameObject.transform;
                                    if (transform != null)
                                    {
                                        transform.position += finalPosition;
                                        transform.rotation = rotation * transform.rotation;
                                        transform.localScale = Vector3.Scale(finalScale, transform.localScale);
                                        transform.SetParent(root);
                                        
                                        Physics.SyncTransforms();
                                    }

                                    nodes.Add(node);

                                    /*lock (__nodes)
                                    {
                                        __nodes[offset] = node;
                                    }*/
                                }));

                                break;
                            }
                        }
                    }
                }
            }

            return result;
        }
        
        private static bool __Check(float x, float y, MapInfo[] mapInfos, MapFilter[] filters, out float result)
        {
            result = 1.0f;
            int numFilters = filters == null ? 0 : filters.Length;
            if (numFilters > 0)
            {
                int numMaps = mapInfos == null ? 0 : mapInfos.Length;
                if (numMaps > 0)
                {
                    float temp;
                    MapFilter filter;
                    for(int i = 0; i < numFilters; ++i)
                    {
                        filter = filters[i];
                        if (filter.index >= 0 && filter.index < numMaps)
                        {
                            temp = mapInfos[filter.index].Get(x, y);
                            if (temp < filter.min || temp > filter.max)
                            {
                                result = 0.0f;

                                return false;
                            }

                            temp = Mathf.Clamp01(Mathf.Abs(temp - filter.center) * filter.scale + filter.offset);

                            result *= filter.isInverce ? temp : 1.0f - temp;
                        }
                    }
                }
            }

            return true;
        }

        private static bool __Get(float x, float y, MapInfo[] mapInfos, MapFilter[] filters, out float result)
        {
            result = 0.0f;
            int numFilters = filters == null ? 0 : filters.Length;
            if (numFilters > 0)
            {
                int numMaps = mapInfos == null ? 0 : mapInfos.Length;
                if (numMaps > 0)
                {
                    float temp;
                    MapFilter filter;
                    for (int i = 0; i < numFilters; ++i)
                    {
                        filter = filters[i];
                        if (filter.index >= 0 && filter.index < numMaps)
                        {
                            temp = mapInfos[filter.index].Get(x, y);
                            if (temp < filter.min || temp > filter.max)
                            {
                                result = 0.0f;

                                return false;
                            }

                            result += Mathf.Abs(temp - filter.center) * filter.scale + filter.offset;
                        }
                    }
                }
            }

            return true;
        }

        private static float __GetHeight(int layerIndex, float x, float y, MapInfo[] mapInfos, LayerInfo[] layerInfos, out float layerHeight)
        {
            if (layerIndex < 0 || layerIndex >= layerInfos.Length)
            {
                layerHeight = 0.0f;

                return 0.0f;
            }

            float height = 0.0f;
            LayerInfo layerInfo = layerInfos[layerIndex];
            if (__Check(x, y, mapInfos, layerInfo.filters, out layerHeight))
            {
                layerHeight = (layerInfo.power > 0.0f ? Mathf.Pow(layerHeight, layerInfo.power) : layerHeight) * layerInfo.scale + layerInfo.offset;

                height += layerHeight;
            }

            float temp;
            height += __GetHeight(layerInfo.layerIndex, x, y, mapInfos, layerInfos, out temp);

            return height;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Serializable]
    public class FlatTerrain : FlatTerrain<ManifoldDC, ManifoldDC.Octree>
    {
        private ManifoldDC __engine;

        public override ManifoldDC engine
        {
            get
            {
                return __engine;
            }
        }

        public override IEngineBuilder Create(int depth, float increment, Vector3 scale, IEngineSampler sampler)
        {
            Debug.Log("Create");

            __engine = new ManifoldDC(depth, increment, scale, sampler);

            return new ManifoldDC.BoundsBuilder(__engine);
        }
    }
}