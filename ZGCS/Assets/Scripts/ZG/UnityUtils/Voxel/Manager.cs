using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZG.Voxel.Old
{
    [System.Serializable]
    public struct Level
    {
        //public int depth;
        public int qefSweeps;
        //public float qefError;
        public float threshold;

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is Level ? this == (Level)obj : base.Equals(obj);
        }

        public static bool operator ==(Level x, Level y)
        {
            return
                //x.depth == y.depth &&
                x.qefSweeps == y.qefSweeps &&
                //Mathf.Approximately(x.qefError, y.qefError) &&
                Mathf.Approximately(x.threshold, y.threshold);
        }

        public static bool operator !=(Level x, Level y)
        {
            return !(x == y);
        }
    }

    [System.Serializable]
    internal struct LevelInfo
    {
        public float distance;

        public Level level;

        public LevelInfo(float distance, Level level)
        {
            this.distance = distance;
            this.level = level;
        }
    }

    public class Manager<T, U> where T : Manager<T, U>.Node, new()
    {
        private struct Info
        {
            public Level level;

            public Vector3Int position;

            public Info(Level level, Vector3Int position)
            {
                this.level = level;
                this.position = position;
            }
        }

        private struct MeshData
        {
            public Info info;

            public U data;

            public MeshData(Info info, U data)
            {
                this.info = info;
                this.data = data;
            }
        }

        private struct ObjectData
        {
            public Level level;

            public GameObject gameObject;

            public ObjectData(Level level, GameObject gameObject)
            {
                this.level = level;
                this.gameObject = gameObject;
            }
        }

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

        public abstract class Node : Octree
        {
            private Dictionary<Vector3Int, Block[]> __blocks;

            public int size
            {
                get
                {
                    return (1 << depth) + 3;
                }
            }

            public Vector3Int position
            {
                get
                {
                    Vector3 scale = base.scale, result = scale * ((1 << depth) - 1);
                    result.x = 1.0f / result.x;
                    result.y = 1.0f / result.y;
                    result.z = 1.0f / result.z;

                    return Vector3Int.RoundToInt(Vector3.Scale(result, offset));
                }
            }

            public Block this[Vector3Int position]
            {
                get
                {
                    int mask = (1 << depth) - 1, size = this.size;

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

                    return GetBlocks(offset)[(position.x + 1) + (position.y + 1) * size + (position.z + 1) * size * size];
                }

                set
                {
                    int mask = (1 << depth) - 1;

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

                    __Set(offset, position + new Vector3Int(1, 1, 1), value);
                }
            }

            public Block[] GetBlocks(Vector3Int position)
            {
                if (__blocks == null)
                    __blocks = new Dictionary<Vector3Int, Block[]>();

                int size = this.size, count = size * size, length = count * size;
                Block[] blocks;
                if (!__blocks.TryGetValue(position, out blocks) || blocks == null || blocks.Length != length)
                {
                    Array.Resize(ref blocks, length);
                    Fill(blocks, position);
                    __blocks[position] = blocks;

                    /*if(UnityEngine.Random.value < 0.2f)
                    {
                        int depth = base.depth;
                        Vector3Int position = this.position;
                        position = new Vector3Int(
                            position.x << depth + UnityEngine.Random.Range(0, size),
                            position.y << depth + UnityEngine.Random.Range(0, size),
                            position.z << depth + UnityEngine.Random.Range(0, size));
                    }*/
                }

                return blocks;
            }

            public void Do(Vector3 position, float radius)
            {
                Vector3 scale = base.scale,
                    offset = new Vector3(position.x / scale.x, position.y / scale.y, position.z / scale.z);
                Vector3Int min = Vector3Int.FloorToInt(new Vector3(offset.x - radius, offset.y - radius, offset.z - radius) - scale),
                    max = Vector3Int.CeilToInt(new Vector3(offset.x + radius, offset.y + radius, offset.z + radius) + scale),
                    temp;

                int i, j, k;
                Block block;
                for (i = min.x; i <= max.x; ++i)
                {
                    for (j = min.y; j <= max.y; ++j)
                    {
                        for (k = min.z; k <= max.z; ++k)
                        {
                            temp = new Vector3Int(i, j, k);
                            block = this[temp];

                            block.density = Mathf.Max(
                                block.density, 
                                Mathf.Clamp((radius - Vector3.Distance(new Vector3(i * scale.x, j * scale.y, k * scale.z), position)) / scale.y, -1, 1));

                            this[temp] = block;
                        }
                    }
                }
            }

            public void Do(Bounds bounds)
            {
                Vector3 scale = base.scale,
                    offset = new Vector3(position.x / scale.x, position.y / scale.y, position.z / scale.z), temp;
                Vector3Int min = Vector3Int.FloorToInt(bounds.min - scale),
                    max = Vector3Int.CeilToInt(bounds.max + scale),
                    key;

                int i, j, k;
                Block block;
                for (i = min.x; i <= max.x; ++i)
                {
                    for (j = min.y; j <= max.y; ++j)
                    {
                        for (k = min.z; k <= max.z; ++k)
                        {
                            key = new Vector3Int(i, j, k);
                            block = this[key];

                            temp = Vector3.Scale(key, scale) - bounds.center;
                            temp = new Vector3(Mathf.Abs(temp.x), Mathf.Abs(temp.y), Mathf.Abs(temp.z)) - bounds.extents;

                            block.density = Mathf.Max(
                                block.density,
                                Mathf.Clamp(-Mathf.Min(Mathf.Max(temp.x, temp.y, temp.z), Vector3.Max(temp, Vector3.zero).magnitude) / scale.y, -1, 1));

                            this[key] = block;
                        }
                    }
                }
            }

            float Cuboid(Vector3 worldPosition, Vector3 origin, Vector3 halfDimensions)
            {
                Vector3 local_pos = worldPosition - origin;
                Vector3 pos = local_pos;

                Vector3 d = new Vector3(Mathf.Abs(pos.x), Mathf.Abs(pos.y), Mathf.Abs(pos.z)) - halfDimensions;
                float m = Mathf.Max(d.x, Mathf.Max(d.y, d.z));
                return Mathf.Min(m, Vector3.Max(d, Vector3.zero).magnitude);
            }

            public override float GetDensity(Vector3 position)
            {
                return position.y * 0.1f - Mathf.PerlinNoise(position.x * 0.1f, position.z * 0.1f);
                return Vector3.Distance(position, new Vector3(8.0f, 8.0f, 8.0f)) - 5.0f;
                //return Cuboid(position, new Vector3(8.0f, 8.0f, 8.0f), new Vector3(5f, 5f, 5f));
                //return Vector3.Distance(position, new Vector3(16f, 16f, 16f)) - 16.0f;
                //return __GetHeight(position, int.MaxValue);
                Block[] blocks = GetBlocks(this.position);

                position -= offset;
                
                Vector3 scale = base.scale;
                position.x /= scale.x;
                position.y /= scale.y;
                position.z /= scale.z;

                ++position.x;
                ++position.y;
                ++position.z;

                int size = this.size, maxIndex = size - 1;
                Vector3Int from = Vector3Int.FloorToInt(position), to = Vector3Int.CeilToInt(position);

                /*Vector3Int min = Vector3Int.zero, max = new Vector3Int(maxIndex, maxIndex, maxIndex);
                    
                from.Clamp(min, max);
                to.Clamp(min, max);
                
                position.x = Mathf.Clamp(position.x, from.x, to.x);
                position.y = Mathf.Clamp(position.y, from.y, to.y);
                position.z = Mathf.Clamp(position.z, from.z, to.z);*/

                int count = size * size, x = from.x, y = from.y * size, z = from.z * count;
                float result = blocks[x + y + z].density;
                return Mathf.Lerp(result, blocks[to.x + y + z].density, position.x - from.x) +
                    Mathf.Lerp(result, blocks[x + to.y * size + z].density, position.y - from.y) +
                    Mathf.Lerp(result, blocks[x + y + to.z * count].density, position.z - from.z) - result * 2.0f;
            }

            public abstract void Fill(Block[] blocks, Vector3Int position);

            public abstract bool Build(out U data);

            public abstract GameObject Convert(U data);

            private void __Set(Vector3Int offset, Vector3Int position, Block block)
            {
                int size = this.size;
                GetBlocks(offset)[position.x + position.y * size + position.z * size * size] = block;
                
                if (position.x == 1)
                    __Set(new Vector3Int(offset.x - 1, offset.y, offset.z), new Vector3Int(size - 3, position.y, position.z), block);

                if (position.x == 2)
                    __Set(new Vector3Int(offset.x - 1, offset.y, offset.z), new Vector3Int(size - 2, position.y, position.z), block);
                
                if (position.x == 3)
                    __Set(new Vector3Int(offset.x - 1, offset.y, offset.z), new Vector3Int(size - 1, position.y, position.z), block);

                if (position.x == (size - 4))
                    __Set(new Vector3Int(offset.x + 1, offset.y, offset.z), new Vector3Int(0, position.y, position.z), block);
                
                if (position.y == 1)
                    __Set(new Vector3Int(offset.x, offset.y - 1, offset.z), new Vector3Int(position.x, size - 3, position.z), block);

                if (position.y == 2)
                    __Set(new Vector3Int(offset.x, offset.y - 1, offset.z), new Vector3Int(position.x, size - 2, position.z), block);

                if (position.y == 3)
                    __Set(new Vector3Int(offset.x, offset.y - 1, offset.z), new Vector3Int(position.x, size - 1, position.z), block);

                if (position.y == (size - 4))
                    __Set(new Vector3Int(offset.x, offset.y + 1, offset.z), new Vector3Int(position.x, 0, position.z), block);
                
                if (position.z == 1)
                    __Set(new Vector3Int(offset.x, offset.y, offset.z - 1), new Vector3Int(position.x, position.y, size - 3), block);

                if (position.z == 2)
                    __Set(new Vector3Int(offset.x, offset.y, offset.z - 1), new Vector3Int(position.x, position.y, size - 2), block);

                if (position.z == 3)
                    __Set(new Vector3Int(offset.x, offset.y, offset.z - 1), new Vector3Int(position.x, position.y, size - 1), block);

                if (position.z == (size - 4))
                    __Set(new Vector3Int(offset.x, offset.y, offset.z + 1), new Vector3Int(position.x, position.y, 0), block);
            }
        }
        
        [SerializeField]
        internal int _depth;

        [SerializeField]
        internal Vector3 _size;

        [SerializeField]
        internal T _node;

        [SerializeField]
        internal LevelInfo[] _levelInfos;

        private BoundsInt __bounds;
        private List<Vector3Int> __buffer;
        private List<Info> __in = new List<Info>();
        private Pool<MeshData> __out = new Pool<MeshData>();
        private Dictionary<Vector3Int, ObjectData> __objects = new Dictionary<Vector3Int, ObjectData>();

        public bool GetLevel(float distance, out Level level)
        {
            if (_levelInfos != null)
            {
                foreach (LevelInfo levelInfo in _levelInfos)
                {
                    if (levelInfo.distance > distance)
                    {
                        level = levelInfo.level;

                        return true;
                    }
                }
            }

            level = default(Level);

            return false;
        }

        public void Do(Vector3 position, float radius)
        {
            if (_node == null)
                return;

            _node.Do(position, radius);

            float size = radius * 2.0f;
            Rebuild(new Bounds(position, new Vector3(size, size, size) + _size));
        }

        public void Do(Bounds bounds)
        {
            if (_node == null)
                return;

            _node.Do(bounds);
            
            Rebuild(new Bounds(bounds.center, bounds.size + _size));
        }

        public void Show(BoundsInt bounds)
        {
            if (__bounds == bounds)
                return;

            Vector3 position = bounds.center;
            
            lock (__out)
            {
                __out.RemoveAll(x => !__Check(x.info, bounds));
            }
            
            lock (__in)
            {
                __in.Clear();

                Vector3Int offset;
                if (__buffer != null)
                    __buffer.Clear();

                ObjectData objectData;
                MeshFilter meshFilter;
                foreach (KeyValuePair<Vector3Int, ObjectData> pair in __objects)
                {
                    offset = pair.Key;
                    objectData = pair.Value;
                    if (!__Check(new Info(objectData.level, offset), bounds))
                    {
                        if (objectData.gameObject != null)
                        {
                            meshFilter = objectData.gameObject.GetComponent<MeshFilter>();
                            if (meshFilter != null)
                                UnityEngine.Object.Destroy(meshFilter.sharedMesh);

                            UnityEngine.Object.Destroy(objectData.gameObject);
                        }

                        if (__buffer == null)
                            __buffer = new List<Vector3Int>();

                        __buffer.Add(offset);
                    }
                }

                if (__buffer != null)
                {
                    foreach (Vector3Int temp in __buffer)
                        __objects.Remove(temp);
                }

                bool result;
                int i, j, k;
                Vector3Int min = bounds.min, max = bounds.max;
                Level level;
                Info info;
                for (i = min.x; i < max.x; ++i)
                {
                    for (j = min.y; j < max.y; ++j)
                    {
                        for (k = min.z; k < max.z; ++k)
                        {
                            offset = new Vector3Int(i, j, k);
                            if (!__objects.ContainsKey(offset))
                            {
                                result = GetLevel(Vector3.Scale(offset - position, _size).magnitude, out level);
                                
                                foreach (KeyValuePair<int, MeshData> pair in (IEnumerable<KeyValuePair<int, MeshData>>)__out)
                                {
                                    info = pair.Value.info;
                                    if (info.position == offset)
                                    {
                                        if (info.level == level)
                                            result = false;
                                        else
                                        {
                                            __out.RemoveAt(pair.Key);

                                            result = result && true;
                                        }

                                        break;
                                    }
                                }
                                
                                if (result)
                                    __in.Add(new Info(level, offset));
                            }
                        }
                    }
                }
            }

            __bounds = bounds;
        }

        public void Show(Bounds bounds)
        {
            Vector3 size = _size * (1.0f - 1.0f / (1 << _depth)), scale = new Vector3(1.0f / size.x, 1.0f / size.y, 1.0f / size.z);
            Vector3Int min = Vector3Int.FloorToInt(Vector3.Scale(bounds.min, scale)), 
                max = Vector3Int.CeilToInt(Vector3.Scale(bounds.max, scale));

            Show(new BoundsInt(min.x, min.y, min.z, max.x - min.x, max.y - min.y, max.z - min.z));
        }

        public void Rebuild(BoundsInt bounds)
        {
            bounds.ClampToBounds(__bounds);

            Vector3Int position = bounds.position;

            lock (__out)
            {
                __out.RemoveAll(x => bounds.Contains(x.info.position));
            }

            lock (__in)
            {
                __in.RemoveAll(x => bounds.Contains(x.position));

                Vector3Int offset;
                /*if (__buffer != null)
                    __buffer.Clear();

                ObjectData objectData;
                MeshFilter meshFilter;
                foreach (KeyValuePair<Vector3Int, ObjectData> pair in __objects)
                {
                    offset = pair.Key;
                    objectData = pair.Value;
                    if (bounds.Contains(offset))
                    {
                        if (objectData.gameObject != null)
                        {
                            meshFilter = objectData.gameObject.GetComponent<MeshFilter>();
                            if (meshFilter != null)
                                UnityEngine.Object.Destroy(meshFilter.sharedMesh);

                            UnityEngine.Object.Destroy(objectData.gameObject);
                        }

                        if (__buffer == null)
                            __buffer = new List<Vector3Int>();

                        __buffer.Add(offset);
                    }
                }

                if (__buffer != null)
                {
                    foreach (Vector3Int temp in __buffer)
                        __objects.Remove(temp);
                }*/

                int i, j, k;
                Vector3Int min = bounds.min, max = bounds.max;
                Level level;
                for (i = min.x; i < max.x; ++i)
                {
                    for (j = min.y; j < max.y; ++j)
                    {
                        for (k = min.z; k < max.z; ++k)
                        {
                            offset = new Vector3Int(i, j, k);

                            if (GetLevel(Vector3.Scale(offset - position, _size).magnitude, out level))
                                __in.Add(new Info(level, offset));
                        }
                    }
                }
            }
        }

        public void Rebuild(Bounds bounds)
        {
            Vector3 size = _size * (1.0f - 1.0f / (1 << _depth)), scale = new Vector3(1.0f / size.x, 1.0f / size.y, 1.0f / size.z);
            Vector3Int min = Vector3Int.FloorToInt(Vector3.Scale(bounds.min, scale)),
                max = Vector3Int.CeilToInt(Vector3.Scale(bounds.max, scale));

            Rebuild(new BoundsInt(min.x, min.y, min.z, max.x - min.x, max.y - min.y, max.z - min.z));
        }

        public bool ThreadUpdate()
        {
            lock (__in)
            {
                bool result;
                int index;
                Vector3 scale;
                Info info;
                U data;
                do
                {
                    index = __in.Count;
                    if (index < 1)
                        return false;

                    --index;

                    info = __in[index];

                    __in.RemoveAt(index);

                    if (_node == null)
                        _node = Create();

                    scale = _size / (1 << _depth);
                    _node.Create(
                        Octree.Boundary.All, 
                        _depth,
                        info.level.qefSweeps,
                        //info.level.qefError,
                        info.level.threshold,
                        scale,
                        Vector3.Scale(info.position, _size - scale));

                    result = !_node.Build(out data);

                    lock (__out)
                    {
                        __out.Add(new MeshData(info, data));
                    }
                    
                    /*else
                    {
                        lock (__objects)
                        {
                            ObjectData objectData;
                            if (__objects.TryGetValue(info.position, out objectData))
                                objectData.level = info.level;
                            else
                                objectData = new ObjectData(info.level, null);

                            __objects[info.position] = objectData;
                        }
                    }*/
                } while (result);

            }

            return true;
        }

        public GameObject MainUpdate()
        {
            KeyValuePair<int, MeshData> pair;
            lock(__out)
            {
                Pool<MeshData>.PairEnumerator enumerable = __out.GetPairEnumerator();
                if (!enumerable.MoveNext())
                    return null;

                pair = enumerable.Current;

                __out.RemoveAt(pair.Key);
            }

            MeshData meshData = pair.Value;
            
            GameObject gameObject = _node.Convert(meshData.data);

            //lock (__objects)
            {
                ObjectData objectData;
                if (__objects.TryGetValue(meshData.info.position, out objectData) && objectData.gameObject != null)
                    UnityEngine.Object.Destroy(objectData.gameObject);

                __objects[meshData.info.position] = new ObjectData(meshData.info.level, gameObject);
            }

            return gameObject;
        }

        public virtual T Create()
        {
            return new T();
        }
        
        private bool __Check(Info info, BoundsInt bounds)
        {
            if (!bounds.Contains(info.position))
                return false;

            Level level;
            return GetLevel(Vector3.Scale(info.position - bounds.position, _size).magnitude, out level) && level == info.level;
        }
    }

    public class FlatMananger<T> : Manager<T, FlatMananger<T>.Data?> where T : FlatMananger<T>.Node, new()
    {
        public struct Data
        {
            public Vector3[] vertices;

            public int[][] indices;

            public Mesh ToMesh(out Dictionary<int, int> materialMap)
            {
                materialMap = null;

                int numIndices = this.indices == null ? 0 : this.indices.Length;
                if (numIndices < 0)
                    return null;

                int subMeshCount = 0;
                for(int i = 0; i < numIndices; ++i)
                {
                    if (this.indices[i] == null)
                        continue;

                    ++subMeshCount;
                }

                Mesh mesh = new Mesh();
                mesh.subMeshCount = subMeshCount;

                mesh.vertices = vertices;

                subMeshCount = 0;

                int[] indices;
                for (int i = 0; i < numIndices; ++i)
                {
                    indices = this.indices[i];
                    if (indices == null)
                        continue;

                    if (materialMap == null)
                        materialMap = new Dictionary<int, int>();

                    materialMap[i] = subMeshCount;

                    mesh.SetTriangles(indices, subMeshCount);

                    ++subMeshCount;
                }

                mesh.RecalculateBounds();
                mesh.RecalculateNormals();

                return mesh;
            }
        }

        public new abstract class Node : Manager<T, FlatMananger<T>.Data?>.Node
        {
            public new delegate int TileProcessor(int x, int y, int z, int w, Axis axis, Vector3Int position);

            private struct Tile
            {
                public int x;
                public int y;
                public int z;
                public int w;

                public int materialIndex;

                public Tile(int x, int y, int z, int w, int materialIndex)
                {
                    this.x = x;
                    this.y = y;
                    this.z = z;
                    this.w = w;

                    this.materialIndex = materialIndex;
                }
            }

            public TileProcessor tileProcessor;
            public Material[] materials;

            public override bool Build(out Data? data)
            {
                data = null;

                List<Tile> tiles = null;
                Build((x, y, z, w, axis, offset) =>
                {
                    if (tiles == null)
                        tiles = new List<Tile>();

                    tiles.Add(new Tile(x, y, z, w, tileProcessor == null ? 0 : tileProcessor(x, y, z, w, axis, offset)));
                });

                int numTiles = tiles == null ? 0 : tiles.Count;
                if (numTiles < 1)
                    return false;

                Data result;
                int numVertices = numTiles * 6, index = 0;//, indexX, indexY, indexZ, indexW;
                Info a, b, c, d;
                List<int> indices;
                //List<Vector3> vertices = null;
                Pool<List<int>> materialMap = null;
                //Dictionary<int, int> indexMap = null;
                //data.indices = new int[numVertices];
                result.vertices = new Vector3[numVertices];
                foreach (Tile tile in tiles)
                {
                    if (!Get(tile.x, out a) || !Get(tile.y, out b) || !Get(tile.z, out c) || !Get(tile.w, out d))
                        continue;

                    if (materialMap == null)
                        materialMap = new Pool<List<int>>();

                    if(!materialMap.TryGetValue(tile.materialIndex, out indices) || indices == null)
                    {
                        indices = new List<int>();

                        materialMap.Insert(tile.materialIndex, indices);
                    }

                    /*if (indexMap == null)
                        indexMap = new Dictionary<int, int>();

                    if (!indexMap.TryGetValue(tile.x, out indexX))
                    {
                        if (vertices == null)
                            vertices = new List<Vector3>();

                        indexX = vertices.Count;

                        indexMap[tile.x] = indexX;

                        vertices.Add(a.position);
                    }

                    if (!indexMap.TryGetValue(tile.y, out indexY))
                    {
                        if (vertices == null)
                            vertices = new List<Vector3>();

                        indexY = vertices.Count;

                        indexMap[tile.y] = indexY;

                        vertices.Add(b.position);
                    }

                    if (!indexMap.TryGetValue(tile.z, out indexZ))
                    {
                        if (vertices == null)
                            vertices = new List<Vector3>();

                        indexZ = vertices.Count;

                        indexMap[tile.z] = indexZ;

                        vertices.Add(c.position);
                    }

                    if (!indexMap.TryGetValue(tile.w, out indexW))
                    {
                        if (vertices == null)
                            vertices = new List<Vector3>();

                        indexW = vertices.Count;

                        indexMap[tile.w] = indexW;

                        vertices.Add(d.position);
                    }

                    indices.Add(indexX);
                    indices.Add(indexY);
                    indices.Add(indexZ);

                    indices.Add(indexZ);
                    indices.Add(indexY);
                    indices.Add(indexW);*/

                    indices.Add(index);

                    result.vertices[index] = a.position;

                    ++index;

                    indices.Add(index);

                    result.vertices[index] = b.position;

                    ++index;
                    
                    indices.Add(index);

                    result.vertices[index] = c.position;

                    ++index;
                    
                    indices.Add(index);

                    result.vertices[index] = c.position;

                    ++index;

                    indices.Add(index);

                    result.vertices[index] = b.position;

                    ++index;

                    indices.Add(index);

                    result.vertices[index] = d.position;

                    ++index;
                }

                //result.vertices = vertices == null ? null : vertices.ToArray();

                if (materialMap == null)
                    result.indices = null;
                else
                {
                    result.indices = new int[materialMap.capacity][];

                    foreach(KeyValuePair<int, List<int>> pair in (IEnumerable<KeyValuePair<int, List<int>>>)materialMap)
                        result.indices[pair.Key] = pair.Value.ToArray();
                }

                data = result;

                return true;
            }

            public override GameObject Convert(Data? data)
            {
                if (data == null)
                    return null;

                Dictionary<int, int> materialMap;

                Mesh mesh = ((Data)data).ToMesh(out materialMap);
                if (mesh == null)
                    return null;

                GameObject gameObject = new GameObject();

                MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
                if (meshFilter != null)
                    meshFilter.sharedMesh = mesh;

                if (materialMap != null)
                {
                    Material[] materials = new Material[materialMap.Count];
                    foreach(KeyValuePair<int, int> pair in materialMap)
                        materials[pair.Value] = this.materials[pair.Key];

                    MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
                    if (meshRenderer != null)
                        meshRenderer.sharedMaterials = materials;
                }
                
                MeshCollider meshCollider = gameObject.AddComponent<MeshCollider>();
                if(meshCollider != null)
                    meshCollider.sharedMesh = mesh;

                return gameObject;
            }
        }
    }
}