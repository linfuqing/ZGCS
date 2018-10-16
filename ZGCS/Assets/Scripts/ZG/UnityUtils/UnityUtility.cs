using UnityEngine;

namespace ZG
{
    public enum TouchFeedbackType
    {
        Light,
        Medium,
        Heavy,
        Selection
    }

    public static class UnityUtility
    {
#if UNITY_IOS
    [DllImport("__Internal")]
    public extern static void TouchFeedback(TouchFeedbackType type);
#else
        public static void TouchFeedback(TouchFeedbackType type)
        {
        }
#endif

        public static void SetLayer(this GameObject gameObject, int layer)
        {
            if (gameObject == null)
                return;

            gameObject.layer = layer;

            Transform transform = gameObject.transform;
            if (transform != null)
            {
                foreach (Transform child in transform)
                    SetLayer(child == null ? null : child.gameObject, layer);
            }
        }

        public static Plane InverseTransform(this Transform transform, Plane plane)
        {
            return new Plane(
                transform.InverseTransformDirection(plane.normal),
                transform.InverseTransformPoint(plane.normal * -plane.distance));
        }

        public static string GetPath(this Transform transform, Transform root)
        {
            System.Text.StringBuilder stringBuilder = null;
            while (transform != null)
            {
                if (transform == root)
                    break;

                if (stringBuilder == null)
                    stringBuilder = new System.Text.StringBuilder(transform.name);
                else
                {
                    stringBuilder.Insert(0, transform.name);
                    stringBuilder.Insert(0, '/');
                }

                transform = transform.parent;
            }

            return transform == null ? null : stringBuilder.ToString();
        }

        public static int Replace(this GameObject gameObject, Material source, Material destination)
        {
            Renderer[] renderers = gameObject == null ? null : gameObject.GetComponentsInChildren<Renderer>();
            if (renderers == null)
                return 0;

            bool isChanged;
            int count = 0, i, numMaterials;
            Material material;
            Material[] materials;
            foreach(Renderer renderer in renderers)
            {
                materials = renderer == null ? null : renderer.sharedMaterials;
                numMaterials = materials == null ? 0 : materials.Length;
                if (numMaterials < 1)
                    continue;

                isChanged = false;
                for (i = 0; i < numMaterials; ++i)
                {
                    material = materials[i];
                    if (material != source)
                        continue;

                    materials[i] = destination;

                    ++count;

                    isChanged = true;
                }

                if (!isChanged)
                    continue;

                renderer.sharedMaterials = materials;
            }

            return count;
        }
    }
}