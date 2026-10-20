using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(MonsterTemperament))]
public sealed class MonsterTemperamentDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        property.intValue = (int)(MonsterTemperament)EditorGUI.EnumFlagsField(position, label, (MonsterTemperament)property.intValue);
        EditorGUI.EndProperty();
    }
}
