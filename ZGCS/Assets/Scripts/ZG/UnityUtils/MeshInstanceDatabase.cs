using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Rendering;
using UnityEngine;

namespace ZG
{
    public class MeshInstanceDatabase : ScriptableObject
    {
        [Serializable]
        public struct Node
        {
#if UNITY_EDITOR
            public string name;
#endif
            public int objectIndex;
            public int lod;

            public Matrix4x4 matrix;

            public MeshInstanceRenderer meshInstanceRenderer;
        }

        [Serializable]
        public struct Object
        {

#if UNITY_EDITOR
            public string name;
#endif
            public float size;
            public float limit0;
            public float limit1;
            public float limit2;

            public Matrix4x4 matrix;
        }

        public Node[] nodes;

        public Object[] objects;

        public static Node[] CreateDynamic(Transform root, out Object[] objects)
        {
            SkinnedMeshRenderer[] meshRenderers = root == null ? null : root.GetComponentsInChildren<SkinnedMeshRenderer>(!root.gameObject.activeInHierarchy);
            if (meshRenderers == null)
            {
                objects = null;

                return null;
            }

#if UNITY_EDITOR
            int count = meshRenderers == null ? 0 : meshRenderers.Length, index = 0;
#endif
            int i, numMaterials;
            Node node;
            Mesh mesh;
            Transform transform;
            Material[] materials;
            List<Node> nodes = null;
            Dictionary<Renderer, int> indices = null;
            foreach (SkinnedMeshRenderer meshRenderer in meshRenderers)
            {
                mesh = meshRenderer == null ? null : meshRenderer.sharedMesh;
                if (mesh == null)
                    continue;

                transform = meshRenderer.transform;
                if (transform == null)
                    continue;

#if UNITY_EDITOR
                node.name = meshRenderer.name;

                UnityEditor.EditorUtility.DisplayProgressBar("Building Nodes..", node.name, (index++ * 1.0f) / count);
#endif
                
                node.objectIndex = -1;
                node.lod = 0;

                node.matrix = transform.localToWorldMatrix;

                node.meshInstanceRenderer.mesh = mesh;
                node.meshInstanceRenderer.castShadows = meshRenderer.shadowCastingMode;
                node.meshInstanceRenderer.receiveShadows = meshRenderer.receiveShadows;

                node.meshInstanceRenderer.subMesh = 0;

                materials = meshRenderer.sharedMaterials;
                numMaterials = materials == null ? 0 : materials.Length;
                if (numMaterials > 0)
                {
                    if (nodes == null)
                        nodes = new List<Node>();

                    if (indices == null)
                        indices = new Dictionary<Renderer, int>();

                    indices.Add(meshRenderer, nodes.Count);

                    for (i = 0; i < numMaterials; ++i)
                    {
                        node.meshInstanceRenderer.material = materials[i];

                        nodes.Add(node);

                        ++node.meshInstanceRenderer.subMesh;
                    }
                }
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.ClearProgressBar();
#endif

            Node[] results = nodes == null ? null : nodes.ToArray();
            objects = __Create(root, results, indices);

            return results;
        }

        public static Node[] CreateStatic(Transform root, out Object[] objects)
        {
            GameObject gameObject = root == null ? null : root.gameObject;
            bool isActive = gameObject != null && gameObject.activeInHierarchy;
            MeshRenderer[] meshRenderers = root == null ? null : root.GetComponentsInChildren<MeshRenderer>(!isActive);
            int count = meshRenderers == null ? 0 : meshRenderers.Length;
            if (count < 1)
            {
                objects = null;

                return null;
            }

#if UNITY_EDITOR
            int index = 0;
#endif
            int i, numMaterials;
            Node node;
            Mesh mesh;
            MeshFilter meshFilter;
            Transform transform;
            Material[] materials;
            List<Node> nodes = null;
            Dictionary<Renderer, int> indices = null;
            foreach (MeshRenderer meshRenderer in meshRenderers)
            {
                meshFilter = meshRenderer == null ? null : meshRenderer.GetComponent<MeshFilter>();
                mesh = meshFilter == null ? null : meshFilter.sharedMesh;
                if (mesh == null)
                    continue;

                transform = meshRenderer.transform;
                if (transform == null)
                    continue;

#if UNITY_EDITOR
                node.name = meshRenderer.name;

                UnityEditor.EditorUtility.DisplayProgressBar("Building Nodes..", node.name, (index++ * 1.0f) / count);
#endif
                
                node.objectIndex = -1;
                node.lod = 0;

                node.matrix = transform.localToWorldMatrix;

                node.meshInstanceRenderer.mesh = mesh;
                node.meshInstanceRenderer.castShadows = meshRenderer.shadowCastingMode;
                node.meshInstanceRenderer.receiveShadows = meshRenderer.receiveShadows;

                node.meshInstanceRenderer.subMesh = meshRenderer.subMeshStartIndex;

                materials = meshRenderer.sharedMaterials;
                numMaterials = materials == null ? 0 : materials.Length;
                if (numMaterials > 0)
                {
                    if (nodes == null)
                        nodes = new List<Node>();

                    if (indices == null)
                        indices = new Dictionary<Renderer, int>();

                    indices.Add(meshRenderer, nodes.Count);

                    for (i = 0; i < numMaterials; ++i)
                    {
                        node.meshInstanceRenderer.material = materials[i];

                        nodes.Add(node);

                        ++node.meshInstanceRenderer.subMesh;
                    }
                }
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.ClearProgressBar();
#endif

            Node[] results = nodes == null ? null : nodes.ToArray();
            objects = __Create(root, results, indices);

            return results;
        }

        public static NativeArray<Entity> Instantiate(
            Allocator allocator, 
            EntityManager entityManager, 
            Node[] nodes, 
            Object[] objects, 
            out NativeArray<Entity> results)
        {
            results = default(NativeArray<Entity>);

            if (entityManager == null)
                return default(NativeArray<Entity>);

            int numNodes = nodes == null ? 0 : nodes.Length;
            if (numNodes < 1)
                return default(NativeArray<Entity>);

            TransformMatrix transformMatrix;
            Entity entity;
            float bias = 1.0f / QualitySettings.lodBias - 1.0f;
            int numObjects = objects == null ? 0 : objects.Length;
            if (numObjects > 0)
            {
                EntityArchetype objectType = entityManager.CreateArchetype(
                    ComponentType.Create<TransformMatrix>(),
                    ComponentType.Create<MeshLODGroupComponent>());

                Object result;
                MeshLODGroupComponent meshLODGroupComponent;
                results = new NativeArray<Entity>(numObjects, allocator);
                entityManager.CreateEntity(objectType, results);
                for (int i = 0; i < numObjects; ++i)
                {
                    result = objects[i];
                    entity = results[i];
                    
                    transformMatrix.Value = result.matrix;
                    entityManager.SetComponentData(entity, transformMatrix);

                    meshLODGroupComponent.activeLod = 0;
                    meshLODGroupComponent.size = result.size;
                    meshLODGroupComponent.biasMinusOne = bias;
                    meshLODGroupComponent.limit0 = result.limit0;
                    meshLODGroupComponent.limit1 = result.limit1;
                    meshLODGroupComponent.limit2 = result.limit2;

                    entityManager.SetComponentData(entity, meshLODGroupComponent);
                }
            }

            EntityArchetype nodeType = entityManager.CreateArchetype(
                ComponentType.Create<TransformMatrix>(),
                ComponentType.Create<MeshCullingComponent>(),
                ComponentType.Create<MeshInstanceRenderer>());

            Node node;
            MeshCullingComponent meshCullingComponent;
            MeshLODComponent meshLODComponent;
            Vector3 extents;
            Bounds bounds;
            NativeArray<Entity> entities = new NativeArray<Entity>(numNodes, allocator);
            entityManager.CreateEntity(nodeType, entities);
            for (int i = 0; i < numNodes; ++i)
            {
                node = nodes[i];
                entity = entities[i];

                transformMatrix.Value = node.matrix;
                entityManager.SetComponentData(entity, transformMatrix);

                bounds = node.meshInstanceRenderer.mesh == null ? new Bounds() : node.meshInstanceRenderer.mesh.bounds;
                extents = bounds.extents;
                meshCullingComponent.BoundingSphereCenter = bounds.center;
                meshCullingComponent.BoundingSphereRadius = Mathf.Max(extents.x, extents.y, extents.z);
                meshCullingComponent.CullStatus = 0.0f;
                entityManager.SetComponentData(entity, meshCullingComponent);

                if(node.objectIndex >= 0 && node.objectIndex < numObjects)
                {
                    meshLODComponent.isInactive = 0;
                    meshLODComponent.group = results[node.objectIndex];
                    meshLODComponent.lod = node.lod;

                    entityManager.AddComponentData(entity, meshLODComponent);
                }

                entityManager.SetSharedComponentData(entity, node.meshInstanceRenderer);
            }

            return entities;
        }

        private static Object[] __Create(Transform root, Node[] nodes, Dictionary<Renderer, int> indices)
        {
            LODGroup[] lodGroups = root == null ? null : root.GetComponentsInChildren<LODGroup>(!root.gameObject.activeInHierarchy);
            int count = lodGroups == null ? 0 : lodGroups.Length;
            if (count < 1)
                return null;

#if UNITY_EDITOR
            int index = 0;
#endif
            int numLods, numMaterials, objectIndex, nodeIndex, i, j;
            Transform transform;
            Node node;
            Object result;
            List<Object> results = null;
            LOD[] lods;
            Renderer[] renderers;
            Material[] materials;
            foreach (LODGroup lodGroup in lodGroups)
            {
                transform = lodGroup == null ? null : lodGroup.transform;
                if (transform == null)
                    continue;

#if UNITY_EDITOR
                result.name = lodGroup.name;

                UnityEditor.EditorUtility.DisplayProgressBar("Building Objects..", lodGroup.name, (index++ * 1.0f) / count);
#endif

                result.matrix = transform.localToWorldMatrix;

                result.size = lodGroup.size;

                lods = lodGroup.GetLODs();
                numLods = lods == null ? 0 : lods.Length;

                result.limit0 = numLods > 0 ? lods[0].screenRelativeTransitionHeight : 0.0f;
                result.limit1 = numLods > 1 ? lods[1].screenRelativeTransitionHeight : 0.0f;
                result.limit2 = numLods > 2 ? lods[2].screenRelativeTransitionHeight : 0.0f;

                if (results == null)
                    results = new List<Object>();

                objectIndex = results.Count;

                results.Add(result);

                if (indices != null)
                {
                    for (i = 0; i < numLods; ++i)
                    {
                        renderers = lods[i].renderers;
                        if (renderers != null)
                        {
                            foreach (Renderer renderer in renderers)
                            {
                                if (renderer == null || !indices.TryGetValue(renderer, out nodeIndex))
                                    continue;

                                materials = renderer.sharedMaterials;
                                numMaterials = materials == null ? 0 : materials.Length;
                                for (j = 0; j < numMaterials; ++j)
                                {
                                    node = nodes[nodeIndex + j];
                                    node.objectIndex = objectIndex;
                                    node.lod = i;

                                    nodes[nodeIndex] = node;
                                }
                            }
                        }
                    }
                }
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.ClearProgressBar();
#endif

            return results == null ? null : results.ToArray();
        }
    }
}