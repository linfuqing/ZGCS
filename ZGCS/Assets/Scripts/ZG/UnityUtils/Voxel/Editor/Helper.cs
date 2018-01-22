using UnityEngine;
using UnityEditor;

namespace ZG.Voxel
{
    public static class Helper
    {


#if UNITY_EDITOR
        [UnityEditor.MenuItem("Assets/Create/ZG/Voxel/Flat Terrain Data")]
        public static void CreateFlatTerrain()
        {
            EditorHelper.CreateAsset<FlatTerrainData>("Flat Terrain Data");
        }
#endif
    }
}