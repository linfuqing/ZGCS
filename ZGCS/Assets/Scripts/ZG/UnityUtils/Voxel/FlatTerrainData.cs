using UnityEngine;

namespace ZG.Voxel
{
    public class FlatTerrainData : ScriptableObject
    {
        public FlatTerrain terrain;
        
        void OnValidate()
        {
            if (terrain != null)
                terrain.OnValidate();
        }
    }
}