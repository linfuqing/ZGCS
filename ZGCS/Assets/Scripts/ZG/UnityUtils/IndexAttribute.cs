using UnityEngine;

namespace ZG
{
    public class IndexAttribute : PropertyAttribute
    {
        public string relativePropertyPath;
        public string emptyName;
        public string nameKey;
        public int pathLevel;
        public int uniqueLevel;

        private string __path;

        public string path
        {
            get
            {
                return __path;
            }
        }

        public IndexAttribute(string path)
        {
            __path = path;
        }
    }
}
