using UnityEngine;
using UnityEditor;

namespace ZG.Flat
{
    public class FlatWaterEditor : EditorWindow
    {
        public const string SETMENT_X_KEY = "FlatWaterSegmentX";
        public const string SETMENT_Y_KEY = "FlatWaterSegmentY";
        public const string WIDTH_KEY = "FlatWaterWidth";
        public const string HEIGHT_KEY = "FlatWaterHeight";
        public const string PATH_KEY = "FlatWaterPath";

        private static int __segmentX = 10;
        private static int __segmentY = 10;

        private static float __width = 10.0f;
        private static float __height = 10.0f;

        private string __path;

        [MenuItem("Assets/Create/ZG/Flat/Water Mesh")]
        public static void CreateOnAssets()
        {
            EditorHelper.CreateAsset(Build(__segmentX, __segmentY, __width, __height));
        }

        [MenuItem("Window/ZG/Flat Water Editor")]
        public static void GetWindow()
        {
            GetWindow<FlatWaterEditor>();
        }

        public static Mesh Build(int segmentX, int segmentY, float width, float height)
        {
            int size = segmentX * segmentY;
            int length = size * 6;

            Vector3[] vertices = new Vector3[length];
            Vector3[] normals = new Vector3[length];
            int[] triangles = new int[length];

            Vector2 segmentSize = new Vector2(width / segmentX, height / segmentY),
                halfSize = new Vector2(width * 0.5f, height * 0.5f);
            int index = 0, i, j;
            for (i = 0; i < segmentX; ++i)
            {
                for (j = 0; j < segmentY; ++j)
                {
                    vertices[index] = new Vector3(segmentSize.x * i - halfSize.x, 0.0f, segmentSize.y * j - halfSize.y);
                    normals[index] = new Vector3(1.0f, 1.0f, 1.0f);
                    triangles[index] = index;

                    ++index;

                    vertices[index] = new Vector3(segmentSize.x * i - halfSize.x, 0.0f, segmentSize.y * (j + 1) - halfSize.y);
                    normals[index] = new Vector3(1.0f, -1.0f, 1.0f);
                    triangles[index] = index;

                    ++index;

                    vertices[index] = new Vector3(segmentSize.x * (i + 1) - halfSize.x, 0.0f, segmentSize.y * j - halfSize.y);
                    normals[index] = new Vector3(-1.0f, 1.0f, 1.0f);
                    triangles[index] = index;

                    ++index;

                    vertices[index] = new Vector3(segmentSize.x * (i + 1) - halfSize.x, 0.0f, segmentSize.y * (j + 1) - halfSize.y);
                    normals[index] = new Vector3(-1.0f, -1.0f, 0.0f);
                    triangles[index] = index;

                    ++index;

                    vertices[index] = new Vector3(segmentSize.x * (i + 1) - halfSize.x, 0.0f, segmentSize.y * j - halfSize.y);
                    normals[index] = new Vector3(-1.0f, 1.0f, 0.0f);
                    triangles[index] = index;

                    ++index;

                    vertices[index] = new Vector3(segmentSize.x * i - halfSize.x, 0.0f, segmentSize.y * (j + 1) - halfSize.y);
                    normals[index] = new Vector3(1.0f, -1.0f, 0.0f);
                    triangles[index] = index;

                    ++index;
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "Water Mesh";
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            return mesh;
        }

        void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            __segmentX = EditorGUILayout.IntField("Segment X", __segmentX);
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetInt(SETMENT_X_KEY, __segmentX);

            EditorGUI.BeginChangeCheck();
            __segmentY = EditorGUILayout.IntField("Segment Y", __segmentY);
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetInt(SETMENT_Y_KEY, __segmentY);

            EditorGUI.BeginChangeCheck();
            __width = EditorGUILayout.FloatField("Width", __width);
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetFloat(WIDTH_KEY, __width);

            EditorGUI.BeginChangeCheck();
            __height = EditorGUILayout.FloatField("Height", __height);
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetFloat(HEIGHT_KEY, __height);

            if(GUILayout.Button("Build"))
            {
                __path = EditorUtility.SaveFilePanelInProject("Save Flat Water Mesh", "Flat Water Mesh", "asset", string.Empty, __path);
                if (!string.IsNullOrEmpty(__path))
                    AssetDatabase.CreateAsset(Build(__segmentX, __segmentY, __width, __height), __path);
            }
        }

        void OnEnable()
        {
            __segmentX = EditorPrefs.GetInt(SETMENT_X_KEY, 10);
            __segmentY = EditorPrefs.GetInt(SETMENT_Y_KEY, 10);

            __width = EditorPrefs.GetFloat(WIDTH_KEY, 10.0f);
            __height = EditorPrefs.GetFloat(HEIGHT_KEY, 10.0f);

            __path = EditorPrefs.GetString(PATH_KEY, "Assets");
        }
    }
}