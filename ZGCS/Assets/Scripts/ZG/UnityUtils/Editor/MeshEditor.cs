using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace ZG
{
    public class MeshEditor : EditorWindow
    {
        private static Dictionary<UnityEngine.Object, UnityEngine.Object> __map;
        private static string __path;

        public const char KEY_SEPARATOR = ',';

        public const string KEY_IS_NORMALS_ONLY = "MeshEditorIsNormalsOnly";

        public const string KEY_IS_BACKUP_TO_TANGENTS = "MeshEditorIsBackupToTangents";

        public const string KEY_SEGMENT_LENGTH = "MeshEditorSegmentLength";
        public const string KEY_SEGMENT_WIDTH = "MeshEditorSegmentWidth";
        public const string KEY_SEGMENT_HEIGHT = "MeshEditorSegmentHeight";

        public const string KEY_SEGMENT_BOUNDS = "MeshEditorSegmentBounds";

        public const string KEY_UNWRAP_PARAM = "MeshEditorUnwrapParam";

        public struct Vertex
        {
            public Vector3 point;
            public Vector2 uv0;
            public Vector2 uv1;

            public Vertex(Vector3 point, Vector2 uv0, Vector2 uv1)
            {
                this.point = point;
                this.uv0 = uv0;
                this.uv1 = uv1;
            }
        }

        public static bool isNormalsOnly
        {
            get
            {
                return EditorPrefs.GetBool(KEY_IS_NORMALS_ONLY);
            }

            set
            {
                EditorPrefs.SetBool(KEY_IS_NORMALS_ONLY, value);
            }
        }

        public static bool isBackupToTangents
        {
            get
            {
                return EditorPrefs.GetBool(KEY_IS_BACKUP_TO_TANGENTS);
            }

            set
            {
                EditorPrefs.SetBool(KEY_IS_BACKUP_TO_TANGENTS, value);
            }
        }

        public static int splitSegmentLength
        {
            get
            {
                return EditorPrefs.GetInt(KEY_SEGMENT_LENGTH);
            }

            set
            {
                EditorPrefs.SetInt(KEY_SEGMENT_LENGTH, value);
            }
        }

        public static int splitSegmentWidth
        {
            get
            {
                return EditorPrefs.GetInt(KEY_SEGMENT_WIDTH);
            }

            set
            {
                EditorPrefs.SetInt(KEY_SEGMENT_WIDTH, value);
            }
        }

        public static int splitSegmentHeight
        {
            get
            {
                return EditorPrefs.GetInt(KEY_SEGMENT_HEIGHT);
            }

            set
            {
                EditorPrefs.SetInt(KEY_SEGMENT_HEIGHT, value);
            }
        }

        public static Bounds splitBounds
        {
            get
            {
                string result = EditorPrefs.GetString(KEY_SEGMENT_BOUNDS);
                if (string.IsNullOrEmpty(result))
                    return new Bounds();

                string[] values = result.Split(KEY_SEPARATOR);
                return new Bounds(
                    new Vector3(
                        float.Parse(values[0]),
                        float.Parse(values[1]),
                        float.Parse(values[2])),
                    new Vector3(
                        float.Parse(values[3]),
                        float.Parse(values[4]),
                        float.Parse(values[5])));
            }

            set
            {
                Vector3 center = value.center, size = value.size;
                EditorPrefs.SetString(KEY_SEGMENT_BOUNDS,
                    center.x.ToString() +
                    KEY_SEPARATOR +
                    center.y +
                    KEY_SEPARATOR +
                    center.z +
                    KEY_SEPARATOR +
                    size.x +
                    KEY_SEPARATOR +
                    size.y +
                    KEY_SEPARATOR +
                    size.z);
            }
        }

        public static UnwrapParam unwrapParam
        {
            get
            {
                UnwrapParam unwrapParam;
                UnwrapParam.SetDefaults(out unwrapParam);
                string result = EditorPrefs.GetString(KEY_UNWRAP_PARAM);
                if (string.IsNullOrEmpty(result))
                    return unwrapParam;

                string[] values = result.Split(KEY_SEPARATOR);
                unwrapParam.angleError = float.Parse(values[0]);
                unwrapParam.areaError = float.Parse(values[1]);
                unwrapParam.hardAngle = float.Parse(values[2]);
                unwrapParam.packMargin = float.Parse(values[3]);

                return unwrapParam;
            }

            set
            {
                EditorPrefs.SetString(KEY_UNWRAP_PARAM,
                    value.angleError.ToString() +
                    KEY_SEPARATOR +
                    value.areaError +
                    KEY_SEPARATOR +
                    value.hardAngle +
                    KEY_SEPARATOR +
                    value.packMargin);
            }
        }

        public static string SaveMeshes(bool isShowProgressbar, GameObject gameObject, ref string folder)
        {
            if (gameObject == null)
                return null;

            bool result = false;
            int i;
            string name;
            UnityEngine.Object instance;
            Mesh mesh;

            string path = folder + '/' + gameObject.name;

            name = path + ".asset";

            MeshFilter[] meshFilters = gameObject.GetComponentsInChildren<MeshFilter>(true);

            int numMeshFilters = meshFilters == null ? 0 : meshFilters.Length;
            if (numMeshFilters > 0)
            {
                //EditorHelper.CreateFolder(name);

                MeshFilter meshFilter;
                for (i = 0; i < numMeshFilters; ++i)
                {
                    meshFilter = meshFilters[i];
                    if (meshFilter == null)
                        continue;

                    mesh = meshFilter == null ? null : meshFilter.sharedMesh;
                    if (mesh == null)
                        continue;

                    if (isShowProgressbar && EditorUtility.DisplayCancelableProgressBar("Save Mesh Filters..", meshFilter.name, i * 1.0f / numMeshFilters))
                    {
                        folder = string.Empty;

                        return null;
                    }

                    if (__map == null)
                        __map = new Dictionary<UnityEngine.Object, UnityEngine.Object>();

                    if (!__map.TryGetValue(mesh, out instance) || !(instance is Mesh))
                    {
                        if (AssetDatabase.IsNativeAsset(mesh))
                            instance = mesh;
                        else
                        {
                            instance = Instantiate(mesh);

                            if (result)
                                AssetDatabase.AddObjectToAsset(instance, name);
                            else
                            {
                                result = true;

                                AssetDatabase.CreateAsset(instance, name);
                            }
                        }

                        __map[mesh] = instance;
                    }

                    meshFilter.sharedMesh = instance as Mesh;
                }
            }


            MeshCollider[] meshColliders = gameObject.GetComponentsInChildren<MeshCollider>(true);

            int numMeshColliders = meshColliders == null ? 0 : meshColliders.Length;
            if (numMeshColliders > 0)
            {
                //EditorHelper.CreateFolder(name);

                MeshCollider meshCollider;
                for (i = 0; i < numMeshColliders; ++i)
                {
                    meshCollider = meshColliders[i];
                    if (meshCollider == null)
                        continue;

                    mesh = meshCollider == null ? null : meshCollider.sharedMesh;
                    if (mesh == null)
                        continue;

                    if (isShowProgressbar && EditorUtility.DisplayCancelableProgressBar("Save Mesh Colliders..", meshCollider.name, i * 1.0f / numMeshColliders))
                    {
                        folder = string.Empty;

                        return null;
                    }

                    if (__map == null)
                        __map = new Dictionary<UnityEngine.Object, UnityEngine.Object>();

                    if (!__map.TryGetValue(mesh, out instance) || !(instance is Mesh))
                    {
                        if (AssetDatabase.IsNativeAsset(mesh))
                            instance = mesh;
                        else
                        {
                            instance = Instantiate(mesh);

                            if (result)
                                AssetDatabase.AddObjectToAsset(instance, name);
                            else
                            {
                                result = true;

                                AssetDatabase.CreateAsset(instance, name);
                            }
                        }

                        __map[mesh] = instance;
                    }

                    meshCollider.sharedMesh = instance as Mesh;
                }
            }

            if(isShowProgressbar)
                EditorUtility.ClearProgressBar();

            return path;
        }

        public static bool Split(MeshFilter source, Plane plane, out MeshFilter destination)
        {
            destination = null;
            if (source != null)
            {
                Mesh x, y;
                if (Split(plane, source.sharedMesh, out x, out y))
                {
                    if (x != null)
                    {
                        source.sharedMesh = x;

                        MeshCollider meshCollider = source.GetComponent<MeshCollider>();
                        if (meshCollider != null)
                            meshCollider.sharedMesh = x;
                    }

                    if (y != null)
                    {
                        if (x == null)
                        {
                            source.sharedMesh = y;

                            MeshCollider meshCollider = source.GetComponent<MeshCollider>();
                            if (meshCollider != null)
                                meshCollider.sharedMesh = y;
                        }
                        else
                        {
                            Transform tranform = source.transform;
                            if (tranform != null)
                            {
                                //tranform.DetachChildren();

                                tranform = tranform.parent;
                            }

                            destination = Instantiate(source, tranform);
                            if (destination != null)
                            {
                                destination.sharedMesh = y;

                                MeshCollider meshCollider = destination.GetComponent<MeshCollider>();
                                if (meshCollider != null)
                                    meshCollider.sharedMesh = y;
                            }
                        }
                    }

                    return true;
                }
            }

            return false;
        }

        public static bool Split(Plane plane, Mesh mesh, out Mesh x, out Mesh y)
        {
            if (mesh == null)
            {
                x = null;
                y = null;

                return false;
            }

            //plane = plane.flipped;

            List<Vertex> verticesX = null, verticesY = null;
            List<int> indicesX = null, indicesY = null;

            Vector3[] vertices = mesh.vertices;
            Vector2[] uvs0 = mesh.uv, uvs1 = mesh.uv2;

            bool isUVs0, isUVs1;
            int vertexCount = mesh.vertexCount;

            if (uvs0 == null || uvs0.Length < vertexCount)
            {
                uvs0 = new Vector2[vertexCount];

                isUVs0 = false;
            }
            else
                isUVs0 = true;

            if (uvs1 == null || uvs1.Length < vertexCount)
            {
                uvs1 = new Vector2[vertexCount];

                isUVs1 = false;
            }
            else
                isUVs1 = true;

            bool[] above = new bool[vertexCount];
            int[] indices = new int[vertexCount];

            Vector3 vertex;
            for (int i = 0; i < vertexCount; ++i)
            {
                vertex = vertices[i];
                if (above[i] = plane.GetSide(vertex))
                {
                    if (verticesX == null)
                        verticesX = new List<Vertex>();

                    indices[i] = verticesX.Count;

                    verticesX.Add(new Vertex(vertex, uvs0[i], uvs1[i]));
                }
                else
                {
                    if (verticesY == null)
                        verticesY = new List<Vertex>();

                    indices[i] = verticesY.Count;

                    verticesY.Add(new Vertex(vertex, uvs0[i], uvs1[i]));
                }
            }

            int[] triangles = mesh.triangles;
            int indexCount = triangles == null ? 0 : triangles.Length, indexX, indexY, indexZ, index0, index1, index2, indexXU, indexYU, indexXV, indexYV;
            bool aboveX, aboveY, aboveZ, result;
            float enter, invMagnitude;
            Vector3 vertexX, vertexY, vertexZ, vertexW;
            Vector2 uv0, uv1, uv0X, uv0Y, uv0Z, uv1X, uv1Y, uv1Z;
            for (int i = 0; i < indexCount; i += 3)
            {
                indexX = triangles[i + 0];
                indexY = triangles[i + 1];
                indexZ = triangles[i + 2];

                aboveX = above[indexX];
                aboveY = above[indexY];
                aboveZ = above[indexZ];

                if (aboveX && aboveY && aboveZ)
                {
                    if (indicesX == null)
                        indicesX = new List<int>();

                    indicesX.Add(indices[indexX]);
                    indicesX.Add(indices[indexY]);
                    indicesX.Add(indices[indexZ]);
                }
                else if (!aboveX && !aboveY && !aboveZ)
                {
                    if (indicesY == null)
                        indicesY = new List<int>();

                    indicesY.Add(indices[indexX]);
                    indicesY.Add(indices[indexY]);
                    indicesY.Add(indices[indexZ]);
                }
                else
                {
                    if (aboveX == aboveY)
                    {
                        result = aboveZ;

                        index0 = indices[indexX];
                        index1 = indices[indexY];
                        index2 = indices[indexZ];

                        vertexX = vertices[indexX];
                        vertexY = vertices[indexY];
                        vertexZ = vertices[indexZ];

                        uv0X = uvs0[indexX];
                        uv0Y = uvs0[indexY];
                        uv0Z = uvs0[indexZ];

                        uv1X = uvs1[indexX];
                        uv1Y = uvs1[indexY];
                        uv1Z = uvs1[indexZ];
                    }
                    else if (aboveY == aboveZ)
                    {
                        result = aboveX;

                        index0 = indices[indexY];
                        index1 = indices[indexZ];
                        index2 = indices[indexX];

                        vertexX = vertices[indexY];
                        vertexY = vertices[indexZ];
                        vertexZ = vertices[indexX];

                        uv0X = uvs0[indexY];
                        uv0Y = uvs0[indexZ];
                        uv0Z = uvs0[indexX];

                        uv1X = uvs1[indexY];
                        uv1Y = uvs1[indexZ];
                        uv1Z = uvs1[indexX];
                    }
                    else
                    {
                        result = aboveY;

                        index0 = indices[indexZ];
                        index1 = indices[indexX];
                        index2 = indices[indexY];

                        vertexX = vertices[indexZ];
                        vertexY = vertices[indexX];
                        vertexZ = vertices[indexY];

                        uv0X = uvs0[indexZ];
                        uv0Y = uvs0[indexX];
                        uv0Z = uvs0[indexY];

                        uv1X = uvs1[indexZ];
                        uv1Y = uvs1[indexX];
                        uv1Z = uvs1[indexY];
                    }

                    indexXU = indexXV = indexYU = indexYV = -1;

                    vertexW = vertexX - vertexZ;
                    invMagnitude = vertexW.magnitude;
                    invMagnitude = 1.0f / invMagnitude;
                    vertexW *= invMagnitude;
                    plane.Raycast(new Ray(vertexZ, vertexW), out enter);

                    vertexW *= enter;
                    vertexW += vertexZ;

                    enter *= invMagnitude;

                    uv0 = (uv0X - uv0Z) * enter + uv0Z;
                    uv1 = (uv1X - uv1Z) * enter + uv1Z;

                    if (verticesX == null)
                        verticesX = new List<Vertex>();

                    indexXU = verticesX.Count;

                    verticesX.Add(new Vertex(vertexW, uv0, uv1));

                    if (verticesY == null)
                        verticesY = new List<Vertex>();

                    indexYU = verticesY.Count;

                    verticesY.Add(new Vertex(vertexW, uv0, uv1));

                    vertexW = vertexY - vertexZ;
                    invMagnitude = vertexW.magnitude;
                    invMagnitude = 1.0f / invMagnitude;
                    vertexW *= invMagnitude;
                    plane.Raycast(new Ray(vertexZ, vertexW), out enter);

                    vertexW *= enter;
                    vertexW += vertexZ;

                    enter *= invMagnitude;

                    uv0 = (uv0Y - uv0Z) * enter + uv0Z;
                    uv1 = (uv1Y - uv1Z) * enter + uv1Z;

                    if (verticesX == null)
                        verticesX = new List<Vertex>();

                    indexXV = verticesX.Count;

                    verticesX.Add(new Vertex(vertexW, uv0, uv1));

                    if (verticesY == null)
                        verticesY = new List<Vertex>();

                    indexYV = verticesY.Count;

                    verticesY.Add(new Vertex(vertexW, uv0, uv1));

                    if (result)
                    {
                        if (indicesX == null)
                            indicesX = new List<int>();

                        indicesX.Add(index2);
                        indicesX.Add(indexXU);
                        indicesX.Add(indexXV);

                        if (indicesY == null)
                            indicesY = new List<int>();

                        indicesY.Add(index0);
                        indicesY.Add(index1);
                        indicesY.Add(indexYU);

                        indicesY.Add(index1);
                        indicesY.Add(indexYV);
                        indicesY.Add(indexYU);
                    }
                    else
                    {
                        if (indicesY == null)
                            indicesY = new List<int>();

                        indicesY.Add(index2);
                        indicesY.Add(indexYU);
                        indicesY.Add(indexYV);

                        if (indicesX == null)
                            indicesX = new List<int>();

                        indicesX.Add(index0);
                        indicesX.Add(index1);
                        indicesX.Add(indexXU);

                        indicesX.Add(index1);
                        indicesX.Add(indexXV);
                        indicesX.Add(indexXU);
                    }
                }
            }

            Vector3[] tempVertices;
            Vector2[] tempUVs0;
            Vector2[] tempUVs1;

            if (verticesX != null && indicesX != null)
            {
                int numVertices = verticesX.Count;

                tempVertices = new Vector3[numVertices];
                tempUVs0 = new Vector2[numVertices];
                tempUVs1 = new Vector2[numVertices];

                Vertex temp;
                for (int i = 0; i < numVertices; ++i)
                {
                    temp = verticesX[i];

                    tempVertices[i] = temp.point;
                    tempUVs0[i] = temp.uv0;
                    tempUVs1[i] = temp.uv1;
                }

                x = new Mesh();
                x.vertices = tempVertices;

                if(isUVs0)
                    x.uv = tempUVs0;

                if(isUVs1)
                    x.uv2 = tempUVs1;

                x.triangles = indicesX.ToArray();

                x.RecalculateNormals();
                x.RecalculateBounds();
            }
            else
                x = null;

            if (verticesY != null && indicesY != null)
            {
                int numVertices = verticesY.Count;

                tempVertices = new Vector3[numVertices];
                tempUVs0 = new Vector2[numVertices];
                tempUVs1 = new Vector2[numVertices];

                Vertex temp;
                for (int i = 0; i < numVertices; ++i)
                {
                    temp = verticesY[i];

                    tempVertices[i] = temp.point;
                    tempUVs0[i] = temp.uv0;
                    tempUVs1[i] = temp.uv1;
                }

                y = new Mesh();
                y.vertices = tempVertices;

                if (isUVs0)
                    y.uv = tempUVs0;

                if(isUVs1)
                    y.uv2 = tempUVs1;

                y.triangles = indicesY.ToArray();

                y.RecalculateNormals();
                y.RecalculateBounds();
            }
            else
                y = null;

            return x != null || y != null;
        }

        public static bool Flat(Mesh source, ref Mesh destination)
        {
            if (source == null)
                return false;

            Vector3[] sourceVertices = source.vertices;
            Vector2[] sourceUV = source.uv, sourceUV2 = source.uv2;
            int[] triangles = source.triangles;
            int indexCount = triangles == null ? 0 : triangles.Length, index;
            Vector2[] destinationUV = sourceUV == null || sourceUV.Length < 1 ? null : new Vector2[indexCount],
                destinationUV2 = sourceUV2 == null || sourceUV2.Length < 1 ? null : new Vector2[indexCount];
            Vector3[] destinationVertices = new Vector3[indexCount];

            for (int i = 0; i < indexCount; i++)
            {
                index = triangles[i];

                destinationVertices[i] = sourceVertices[index];

                if (destinationUV != null)
                    destinationUV[i] = sourceUV[index];

                if (destinationUV2 != null)
                    destinationUV2[i] = sourceUV2[index];

                triangles[i] = i;
            }

            if (destination == null)
                destination = new Mesh();

            destination.vertices = destinationVertices;
            destination.uv = destinationUV;
            destination.uv2 = destinationUV2;
            destination.triangles = triangles;
            destination.RecalculateBounds();
            destination.RecalculateNormals();

            return true;
        }

        public static bool Phong(bool isNormalsOnly, bool isBackupToTangents, Mesh source, ref Mesh destination)
        {
            if (source == null)
                return false;

            Vector3[] points = source.vertices;
            Vector2[] uv = source.uv, uv2 = source.uv2;
            int numPoints = points == null ? 0 : points.Length,
                numUVs = uv == null ? 0 : uv.Length,
                numUV2s = uv2 == null ? 0 : uv2.Length,
                index = 0,
                i, j;
            Vertex vertex;
            List<Vertex> vertices = null;
            Dictionary<int, int> map = null;

            for (i = 0; i < numPoints; i++)
            {
                if (map == null)
                    map = new Dictionary<int, int>();

                if (map.ContainsKey(i))
                    continue;

                map[i] = index;

                vertex = new Vertex(points[i], i < numUVs ? uv[i] : Vector2.zero, i < numUV2s ? uv2[i] : Vector2.zero);

                if (vertices == null)
                    vertices = new List<Vertex>();

                vertices.Add(vertex);

                for (j = i + 1; j < numPoints; ++j)
                {
                    if (vertex.point == points[j] &&
                        (j >= numUVs || vertex.uv0 == uv[j]) &&
                        (j >= numUV2s || vertex.uv1 == uv2[j]))
                        map[j] = index;
                }

                ++index;
            }

            if (destination == null)
                destination = new Mesh();

            int numVertices = vertices == null ? 0 : vertices.Count;
            if (numVertices > 0)
            {
                points = new Vector3[numVertices];
                uv = numUVs > 0 ? new Vector2[numVertices] : null;
                uv2 = numUV2s > 0 ? new Vector2[numVertices] : null;
                for (i = 0; i < numVertices; ++i)
                {
                    vertex = vertices[i];

                    points[i] = vertex.point;

                    if (uv != null)
                        uv[i] = vertex.uv0;

                    if (uv2 != null)
                        uv2[i] = vertex.uv1;
                }
            }

            destination.vertices = points;

            if (numUVs > 0)
                destination.uv = uv;

            if (numUV2s > 0)
                destination.uv2 = uv2;

            int[] triangles = source.triangles;
            if (map != null)
            {
                int indexCount = triangles == null ? 0 : triangles.Length;
                for (i = 0; i < indexCount; ++i)
                {
                    if (map.TryGetValue(triangles[i], out index))
                        triangles[i] = index;
                }
            }

            destination.triangles = triangles;

            destination.RecalculateBounds();
            destination.RecalculateNormals();

            if (isNormalsOnly)
            {
                Vector3[] destinationNormals = destination.normals;
                int numDestinationNormals = destinationNormals == null ? 0 : destinationNormals.Length;
                if (numDestinationNormals > 0)
                {
                    Mesh mesh = Instantiate(source);
                    Vector4[] tangents = null;
                    Vector3[] sourceNormals = source.normals;
                    int numSourceNormals = sourceNormals == null ? 0 : sourceNormals.Length;
                    if (numSourceNormals == numPoints)
                    {
                        if (isBackupToTangents)
                        {
                            Vector3 normal;
                            tangents = new Vector4[numSourceNormals];
                            for (i = 0; i < numSourceNormals; ++i)
                            {
                                normal = sourceNormals[i];
                                tangents[i] = new Vector4(normal.x, normal.y, normal.z, 0.0f);
                            }
                        }
                    }
                    else
                        sourceNormals = new Vector3[numPoints];

                    for (i = 0; i < numPoints; ++i)
                    {
                        if (map.TryGetValue(i, out index) && index >= 0 && index < numDestinationNormals)
                            sourceNormals[i] = destinationNormals[index];
                    }

                    mesh.normals = sourceNormals;

                    if (tangents != null)
                        mesh.tangents = tangents;

                    DestroyImmediate(destination);

                    destination = mesh;
                }
            }

            return true;
        }

        public static Vector3 Abs(Vector3 v)
        {
            return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        }

        public static Bounds GetWorldBounds(Matrix4x4 mat, Bounds bounds)
        {
            var absAxisX = Abs(mat.MultiplyVector(Vector3.right));
            var absAxisY = Abs(mat.MultiplyVector(Vector3.up));
            var absAxisZ = Abs(mat.MultiplyVector(Vector3.forward));
            var worldPosition = mat.MultiplyPoint(bounds.center);
            var worldSize = absAxisX * bounds.size.x + absAxisY * bounds.size.y + absAxisZ * bounds.size.z;
            return new Bounds(worldPosition, worldSize);
        }

        [MenuItem("GameObject/ZG/Mesh/Save", false, 10)]
        public static void Save(MenuCommand menuCommand)
        {
            GameObject gameObject = menuCommand == null ? null : menuCommand.context as GameObject;
            if (gameObject == null)
                return;

            if (__path == null)
            {
                __path = EditorUtility.SaveFolderPanel("Save Meshes", string.Empty, string.Empty);
                if (string.IsNullOrEmpty(__path))
                {
                    __path = null;

                    return;
                }

                __path = __path.Remove(0, Application.dataPath.Length + 1);
                __path = "Assets/" + __path;

                if (__map != null)
                    __map.Clear();

                Selection.selectionChanged += __OnSelectionChange;
            }
            else if (__path == string.Empty)
                return;

            string path = SaveMeshes(true, gameObject, ref __path);
            
            gameObject.SetActive(false);
            gameObject.SetActive(true);

            PrefabUtility.CreatePrefab(path + ".prefab", gameObject);

        }

        [MenuItem("GameObject/ZG/Mesh/Split", false, 10)]
        public static void Split(MenuCommand menuCommand)
        {
            GameObject gameObject = menuCommand == null ? null : menuCommand.context as GameObject;
            if (gameObject == null)
                return;

            int length = splitSegmentLength, width = splitSegmentWidth, height = splitSegmentHeight;
            if (length < 2 && width < 2 && height < 2)
                return;

            int count = (Mathf.Max(length - 1, 0) + Mathf.Max(width - 1, 0) + Mathf.Max(height, 0)), index = 0, i, j, numFilters;
            Bounds bounds = splitBounds;
            Vector3 center = bounds.center, extents = bounds.extents, segments = new Vector3(extents.x / width, extents.y / height, extents.z / length);
            Transform transform;
            MeshFilter source, destination;
            List<MeshFilter> meshFilters = null;

            segments *= 2.0f;

            if (meshFilters != null)
                meshFilters.Clear();

            if (gameObject != null)
            {
                if (meshFilters == null)
                    meshFilters = new List<MeshFilter>();

                gameObject.GetComponentsInChildren(true, meshFilters);
            }

            numFilters = meshFilters == null ? 0 : meshFilters.Count;
            if (numFilters > 0)
            {
                for (i = 1; i < length; ++i)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Split Mesh", "Lenght: " + i, index++ * 1.0f / count))
                    {
                        EditorUtility.ClearProgressBar();

                        return;
                    }

                    for (j = 0; j < numFilters; ++j)
                    {
                        source = meshFilters[j];
                        transform = source == null ? null : source.transform;
                        if (transform != null && Split(source,
                            transform.InverseTransform(new Plane(Vector3.forward, -(center.z - extents.z + segments.z * i))),
                            out destination) && destination != null)
                            meshFilters.Add(destination);
                    }
                }

                numFilters = meshFilters.Count;
                for (i = 1; i < width; ++i)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Split Mesh", "Width: " + i, index++ * 1.0f / count))
                    {
                        EditorUtility.ClearProgressBar();

                        return;
                    }

                    for (j = 0; j < numFilters; ++j)
                    {
                        source = meshFilters[j];
                        transform = source == null ? null : source.transform;
                        if (transform != null && Split(source,
                            transform.InverseTransform(new Plane(Vector3.right, -(center.x - extents.x + segments.x * i))),
                            out destination) && destination != null)
                            meshFilters.Add(destination);
                    }
                }

                numFilters = meshFilters.Count;
                for (i = 1; i < height; ++i)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Split Mesh", "Height: " + i, index++ * 1.0f / count))
                    {
                        EditorUtility.ClearProgressBar();

                        return;
                    }

                    for (j = 0; j < numFilters; ++j)
                    {
                        source = meshFilters[j];
                        transform = source == null ? null : source.transform;
                        if (transform != null && Split(source,
                            transform.InverseTransform(new Plane(Vector3.up, -(center.y - extents.y + segments.y * i))),
                            out destination) && destination != null)
                            meshFilters.Add(destination);
                    }
                }
            }

            EditorUtility.ClearProgressBar();
        }

        [MenuItem("GameObject/ZG/Mesh/Flat", false, 10)]
        public static void Flat(MenuCommand menuCommand)
        {
            GameObject gameObject = menuCommand == null ? null : menuCommand.context as GameObject;
            if (gameObject == null)
                return;

            Mesh mesh;
            MeshFilter[] meshFilters;
            meshFilters = gameObject == null ? null : gameObject.GetComponentsInChildren<MeshFilter>(true);
            if (meshFilters != null)
            {
                foreach (MeshFilter meshFilter in meshFilters)
                {
                    if (meshFilter != null)
                    {
                        mesh = null;
                        if (Flat(meshFilter.sharedMesh, ref mesh) && mesh != null)
                            meshFilter.sharedMesh = mesh;
                    }
                }
            }
        }

        [MenuItem("GameObject/ZG/Mesh/Phong", false, 10)]
        public static void Phong(MenuCommand menuCommand)
        {
            GameObject gameObject = menuCommand == null ? null : menuCommand.context as GameObject;
            if (gameObject == null)
                return;

            Mesh mesh;
            MeshFilter[] meshFilters;
            meshFilters = gameObject == null ? null : gameObject.GetComponentsInChildren<MeshFilter>(true);
            if (meshFilters != null)
            {
                foreach (MeshFilter meshFilter in meshFilters)
                {
                    if (meshFilter != null)
                    {
                        mesh = null;
                        if (Phong(isNormalsOnly, isBackupToTangents, meshFilter.sharedMesh, ref mesh) && mesh != null)
                            meshFilter.sharedMesh = mesh;
                    }
                }
            }
        }

        [MenuItem("GameObject/ZG/Mesh/Generate Per Triangle UV", false, 10)]
        public static void GeneratePerTriangleUV(MenuCommand menuCommand)
        {
            GameObject gameObject = menuCommand == null ? null : menuCommand.context as GameObject;
            if (gameObject == null)
                return;

            int i, numMeshFilters;
            Mesh mesh;
            MeshFilter meshFilter;
            MeshFilter[] meshFilters;
            HashSet<Mesh> meshes = null;

            meshFilters = gameObject.GetComponentsInChildren<MeshFilter>(true);
            numMeshFilters = meshFilters == null ? 0 : meshFilters.Length;
            if (numMeshFilters > 0)
            {
                for (i = 0; i < numMeshFilters; ++i)
                {
                    meshFilter = meshFilters[i];
                    if (meshFilter == null)
                        continue;

                    if (EditorUtility.DisplayCancelableProgressBar("Generate By Mesh Filters..", meshFilter.name, i * 1.0f / numMeshFilters))
                        break;

                    mesh = meshFilter.sharedMesh;
                    if (mesh != null)
                    {
                        if (meshes == null)
                            meshes = new HashSet<Mesh>();

                        if (meshes.Add(mesh))
                        {
                            mesh.uv = Unwrapping.GeneratePerTriangleUV(mesh, unwrapParam);

                            EditorUtility.SetDirty(mesh);
                        }
                    }
                }
            }

            EditorUtility.ClearProgressBar();
        }

        [MenuItem("GameObject/ZG/Mesh/Generate Secondary UV Set", false, 10)]
        public static void GenerateSecondaryUVSet(MenuCommand menuCommand)
        {
            GameObject gameObject = menuCommand == null ? null : menuCommand.context as GameObject;
            if (gameObject == null)
                return;

            int i, numMeshFilters;
            Mesh mesh;
            MeshFilter meshFilter;
            MeshFilter[] meshFilters;
            HashSet<Mesh> meshes = null;
            meshFilters = gameObject.GetComponentsInChildren<MeshFilter>(true);
            numMeshFilters = meshFilters == null ? 0 : meshFilters.Length;
            if (numMeshFilters > 0)
            {
                for (i = 0; i < numMeshFilters; ++i)
                {
                    meshFilter = meshFilters[i];
                    if (meshFilter == null)
                        continue;

                    if (EditorUtility.DisplayCancelableProgressBar("Generate By Mesh Filters..", meshFilter.name, i * 1.0f / numMeshFilters))
                        break;

                    mesh = meshFilter.sharedMesh;
                    if (mesh != null)
                    {
                        if (meshes == null)
                            meshes = new HashSet<Mesh>();

                        if (meshes.Add(mesh))
                        {
                            Unwrapping.GenerateSecondaryUVSet(mesh, unwrapParam);
                            //mesh.uv2 = mesh.uv;
                            Vector2[] uv = mesh.uv;
                            if (uv == null || uv.Length < mesh.vertexCount)
                                mesh.uv = mesh.uv2;

                            EditorUtility.SetDirty(mesh);
                        }
                    }
                }
            }

            EditorUtility.ClearProgressBar();
        }

        [MenuItem("Assets/ZG/Mesh/Instantiate Sub Material", false, 10)]
        public static void InstantiateSubMaterial(MenuCommand menuCommand)
        {
            Material material = menuCommand.context as Material;
            if (material == null)
                return;

            MeshRenderer[] renderers = FindObjectsOfType<MeshRenderer>();
            int numRenderers = renderers == null ? 0 : renderers.Length;
            if (numRenderers < 1)
                return;

            const uint MAX_INDEX_COUNT = 65000;

            int i, k, temp, numCombineInstances, numVertexIndices, vertexIndex, index;
            uint numIndices;
            CombineInstance combineInstance;
            Mesh mesh;
            MeshRenderer renderer;
            MeshFilter meshFilter;
            GameObject gameObject;

            Material[] materials;
            Vector3[] vertexBuffer;
            CombineInstance[] results;
            List<CombineInstance> combineInstances = null;
            List<Vector3> vertices = null;
            List<int> indices = null, indexBuffer = null;
            Dictionary<int, int> indexMap = null;

            for (i = 0; i < numRenderers; ++i)
            {
                renderer = renderers[i];
                gameObject = renderer == null ? null : renderer.gameObject;
                if (gameObject == null || !gameObject.isStatic)
                    continue;

                materials = renderer == null ? null : renderer.sharedMaterials;
                index = materials == null ? -1 : System.Array.IndexOf(materials, material);
                if (index == -1)
                    continue;

                meshFilter = renderer.GetComponent<MeshFilter>();
                mesh = meshFilter == null ? null : meshFilter.sharedMesh;
                if (mesh == null)
                    continue;

                combineInstance = new CombineInstance();
                combineInstance.mesh = mesh;
                combineInstance.subMeshIndex = index;

                if (combineInstances == null)
                    combineInstances = new List<CombineInstance>();

                combineInstances.Add(combineInstance);
            }

            numCombineInstances = combineInstances == null ? 0 : combineInstances.Count;
            if (numCombineInstances < 1)
                return;

            index = 0;
            numIndices = 0;
            for (i = 0; i < numCombineInstances; ++i)
            {
                combineInstance = combineInstances[i];
                temp = (int)combineInstance.mesh.GetIndexCount(combineInstance.subMeshIndex);
                numIndices += (uint)temp;
                if (numIndices > MAX_INDEX_COUNT)
                {
                    numIndices = (uint)temp;
                    temp = i - index;
                    results = new CombineInstance[temp];

                    combineInstances.CopyTo(index, results, 0, (int)temp);

                    index = i;

                    mesh = new Mesh();
                    //mesh.CombineMeshes(results, true, false, false);

                    if (vertices != null)
                        vertices.Clear();

                    if (indices != null)
                        indices.Clear();

                    foreach (CombineInstance result in results)
                    {
                        if (indexBuffer == null)
                            indexBuffer = new List<int>();

                        vertexBuffer = result.mesh.vertices;

                        result.mesh.GetIndices(indexBuffer, result.subMeshIndex);

                        if (indexMap == null)
                            indexMap = new Dictionary<int, int>();
                        else
                            indexMap.Clear();

                        numVertexIndices = indexBuffer.Count;
                        for (k = 0; k < numVertexIndices; ++k)
                        {
                            vertexIndex = indexBuffer[k];
                            if (!indexMap.TryGetValue(vertexIndex, out temp))
                            {
                                if (vertices == null)
                                    vertices = new List<Vector3>();

                                temp = vertices.Count;

                                vertices.Add(vertexBuffer[vertexIndex]);

                                indexMap[vertexIndex] = temp;
                            }

                            if (indices == null)
                                indices = new List<int>();

                            indices.Add(temp);
                        }
                    }

                    mesh.vertices = vertices == null ? null : vertices.ToArray();
                    mesh.triangles = indices == null ? null : indices.ToArray();

                    gameObject = new GameObject(material.name);
                    meshFilter = gameObject.AddComponent<MeshFilter>();
                    if (meshFilter != null)
                        meshFilter.sharedMesh = mesh;

                    renderer = gameObject.AddComponent<MeshRenderer>();
                    if (renderer != null)
                        renderer.sharedMaterial = material;
                }
            }

            temp = numCombineInstances - index;
            results = new CombineInstance[temp];

            combineInstances.CopyTo(index, results, 0, (int)temp);

            mesh = new Mesh();
            //mesh.CombineMeshes(results, true, false, false);

            if (vertices != null)
                vertices.Clear();

            if (indices != null)
                indices.Clear();

            foreach (CombineInstance result in results)
            {
                if (indexBuffer == null)
                    indexBuffer = new List<int>();

                vertexBuffer = result.mesh.vertices;

                result.mesh.GetIndices(indexBuffer, result.subMeshIndex);

                if (indexMap == null)
                    indexMap = new Dictionary<int, int>();
                else
                    indexMap.Clear();

                numVertexIndices = indexBuffer.Count;
                for (k = 0; k < numVertexIndices; ++k)
                {
                    vertexIndex = indexBuffer[k];
                    if (!indexMap.TryGetValue(vertexIndex, out temp))
                    {
                        if (vertices == null)
                            vertices = new List<Vector3>();

                        temp = vertices.Count;

                        vertices.Add(vertexBuffer[vertexIndex]);

                        indexMap[vertexIndex] = temp;
                    }

                    if (indices == null)
                        indices = new List<int>();

                    indices.Add(temp);
                }
            }

            mesh.vertices = vertices == null ? null : vertices.ToArray();
            mesh.triangles = indices == null ? null : indices.ToArray();

            gameObject = new GameObject(material.name);
            meshFilter = gameObject.AddComponent<MeshFilter>();
            if (meshFilter != null)
                meshFilter.sharedMesh = mesh;

            renderer = gameObject.AddComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;

            combineInstances.Clear();
        }

        [MenuItem("Window/ZG/Mesh Editor")]
        public static void GetWindow()
        {
            GetWindow<MeshEditor>();
        }

        private static void __OnSelectionChange()
        {
            Selection.selectionChanged -= __OnSelectionChange;

            __path = null;
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Phong");

            isNormalsOnly = EditorGUILayout.Toggle("Is Normal Only", isNormalsOnly);
            isBackupToTangents = EditorGUILayout.Toggle("Is Backup To Tangents", isBackupToTangents);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Split");

            splitSegmentLength = EditorGUILayout.IntField("Split Segment Length", splitSegmentLength);
            splitSegmentWidth = EditorGUILayout.IntField("Split Segment Width", splitSegmentWidth);
            splitSegmentHeight = EditorGUILayout.IntField("Split Segment Height", splitSegmentHeight);

            splitBounds = EditorGUILayout.BoundsField("Split Bounds", splitBounds);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Unwrap Param");

            UnwrapParam unwrapParam = MeshEditor.unwrapParam;
            EditorGUI.BeginChangeCheck();
            unwrapParam.angleError = EditorGUILayout.FloatField("Angle Error", unwrapParam.angleError);
            unwrapParam.areaError = EditorGUILayout.FloatField("Area Error", unwrapParam.areaError);
            unwrapParam.hardAngle = EditorGUILayout.FloatField("Hard Angle", unwrapParam.hardAngle);
            unwrapParam.packMargin = EditorGUILayout.FloatField("Pack Margin", unwrapParam.packMargin);
            if(EditorGUI.EndChangeCheck())
                MeshEditor.unwrapParam = unwrapParam;

            if(GUILayout.Button("Reset"))
            {
                UnwrapParam.SetDefaults(out unwrapParam);

                MeshEditor.unwrapParam = unwrapParam;
            }
        }
    }
}