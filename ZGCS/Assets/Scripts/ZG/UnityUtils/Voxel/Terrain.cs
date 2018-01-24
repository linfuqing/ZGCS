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

                public void Do(float increment, Vector3Int position, Vector3Int size, Engine engine, Chunk[] chunks, Action<Instance> instantiate)
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
                    for (i = 0; i < numLineInfos; ++i)
                    {
                        line = __lines[i];
                        numPoints = line == null ? 0 : line.Count;
                        if (numPoints < 2)
                            continue;

                        for(j = 1; j < numPoints; ++j)
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
                                    instantiate);

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
                            instantiate);
                    }
                }

                private void __Do(
                    int materialIndex, 
                    float halfWidth, 
                    float radius, 
                    float pointCount,
                    Vector3 position, 
                    Engine engine, 
                    GameObject gameObject, 
                    Action<Instance> instantiate)
                {
                    int numPoints = __points == null ? 0 : __points.Count;
                    if (numPoints < 2 || instantiate == null)
                        return;

                    int segment = 50;
                    float orginRadius = 30.0f;
                    float doRadius = 25.0f;

                    engine.Do(position, doRadius);
                    
                    int i, count = numPoints - 1;
                    Vector2 min = new Vector2(position.x - orginRadius, position.z - orginRadius), max = new Vector2(position.x + orginRadius, position.z + orginRadius), temp;
                    Vector3 point;
                    //Quaternion rotation;
                    for (i = 1; i < count; ++i)
                    {
                        point = __points[i];

                        //rotation = Quaternion.FromToRotation(Vector3.forward, to - from);

                        engine.Do(point, radius);

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

                    float anglePerSegment = Mathf.PI * 2.0f / segment, angle = 0.0f;
                    for(i = 0; i < segment; ++i)
                    {
                        delaunay.AddPoint(new Vector2(position.x + orginRadius * Mathf.Cos(angle), position.z + orginRadius * Mathf.Sin(angle)));

                        angle += anglePerSegment;
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
                                if(triangle.x < index && triangle.y < index)
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
                                
                                if ((temp - x).Cross(y - x) > 0.0f && (temp - y).Cross(z - y) > 0.0f && (temp - z).Cross(x - z) > 0.0f)
                                {
                                    delaunay.AddPoint(temp);

                                    break;
                                }
                            }
                        }
                    }

                    float height = position.y;
                    for(i = 0; i < numPoints; ++i)
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
                         
                         if (engine == null || engine.__chunks == null)
                             return 0.0f;

                         Vector3 scale = engine.scale;
                         Vector2Int 
                         offset = new Vector2Int(Mathf.RoundToInt(x.x / scale.x), Mathf.RoundToInt(x.y / scale.z)), 
                         world = new Vector2Int(offset.x / engine.__mapSize.x, offset.y / engine.__mapSize.y),
                         local = Vector2Int.Scale(world, engine.__mapSize);

                         local.x = offset.x - local.x;
                         local.y = offset.y - local.y;

                         if (local.x < 0)
                         {
                             local.x += engine.__mapSize.x;

                             --world.x;
                         }

                         if (local.y < 0)
                         {
                             local.y += engine.__mapSize.y;

                             --world.y;
                         }

                         Chunk[] chunks;
                         if (!engine.__chunks.TryGetValue(world, out chunks))
                             return 0.0f;

                         int index = local.x + local.y * engine.__mapSize.x, numChunks = chunks == null ? 0 : chunks.Length;
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

            public float Get(int volumeIndex, float result, Vector3 point, VolumeInfo[] volumeInfos)
            {
                if (volumeIndex < 0)
                    return result;

                int numVolumeInfos = volumeInfos == null ? 0 : volumeInfos.Length;
                if (volumeIndex >= numVolumeInfos)
                    return result;

                if (__noise == null)
                {
                    PerlinNoise3.RandomValue[] randomValues = new PerlinNoise3.RandomValue[__noiseSize];
                    for (int i = 0; i < __noiseSize; ++i)
                        randomValues[i] = new PerlinNoise3.RandomValue((float)__random.NextDouble(), (float)__random.NextDouble());

                    __noise = new PerlinNoise3(randomValues);
                }

                VolumeInfo volumeInfo = volumeInfos[volumeIndex];
                return Mathf.Max(volumeInfo.Get(__noise, point.x, point.y, point.z) / volumeInfo.scale.magnitude, result);// Mathf.Clamp(volumeInfo.Get(__noise, point.x, point.y, point.z) / (volumeInfo.scale.y * scale.y), -1.0f, 1.0f);
            }
            
            public bool Create(float increment, Vector2Int position, MapInfo[] mapInfos, VolumeInfo[] volumeInfos, LayerInfo[] layerInfos, LineInfo[] lineInfos, Action<Instance> instantiate)
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
                                            
                                            result = false;
                                            while (point.y <= next)
                                            {
                                                block = new ProcessorEx.Block(
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
                                            
                                            l = chunk.index + chunk.count;
                                            previous = previous - y;
                                            do
                                            {
                                                --l;
                                                point.y -= scale.y;

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

                                            point.y = scale.y * chunk.count;
                                        }
                                        else
                                        {
                                            y = length;

                                            middle = 0.0f;
                                        }

                                        result = false;
                                        while (point.y <= current)
                                        {
                                            block = new ProcessorEx.Block(
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

                                            point.y += scale.y;
                                        }
                                        
                                        l = chunk.index + chunk.count;

                                        previous = current + y;
                                        while (point.y < previous)
                                        {
                                            block = new ProcessorEx.Block(
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

                                            block.density += scale.y;
                                            
                                            point.y += scale.y;
                                        }

                                        if (result)
                                        {
                                            while (l < __blocks.Count)
                                            {
                                                block = new ProcessorEx.Block(
                                                       layerInfo.materialIndex,
                                                       Get(layerInfo.volumeIndex, point.y - middle - y, point, volumeInfos));

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

                            //__liner.Set(index, point.x, point.z, mapInfos);

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

                        __liner.Do(increment, offset, extends, this, chunks, instantiate);

                        if (builder != null)
                            builder.Set(new BoundsInt(offset, extends));
                    }
                }
                return true;
            }

            public override bool Get(Vector3Int position, out ProcessorEx.Block block)
            {
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
                            if (position.y >= 0 && position.y < chunk.count)
                                block = __blocks[chunk.index + position.y];
                            else
                            {
                                block = __blocks[chunk.index + Mathf.Clamp(position.y, 0, chunk.count - 1)];

                                block.density = position.y * scale.y - (position.y < 0 ? 0.0f : chunk.height);
                            }
                            
                            return true;
                        }
                    }
                }

                block = new ProcessorEx.Block(0, position.y * scale.y);

                return true;
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
                        engine.Create(increment, new Vector2Int(i, j), mapInfos, volumeInfos, layerInfos, lineInfos, Instantiate);
                }
            }
            
            return base.Create(world, increment);
        }

        public override GameObject Convert(MeshData<Vector3> meshData)
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