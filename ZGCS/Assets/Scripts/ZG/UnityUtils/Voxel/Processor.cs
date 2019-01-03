using System;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;

namespace ZG.Voxel
{
    [Flags]
    public enum Flag
    {
        Boundary = 0x01,
        CastShadows = 0x02,
        Collide = 0x04,
        Static = 0x08
    }

    [Serializable]
    public struct Level : IEquatable<Level>
    {
        [Mask]
        public Flag flag;

        //public int depth;
        public int qefSweeps;

        public int minCollapseDegree;
        public int maxCollapseDegree;
        public int maxIterations;
        public float targetPecentage;
        public float edgeFraction;
        public float minAngleCosine;
        public float maxEdgeSize;
        public float maxError;

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
            return obj is Level ? Equals((Level)obj) : base.Equals(obj);
        }

        public bool Equals(Level other)
        {
            return
                //x.depth == y.depth &&
                qefSweeps == other.qefSweeps &&
                //Mathf.Approximately(x.qefError, y.qefError) &&
                Mathf.Approximately(threshold, other.threshold);
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

    public abstract class Processor<T, U> 
        where T : IEngine 
        where U : IEngineProcessor<T>, new()
    {
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

        private struct ThreadData : IEquatable<ThreadData>
        {
            public int id;
            public int version;

            public bool Equals(ThreadData other)
            {
                return id == other.id && version == other.version;
            }

            public override int GetHashCode()
            {
                return id;
            }
        }

        private struct MeshData
        {
            public Info info;

            public ThreadData threadData;

            public IDictionary<Vector3Int, MeshData<Vector3>>[] meshes;

            public MeshData(Info info, ThreadData threadData, IDictionary<Vector3Int, MeshData<Vector3>>[] meshes)
            {
                this.info = info;
                this.threadData = threadData;
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

        public delegate int SubMeshHandler(
            int level, 
            Face face,
            IReadOnlyList<Vertex> vertices, 
            U processor);

        public SubMeshHandler subMeshHandler;

        public int millisecondsTimeout = 2000;
        
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
        private volatile IEngineBuilder __builder;
        private ReaderWriterLockSlim __readerWriterLockSlim = new ReaderWriterLockSlim();
        private List<Vector3Int> __buffer;
        private List<Info> __in = new List<Info>();
        private Pool<MeshData> __out = new Pool<MeshData>();
        private Pool<U> __processors = new Pool<U>();
        private Dictionary<Vector3Int, ObjectData> __objects = new Dictionary<Vector3Int, ObjectData>();
        private Dictionary<ThreadData, List<Instance>> __instances = new Dictionary<ThreadData, List<Instance>>();
        private Dictionary<ThreadData, int> __threadCounters = new Dictionary<ThreadData, int>();
        private Dictionary<int, int> __threadVersions = new Dictionary<int, int>();

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

        public IEngineBuilder builder
        {
            get
            {
                lock (this)
                {
                    if (__builder == null)
                        __builder = Create(_depth, _increment, scale);
                }

                return __builder;
            }
        }

        ~Processor()
        {
            __readerWriterLockSlim.Dispose();
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

        public bool Instantiate(Instance instance)
        {
            ThreadData threadData;
            threadData.id = Thread.CurrentThread.ManagedThreadId;
            lock (__threadVersions)
            {
                if (!__threadVersions.TryGetValue(threadData.id, out threadData.version))
                    return false;
            }
            
            lock (__instances)
            {
                List<Instance> instances;
                if(!__instances.TryGetValue(threadData, out instances))
                {
                    instances = new List<Instance>();

                    __instances[threadData] = instances;
                }

                instances.Add(instance);
            }

            return true;
        }

        public bool ThreadUpdate()
        {
            ThreadData threadData;
            threadData.id = Thread.CurrentThread.ManagedThreadId;
            lock (__threadVersions)
            {
                if (!__threadVersions.TryGetValue(threadData.id, out threadData.version))
                    threadData.version = 0;

                __threadVersions[threadData.id] = ++threadData.version;
            }

            int index, numLevels, mask, i;
            Vector3 min, max;
            Level level;
            Info info;
            Bounds bounds;
            MeshData<Vector3> mesh;
            U processor;
            T engine;
            IEngineBuilder builder;
            Dictionary<Vector3Int, MeshData<Vector3>> map;
            List<KeyValuePair<Vector3Int, MeshData<Vector3>>> buffer;
            List<KeyValuePair<Vector3Int, MeshData<Vector3>>>[] meshes;
            IDictionary<Vector3Int, MeshData<Vector3>>[] result = null;
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

                lock(__processors)
                {
                    index = __processors.nextIndex;

                    __processors.TryGetValue(index, out processor);

                    if (processor == null)
                        processor = new U();

                    __processors.Insert(index, processor);
                }

                numLevels = info.levels == null ? 0 : info.levels.Length;

                meshes = null;

                builder = this.builder;
                if (builder != null)
                {
                    engine = default(T);
                    while (true)
                    {
                        if (__readerWriterLockSlim.TryEnterWriteLock(millisecondsTimeout))
                        {
                            engine = CreateOrUpdate(info.position);

                            __readerWriterLockSlim.ExitWriteLock();

                            break;
                        }
                        else
                            Debug.LogWarning("CreateOrUpdate Timeout.");
                    }

                    while (true)
                    {
                        if (__readerWriterLockSlim.TryEnterUpgradeableReadLock(millisecondsTimeout))
                        {
                            if (builder.Check(info.position))
                            {
                                while (true)
                                {
                                    if (__readerWriterLockSlim.TryEnterWriteLock(millisecondsTimeout))
                                    {
                                        builder.Create(info.position);
                                        __readerWriterLockSlim.ExitWriteLock();

                                        break;
                                    }
                                    else
                                        Debug.LogWarning("Write Timeout.");
                                }
                            }

                            __readerWriterLockSlim.ExitUpgradeableReadLock();

                            break;
                        }
                        else
                            Debug.LogWarning("Upgrade Timeout.");
                    }

                    while (true)
                    {
                        if (__readerWriterLockSlim.TryEnterReadLock(millisecondsTimeout))
                        {
                            for (i = 0; i < numLevels; ++i)
                            {
                                level = info.levels[i];

                                if (processor.Create(
                                    (level.flag & Flag.Boundary) == 0 ? Boundary.None : Boundary.All,
                                    level.qefSweeps,
                                    level.threshold,
                                    info.position,
                                    engine))
                                {
                                    int temp = i;
                                    if (processor.Build(Boundary.LeftLowerBack, (face, vertices) =>
                                    {
                                        return subMeshHandler == null ? 0 : subMeshHandler(temp, face, vertices, processor);
                                    }, out mesh))
                                    {
                                        if (level.minCollapseDegree > 0)
                                        {
                                            mesh = mesh.Simplify(
                                                level.qefSweeps,
                                                level.minCollapseDegree,
                                                level.maxCollapseDegree,
                                                level.maxIterations,
                                                level.targetPecentage,
                                                level.edgeFraction,
                                                level.minAngleCosine,
                                                level.maxEdgeSize,
                                                level.maxError);
                                        }

                                        if (meshes == null)
                                            meshes = new List<KeyValuePair<Vector3Int, MeshData<Vector3>>>[numLevels];

                                        buffer = new List<KeyValuePair<Vector3Int, MeshData<Vector3>>>();
                                        buffer.Add(new KeyValuePair<Vector3Int, MeshData<Vector3>>(Vector3Int.zero, mesh));

                                        meshes[i] = buffer;
                                    }
                                }
                            }
                            
                            __readerWriterLockSlim.ExitReadLock();

                            break;
                        }
                        else
                            Debug.LogWarning("Read Timeout.");
                    }
                }

                lock (__processors)
                {
                    __processors.RemoveAt(index);
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
                        foreach (KeyValuePair<Vector3Int, MeshData<Vector3>> pair in buffer)
                        {
                            if (map == null)
                                map = new Dictionary<Vector3Int, MeshData<Vector3>>();

                            map.Add(pair.Key, pair.Value);
                        }

                        if (map == null)
                            continue;

                        if (result == null)
                            result = new IDictionary<Vector3Int, MeshData<Vector3>>[numLevels];

                        result[i] = map;
                    }
                }

                lock (__threadCounters)
                {
                    if (!__threadCounters.TryGetValue(threadData, out i))
                        i = 0;

                    ++i;

                    __threadCounters[threadData] = i;
                }

                lock (__out)
                {
                    __out.Add(new MeshData(info, threadData, result));
                    
                    --__count;
                    
                    Debug.Log("Thread count: " + __count);
                }

            } while (result == null);
            
            return true;
        }

        public GameObject MainUpdate()
        {
            KeyValuePair<int, MeshData>? pair;
            lock (__out)
            {
                Pool<MeshData>.PairEnumerator enumerable = __out.GetPairEnumerator();
                if (enumerable.MoveNext())
                {
                    pair = enumerable.Current;

                    __out.RemoveAt(pair.Value.Key);
                }
                else
                    pair = null;
            }

            GameObject gameObject;
            if (pair == null)
                gameObject = null;
            else
            {
                MeshData meshData = pair.Value.Value;

                int count = Mathf.Min(
                    meshData.info.levels == null ? 0 : meshData.info.levels.Length,
                    meshData.meshes == null ? 0 : meshData.meshes.Length);

                if (count < 1)
                    gameObject = null;
                else
                {
                    IDictionary<Vector3Int, MeshData<Vector3>> source = meshData.meshes[0];
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
                            MeshData<Vector3> instance;
                            Transform root = null, parent = null, child;
                            GameObject local, world;
                            MeshCollider meshCollider;
                            LODGroup lodGroup;
                            IDictionary<Vector3Int, MeshData<Vector3>> destination;
                            List<LOD> lods;
                            List<Renderer> renderers = null;
                            List<MeshFilter> meshFilters = null;
                            foreach (KeyValuePair<Vector3Int, MeshData<Vector3>> mesh in source)
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

                                    level = meshData.info.levels[i];

                                    local = Convert(instance, level);
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

                                    lod.screenRelativeTransitionHeight = level.screenRelativeTransitionHeight;
                                    lod.fadeTransitionWidth = level.fadeTransitionWidth;
                                    lod.renderers = renderers.ToArray();

                                    if (lods == null)
                                        lods = new List<LOD>();

                                    lods.Add(lod);

                                    local.isStatic = (level.flag & Flag.Static) != 0;

                                    if ((level.flag & Flag.CastShadows) == 0)
                                    {
                                        foreach (Renderer renderer in renderers)
                                            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                                    }
                                    else
                                    {
                                        foreach (Renderer renderer in renderers)
                                            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                                    }

                                    if ((level.flag & Flag.Collide) != 0)
                                    {
                                        local = UnityEngine.Object.Instantiate(local, parent);
                                        if (local != null)
                                        {
                                            if ((level.flag & Flag.Static) != 0)
                                                local.isStatic = false;

                                            local.GetComponentsInChildren(renderers);
                                            if (local != null)
                                            {
                                                foreach (Renderer renderer in renderers)
                                                    UnityEngine.Object.Destroy(renderer);

                                                if (meshFilters == null)
                                                    meshFilters = new List<MeshFilter>();

                                                local.GetComponentsInChildren(meshFilters);
                                                foreach (MeshFilter meshFilter in meshFilters)
                                                {
                                                    local = meshFilter == null ? null : meshFilter.gameObject;
                                                    meshCollider = local == null ? null : local.AddComponent<MeshCollider>();
                                                    if (meshCollider != null)
                                                    {
                                                        meshCollider.sharedMesh = meshFilter.sharedMesh;

                                                        UnityEngine.Object.Destroy(meshFilter);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }

                                if (lodGroup != null && lods != null)
                                {
                                    lodGroup.SetLODs(lods.ToArray());
                                }
                            }
                        }
                        else
                        {
                            gameObject = null;

                            Level level = meshData.info.levels[0];
                            Transform parent = null, child;
                            GameObject instance, temp;
                            MeshCollider meshCollider;
                            List<Renderer> renderers = null;
                            List<MeshFilter> meshFilters = null;
                            foreach (KeyValuePair<Vector3Int, MeshData<Vector3>> mesh in source)
                            {
                                instance = Convert(mesh.Value, level);
                                if (instance == null)
                                    continue;

                                child = instance == null ? null : instance.transform;
                                if (child == null)
                                    continue;

                                if (gameObject == null)
                                {
                                    gameObject = new GameObject();

                                    parent = gameObject.transform;
                                }

                                child.SetParent(parent);

                                instance.isStatic = (level.flag & Flag.Static) != 0;

                                if (renderers == null)
                                    renderers = new List<Renderer>();

                                instance.GetComponentsInChildren(renderers);
                                if ((level.flag & Flag.CastShadows) == 0)
                                {
                                    foreach (Renderer renderer in renderers)
                                        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                                }
                                else
                                {
                                    foreach (Renderer renderer in renderers)
                                        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                                }

                                if ((level.flag & Flag.Collide) != 0)
                                {
                                    if (meshFilters == null)
                                        meshFilters = new List<MeshFilter>();

                                    instance.GetComponentsInChildren(meshFilters);

                                    foreach (MeshFilter meshFilter in meshFilters)
                                    {
                                        temp = meshFilter == null ? null : meshFilter.gameObject;
                                        meshCollider = temp == null ? null : temp.AddComponent<MeshCollider>();
                                        if (meshCollider != null)
                                            meshCollider.sharedMesh = meshFilter.sharedMesh;
                                    }
                                }
                            }
                        }
                    }
                }
                
                lock (__threadCounters)
                {
                    if (__threadCounters.TryGetValue(meshData.threadData, out count))
                    {
                        if (count > 1)
                            __threadCounters[meshData.threadData] = count - 1;
                        else
                        {
                            __threadCounters.Remove(meshData.threadData);

                            lock (__instances)
                            {
                                List<Instance> instances;
                                if (__instances.TryGetValue(meshData.threadData, out instances) && instances != null)
                                {
                                    UnityEngine.Object target;
                                    foreach (Instance instance in instances)
                                    {
                                        if (instance.predicate != null && !instance.predicate(instance.target))
                                            continue;

                                        target = UnityEngine.Object.Instantiate(instance.target);
                                        if (instance.handler != null)
                                            instance.handler(target);
                                    }

                                    instances.Clear();
                                }
                            }
                        }
                    }
                    else
                        Debug.LogError("WTF?");
                }

                //lock (__objects)
                {
                    ObjectData objectData;
                    if (__objects.TryGetValue(meshData.info.position, out objectData) && objectData.gameObject != null)
                        UnityEngine.Object.Destroy(objectData.gameObject);

                    __objects[meshData.info.position] = new ObjectData(gameObject, meshData.info.levels);
                }
            }
            
            return gameObject;
        }

        public abstract GameObject Convert(MeshData<Vector3> meshData, Level level);

        public abstract T CreateOrUpdate(Vector3Int world);

        public abstract IEngineBuilder Create(int depth, float increment, Vector3 scale);

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
}