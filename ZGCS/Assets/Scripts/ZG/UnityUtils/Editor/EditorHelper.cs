using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ZG
{
    public static class EditorHelper
    {
        public static IEnumerable<Type> loadedTypes
        {
            get
            {
                Assembly assembly = Assembly.Load("UnityEditor");
                if (assembly != null)
                {
                    Type editorAssemblies = assembly.GetType("UnityEditor.EditorAssemblies");
                    if (editorAssemblies != null)
                    {
                        MethodInfo loadedTypes = editorAssemblies.GetMethod("get_loadedTypes", BindingFlags.NonPublic | BindingFlags.Static);
                        IEnumerable<Type> types = loadedTypes.Invoke(null, null) as IEnumerable<Type>;

                        return types;
                    }
                }

                return null;
            }
        }

        public static IEnumerable<SerializedProperty> GetSiblings(this SerializedProperty property, int level)
        {
            if (property == null || level < 1)
                yield break;

            SerializedObject serializedObject = property.serializedObject;
            if (serializedObject == null)
                yield break;

            string propertyPath = property.propertyPath;
            if (propertyPath == null)
                yield break;

            Match match = Regex.Match(propertyPath, @".Array\.data\[([0-9]+)\]", RegexOptions.RightToLeft);
            if (match == Match.Empty)
                yield break;

            int matchIndex = match.Index;
            SerializedProperty parent = serializedObject.FindProperty(propertyPath.Remove(matchIndex));
            int arraySize = parent == null ? 0 : parent.isArray ? parent.arraySize : 0;
            if (arraySize < 1)
                yield break;

            StringBuilder stringBuilder = new StringBuilder(propertyPath);
            Group group = match.Groups[1];
            int index = int.Parse(group.Value), startIndex = group.Index, count = group.Length, i;
            for (i = 0; i < arraySize; ++ i)
            {
                if (i == index)
                    continue;

                stringBuilder = stringBuilder.Remove(startIndex, count);

                count = stringBuilder.Length;
                stringBuilder = stringBuilder.Insert(startIndex, i);
                count = stringBuilder.Length - count;

                yield return serializedObject.FindProperty(stringBuilder.ToString());
            }

            foreach (SerializedProperty temp in parent.GetSiblings(level - 1))
            {
                arraySize = temp == null ? 0 : temp.isArray ? temp.arraySize : 0;
                if (arraySize > 0)
                {
                    stringBuilder.Remove(0, matchIndex);
                    startIndex -= matchIndex;

                    propertyPath = temp.propertyPath;
                    stringBuilder = stringBuilder.Insert(0, propertyPath);
                    matchIndex = propertyPath == null ? 0 : propertyPath.Length;
                    startIndex += matchIndex;
                    for (i = 0; i < arraySize; ++i)
                    {
                        stringBuilder = stringBuilder.Remove(startIndex, count);

                        count = stringBuilder.Length;
                        stringBuilder = stringBuilder.Insert(startIndex, i);
                        count = stringBuilder.Length - count;

                        yield return serializedObject.FindProperty(stringBuilder.ToString());
                    }
                }
            }
        }

        public static SerializedProperty GetParent(this SerializedProperty property)
        {
            SerializedObject serializedObject = property == null ? null : property.serializedObject;
            if (serializedObject == null)
                return null;

            string path = Regex.Replace(property.propertyPath, @".((\w+\d*)|(Array\.data\[[\d]+\]))$", "");

            return serializedObject.FindProperty(path);
        }

        public static string GetPropertyPath(string path)
        {
            return Regex.Replace(path, @".Array\.data(\[\d+\])", "$1");
        }

        public static void HelpBox(Rect position, GUIContent label, string message, MessageType type)
        {
            float width = position.width;
            position.width = EditorGUIUtility.labelWidth;
            EditorGUI.PrefixLabel(position, label);
            position.x += position.width;
            position.width = width - position.width;
            EditorGUI.HelpBox(position, message, MessageType.Error);
        }
        
        public static void CreateFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string directoryName = Path.GetDirectoryName(path);
            if (!AssetDatabase.IsValidFolder(directoryName))
                CreateFolder(directoryName);

            AssetDatabase.CreateFolder(directoryName, Path.GetFileName(path));
        }

        public static void CreateAsset(UnityEngine.Object asset)
        {
            if (asset == null)
                return;

            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (path == "")
                path = "Assets";
            else if (Path.GetExtension(path) != "")
                path = path.Replace(Path.GetFileName(AssetDatabase.GetAssetPath(Selection.activeObject)), "");

            string assetPathAndName = AssetDatabase.GenerateUniqueAssetPath(path + "/" + asset.name + ".asset");

            AssetDatabase.CreateAsset(asset, assetPathAndName);

            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
        }

        public static T CreateAsset<T>(string assetName) where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            if (asset != null)
            {
                asset.name = assetName;

                CreateAsset(asset);
            }

            return asset;
        }
    }
}