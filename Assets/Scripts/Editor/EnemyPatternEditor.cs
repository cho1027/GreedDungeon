using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// EnemyPattern 인스펙터: 단계 추가 메뉴, 순서 이동/삭제, 패턴 요약.
/// </summary>
[CustomEditor(typeof(EnemyPattern))]
public class EnemyPatternEditor : Editor
{
    private static List<Type> _stepTypes;

    private static List<Type> StepTypes
    {
        get
        {
            if (_stepTypes == null)
            {
                _stepTypes = TypeCache.GetTypesDerivedFrom<PatternStep>()
                    .Where(t => !t.IsAbstract && !t.IsGenericType && t.GetConstructor(Type.EmptyTypes) != null)
                    .OrderBy(t => t.Name)
                    .ToList();
            }
            return _stepTypes;
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 기본 필드 (이름, 아이콘, 수치 표시)
        SerializedProperty it = serializedObject.GetIterator();
        bool enter = true;
        while (it.NextVisible(enter))
        {
            enter = false;
            if (it.name == "_steps") continue;

            using (new EditorGUI.DisabledScope(it.name == "m_Script"))
                EditorGUILayout.PropertyField(it, true);
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("단계 (위에서부터 순서대로 실행)", EditorStyles.boldLabel);

        SerializedProperty steps = serializedObject.FindProperty("_steps");
        int remove = -1, moveUp = -1, moveDown = -1;

        for (int i = 0; i < steps.arraySize; i++)
        {
            SerializedProperty element = steps.GetArrayElementAtIndex(i);
            var step = element.managedReferenceValue as PatternStep;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            string title = step != null ? $"{i + 1}. {step.Describe()}" : $"{i + 1}. (비어 있음)";
            element.isExpanded = EditorGUILayout.Foldout(element.isExpanded, title, true, EditorStyles.foldoutHeader);

            using (new EditorGUI.DisabledScope(i == 0))
                if (GUILayout.Button("▲", GUILayout.Width(24))) moveUp = i;
            using (new EditorGUI.DisabledScope(i == steps.arraySize - 1))
                if (GUILayout.Button("▼", GUILayout.Width(24))) moveDown = i;
            if (GUILayout.Button("✕", GUILayout.Width(24))) remove = i;

            EditorGUILayout.EndHorizontal();

            if (element.isExpanded && step != null)
            {
                EditorGUI.indentLevel++;
                DrawStepFields(element);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        if (remove >= 0) steps.DeleteArrayElementAtIndex(remove);
        else if (moveUp > 0) steps.MoveArrayElement(moveUp, moveUp - 1);
        else if (moveDown >= 0) steps.MoveArrayElement(moveDown, moveDown + 1);

        if (GUILayout.Button("+ 단계 추가", GUILayout.Height(24)))
            ShowAddMenu();

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("패턴 요약", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(((EnemyPattern)target).Describe(), MessageType.None);
    }

    private static void DrawStepFields(SerializedProperty element)
    {
        SerializedProperty delay = null;
        SerializedProperty child = element.Copy();
        SerializedProperty end = element.GetEndProperty();
        bool enter = true;

        while (child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end))
        {
            enter = false;
            if (child.name == "delayAfter")
            {
                delay = child.Copy(); // 공통 필드는 맨 아래에
                continue;
            }
            EditorGUILayout.PropertyField(child, true);
        }

        if (delay != null) EditorGUILayout.PropertyField(delay);
    }

    private void ShowAddMenu()
    {
        var menu = new GenericMenu();
        foreach (Type type in StepTypes)
        {
            var sample = (PatternStep)Activator.CreateInstance(type);
            Type captured = type;
            menu.AddItem(new GUIContent(sample.Label), false, () => AddStep(captured));
        }
        menu.ShowAsContext();
    }

    private void AddStep(Type type)
    {
        var pattern = (EnemyPattern)target;
        Undo.RecordObject(pattern, "단계 추가");
        pattern.EditorAddStep((PatternStep)Activator.CreateInstance(type));
        EditorUtility.SetDirty(pattern);
        serializedObject.Update();
    }
}

/// <summary>
/// PatternShape 인스펙터: 모양에 필요한 값만 보여주고, 범위를 격자로 미리 보여준다.
/// Custom 모양은 격자 칸을 클릭해서 켜고 끈다. (위쪽 = 바라보는 방향, 파란 칸 = 적)
/// </summary>
[CustomPropertyDrawer(typeof(PatternShape))]
public class PatternShapeDrawer : PropertyDrawer
{
    private const float CellSize = 16f;
    private const float CellGap = 2f;
    private const int MaxRadius = 6;

    private static readonly List<Vector2Int> Preview = new List<Vector2Int>();

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        if (!property.isExpanded) return line;

        var type = (PatternShapeType)property.FindPropertyRelative("type").enumValueIndex;
        int lines = 1 /*foldout*/ + 1 /*type*/ + 1 /*stop*/ + 1 /*hint*/;
        if (type != PatternShapeType.Custom) lines++;        // range
        if (type == PatternShapeType.Wide) lines++;          // sideLines

        int size = Radius(property) * 2 + 1;
        return lines * line + size * (CellSize + CellGap) + 6f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float lineH = EditorGUIUtility.singleLineHeight;
        float step = lineH + EditorGUIUtility.standardVerticalSpacing;
        var r = new Rect(position.x, position.y, position.width, lineH);

        SerializedProperty typeProp = property.FindPropertyRelative("type");
        var type = (PatternShapeType)typeProp.enumValueIndex;

        property.isExpanded = EditorGUI.Foldout(r, property.isExpanded, $"{label.text}  ({Build(property).Describe()})", true);
        r.y += step;

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;

            EditorGUI.PropertyField(r, typeProp); r.y += step;
            if (type != PatternShapeType.Custom) { EditorGUI.PropertyField(r, property.FindPropertyRelative("range")); r.y += step; }
            if (type == PatternShapeType.Wide) { EditorGUI.PropertyField(r, property.FindPropertyRelative("sideLines")); r.y += step; }
            EditorGUI.PropertyField(r, property.FindPropertyRelative("stopAtObstacles")); r.y += step;

            string hint = type == PatternShapeType.Custom
                ? "칸을 클릭해서 켜고 끄기 (위 = 앞, 파랑 = 적)"
                : "미리보기 (위 = 바라보는 방향, 파랑 = 적, 빨강 = 공격 범위)";
            EditorGUI.LabelField(EditorGUI.IndentedRect(r), hint, EditorStyles.miniLabel);
            r.y += step;

            DrawGrid(new Rect(EditorGUI.IndentedRect(r).x, r.y, position.width, 0f), property, type);

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    private static void DrawGrid(Rect area, SerializedProperty property, PatternShapeType type)
    {
        int radius = Radius(property);
        int size = radius * 2 + 1;

        PatternShape shape = Build(property);
        shape.Calculate(
            c => Mathf.Abs(c.x) <= radius && Mathf.Abs(c.y) <= radius,
            c => true,
            Vector2Int.zero, Vector2Int.up, Preview);

        SerializedProperty custom = property.FindPropertyRelative("customCells");
        Event e = Event.current;

        for (int row = 0; row < size; row++)
        {
            for (int col = 0; col < size; col++)
            {
                var local = new Vector2Int(col - radius, radius - row); // 위쪽 줄 = 앞
                var cellRect = new Rect(area.x + col * (CellSize + CellGap), area.y + row * (CellSize + CellGap), CellSize, CellSize);

                Color color;
                if (local == Vector2Int.zero) color = new Color(0.3f, 0.55f, 1f);
                else if (Preview.Contains(local)) color = new Color(0.9f, 0.25f, 0.25f);
                else color = new Color(0.5f, 0.5f, 0.5f, 0.35f);

                EditorGUI.DrawRect(cellRect, color);

                if (type == PatternShapeType.Custom && local != Vector2Int.zero &&
                    e.type == EventType.MouseDown && e.button == 0 && cellRect.Contains(e.mousePosition))
                {
                    ToggleCustomCell(custom, local);
                    e.Use();
                }
            }
        }
    }

    private static void ToggleCustomCell(SerializedProperty custom, Vector2Int local)
    {
        for (int i = 0; i < custom.arraySize; i++)
        {
            if (custom.GetArrayElementAtIndex(i).vector2IntValue == local)
            {
                custom.DeleteArrayElementAtIndex(i);
                custom.serializedObject.ApplyModifiedProperties();
                return;
            }
        }

        custom.InsertArrayElementAtIndex(custom.arraySize);
        custom.GetArrayElementAtIndex(custom.arraySize - 1).vector2IntValue = local;
        custom.serializedObject.ApplyModifiedProperties();
    }

    /// <summary>미리보기 격자 반지름 (모양이 다 보일 만큼, 최소 2)</summary>
    private static int Radius(SerializedProperty property)
    {
        var type = (PatternShapeType)property.FindPropertyRelative("type").enumValueIndex;
        int radius = 2;

        if (type == PatternShapeType.Custom)
        {
            SerializedProperty custom = property.FindPropertyRelative("customCells");
            for (int i = 0; i < custom.arraySize; i++)
            {
                Vector2Int c = custom.GetArrayElementAtIndex(i).vector2IntValue;
                radius = Mathf.Max(radius, Mathf.Abs(c.x) + 1, Mathf.Abs(c.y) + 1);
            }
        }
        else
        {
            radius = Mathf.Max(radius, property.FindPropertyRelative("range").intValue);
            if (type == PatternShapeType.Wide)
                radius = Mathf.Max(radius, property.FindPropertyRelative("sideLines").intValue);
        }

        return Mathf.Min(radius, MaxRadius);
    }

    /// <summary>SerializedProperty 값으로 임시 PatternShape 를 만든다 (미리보기 계산용)</summary>
    private static PatternShape Build(SerializedProperty property)
    {
        var shape = new PatternShape
        {
            type = (PatternShapeType)property.FindPropertyRelative("type").enumValueIndex,
            range = property.FindPropertyRelative("range").intValue,
            sideLines = property.FindPropertyRelative("sideLines").intValue,
            stopAtObstacles = false, // 미리보기는 빈 방 기준
            customCells = new List<Vector2Int>()
        };

        SerializedProperty custom = property.FindPropertyRelative("customCells");
        for (int i = 0; i < custom.arraySize; i++)
            shape.customCells.Add(custom.GetArrayElementAtIndex(i).vector2IntValue);

        return shape;
    }
}
