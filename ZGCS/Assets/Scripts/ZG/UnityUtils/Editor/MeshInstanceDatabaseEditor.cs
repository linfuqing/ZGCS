using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace ZG
{
    [CustomEditor(typeof(MeshInstanceDatabase))]
    public class MeshInstanceDatabaseEditor : Editor
    {
        public enum Type
        {
            Static,
            Dynamic
        }

        private Type __type;
        private Transform __transform;

        [MenuItem("Assets/Create/ZG/Mesh Instance Database")]
        public static void Create()
        {
            EditorHelper.CreateAsset<MeshInstanceDatabase>("Mesh Instance Database");
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            __transform = EditorGUILayout.ObjectField(__transform, typeof(Transform), true) as Transform;
            __type = (Type)EditorGUILayout.EnumPopup(__type);
            if (EditorGUI.EndChangeCheck())
            {
                MeshInstanceDatabase target = base.target as MeshInstanceDatabase;
                if (target != null)
                {
                    switch(__type)
                    {
                        case Type.Static:
                            target.nodes = MeshInstanceDatabase.CreateStatic(__transform, out target.objects);
                            break;
                        case Type.Dynamic:
                            target.nodes = MeshInstanceDatabase.CreateDynamic(__transform, out target.objects);
                            break;
                    }

                    EditorUtility.SetDirty(target);
                }
            }

            base.OnInspectorGUI();
        }
    }
}