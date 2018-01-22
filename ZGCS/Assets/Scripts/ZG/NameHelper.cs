using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ZG
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class NameAttribute : Attribute
    {
    }
    
    public static class NameHelper
    {
        private static int __distance = -1;
        private static int[][] __distanceMap = null;

        private static int __Distance(string x, string y, int xBegin, int xEnd, int yBegin, int yEnd, bool isUpdateDistance)
        {
            int lengthX = Math.Max(xBegin, xEnd) + 1;
            if (__distanceMap == null)
                __distanceMap = new int[lengthX][];
            else if (__distanceMap.Length < lengthX)
                Array.Resize(ref __distanceMap, lengthX);

            int count, lengthY = Math.Max(yBegin, yEnd) + 1;
            int[] distanceMap = __distanceMap[xBegin];
            if (distanceMap == null)
            {
                distanceMap = new int[lengthY];

                count = 0;
            }
            else
            {
                count = distanceMap.Length;
                if (count < lengthY)
                    Array.Resize(ref distanceMap, lengthY);
                else
                    count = lengthY;
            }

            __distanceMap[xBegin] = distanceMap;

            int length = Math.Max(lengthX, lengthY) + 1;
            if (__distance > -1 && length > int.MaxValue - __distance)
            {
                __distance = -1;
                int i;
                foreach (int[] temp in __distanceMap)
                {
                    count = temp == null ? 0 : temp.Length;
                    for (i = 0; i < count; ++i)
                        temp[i] = -1;
                }
            }
            else
            {
                for (int i = count; i < lengthY; ++i)
                    distanceMap[i] = __distance;
            }

            int distance = distanceMap[yBegin];
            if (distance > __distance)
                return distance - __distance - 1;

            if (xBegin > xEnd)
            {
                distance = yBegin > yEnd ? 0 : yEnd - yBegin + 1;

                distanceMap[yBegin] = distance + __distance + 1;

                if (isUpdateDistance)
                    __distance += length;

                return distance;
            }

            if (yBegin > yEnd)
            {
                distance = xBegin > xEnd ? 0 : xEnd - xBegin + 1;

                distanceMap[yBegin] = distance + __distance + 1;

                if (isUpdateDistance)
                    __distance += length;

                return distance;
            }

            distance = x[xBegin] == y[yBegin] ? __Distance(x, y, xBegin + 1, xEnd, yBegin + 1, yEnd, false) :
                Math.Min(
                Math.Min(__Distance(x, y, xBegin, xEnd, yBegin + 1, yEnd, false),
                __Distance(x, y, xBegin + 1, xEnd, yBegin, yEnd, false)),
                __Distance(x, y, xBegin + 1, xEnd, yBegin + 1, yEnd, false)) + 1;

            distanceMap[yBegin] = distance + __distance + 1;

            if (isUpdateDistance)
                __distance += length;

            return distance;
        }

        public static int Distance(this string x, string y, int xBegin, int xEnd, int yBegin, int yEnd)
        {
            return __Distance(x, y, xBegin, xEnd, yBegin, yEnd, true);
        }

        public static int Distance(this string x, string y)
        {
            return Distance(x, y, 0, (x == null ? 0 : x.Length) - 1, 0, (y == null ? 0 : y.Length) - 1);
        }

        public static bool IsIndex(this Type type)
        {
            return type == typeof(int) ||
               type == typeof(short) ||
               type == typeof(byte) ||
               type == typeof(sbyte) ||
               type == typeof(ushort) ||
               type == typeof(uint) ||
               type == typeof(ulong) ||
               type == typeof(long);
        }

        public static string GetName(this object target)
        {
            if (target == null)
                return null;

            Type type = target.GetType();
            if (type == typeof(string))
                return target as string;

            bool isMatch;
            string name;
            MethodInfo methodInfo;
            MethodInfo[] methodInfos;
            ParameterInfo[] parameterInfos;
            PropertyInfo[] propertyInfos;
            FieldInfo[] fieldInfos;
            while (type != null)
            {
                methodInfos = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (methodInfos != null)
                {
                    foreach (MethodInfo method in methodInfos)
                    {
                        if (method == null)
                            continue;

                        isMatch = method.IsDefined(typeof(NameAttribute), false);
                        if (!isMatch && method.IsPublic)
                        {
                            name = method == null ? null : method.Name;
                            isMatch = name != null && (name == "get_name" || name == "get_Name") && method.ReturnType == typeof(string);
                        }

                        if (isMatch)
                        {
                            parameterInfos = method.GetParameters();
                            if (parameterInfos == null || parameterInfos.Length < 1)
                                return method.Invoke(target, null) as string;
                        }
                    }
                }

                propertyInfos = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (propertyInfos != null)
                {
                    foreach (PropertyInfo propertyInfo in propertyInfos)
                    {
                        if (propertyInfo == null)
                            continue;

                        isMatch = propertyInfo.IsDefined(typeof(NameAttribute), false);
                        if (!isMatch)
                        {
                            name = propertyInfo.Name;
                            isMatch = name != null && (name == "name" || name == "Name") && propertyInfo.PropertyType == typeof(string);
                        }

                        if (isMatch)
                        {
                            methodInfo = propertyInfo.GetGetMethod();
                            if (methodInfo != null && methodInfo.IsPublic)
                            {
                                parameterInfos = methodInfo.GetParameters();
                                if (parameterInfos == null || parameterInfos.Length < 1)
                                    return methodInfo.Invoke(target, null) as string;
                            }
                        }
                    }
                }

                fieldInfos = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fieldInfos != null)
                {
                    foreach (FieldInfo fieldInfo in fieldInfos)
                    {
                        if (fieldInfo == null)
                            continue;

                        isMatch = fieldInfo.IsDefined(typeof(NameAttribute), false);
                        if (!isMatch && fieldInfo.IsPublic)
                        {
                            name = fieldInfo.Name;
                            isMatch = name != null && (name == "name" || name == "Name") && fieldInfo.FieldType == typeof(string);
                        }

                        if (isMatch)
                            return fieldInfo.GetValue(target) as string;
                    }
                }

                type = type.BaseType;
            }

            return null;
        }

        public static string GetName(this object target, string key)
        {
            if (target == null)
                return null;

            Type type = target.GetType();
            MethodInfo methodInfo;
            PropertyInfo propertyInfo;
            FieldInfo fieldInfo;
            ParameterInfo[] parameterInfos;
            while (type != null)
            {
                methodInfo = type.GetMethod(key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (methodInfo != null && methodInfo.ReturnType == typeof(string))
                {
                    parameterInfos = methodInfo.GetParameters();
                    if (parameterInfos == null || parameterInfos.Length < 1)
                        return methodInfo.Invoke(target, null) as string;
                }

                propertyInfo = type.GetProperty(key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (propertyInfo != null && propertyInfo.CanRead && propertyInfo.PropertyType == typeof(string))
                {
                    methodInfo = propertyInfo.GetGetMethod();
                    if (methodInfo != null)
                    {
                        parameterInfos = methodInfo.GetParameters();
                        if (parameterInfos == null || parameterInfos.Length < 1)
                            return methodInfo.Invoke(target, null) as string;
                    }
                }

                fieldInfo = type.GetField(key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fieldInfo != null && fieldInfo.FieldType == typeof(string))
                    return fieldInfo.GetValue(target) as string;

                type = type.BaseType;
            }

            return null;
        }

        public static string MakeUnique(this string name, IEnumerable<string> names)
        {
            if (name == null)
                name = string.Empty;

            if (names != null)
            {
                string newName;
                foreach (string temp in names)
                {
                    if (temp == name)
                    {
                        newName = Regex.Replace(
                            name,
                            @"[\s\t]*\([\s\t]*(\d+)[\s\t]*\)[\s\t]*$",
                            match => " (" + (int.Parse(match.Groups[1].Value) + 1).ToString() + ')',
                            RegexOptions.RightToLeft);
                        if (name == newName)
                            name += " (0)";
                        else
                            name = newName;

                        return name.MakeUnique(names);
                    }
                }
            }

            return name;
        }

        public static int IndexOf(this IEnumerable enumerable, string name)
        {
            if (enumerable == null)
                return -1;

            int index = 0;
            foreach (object element in enumerable)
            {
                if (GetName(element) == name)
                    return index;

                ++index;
            }

            return -1;
        }

        public static int Approximately(this string name, IEnumerable targets)
        {
            if (targets == null)
                return -1;

            name = name == null ? null : PinYinConverter.Get(Regex.Replace(name, @"\s+", string.Empty)).ToLower();

            bool isContains = false;
            int result = -1, index = 0, minDistance = int.MaxValue, distance;
            string temp;
            foreach(object target in targets)
            {
                temp = GetName(target);
                temp = temp == null ? null : PinYinConverter.Get(Regex.Replace(temp, @"\s+", string.Empty)).ToLower();
                if (!string.IsNullOrEmpty(temp) && !string.IsNullOrEmpty(name) && (temp.Contains(name) || name.Contains(temp)))
                {
                    if (!isContains)
                    {
                        isContains = true;

                        minDistance = int.MaxValue;
                    }
                }
                else if (isContains)
                {
                    ++index;

                    continue;
                }

                distance = Distance(name, temp);
                if (distance < minDistance)
                {
                    minDistance = distance;

                    result = index;
                }

                ++index;
            }

            return result;
        }
        
        public static object As(this string name, Type type, IEnumerable<string> names)
        {
            if (type == typeof(string))
                return name;

            if (type == typeof(float))
                return string.IsNullOrEmpty(name) ? 0.0f : float.Parse(name);

            if (type == typeof(double))
                return string.IsNullOrEmpty(name) ? 0.0 : double.Parse(name);

            if (IsIndex(type))
            {
                int result;
                if (names == null)
                    result = string.IsNullOrEmpty(name) ? 0 : int.Parse(name);
                else
                {
                    result = -1;
                    int index = 0;
                    foreach (string temp in names)
                    {
                        if (temp == name)
                        {
                            result = index;

                            break;
                        }

                        ++index;
                    }
                }

                return Convert.ChangeType(result, type);
            }

            return null;
        }
    }
}
