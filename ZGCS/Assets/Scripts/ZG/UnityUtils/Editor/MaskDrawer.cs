using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace ZG
{
    [CustomPropertyDrawer(typeof(MaskAttribute))]
    public class MaskDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            switch(property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    break;
                case SerializedPropertyType.Enum:
                    FieldInfo fieldInfo = base.fieldInfo;
                    Enum value = fieldInfo == null ? null : Enum.ToObject(fieldInfo.FieldType, property.intValue) as Enum;
                    value = EditorGUI.EnumFlagsField(position, property.displayName, value);
                    property.intValue = value == null ? 0 : value.GetHashCode();
                    break;
                default:
                    EditorGUI.LabelField(position, "Need Enum.");
                    break;
            }
        }
    }
}