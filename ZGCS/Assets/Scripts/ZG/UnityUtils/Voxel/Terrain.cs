using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZG.Voxel
{
    [Serializable]
    public class FlatTerrain : ProcessorEx
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
        public struct MapFilter
        {
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
                    for(int i = 0; i < octaveCount; ++i)
                    {
                        if (i > 0)
                        {
                            frequency *= this.frequency;
                            amplitude *= persistence;
                        }

                        result += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
                    }

                    return result / octaveCount;
                }
                
                return Mathf.PerlinNoise(x, y);
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
            
            public float Get(PerlinNoise3 noise, float x, float y, float z)
            {
                return noise == null ? 0.0f : noise.Get(x * scale.x + offset.x, y * scale.y + offset.y, z * scale.z + offset.z);
            }
        }

        [Serializable]
        public struct LayerInfo
        {
#if UNITY_EDITOR
            public string name;
#endif
            [Index("materials", pathLevel = 1)]
            public int materialIndex;

            [Index("volumeInfos", emptyName = "无", pathLevel = 1)]
            public int volumeIndex;

            [Index("layerInfos", emptyName = "无", pathLevel = 1)]
            public int layer;

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
                All
            }

            public Rotation rotation;

            [Index("gameObjects", pathLevel = 1, uniqueLevel = 2)]
            public int index;

            public float chance;

            public float dot;

            public float offset;

            public Vector3 normal;
            
            [Index("materials", pathLevel = 1, uniqueLevel = 1)]
            public int[] materialFilters;

            public MapFilter[] mapFilters;
        }
        
        [Serializable]
        public struct LineInfo
        {
            public string name;

            [Index("materials", pathLevel = 1)]
            public int materialIndex;

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
        
        public new class Engine : ProcessorEx.Engine
        {
            private struct Chunk
            {
                public int index;
                public int count;

                public float height;
            }

            private class Liner
            {
                private System.Random __random;
                private NavPath __path;
                private LineInfo[] __lineInfos;
                private List<Vector3> __points;
                private List<Vector3Int> __triangles;
                private List<int>[] __lines;

                public Liner(System.Random random)
                {
                    __random = random;
                }
                
                public void Create(LineInfo[] lineInfos)
                {
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

                public void Do(float increment, Vector3Int position, Vector3Int size, ProcessorEx.Engine engine, Chunk[] chunks)
                {
                    if (engine == null)
                        return;

                    if (__path != null)
                        __path.Clear();

                    int numLineInfos = __lineInfos == null ? 0 : __lineInfos.Length, numPoints, fromPointIndex, toPointIndex, depth, i, j;

                    float fromHeight, toHeight;
                    Vector2Int fromPoint, toPoint;
                    Vector3Int source, destination;
                    Vector3 offset, scale = engine.scale, temp;
                    LineInfo lineInfo;
                    List<int> line;
                    Func<Vector2, float> heightGetter = x =>
                    {
                        x.x /= scale.x;
                        x.y /= scale.y;

                        x.x -= position.x;
                        x.y -= position.z;

                        return chunks[Mathf.RoundToInt(x.x) + Mathf.RoundToInt(x.y) * size.x].height;
                    };

                    for (i = 0; i < numLineInfos; ++i)
                    {
                        line = __lines[i];
                        numPoints = line == null ? 0 : line.Count;
                        if (numPoints < 2)
                            continue;

                        lineInfo = __lineInfos[i];

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
                                destination = Vector3Int.Max(source, size);
                                if (source != destination)
                                    __path = new NavPath(destination);
                            }

                            toPointIndex = line[j];
                            toPoint = new Vector2Int(toPointIndex % size.x, toPointIndex / size.x);
                            toHeight = chunks[toPointIndex].height;

                            depth = __path.Search(
                                NavPath.Type.Min,
                                int.MaxValue,
                                0,
                                lineInfo.minDentity, 
                                lineInfo.maxDentity, 
                                lineInfo.minExtends, 
                                lineInfo.maxExtends,
                                position,
                                new Vector3Int(fromPoint.x, Mathf.CeilToInt(fromHeight / scale.y) - position.y, fromPoint.y),
                                new Vector3Int(toPoint.x, Mathf.CeilToInt(toHeight / scale.y) - position.y, toPoint.y), 
                                engine);
                            
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

                                    offset = engine.ApproximateZeroCrossingPosition(offset, new Vector3(offset.x, offset.y - scale.y, offset.z), increment);

                                    if (__points == null)
                                        __points = new List<Vector3>();

                                    __points.Add(offset);
                                }
                            }
                            else
                            {
                                __Do(
                                    lineInfo.materialIndex, 
                                    lineInfo.halfWidth, 
                                    lineInfo.radius, 
                                    lineInfo.countPerUnit, 
                                    temp, 
                                    engine, 
                                    lineInfo.gameObject,
                                    heightGetter);
                                
                                temp = new Vector3(
                                    (fromPoint.x + position.x) * scale.x,
                                    fromHeight,
                                    (fromPoint.y + position.z) * scale.z);
                            }
                        }

                        __Do(
                            lineInfo.materialIndex,
                            lineInfo.halfWidth, 
                            lineInfo.radius, 
                            lineInfo.countPerUnit, 
                            temp, 
                            engine, 
                            lineInfo.gameObject,
                            heightGetter);
                    }
                }

                private void __Do(
                    int materialIndex, 
                    float halfWidth, 
                    float radius, 
                    float pointCount,
                    Vector3 position, 
                    ProcessorEx.Engine engine, 
                    GameObject gameObject, 
                    Func<Vector2, float> heightGetter)
                {
                    int numPoints = __points == null ? 0 : __points.Count;
                    if (numPoints < 2)
                        return;

                    int i, count = numPoints - 1;
                    Vector2 min = new Vector2(position.x, position.z), max = min, temp;
                    Vector3 point;
                    //Quaternion rotation;
                    for (i = 1; i < count; ++i)
                    {
                        point = __points[i];

                        //rotation = Quaternion.FromToRotation(Vector3.forward, to - from);

                        engine.Do(point, radius, materialIndex);

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

                    if (__triangles != null)
                        __triangles.Clear();

                    delaunay.DeleteFrames(triangle =>
                    {
                        if (triangle.x < 4 || triangle.y < 4 || triangle.z < 4)
                            return true;
                        
                        int vertexX = triangle.x & 1, vertexY = triangle.y & 1, vertexZ = triangle.z & 1;
                        if (vertexX == vertexY && vertexY == vertexZ)
                            return true;
                        
                        if (__triangles == null)
                            __triangles = new List<Vector3Int>();

                        __triangles.Add(triangle);

                        return false;
                    });

                    if(__triangles != null)
                    {
                        Vector2 z;
                        pointCount = Mathf.RoundToInt(pointCount * (size.x * size.y));
                        for(i = 0; i < pointCount; ++i)
                        {
                            temp = new Vector2((float)(__random.NextDouble() * size.x + min.x), (float)(__random.NextDouble() * size.y + min.y));
                            foreach(Vector3Int triangle in __triangles)
                            {
                                if (!delaunay.Get(triangle.x, out x))
                                    continue;

                                if (!delaunay.Get(triangle.y, out y))
                                    continue;

                                if (!delaunay.Get(triangle.z, out z))
                                    continue;
                                
                                if (Delaunay.Cross(temp - x, y - x) > 0.0f && Delaunay.Cross(temp - y, z - y) > 0.0f && Delaunay.Cross(temp - z, x - z) > 0.0f)
                                {
                                    delaunay.AddPoint(temp);

                                    break;
                                }
                            }
                        }
                    }

                    gameObject = gameObject == null ? new GameObject() : UnityEngine.Object.Instantiate(gameObject);
                    MeshFilter meshFilter = gameObject == null ? null : gameObject.AddComponent<MeshFilter>();
                    if(meshFilter != null)
                        meshFilter.sharedMesh = delaunay.ToMesh(null, true, heightGetter);

                    if (__points != null)
                        __points.Clear();
                }

                private float __GetLineHeight(Vector2 pointToBuild)
                {
                    int numPoints = __points == null ? 0 : __points.Count, source = numPoints, destination;
                    float minDistance = float.MaxValue, distance;
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

                    return 0.0f;
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
            
            public IBuilder builder;

            private int __noiseSize;
            
            private Vector2Int __mapSize;

            private System.Random __random;
            private PerlinNoise3 __noise;
            private Liner __liner;
            //private float[] __results;
            private float[] __layers;
            private List<ProcessorEx.Block> __blocks;
            private Dictionary<Vector2Int, Chunk[]> __chunks = new Dictionary<Vector2Int, Chunk[]>();
            
            public Engine(int noiseSize, int depth, Vector3 scale, Vector3 offset, Vector2Int mapSize, System.Random random) : base(depth, scale, offset)
            {
                __noiseSize = noiseSize;
                
                __mapSize = mapSize;

                __random = random;
            }

            public float Get(int volumeIndex, Vector3 point, VolumeInfo[] volumeInfos)
            {
                if (volumeIndex < 0)
                    return -1.0f;

                int numVolumeInfos = volumeInfos == null ? 0 : volumeInfos.Length;
                if (volumeIndex >= numVolumeInfos)
                    return -1.0f;

                if (__noise == null)
                {
                    PerlinNoise3.RandomValue[] randomValues = new PerlinNoise3.RandomValue[__noiseSize];
                    for (int i = 0; i < __noiseSize; ++i)
                        randomValues[i] = new PerlinNoise3.RandomValue((float)__random.NextDouble(), (float)__random.NextDouble());

                    __noise = new PerlinNoise3(randomValues);
                }

                VolumeInfo volumeInfo = volumeInfos[volumeIndex];
                return volumeInfo.Get(__noise, point.x, point.y, point.z);// Mathf.Clamp(volumeInfo.Get(__noise, point.x, point.y, point.z) / (volumeInfo.scale.y * scale.y), -1.0f, 1.0f);
            }

            /*public bool Create(float increment, Vector2Int position, MapInfo[] mapInfos, VolumeInfo[] volumeInfos, HeightInfo[] heightInfos, LineInfo[] riverInfos)
            {
                if (__chunks == null)
                    __chunks = new Dictionary<Vector2Int, Chunk[]>();

                if (__chunks.ContainsKey(position))
                    return false;

                int numHeightInfos = heightInfos == null ? 0 : heightInfos.Length;
                if (__results == null || __results.Length < numHeightInfos)
                    __results = new float[numHeightInfos];
                
                int size = __mapSize.x * __mapSize.y, index = 0, min = int.MaxValue, max = int.MinValue, i, j, k, l;
                float height, result, source, destination, temp;
                Vector3 scale = base.scale, point;
                HeightInfo heightInfo;
                Chunk chunk;
                ProcessorEx.Block block;
                Chunk[] chunks = new Chunk[size];

                __chunks[position] = chunks;

                position = Vector2Int.Scale(position, __mapSize);

                float x = position.x * scale.x, y = 1.0f / scale.y, length = y * scale.y;
                point.z = position.y * scale.z;
                for (i = 0; i < __mapSize.y; ++i)
                {
                    point.x = x;
                    for (j = 0; j < __mapSize.x; ++j)
                    {
                        if (__blocks == null)
                            __blocks = new List<ProcessorEx.Block>(size * (1 << depth));

                        chunk.index = __blocks.Count;
                        chunk.count = 0;

                        point.y = 0.0f;

                        height = 0.0f;
                        for (k = 0; k < numHeightInfos; ++k)
                        {
                            heightInfo = heightInfos[k];
                            if (__Check(point.x, point.z, mapInfos, heightInfo.filters, out result))
                            {
                                result = (heightInfo.power > 0.0f ? Mathf.Pow(result, heightInfo.power) : result) * heightInfo.scale + heightInfo.offset;
                                result = result + (heightInfo.layer > 0 ? __results[heightInfo.layer] : 0.0f);
                                
                                if (heightInfo.depth > 0)
                                {
                                    source = result - scale.y * heightInfo.depth;
                                    if (point.y <= source)
                                    {
                                        destination = __blocks[chunk.index + chunk.count - 1].density + length;
                                        temp = (source - point.y) * y;
                                        do
                                        {
                                            block = new ProcessorEx.Block(
                                                    heightInfo.materialIndex,
                                                    Mathf.Clamp(Mathf.Min(destination, temp), -1.0f, 1.0f));
                                            
                                            l = chunk.index + chunk.count;
                                            if (l < __blocks.Count)
                                            {
                                                block.density = Mathf.Min(block.density, __blocks[l].density);

                                                __blocks[l] = block;
                                            }
                                            else
                                                __blocks.Add(block);

                                            ++chunk.count;

                                            point.y += scale.y;
                                            
                                            destination += length;
                                            
                                            temp -= length;
                                        } while (point.y <= source) ;

                                        temp = block.density - length;
                                        if (temp > -1.0f)
                                        {
                                            l = chunk.index + chunk.count;
                                            do
                                            {
                                                block = new ProcessorEx.Block(
                                                       heightInfo.materialIndex,
                                                       Mathf.Clamp(temp, -1.0f, 1.0f));
                                                
                                                if (l < __blocks.Count)
                                                {
                                                    block.density = Mathf.Min(block.density, __blocks[l].density);

                                                    __blocks[l] = block;
                                                }
                                                else
                                                    __blocks.Add(block);

                                                ++l;

                                                temp -= length;
                                            } while (temp > -1.0f);
                                        }

                                        height = source;
                                    }
                                    else
                                    {
                                        //temp += scale.y;

                                        l = Mathf.FloorToInt((point.y - source) / scale.y);
                                        if (l > chunk.count)
                                        {
                                            source += (l - chunk.count) * scale.y;

                                            l = chunk.count;
                                        }

                                        if (result < height)
                                        {
                                            temp = source;
                                            l = chunk.index + chunk.count - l;
                                            while (temp < result)
                                            {
                                                if (temp > point.y)
                                                    break;

                                                block = __blocks[l];
                                                block.materialIndex = heightInfo.materialIndex;
                                                __blocks[l] = block;

                                                ++l;

                                                temp += scale.y;
                                            }
                                        }
                                        else
                                        {
                                            point.y -= l * scale.y;

                                            chunk.count -= l;

                                            height = source;
                                        }
                                    }
                                }

                                if (result >= height)
                                {
                                    source = height > 0.0f ? (height - point.y) * y : -1.0f;
                                    height = chunk.count > 0 ? __blocks[chunk.index + chunk.count - 1].density : -1.0f;
                                    destination = length;

                                    temp = (point.y - result) * y;
                                    while (point.y <= result)
                                    {
                                        block = new ProcessorEx.Block(
                                            heightInfo.materialIndex,
                                            Mathf.Clamp(Get(heightInfo.volumeIndex, point, volumeInfos), temp, -temp));
                                        
                                        block.density = Mathf.Clamp(block.density, Mathf.Max(source, height - destination), height + destination);
                                        if (block.density > -1.0f && min > chunk.count)
                                            min = chunk.count;

                                        l = chunk.index + chunk.count;
                                        if (l < __blocks.Count)
                                        {
                                            block.density = Mathf.Min(block.density, __blocks[l].density);

                                            __blocks[l] = block;
                                        }
                                        else
                                            __blocks.Add(block);
                                        
                                        ++chunk.count;

                                        point.y += scale.y;

                                        source -= length;

                                        destination += length;

                                        temp += length;
                                    }

                                    if (temp < 1.0f)
                                    {
                                        l = chunk.index + chunk.count;
                                        do
                                        {
                                            block = new ProcessorEx.Block(
                                                   heightInfo.materialIndex,
                                                   Mathf.Max(Get(heightInfo.volumeIndex, point, volumeInfos), temp));
                                            
                                            block.density = Mathf.Clamp(block.density, Mathf.Max(source, height - destination), height + destination);
                                            if (l < __blocks.Count)
                                            {
                                                block.density = Mathf.Min(block.density, __blocks[l].density);

                                                __blocks[l] = block;
                                            }
                                            else
                                                __blocks.Add(block);

                                            ++l;

                                            point.y += scale.y;

                                            source -= length;

                                            destination += length;

                                            temp += length;
                                        } while (temp < 1.0f);

                                        point.y = chunk.count * scale.y;
                                    }

                                    height = result;
                                }
                            }
                            
                            __results[k] = result;
                        }

                        chunk.count = __blocks.Count - chunk.index;
                        max = Mathf.Max(max, chunk.count);

                        chunk.height = height;

                        chunks[index++] = chunk;
                        
                        point.x += scale.x;
                    }

                    point.z += scale.z;
                }

                if (min <= max)
                {
                    i = max - min;

                    if (riverInfos != null)
                    {
                        Vector2Int from = new Vector2Int(UnityEngine.Random.Range(0, __mapSize.x), UnityEngine.Random.Range(0, __mapSize.y)),
                            to = new Vector2Int(UnityEngine.Random.Range(0, __mapSize.x), UnityEngine.Random.Range(0, __mapSize.y));

                        chunk = chunks[from.x + from.y * __mapSize.x];
                        Vector3Int origin = new Vector3Int(from.x, chunk.count - min - 1, from.y);
                        NavPath path = new NavPath(
                            1.0f, 
                            -0.4f, 
                            new Vector3Int(1, 1, 1), 
                            new Vector3Int(3, 5, 3), 
                            new Vector3Int(__mapSize.x, i, __mapSize.y), 
                            new Vector3Int(position.x, min, position.y), 
                            this);
                        j = path.Search(
                            NavPath.Type.Min,
                            int.MaxValue,
                            0,
                            origin,
                            new Vector3Int(to.x, chunks[to.x + to.y * __mapSize.x].count - min - 1, to.y));

                        if (j > 0)
                        {
                            Vector3 current;
                            List<Vector3> points = new List<Vector3>();
                            List<Vector2> lines = new List<Vector2>();
                            points.Add(new Vector3(
                                (origin.x + position.x) * scale.x,
                                chunk.height,
                                (origin.z + position.y) * scale.z));

                            lines.Add(new Vector2((origin.x + position.x) * scale.x, (origin.z + position.y) * scale.z));

                            foreach (Vector3Int pathPoint in path)
                            {
                                chunk = chunks[pathPoint.x + pathPoint.z * __mapSize.x];

                                current = new Vector3(
                                    (pathPoint.x + position.x) * scale.x,
                                    (pathPoint.y + min) * scale.y,
                                    (pathPoint.z + position.y) * scale.z);

                                current = ApproximateZeroCrossingPosition(current, new Vector3(current.x, current.y - scale.y, current.z), increment);
                                
                                lines.Add(new Vector2(current.x, current.z));
                                
                                points.Add(current);
                            }

                            int numPoints = points == null ? 0 : points.Count;
                            Delaunay delaunay = new Delaunay(lines.ToArray(), 4.0f, 10);
                            GameObject gameObject = new GameObject();
                            gameObject.AddComponent<MeshFilter>().sharedMesh = delaunay.ToMesh(null, true, pointToBuild =>
                            {
                                float minDistance = float.MaxValue, distance;
                                Vector3 pointToCheck;
                                j = numPoints;
                                for(k = 0; k < numPoints; ++k)
                                {
                                    pointToCheck = points[k];

                                    distance = (new Vector2(pointToCheck.x, pointToCheck.z) - pointToBuild).sqrMagnitude;
                                    if(distance < minDistance)
                                    {
                                        minDistance = distance;

                                        j = k;
                                    }
                                }

                                if (j < numPoints)
                                {
                                    minDistance = int.MaxValue;
                                    if (j > 0)
                                    {
                                        k = j - 1;

                                        pointToCheck = points[k];

                                        minDistance = (new Vector2(pointToCheck.x, pointToCheck.z) - pointToBuild).sqrMagnitude;
                                    }

                                    if(j < numPoints - 1)
                                    {
                                        pointToCheck = points[j + 1];

                                        distance = (new Vector2(pointToCheck.x, pointToCheck.z) - pointToBuild).sqrMagnitude;
                                        if(distance < minDistance)
                                            k = j + 1;
                                    }

                                    Vector3 start = points[j], end = points[k], normal = end - start;

                                    return Mathf.Lerp(start.y, end.y, Vector3.Project(new Vector3(pointToBuild.x - start.x, 0.0f, pointToBuild.y), normal).magnitude / normal.magnitude);
                                }

                                return 0.0f;
                            });

                            gameObject.AddComponent<MeshRenderer>();

                            Vector3 previous = points[0];
                            Quaternion rotation;
                            for(k = 1; k < numPoints; ++k)
                            {
                                current = points[k];
                                
                                //Debug.DrawRay(previous, current - previous, Color.red, 1000.0f);

                                rotation = Quaternion.FromToRotation(Vector3.forward, current - previous);

                                //UnityEngine.Object.Instantiate(riverInfos[0].gameObject, current + new Vector3(0f, -2.0f, 0.0f), rotation);

                                //Do(new Bounds(current, new Vector3(2f, 4f, 2f)), rotation, 1);
                                Do(current, 3.0f);
                                
                                previous = current;
                            }
                        }
                    }

                    if (builder != null)
                        builder.Set(new BoundsInt(new Vector3Int(position.x, min, position.y), new Vector3Int(__mapSize.x, max - min, __mapSize.y)));
                }

                return true;
            }*/

            public bool Create(float increment, Vector2Int position, MapInfo[] mapInfos, VolumeInfo[] volumeInfos, LayerInfo[] layerInfos, LineInfo[] lineInfos)
            {
                lock (__chunks)
                {
                    if (__chunks.ContainsKey(position))
                        return false;

                    bool result;
                    int size = __mapSize.x * __mapSize.y, index = 0, min = int.MaxValue, max = int.MinValue, i, j, k, l;
                    float previous, current, next, middle, temp;
                    Vector3 scale = base.scale, point;
                    LayerInfo layerInfo;
                    Chunk chunk;
                    ProcessorEx.Block block;
                    Chunk[] chunks = new Chunk[size];

                    __chunks[position] = chunks;

                    int numLayerInfos = layerInfos == null ? 0 : layerInfos.Length;
                    if (__layers == null || __layers.Length < numLayerInfos)
                        __layers = new float[numLayerInfos];

                    if (__liner == null)
                        __liner = new Liner(__random);

                    __liner.Create(lineInfos);

                    position = Vector2Int.Scale(position, __mapSize);

                    float x = position.x * scale.x, y, length;
                    point.z = position.y * scale.z;
                    for (i = 0; i < __mapSize.y; ++i)
                    {
                        point.x = x;
                        for (j = 0; j < __mapSize.x; ++j)
                        {
                            if (__blocks == null)
                                __blocks = new List<ProcessorEx.Block>(size * (1 << depth));

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
                                    current = current + (layerInfo.layer > 0 ? __layers[layerInfo.layer] : 0.0f);

                                    if (layerInfo.max > 0.0f)
                                        current = Mathf.Min(current, layerInfo.max);

                                    if (layerInfo.depth > 0)
                                    {
                                        next = current - scale.y * layerInfo.depth;
                                        if (point.y > next)
                                        {
                                            //temp += scale.y;

                                            l = Mathf.FloorToInt((point.y - next) / scale.y);
                                            if (l > chunk.count)
                                            {
                                                next += (l - chunk.count) * scale.y;

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

                                                    temp += scale.y;
                                                }
                                            }
                                            else
                                            {
                                                point.y -= l * scale.y;

                                                chunk.count -= l;

                                                previous = next;
                                            }
                                        }
                                        else
                                        {
                                            length = next - previous;

                                            y = length * 0.5f;

                                            middle = previous + y;

                                            length = 1.0f / y;

                                            result = false;
                                            while (point.y <= next)
                                            {
                                                block = new ProcessorEx.Block(
                                                    layerInfo.materialIndex,
                                                    Mathf.Max(Get(layerInfo.volumeIndex, point, volumeInfos), 1.0f - Mathf.Abs(point.y - middle) * length));

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

                                                point.y += scale.y;
                                            }

                                            previous = next;
                                        }
                                    }

                                    if (current >= previous)
                                    {
                                        length = current - previous;
                                        if (previous > 0.0f)
                                        {
                                            temp = point.y - scale.y;
                                            /*if (current - temp < point.y - current)
                                            {
                                                point.y = temp;

                                                --chunk.count;
                                            }*/

                                            y = length * 0.5f;

                                            middle = previous + y;

                                            length = 1.0f / y;

                                            l = chunk.index + chunk.count;
                                            previous = previous - y;
                                            do
                                            {
                                                --l;
                                                point.y -= scale.y;

                                                temp = Mathf.Max(Get(layerInfo.volumeIndex, point, volumeInfos), Mathf.Abs(point.y - middle) * length - 1.0f);

                                                block = __blocks[l];

                                                if (block.density > temp)
                                                {
                                                    block.density = temp;

                                                    __blocks[l] = block;
                                                }
                                                else
                                                    break;
                                            } while (l > chunk.index && point.y > previous);

                                            point.y = scale.y * chunk.count;
                                        }
                                        else
                                        {
                                            y = length;

                                            middle = 0.0f;

                                            length = 1.0f / y;
                                        }

                                        result = false;
                                        while (point.y <= current)
                                        {
                                            block = new ProcessorEx.Block(
                                                layerInfo.materialIndex,
                                                Mathf.Max(Get(layerInfo.volumeIndex, point, volumeInfos), Mathf.Abs(point.y - middle) * length - 1.0f));

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

                                            point.y += scale.y;
                                        }

                                        l = chunk.index + chunk.count;

                                        previous = current + y;
                                        while (point.y < previous)
                                        {
                                            block = new ProcessorEx.Block(
                                                layerInfo.materialIndex,
                                                Mathf.Max(Get(layerInfo.volumeIndex, point, volumeInfos), Mathf.Abs(point.y - middle) * length - 1.0f));

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

                                            point.y += scale.y;
                                        } 

                                        if (result)
                                        {
                                            while (l < __blocks.Count)
                                            {
                                                block = new ProcessorEx.Block(
                                                       layerInfo.materialIndex,
                                                       Mathf.Max(Get(layerInfo.volumeIndex, point, volumeInfos), Mathf.Abs(point.y - middle) * length - 1.0f));

                                                __blocks[l] = block;

                                                ++l;

                                                point.y += scale.y;
                                            }
                                        }

                                        point.y = scale.y * chunk.count;

                                        previous = current;
                                    }
                                }

                                __layers[k] = previous;
                            }

                            __liner.Set(index, point.x, point.z, mapInfos);

                            chunk.count = __blocks.Count - chunk.index;

                            max = Mathf.Max(max, chunk.count);

                            chunk.height = previous;

                            chunks[index++] = chunk;

                            point.x += scale.x;
                        }

                        point.z += scale.z;
                    }

                    if (min <= max)
                    {
                        Vector3Int offset = new Vector3Int(position.x, min, position.y), extends = new Vector3Int(__mapSize.x, max - min, __mapSize.y);

                        __liner.Do(increment, offset, extends, this, chunks);

                        if (builder != null)
                            builder.Set(new BoundsInt(offset, extends));
                    }
                }
                return true;
            }

            public override bool Get(Vector3Int position, out ProcessorEx.Block block)
            {
                if (position.y < -1)
                {
                    block = new ProcessorEx.Block(0, -1.0f);

                    return true;
                }

                if (__chunks != null)
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
                            if(position.y < 0)
                            {
                                if (chunk.count > 0)
                                {
                                    block = __blocks[chunk.index];
                                    block.density = Mathf.Max(block.density - 1.0f, -1.0f);

                                    return true;
                                }

                                block = new ProcessorEx.Block(0, -1.0f);
                            }
                            else if(chunk.count > position.y)
                                block = __blocks[chunk.index + position.y];
                            else
                            {
                                block = __blocks[chunk.index + chunk.count - 1];
                                
                                block.density = (position.y * scale.y - chunk.height) / chunk.height;
                            }
                            
                            return true;
                        }
                    }
                }
                
                block = default(ProcessorEx.Block);

                return false;
            }

            public override bool Set(Vector3Int position, ProcessorEx.Block block)
            {
                if (position.y < 0)
                    return false;

                if (__chunks != null)
                {
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
                                if(builder != null && !Mathf.Approximately(__blocks[index].density, block.density))
                                    builder.Set(position);

                                __blocks[index] = block;

                                return true;
                            }
                        }
                    }
                }
                
                return false;
            }
        }

        public int seed = 186;

        public int noiseSize;
        
        public Vector2Int mapSize;
        
        [HideInInspector]
        public Transform root;

        public MapInfo[] mapInfos;
        public VolumeInfo[] volumeInfos;
        [UnityEngine.Serialization.FormerlySerializedAs("heightInfos")]
        public LayerInfo[] layerInfos;
        public ObjectInfo[] objectInfos;
        public LineInfo[] lineInfos;

        public Material[] materials;
        public GameObject[] gameObjects;

        private System.Random __random;
        private Dictionary<Vector3Int, Node> __nodes = new Dictionary<Vector3Int, Node>();

        public IEnumerable<KeyValuePair<Vector3Int, Node>> nodes
        {
            get
            {
                return __nodes;
            }
        }
        
        public FlatTerrain()
        {
            tileProcessor = __GetMaterialIndex;
        }

        public override DualContouring Create(Vector3Int world, float increment)
        {
            DualContouring.IBuilder builder = base.builder;
            Engine engine = builder == null ? null : builder.parent as Engine;
            if(engine != null)
            {
                int i, j, size = (1 << depth) + 1, mask = size - 2;
                Vector2Int min = new Vector2Int(world.x * mask - 1, world.z * mask - 1), max = min + new Vector2Int(size, size), position, offset;

                offset = new Vector2Int(min.x / mapSize.x, min.y / mapSize.y);
                position = min - Vector2Int.Scale(offset, mapSize);
                if(position.x < 0)
                    --offset.x;

                if (position.y < 0)
                    --offset.y;

                min = offset;
                max = new Vector2Int(max.x / mapSize.x, max.y / mapSize.y);

                /*offset = new Vector2Int(max.x / mapSize.x, max.y / mapSize.y);
                position = max - Vector2Int.Scale(offset, mapSize);
                if (position.x > 0)
                    ++offset.x;

                if (position.y > 0)
                    ++offset.y;

                max = offset;*/
                
                for (i = min.x; i <= max.x; ++i)
                {
                    for(j = min.y; j <= max.y; ++j)
                        engine.Create(increment, new Vector2Int(i, j), mapInfos, volumeInfos, layerInfos, lineInfos);
                }
            }
            
            return base.Create(world, increment);
        }

        public override GameObject Convert(MeshData<Bounds> meshData)
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

            MeshCollider meshCollider = gameObject.AddComponent<MeshCollider>();
            if (meshCollider != null)
                meshCollider.sharedMesh = mesh;

            return gameObject;
        }

        public override DualContouring.IBuilder Create(int depth, Vector3 scale)
        {
            if (__random == null)
                __random = new System.Random(seed);

            Engine node = new Engine(noiseSize, depth, scale, Vector3.zero, mapSize, __random);
            node.builder = new DualContouring.BoundsBuilder(node);
            return node.builder;
        }
        
        private int __GetMaterialIndex(
            DualContouring.Octree.Info x,
            DualContouring.Octree.Info y,
            DualContouring.Octree.Info z,
            DualContouring.Octree.Info w,
            DualContouring.Axis axis,
            Vector3Int offset,
            DualContouring.Octree octree)
        {
            if (octree == null)
                return -1;

            DualContouring.IBuilder builder = base.builder;
            Engine engine = builder == null ? null : builder.parent as Engine;
            if (engine == null)
                return -1;
            
            Vector3 scale = base.scale, position = octree.offset + Vector3.Scale(offset, scale);
            offset = Vector3Int.RoundToInt(new Vector3(position.x / scale.x, position.y / scale.y, position.z / scale.z));
            Block block;
            if (!engine.Get(offset, out block))
            {
                Debug.Log("wtf?");

                return -1;
            }

            lock(__nodes)
            {
                if (__nodes.ContainsKey(offset))
                    return block.materialIndex;
            }

            if (objectInfos != null)
            {
                Vector3 point;
                if (!octree.Get(x, out point))
                    return block.materialIndex;

                DualContouring.Block a, b, c, d;
                if (!octree.Get(x, out a) || !octree.Get(y, out b) || !octree.Get(z, out c) || !octree.Get(w, out d))
                    return block.materialIndex;
                
                lock (__nodes)
                {
                    __nodes[offset] = new Node(-1, null);
                }
                
                int numGameObjects = gameObjects == null ? 0 : gameObjects.Length;
                float chance = 0.0f, random = (float)__random.NextDouble(), temp;
                Vector3 normal;
                Plane plane;
                GameObject gameObject;
                foreach (ObjectInfo objectInfo in objectInfos)
                {
                    chance += objectInfo.chance;

                    if (chance > random && (objectInfo.materialFilters == null || objectInfo.materialFilters.Length < 1 || Array.IndexOf(objectInfo.materialFilters, block.materialIndex) != -1))
                    {
                        normal = (a.normal + b.normal + c.normal + d.normal).normalized;
                        if (Vector3.Dot(normal, objectInfo.normal) > objectInfo.dot && __Check(position.x, position.z, mapInfos, objectInfo.mapFilters, out temp) && temp > (float)__random.NextDouble())
                        {
                            gameObject = objectInfo.index >= 0 && objectInfo.index < numGameObjects ? gameObjects[objectInfo.index] : null;
                            if (gameObject == null)
                                continue;
                            
                            int index = objectInfo.index;
                            
                            plane = new Plane(normal, point);
                            Vector3 finalPosition = plane.ClosestPointOnPlane(position) + objectInfo.normal * objectInfo.offset;

                            Quaternion rotation;
                            switch (objectInfo.rotation)
                            {
                                case ObjectInfo.Rotation.AixY:
                                    rotation = Quaternion.Euler(0.0f, (float)(__random.NextDouble() * 360.0), 0.0f);
                                    break;
                                case ObjectInfo.Rotation.All:
                                    rotation = Quaternion.Euler((float)(__random.NextDouble() * 360.0), (float)(__random.NextDouble() * 360.0), (float)(__random.NextDouble() * 360.0));
                                    break;
                                default:
                                    rotation = Quaternion.identity;
                                    break;
                            }

                            Instantiate(new Instance(gameObject, instance =>
                            {
                                GameObject target = instance as GameObject;
                                if (target == null)
                                    return false;
                                List<Collider> colliders = new List<Collider>();

                                target.GetComponentsInChildren(colliders);
                                foreach (Collider collider in colliders)
                                {
                                    Transform transform = collider == null ? null : collider.transform;
                                    if (transform != null)
                                    {
                                        Bounds bounds = collider.bounds;
                                        if (Physics.CheckBox(transform.position + bounds.center, Vector3.Scale(bounds.extents, transform.lossyScale), transform.rotation))
                                            return false;
                                    }
                                }

                                return true;
                            }, instance =>
                            {
                                Node node = new Node(index, instance as GameObject);

                                Transform transform = node.gameObject == null ? null : node.gameObject.transform;
                                if (transform != null)
                                {
                                    transform.position = finalPosition;
                                    transform.rotation = rotation;
                                    transform.SetParent(root);
                                }
                                
                                lock (__nodes)
                                {
                                    __nodes[offset] = node;
                                }
                            }));

                            break;
                        }
                    }

                    if (chance >= 1.0f)
                    {
                        chance = 0.0f;

                        random = (float)__random.NextDouble();
                    }
                }
            }

            return block.materialIndex;
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
                            
                            result *= 1.0f - Mathf.Clamp01(Mathf.Abs(temp - filter.center) * filter.scale + filter.offset);
                        }
                    }
                }
            }

            return true;
        }
    }
}