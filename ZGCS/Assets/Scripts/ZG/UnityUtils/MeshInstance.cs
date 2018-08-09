using System;
using System.Reflection;
using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace ZG
{
    [ExecuteInEditMode]
    public class MeshInstance : MonoBehaviour
    {
        [SerializeField]
        internal MeshInstanceDatabase _database;

        private EntityManager __entityManager;
        private NativeArray<Entity> __nodes;
        private NativeArray<Entity> __objects;

        public MeshInstanceDatabase database
        {
            get
            {
                return _database;
            }

            set
            {
                _database = value;

                OnValidate();
            }
        }

        public void OnValidate()
        {
            if (_database == null)
                return;

            if (__entityManager == null || !__entityManager.IsCreated)
            {
                World world = World.Active;
                if (world == null)
                {
#if UNITY_EDITOR
                    if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        if (UnityEditor.EditorApplication.isPlaying)
                            Debug.LogError("Loading GameObjectEntity in Playmode but there is no active World");

                        return;
                    }
#endif
                    Assembly assembly = Assembly.Load("Unity.Entities.Hybrid");
                    if (assembly != null)
                    {
                        Type defaultWorldInitialization = assembly.GetType("Unity.Entities.DefaultWorldInitialization");
                        if (defaultWorldInitialization != null)
                        {
                            MethodInfo initialize = defaultWorldInitialization.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                            initialize.Invoke(null, new object[] { "Editor World", true });
                        }
                    }

                    world = World.Active;
                }

                __entityManager = world == null ? null : world.GetOrCreateManager<EntityManager>();
            }

            if (__entityManager != null)
            {
                if (__nodes.IsCreated)
                {
                    __entityManager.DestroyEntity(__nodes);

                    __nodes.Dispose();

                    __nodes = default(NativeArray<Entity>);
                }

                if(__objects.IsCreated)
                {
                    __entityManager.DestroyEntity(__objects);

                    __objects.Dispose();

                    __objects = default(NativeArray<Entity>);
                }

                __nodes = MeshInstanceDatabase.Instantiate(
                    Allocator.Persistent,
                    __entityManager, 
                    _database.nodes, 
                    _database.objects, 
                    out __objects);
            }
        }
        
        public void OnEnable()
        {
            OnValidate();
        }

        public void OnDisable()
        {
            if (__nodes.IsCreated)
            {
                if(__entityManager != null && __entityManager.IsCreated)
                    __entityManager.DestroyEntity(__nodes);

                __nodes.Dispose();

                __nodes = default(NativeArray<Entity>);
            }

            if (__objects.IsCreated)
            {
                if (__entityManager != null && __entityManager.IsCreated)
                    __entityManager.DestroyEntity(__objects);

                __objects.Dispose();

                __objects = default(NativeArray<Entity>);
            }
        }
    }
}