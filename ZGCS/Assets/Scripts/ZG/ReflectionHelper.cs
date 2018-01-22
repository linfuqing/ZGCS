using System;
using System.Reflection;
using System.Collections;

namespace ZG
{
    public static class ReflectionHelper
    {
        public static Type GetArrayElementType(this Type type)
        {
            if (type == null)
                return null;

            if (type.IsArray)
                return type.GetElementType();

            if(type.IsGenericType)
            {
                Type[] genericArguments = type.GetGenericArguments();
                if (genericArguments != null && genericArguments.Length > 0)
                    return genericArguments[0];

                return null;
            }
            
            return typeof(object);
        }

        public static bool IsGenericTypeOf(this Type type, Type definition, out Type genericType)
        {
            if (type == null || definition == null || !definition.IsGenericTypeDefinition)
            {
                genericType = null;

                return false;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == definition)
            {
                genericType = type;

                return true;
            }

            return type.BaseType.IsGenericTypeOf(definition, out genericType);
        }

        public static FieldInfo GetInheritedField(this Type type, string name, BindingFlags bindingFlags)
        {
            if (type == null)
                return null;

            FieldInfo fieldInfo = type.GetField(name, bindingFlags);
            return fieldInfo == null ? GetInheritedField(type.BaseType, name, bindingFlags) : fieldInfo;
        }

        public static object Get(this object root, Action<object> visit, string path, int count, ref int startIndex, out int index, out FieldInfo fieldInfo)
        {
            index = -1;
            fieldInfo = null;

            Type type;
            IList list;
            object result = root;
            string substring;
            char temp;
            int i, length, endIndex, pathLength = Math.Min(path == null ? 0 : path.Length, startIndex + count);
            while (startIndex < pathLength)
            {
                if (result == null)
                    return null;

                if (visit != null)
                    visit(result);

                endIndex = path.IndexOf('.', startIndex);
                endIndex = endIndex == -1 ? pathLength : endIndex;
                length = endIndex - startIndex;
                
                i = path.IndexOf('[', startIndex, length);
                if (i != -1)
                {
                    substring = path.Substring(startIndex, i - startIndex);

                    index = 0;
                    while (++i < endIndex)
                    {
                        temp = path[i];
                        if (char.IsNumber(temp))
                        {
                            index *= 10;
                            index += Convert.ToInt32(temp) - 48;
                        }
                        else if (temp == ']')
                            break;
                    }
                }
                else
                {
                    substring = path.Substring(startIndex, length);

                    index = -1;
                }

                type = result.GetType();
                if (type == null)
                    return null;

                fieldInfo = type.GetInheritedField(substring, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fieldInfo == null)
                    return null;

                result = fieldInfo.GetValue(result);

                if (index != -1)
                {
                    list = result as IList;
                    if (list == null)
                        return null;

                    fieldInfo = null;

                    if (index < list.Count)
                        result = list[index];
                    else
                        return null;
                }
                
                startIndex = endIndex + 1;
            }

            return result;
        }
        
        public static object Get(this object root, Action<object> visit, ref string path, out FieldInfo fieldInfo)
        {
            int count = path == null ? 0 : path.Length, startIndex = 0, index;
            object target = root.Get(visit, path, count, ref startIndex, out index, out fieldInfo);
            if (startIndex < count)
                path = path.Substring(startIndex);

            return target;
        }

        public static object Get(this object root, ref string path, out FieldInfo fieldInfo)
        {
            return root.Get(null, ref path, out fieldInfo);
        }

        public static object Get(this object root, ref string path)
        {
            FieldInfo fieldInfo;
            return Get(root, ref path, out fieldInfo);
        }

        public static object Get(this object root, string path)
        {
            return Get(root, ref path);
        }
    }
}
