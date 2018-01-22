using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZG.Voxel
{
    public abstract class Processor
    {
        [Flags]
        public enum Flag
        {
            Boundary = 0x01, 
            CastShadows = 0x02
        }

        [Serializable]
        public struct Level
        {
            [Mask]
            public Flag flag;

            //public int depth;
            public int qefSweeps;
            //public float qefError;
            public float threshold;

            [Range(0.0f, 1.0f)]
            public float screenRelativeTransitionHeight;

            [Range(0.0f, 1.0f)]
            public float fadeTransitionWidth;

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

        [Serializable]
        internal struct LevelInfo
        {
            public float distance;

            public Level[] levels;

            public LevelInfo(float distance, Level[] levels)
            {
                this.distance = distance;
                this.levels = levels;
            }
        }
        
        private struct Info
        {
            public Vector3Int position;

            public Level[] levels;

            public Info(Vector3Int position, Level[] levels)
            {
                this.position = position;
                this.levels = levels;
            }
        }

        private struct MeshData
        {
            public Info info;

            public IDictionary<Vector3Int, MeshData<Bounds>>[] meshes;

            public MeshData(Info info, IDictionary<Vector3Int, MeshData<Bounds>>[] meshes)
            {
                this.info = info;
                this.meshes = meshes;
            }
        }

        private struct ObjectData
        {
            public GameObject gameObject;

            public Level[] levels;

            public ObjectData(GameObject gameObject, Level[] levels)
            {
                this.gameObject = gameObject;
                this.levels = levels;
            }
        }

        public struct Instance
        {
            public UnityEngine.Object target;
            public Predicate<UnityEngine.Object> predicate;
            public Action<UnityEngine.Object> handler;

            public Instance(UnityEngine.Object target, Predicate<UnityEngine.Object> predicate, Action<UnityEngine.Object> handler)
            {
                this.target = target;
                this.predicate = predicate;
                this.handler = handler;
            }
        }
        
        public delegate int TileProcessor(
            DualContouring.Octree.Info x,
            DualContouring.Octree.Info y, 
            DualContouring.Octree.Info z, 
            DualContouring.Octree.Info w, 
            DualContouring.Axis axis, 
            Vector3Int offset, 
            DualContouring.Octree octree);

        public TileProcessor tileProcessor;
        
        [SerializeField]
        internal int _depth;

        [SerializeField]
        internal float _increment;

        [SerializeField]
        internal Vector3Int _segments;

        [SerializeField]
        internal Vector3 _size;

        [SerializeField]
        internal LevelInfo[] _levelInfos;

        private volatile int __count;

        private BoundsInt __bounds;
        private DualContouring.IBuilder __builder;
        private List<Vector3Int> __buffer;
        private List<Instance> __instances = new List<Instance>();
        private List<Info> __in = new List<Info>();
        private Pool<MeshData> __out = new Pool<MeshData>();
        private Pool<DualContouring.Octree> __octrees = new Pool<DualContouring.Octree>();
        private Dictionary<Vector3Int, ObjectData> __objects = new Dictionary<Vector3Int, ObjectData>();

        public int depth
        {
            get
            {
                return _depth;
            }
        }

        public Vector3 size
        {
            get
            {
                return _size;
            }
        }

        public Vector3 scale
        {
            get
            {
                return _size / (1 << _depth);
            }
        }

        public DualContouring.IBuilder builder
        {
            get
            {
                lock (this)
                {
                    if (__builder == null)
                        __builder = Create(_depth, scale);
                }

                return __builder;
            }
        }
        
        public bool GetLevels(float distance, out Level[] levels)
        {
            if (_levelInfos != null)
            {
                foreach (LevelInfo levelInfo in _levelInfos)
                {
                    if (levelInfo.distance > distance)
                    {
                        levels = levelInfo.levels;

                        return true;
                    }
                }
            }

            levels = null;

            return false;
        }

        public void Show(BoundsInt bounds)
        {
            if (__bounds == bounds)
                return;
            
            __bounds = bounds;

            Vector3 position = bounds.center;
            
            lock (__in)
            {
                __in.Clear();

                while (__count > 0) ;

                lock (__out)
                {
                    __out.RemoveAll(x => !__Check(x.info, bounds));

                    Vector3Int offset;
                    if (__buffer != null)
                        __buffer.Clear();

                    ObjectData objectData;
                    MeshFilter meshFilter;
                    foreach (KeyValuePair<Vector3Int, ObjectData> pair in __objects)
                    {
                        offset = pair.Key;
                        objectData = pair.Value;
                        if (!__Check(new Info(offset, objectData.levels), bounds))
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
                    Info info;
                    Level[] levels;
                    for (i = min.x; i < max.x; ++i)
                    {
                        for (j = min.y; j < max.y; ++j)
                        {
                            for (k = min.z; k < max.z; ++k)
                            {
                                offset = new Vector3Int(i, j, k);
                                if (!__objects.ContainsKey(offset))
                                {
                                    result = GetLevels(Vector3.Scale(offset - position, _size).magnitude, out levels);

                                    foreach (KeyValuePair<int, MeshData> pair in (IEnumerable<KeyValuePair<int, MeshData>>)__out)
                                    {
                                        info = pair.Value.info;
                                        if (info.position == offset)
                                        {
                                            if (info.levels == levels)
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
                                        __in.Add(new Info(offset, levels));
                                }
                            }
                        }
                    }
                }
            }
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

                int i, j, k;
                Vector3Int offset;
                Vector3Int min = bounds.min, max = bounds.max;
                Level[] levels;
                for (i = min.x; i < max.x; ++i)
                {
                    for (j = min.y; j < max.y; ++j)
                    {
                        for (k = min.z; k < max.z; ++k)
                        {
                            offset = new Vector3Int(i, j, k);

                            if (GetLevels(Vector3.Scale(offset - position, _size).magnitude, out levels))
                                __in.Add(new Info(offset, levels));
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

        public void Instantiate(Instance instance)
        {
            lock(__instances)
            {
                __instances.Add(instance);
            }
        }

        public bool ThreadUpdate()
        {
            int index, numLevels, mask, i;
            Vector3 min, max;
            Level level;
            Info info;
            Bounds bounds;
            MeshData<Bounds> mesh;
            DualContouring.Octree octree;
            Dictionary<Vector3Int, MeshData<Bounds>> map;
            List<KeyValuePair<Vector3Int, MeshData<Bounds>>> buffer;
            List<KeyValuePair<Vector3Int, MeshData<Bounds>>>[] meshes;
            IDictionary<Vector3Int, MeshData<Bounds>>[] result = null;
            do
            {
                lock (__in)
                {
                    index = __in.Count;
                    if (index < 1)
                        return false;

                    ++__count;

                    --index;

                    info = __in[index];

                    __in.RemoveAt(index);
                }

                lock(__octrees)
                {
                    index = __octrees.nextIndex;

                    __octrees.TryGetValue(index, out octree);

                    if (octree == null)
                        octree = new DualContouring.Octree();

                    __octrees.Insert(index, octree);
                }

                numLevels = info.levels == null ? 0 : info.levels.Length;
                meshes = null;
                for (i = 0; i < numLevels; ++i)
                {
                    level = info.levels[i];
                    
                    if (octree.Create(
                        (level.flag & Flag.Boundary) == 0 ? DualContouring.Octree.Boundary.None : DualContouring.Octree.Boundary.All,
                        level.qefSweeps,
                        level.threshold,
                        info.position,
                        Create(info.position, _increment)))
                    {
                        if (octree.Build((x, y, z, w, aixs, offset) =>
                        {
                            return tileProcessor == null ? 0 : tileProcessor(x, y, z, w, aixs, offset, octree);
                        }, out mesh))
                        {
                            if (meshes == null)
                                meshes = new List<KeyValuePair<Vector3Int, MeshData<Bounds>>>[numLevels];

                            buffer = new List<KeyValuePair<Vector3Int, MeshData<Bounds>>>();
                            buffer.Add(new KeyValuePair<Vector3Int, MeshData<Bounds>>(Vector3Int.zero, mesh));

                            meshes[i] = buffer;
                        }
                    }
                }

                lock (__octrees)
                {
                    __octrees.RemoveAt(index);
                }

                result = null;
                if (meshes != null)
                {
                    mask = (1 << _depth) - 1;

                    min = Vector3.Scale(info.position * mask, scale);
                    max = min + _size;
                    bounds = new Bounds((min + max) * 0.5f, max - min);
                    for (i = 0; i < numLevels; ++i)
                    {
                        buffer = meshes[i];
                        if (buffer == null)
                            continue;

                        buffer.Split(bounds, _segments);

                        map = null;
                        foreach (KeyValuePair<Vector3Int, MeshData<Bounds>> pair in buffer)
                        {
                            if (map == null)
                                map = new Dictionary<Vector3Int, MeshData<Bounds>>();

                            map.Add(pair.Key, pair.Value);
                        }

                        if (map == null)
                            continue;

                        if (result == null)
                            result = new IDictionary<Vector3Int, MeshData<Bounds>>[numLevels];

                        result[i] = map;
                    }
                }

                lock (__out)
                {
                    __out.Add(new MeshData(info, result));
                    
                    --__count;
                }
            } while (result == null);

            return true;
        }

        public GameObject MainUpdate()
        {
            KeyValuePair<int, MeshData> pair;
            lock (__out)
            {
                Pool<MeshData>.PairEnumerator enumerable = __out.GetPairEnumerator();
                if (!enumerable.MoveNext())
                    return null;

                pair = enumerable.Current;

                __out.RemoveAt(pair.Key);
            }

            MeshData meshData = pair.Value;

            int count = Mathf.Min(
                meshData.info.levels == null ? 0 : meshData.info.levels.Length, 
                meshData.meshes == null ? 0 : meshData.meshes.Length);

            GameObject gameObject;
            if (count < 1)
                gameObject = null;
            else
            {
                IDictionary<Vector3Int, MeshData<Bounds>> source = meshData.meshes[0];
                if (source == null)
                    gameObject = null;
                else
                {
                    if (count > 1)
                    {
                        gameObject = null;

                        int i;
                        Vector3Int position;
                        Level level;
                        LOD lod;
                        MeshData<Bounds> instance;
                        Transform root = null, parent = null, child;
                        GameObject local, world;
                        LODGroup lodGroup;
                        IDictionary<Vector3Int, MeshData<Bounds>> destination;
                        List<LOD> lods;
                        List<Renderer> renderers = null;
                        foreach (KeyValuePair<Vector3Int, MeshData<Bounds>> mesh in source)
                        {
                            world = null;
                            lodGroup = null;
                            lods = null;

                            position = mesh.Key;
                            for (i = 0; i < count; ++i)
                            {
                                destination = meshData.meshes[i];
                                if (destination == null || !destination.TryGetValue(position, out instance))
                                    continue;

                                local = Convert(instance);
                                if (local == null)
                                    continue;

                                if (gameObject == null)
                                {
                                    gameObject = new GameObject();

                                    root = gameObject.transform;
                                }

                                if (world == null)
                                {
                                    world = new GameObject();

                                    parent = world.transform;
                                    if (parent != null)
                                        parent.SetParent(root);

                                    lodGroup = world.AddComponent<LODGroup>();
                                }

                                child = local.transform;
                                if (child != null)
                                    child.SetParent(parent);

                                if (renderers == null)
                                    renderers = new List<Renderer>();

                                local.GetComponentsInChildren(renderers);

                                level = meshData.info.levels[i];

                                if ((level.flag & Flag.CastShadows) == 0)
                                {
                                    foreach (Renderer renderer in renderers)
                                        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                                }
                                
                                lod.screenRelativeTransitionHeight = level.screenRelativeTransitionHeight;
                                lod.fadeTransitionWidth = level.fadeTransitionWidth;
                                lod.renderers = renderers.ToArray();

                                if (lods == null)
                                    lods = new List<LOD>();

                                lods.Add(lod);
                            }

                            if (lodGroup != null && lods != null)
                            {
                                lodGroup.SetLODs(lods.ToArray());
                            }
                        }
                    }
                    else
                    {
                        GameObject instance;
                        List<GameObject> gameObjects = null;
                        foreach (KeyValuePair<Vector3Int, MeshData<Bounds>> mesh in source)
                        {
                            instance = Convert(mesh.Value);
                            if (instance == null)
                                continue;

                            if (gameObjects == null)
                                gameObjects = new List<GameObject>();

                            gameObjects.Add(instance);
                        }

                        gameObject = null;

                        if (gameObjects != null)
                        {
                            Transform parent = null, child;
                            foreach(GameObject target in gameObjects)
                            {
                                child = target == null ? null : target.transform;
                                if (child == null)
                                    continue;

                                if (gameObject == null)
                                {
                                    gameObject = new GameObject();

                                    parent = gameObject.transform;
                                }

                                child.SetParent(parent);
                            }
                        }
                    }
                }
            }

            //lock (__objects)
            {
                ObjectData objectData;
                if (__objects.TryGetValue(meshData.info.position, out objectData) && objectData.gameObject != null)
                    UnityEngine.Object.Destroy(objectData.gameObject);

                __objects[meshData.info.position] = new ObjectData(gameObject, meshData.info.levels);
            }

            if (gameObject == null)
            {
                lock (__instances)
                {
                    UnityEngine.Object target;
                    foreach (Instance instance in __instances)
                    {
                        if (instance.predicate != null && !instance.predicate(instance.target))
                            continue;

                        target = UnityEngine.Object.Instantiate(instance.target);
                        if(instance.handler != null)
                            instance.handler(target);
                    }

                    __instances.Clear();
                }
            }

            return gameObject;
        }
        
        public virtual DualContouring Create(Vector3Int world, float increment)
        {
            DualContouring.IBuilder builder = this.builder;
            if (builder == null)
                return null;
            
            builder.Create(world, increment);

            return builder.parent;
        }

        public abstract DualContouring.IBuilder Create(int depth, Vector3 scale);

        public abstract GameObject Convert(MeshData<Bounds> meshData);

        private bool __Check(Info info, BoundsInt bounds)
        {
            Vector3Int min = bounds.min, max = bounds.max;
            if (info.position.x < min.x || 
                info.position.y < min.y || 
                info.position.z < min.z || 
                info.position.x > max.x || 
                info.position.y > max.y || 
                info.position.z > max.z)
                return false;

            Level[] levels;
            return GetLevels(Vector3.Scale(info.position - bounds.position, _size).magnitude, out levels) && levels == info.levels;
        }
    }

    public abstract class ProcessorEx : Processor
    {
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

        public abstract class Engine : DualContouring
        {
            //private Dictionary<Vector3Int, ProcessorEx.Block> __blocks;

            public static float Cuboid(Vector3 position, Quaternion rotation, Bounds bounds)
            {
                Vector3 distance = bounds.extents - Abs(rotation * (position - bounds.center));
                if (distance.x < 0.0f)
                {
                    if (distance.y < 0.0f)
                    {
                        if (distance.z < 0.0f)
                            return -distance.magnitude;

                        return -Mathf.Sqrt(distance.x * distance.x +  distance.y * distance.y);
                    }
                    else if (distance.z < 0.0f)
                        return -Mathf.Sqrt(distance.x * distance.x + distance.z * distance.z);

                    return distance.x;
                }
                else if (distance.y < 0.0f)
                {
                    if (distance.z < 0.0f)
                        return -Mathf.Sqrt(distance.y * distance.y + distance.z * distance.z);

                    return distance.y;
                }
                else if (distance.z < 0.0f)
                    return distance.z;

                return Mathf.Min(distance.x, Mathf.Min(distance.y, distance.z));
            }
            
            public Engine(int depth, Vector3 scale, Vector3 offset) : base(depth, scale, offset)
            {

            }

            public abstract bool Get(Vector3Int position, out ProcessorEx.Block block);

            public abstract bool Set(Vector3Int position, ProcessorEx.Block block);

            public void Do(Bounds source, Quaternion rotation)
            {
                Bounds destination = GetWorldBounds(source, rotation);
                Vector3 scale = base.scale,
                    temp = new Vector3(1.0f / scale.x, 1.0f / scale.y, 1.0f / scale.z);
                Vector3Int min = Vector3Int.FloorToInt(Vector3.Scale(destination.min - scale, temp)),
                    max = Vector3Int.CeilToInt(Vector3.Scale(destination.max + scale, temp)), key;

                ProcessorEx.Block block;
                int i, j, k;

                rotation = Quaternion.Inverse(rotation);
                for (i = min.x; i <= max.x; ++i)
                {
                    for (j = min.y; j <= max.y; ++j)
                    {
                        for (k = min.z; k <= max.z; ++k)
                        {
                            key = new Vector3Int(i, j, k);
                            if (Get(key, out block))
                            {
                                block.density = Mathf.Max(
                                    block.density,
                                    Mathf.Clamp(Cuboid(Vector3.Scale(key, scale), rotation, source) / scale.y, -1, 1));

                                Set(key, block);
                            }
                        }
                    }
                }
            }

            public void Do(Bounds source, Quaternion rotation, int materialIndex)
            {
                Bounds destination = GetWorldBounds(source, rotation);
                Vector3 scale = base.scale,
                    temp = new Vector3(1.0f / scale.x, 1.0f / scale.y, 1.0f / scale.z);
                Vector3Int min = Vector3Int.FloorToInt(Vector3.Scale(destination.min - scale, temp)),
                    max = Vector3Int.CeilToInt(Vector3.Scale(destination.max + scale, temp)), key;

                ProcessorEx.Block block;
                float density;
                int i, j, k;

                rotation = Quaternion.Inverse(rotation);
                for (i = min.x; i <= max.x; ++i)
                {
                    for (j = min.y; j <= max.y; ++j)
                    {
                        for (k = min.z; k <= max.z; ++k)
                        {
                            key = new Vector3Int(i, j, k);
                            if (Get(key, out block))
                            {
                                density = Cuboid(Vector3.Scale(key, scale), rotation, source);// / scale.y;
                                if (density > -1.0f)
                                    block.materialIndex = materialIndex;

                                block.density = Mathf.Max(
                                    block.density,
                                    Mathf.Clamp(density, -1, 1));

                                Set(key, block);
                            }
                        }
                    }
                }
            }

            public void Do(Vector3 position, float radius)
            {
                Vector3 scale = base.scale,
                    offset = new Vector3(position.x / scale.x, position.y / scale.y, position.z / scale.z);
                Vector3Int min = Vector3Int.FloorToInt(new Vector3(offset.x - radius, offset.y - radius, offset.z - radius) - scale),
                    max = Vector3Int.CeilToInt(new Vector3(offset.x + radius, offset.y + radius, offset.z + radius) + scale),
                    temp;

                int i, j, k;
                float density;
                ProcessorEx.Block block;
                for (i = min.x; i <= max.x; ++i)
                {
                    for (j = min.y; j <= max.y; ++j)
                    {
                        for (k = min.z; k <= max.z; ++k)
                        {
                            density = radius - Vector3.Distance(new Vector3(i * scale.x, j * scale.y, k * scale.z), position);
                            if (density > -radius)
                            {
                                temp = new Vector3Int(i, j, k);
                                if (Get(temp, out block))
                                {
                                    block.density = Mathf.Max(
                                        block.density,
                                        density);

                                    Set(temp, block);
                                }
                            }
                        }
                    }
                }
            }

            public void Do(Vector3 position, float radius, int materialIndex)
            {
                Vector3 scale = base.scale,
                    offset = new Vector3(position.x / scale.x, position.y / scale.y, position.z / scale.z);
                Vector3Int min = Vector3Int.FloorToInt(new Vector3(offset.x - radius, offset.y - radius, offset.z - radius) - scale),
                    max = Vector3Int.CeilToInt(new Vector3(offset.x + radius, offset.y + radius, offset.z + radius) + scale),
                    temp;

                int i, j, k;
                float density;
                ProcessorEx.Block block;
                for (i = min.x; i <= max.x; ++i)
                {
                    for (j = min.y; j <= max.y; ++j)
                    {
                        for (k = min.z; k <= max.z; ++k)
                        {
                            density = radius - Vector3.Distance(new Vector3(i * scale.x, j * scale.y, k * scale.z), position);
                            if (density > -radius)
                            {
                                temp = new Vector3Int(i, j, k);
                                if (Get(temp, out block))
                                {
                                    block.materialIndex = materialIndex;

                                    block.density = Mathf.Max(block.density, density);

                                    Set(temp, block);
                                }
                            }
                        }
                    }
                }
            }

            public override float GetDensity(Vector3 position)
            {
                Vector3 scale = base.scale;

                position.x /= scale.x;
                position.y /= scale.y;
                position.z /= scale.z;

                ProcessorEx.Block block;
                Vector3Int min = Vector3Int.FloorToInt(position), max = Vector3Int.CeilToInt(position);
                if (min.x == max.x || min.y == max.y || min.z == max.z)
                {
                    float x = Get(min, out block) ? block.density : 1.0f;
                    if (min == max)
                        return x;

                    float y = Get(max, out block) ? block.density : 1.0f;
                    if ((min.x == max.x && min.y == max.y) || (min.x == max.x && min.z == max.z) || (min.y == max.y && min.z == max.z))
                        return Mathf.Lerp(x, y, position.x - min.x + position.y - min.y + position.z - min.z);

                    float z, w, u, v;
                    if(min.x == max.x)
                    {
                        z = Get(new Vector3Int(min.x, max.y, min.z), out block) ? block.density : 1.0f;
                        w = Get(new Vector3Int(min.x, min.y, max.z), out block) ? block.density : 1.0f;

                        u = position.y - min.y;
                        v = position.z - min.z;
                    }
                    else if(min.y == max.y)
                    {
                        z = Get(new Vector3Int(max.x, min.y, min.z), out block) ? block.density : 1.0f;
                        w = Get(new Vector3Int(min.x, min.y, max.z), out block) ? block.density : 1.0f;

                        u = position.x - min.x;
                        v = position.z - min.z;
                    }
                    else
                    {
                        z = Get(new Vector3Int(max.x, min.y, min.z), out block) ? block.density : 1.0f;
                        w = Get(new Vector3Int(min.x, max.y, min.z), out block) ? block.density : 1.0f;

                        u = position.x - min.x;
                        v = position.y - min.y;
                    }

                    float invertU = 1.0f - u, invertV = 1.0f - v;

                    return x * invertU * invertV + y * u * v + z * u * invertV + w * v * invertU;
                }

                float leftLowerBack = Get(min, out block) ? block.density : 1.0f, 
                    leftLowerFront = Get(new Vector3Int(min.x, min.y, max.z), out block) ? block.density : 1.0f, 
                    leftUpperBack = Get(new Vector3Int(min.x, max.y, min.z), out block) ? block.density : 1.0f, 
                    leftUpperFront = Get(new Vector3Int(min.x, max.y, max.z), out block) ? block.density : 1.0f, 
                    rightLowerBack = Get(new Vector3Int(max.x, min.y, min.z), out block) ? block.density : 1.0f, 
                    rightLowerFront = Get(new Vector3Int(max.x, min.y, max.z), out block) ? block.density : 1.0f, 
                    rightUpperBack = Get(new Vector3Int(max.x, max.y, min.z), out block) ? block.density : 1.0f, 
                    rightUpperFront = Get(max, out block) ? block.density : 1.0f;

                position -= min;

                Vector3 inverse = new Vector3(1.0f - position.x, 1.0f - position.y, 1.0f - position.z);

                return leftLowerBack * inverse.x * inverse.y * inverse.z +
                    leftLowerFront * inverse.x * inverse.y * position.z +
                    leftUpperBack * inverse.x * position.y * inverse.z +
                    leftUpperFront * inverse.x * position.y * position.z +
                    rightLowerBack * position.x * inverse.y * inverse.z +
                    rightLowerFront * position.x * inverse.y * position.z +
                    rightUpperBack * position.x * position.y * inverse.z +
                    rightUpperFront * position.x * position.y * position.z;
            }
        }

        public class NavPath : NavPathEx
        {
            private float __minDentity;
            private float __maxDentity;
            private Vector3Int __minExtends;
            private Vector3Int __maxExtends;
            private Vector3Int __position;
            private Engine __engine;
            private HashSet<Vector3Int> __points;

            public NavPath(
                Vector3Int size) : base(size)
            {
            }

            public void Clear()
            {
                if (__points != null)
                    __points.Clear();
            }

            public int Search(
                int maxDistance,
                int maxDepth,
                float minDentity, 
                float maxDentity,
                Vector3Int minExtends,
                Vector3Int maxExtends,
                Vector3Int position,
                Vector3Int from,
                Vector3Int to,
                Engine engine)
            {
                __minDentity = minDentity;
                __maxDentity = maxDentity;
                __minExtends = minExtends;
                __maxExtends = maxExtends;
                __position = position;
                __engine = engine;
                
                if (Voluate(to, from) < int.MaxValue)
                {
                    if (__points != null)
                    {
                        int i, j, k;
                        Vector3Int min = from - __minExtends, max = new Vector3Int(from.x + minExtends.x, from.y - 1, from.z + minExtends.z);
                        for (i = min.x; i <= max.x; ++i)
                        {
                            for (j = min.y; j <= max.y; ++j)
                            {
                                for (k = min.z; k <= max.z; ++k)
                                    __points.Remove(position + new Vector3Int(i, j, k));
                            }
                        }

                        min = new Vector3Int(from.x - maxExtends.x, from.y, from.z - maxExtends.z);
                        max = from + __maxExtends;
                        for (i = min.x; i <= max.x; ++i)
                        {
                            for (j = min.y; j <= max.y; ++j)
                            {
                                for (k = min.z; k <= max.z; ++k)
                                {
                                    if (i == from.x && j == from.y && k == from.z)
                                        continue;

                                    __points.Remove(position + new Vector3Int(i, j, k));
                                }
                            }
                        }
                    }

                    int depth = Search(Type.Min, maxDistance, maxDepth, from, to);
                    if (depth > 0)
                    {
                        if (__points == null)
                            __points = new HashSet<Vector3Int>();

                        __points.Add(position + from);

                        foreach (Vector3Int point in this)
                            __points.Add(position + point);
                    }

                    return depth;
                }

                return 0;
            }

            public override int Voluate(Vector3Int from, Vector3Int to)
            {
                if (from == to)
                    return 0;

                if (new Vector3Int(from.x - to.x, Mathf.Abs(from.y - to.y), from.z - to.z) == new Vector3Int(0, 1, 0))
                    return int.MaxValue;

                if (__engine == null)
                    return int.MaxValue;

                Block block;
                if (__engine.Get(__position + from, out block) && block.density <= 0.0f)
                    return int.MaxValue;

                float source = block.density;

                if (!__engine.Get(__position + new Vector3Int(from.x, from.y - 1, from.z), out block) || block.density > 0.0f)
                    return int.MaxValue;
                
                Vector3Int min = from - __minExtends;
                if (min.x < 0 || min.y < 0 || min.z < 0)
                    return int.MaxValue;

                Vector3Int max = new Vector3Int(from.x + __minExtends.x, from.y - 1, from.z + __minExtends.z), size = base.size;
                if (max.x >= size.x || max.y >= size.y || max.z >= size.z)
                    return int.MaxValue;

                int i, j, k;
                Vector3Int position;
                for (i = min.x; i <= max.x; ++i)
                {
                    for (j = min.y; j <= max.y; ++j)
                    {
                        for (k = min.z; k <= max.z; ++k)
                        {
                            position = __position + new Vector3Int(i, j, k);

                            if (__engine.Get(position, out block) ? block.density > __minDentity : __minDentity < 1.0f)
                                return int.MaxValue;
                            
                            if (__points != null && __points.Contains(position))
                                return int.MaxValue;
                        }
                    }
                }

                min = new Vector3Int(from.x - __maxExtends.x, from.y, from.z - __maxExtends.z);
                if (min.x < 0 || min.y < 0 || min.z < 0)
                    return int.MaxValue;

                max = from + __maxExtends;
                if (max.x >= size.x || max.y >= size.y || max.z >= size.z)
                    return int.MaxValue;

                for (i = min.x; i <= max.x; ++i)
                {
                    for (j = min.y; j <= max.y; ++j)
                    {
                        for (k = min.z; k <= max.z; ++k)
                        {
                            if (i == from.x && j == from.y && k == from.z)
                                continue;

                            position = __position + new Vector3Int(i, j, k);

                            if (__engine.Get(position, out block) && block.density < __maxDentity)
                                return int.MaxValue;

                            if (__points != null && __points.Contains(position))
                                return int.MaxValue;
                        }
                    }
                }
                
                return base.Voluate(from, to);
            }
        }

        public static Vector3 Abs(Vector3 x)
        {
            x.x = Mathf.Abs(x.x);
            x.y = Mathf.Abs(x.y);
            x.z = Mathf.Abs(x.z);

            return x;
        }

        public static Bounds GetWorldBounds(Bounds bounds, Quaternion rotation)
        {
            Vector3 absAxisX = Abs(rotation * Vector3.right);
            Vector3 absAxisY = Abs(rotation * Vector3.up);
            Vector3 absAxisZ = Abs(rotation * Vector3.forward);
            Vector3 size = bounds.size;
            size = absAxisX * size.x + absAxisY * size.y + absAxisZ * size.z;
            return new Bounds(bounds.center, size);
        }

        public void Do(Vector3 position, float radius)
        {
            DualContouring.IBuilder builder = base.builder;
            Engine engine = builder == null ? null : builder.parent as Engine;
            if (engine == null)
                return;

            engine.Do(position, radius);

            float size = radius * 2.0f;
            Rebuild(new Bounds(position, new Vector3(size, size, size) + _size));
        }

        public void Do(Bounds bounds, Quaternion rotation)
        {
            DualContouring.IBuilder builder = base.builder;
            Engine engine = builder == null ? null : builder.parent as Engine;
            if (engine == null)
                return;

            engine.Do(bounds, rotation);

            bounds = GetWorldBounds(bounds, rotation);
            Vector3 scale = base.scale, min = bounds.min - scale, max = bounds.max + scale;
            
            Rebuild(new Bounds((min + max) * 0.5f, max - min));
        }
    }
}