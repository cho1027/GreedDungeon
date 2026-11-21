using System;
using GreedDungeon.YeJun;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Unity 6.3 Editor menu: Tools > GreedDungeon > YeJun > Create Title Screen
// This tool creates new assets only. It does not modify GameManager or Build Settings.
public static class GDYeJunTitleSceneBuilder
{
    private const string ScenePath = "Assets/YeJun/TitleScreen/Scenes/GD_YeJun_Title.unity";
    private static Font _font;

    [MenuItem("Tools/GreedDungeon/YeJun/Create Title Screen")]
    private static void CreateTitleScreen()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (System.IO.File.Exists(ScenePath))
        {
            bool replace = EditorUtility.DisplayDialog(
                "타이틀 씬이 이미 있습니다",
                "GD_YeJun_Title.unity 파일이 이미 있어요. 기존 씬을 덮어쓸까요?",
                "덮어쓰기", "취소");
            if (!replace) return;
        }

        EnsureFolder("Assets", "YeJun");
        EnsureFolder("Assets/YeJun", "TitleScreen");
        EnsureFolder("Assets/YeJun/TitleScreen", "Scenes");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camera.tag = "MainCamera";
        Camera cam = camera.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.035f, 0.035f, 0.045f);
        cam.orthographic = true;

        GameObject canvasObject = new GameObject("GD_YeJun_Canvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem));
        Type newInputModule = Type.GetType(
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (newInputModule != null)
        {
            Component module = eventSystem.AddComponent(newInputModule);
            var assign = newInputModule.GetMethod("AssignDefaultActions");
            if (assign != null) assign.Invoke(module, null);
        }
        else
        {
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        Color baseBg = ColorFromHex("101117");
        Color panelBg = ColorFromHex("18191E");
        Color mutedGold = ColorFromHex("AF9771");
        Color pale = ColorFromHex("EAE1D2");
        Color red = ColorFromHex("752C3C");

        MakePanel("Background", canvasRect, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, baseBg);
        MakePanel("Left Ember", canvasRect, new Vector2(0, 0), new Vector2(0, 1),
            new Vector2(36, 0), new Vector2(0, 0), ColorFromHex("3C1C23"));
        MakePanel("Right Ember", canvasRect, new Vector2(1, 0), new Vector2(1, 1),
            new Vector2(36, 0), new Vector2(0, 0), ColorFromHex("3C1C23"));
        MakePanel("Center Panel", canvasRect, new Vector2(.5f,.5f), new Vector2(.5f,.5f),
            new Vector2(810, 800), new Vector2(0, 12), panelBg);
        MakePanel("Upper Gold Line", canvasRect, new Vector2(.5f,.5f), new Vector2(.5f,.5f),
            new Vector2(750, 4), new Vector2(0, 352), mutedGold);
        MakePanel("Lower Gold Line", canvasRect, new Vector2(.5f,.5f), new Vector2(.5f,.5f),
            new Vector2(750, 4), new Vector2(0, -355), mutedGold);
        MakePanel("Red Divider", canvasRect, new Vector2(.5f,.5f), new Vector2(.5f,.5f),
            new Vector2(410, 3), new Vector2(0, 148), red);

        MakeLabel("Title English", canvasRect, "GREED DUNGEON", new Vector2(730,110),
            new Vector2(0, 260), 72, pale, FontStyle.Bold);
        MakeLabel("Title Korean", canvasRect, "그리드던전", new Vector2(680,76),
            new Vector2(0, 187), 38, mutedGold, FontStyle.Normal);
        MakeLabel("Tagline", canvasRect, "욕망의 끝에서 살아남아라", new Vector2(650,55),
            new Vector2(0, 106), 27, ColorFromHex("AFA6A2"), FontStyle.Normal);

        GameObject menuGo = new GameObject("GD_YeJun_Title_Menu", typeof(GDYeJunTitleMenu));
        GDYeJunTitleMenu menu = menuGo.GetComponent<GDYeJunTitleMenu>();

        Button start = MakeButton("Start Button", canvasRect, "게임 시작", new Vector2(460,92),
            new Vector2(0, -18), red, pale);
        Button how = MakeButton("Help Button", canvasRect, "조작 설명", new Vector2(460,92),
            new Vector2(0, -126), ColorFromHex("303139"), pale);
        Button quit = MakeButton("Quit Button", canvasRect, "게임 종료", new Vector2(460,92),
            new Vector2(0, -234), ColorFromHex("303139"), pale);
        UnityEventTools.AddPersistentListener(start.onClick, menu.StartGame);
        UnityEventTools.AddPersistentListener(how.onClick, menu.OpenHelp);
        UnityEventTools.AddPersistentListener(quit.onClick, menu.QuitGame);

        Text status = MakeLabel("Connection Status", canvasRect, "", new Vector2(1200,60),
            new Vector2(0, -490), 23, ColorFromHex("F0B9AA"), FontStyle.Normal);
        menu.statusText = status;

        GameObject helpOverlay = MakePanel("Help Overlay", canvasRect, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, new Color(0, 0, 0, .86f));
        RectTransform helpRect = helpOverlay.GetComponent<RectTransform>();
        GameObject helpBox = MakePanel("Instructions", helpRect, new Vector2(.5f,.5f),
            new Vector2(.5f,.5f), new Vector2(1100, 710), Vector2.zero, panelBg);
        RectTransform inner = helpBox.GetComponent<RectTransform>();
        MakePanel("Title Accent", inner, new Vector2(.5f,.5f), new Vector2(.5f,.5f),
            new Vector2(940, 4), new Vector2(0, 285), mutedGold);
        MakeLabel("Help Title", inner, "조작 설명", new Vector2(950,90),
            new Vector2(0,230), 54, pale, FontStyle.Bold);
        MakeLabel("Help Body", inner,
            "한 턴에 사용할 수 있는 코스트는 3입니다.\n\n" +
            "이동: 상하좌우 한 칸당 코스트 1 사용\n" +
            "공격: 손에 있는 공격 카드를 사용\n" +
            "턴 종료: 적이 예고한 행동을 실행\n\n" +
            "1챕터의 시체 거인을 처치하면 클리어!",
            new Vector2(980,395), new Vector2(0,-28), 32, pale, FontStyle.Normal);
        Button close = MakeButton("Close Help Button", inner, "돌아가기",
            new Vector2(350,80), new Vector2(0,-282), red, pale);
        UnityEventTools.AddPersistentListener(close.onClick, menu.CloseHelp);
        menu.helpPanel = helpOverlay;
        helpOverlay.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("타이틀 화면 생성 완료",
            "새 씬이 생성됐어요!\n\n" + ScenePath +
            "\n\nUnity 상단의 Game 탭에서 Play 버튼을 눌러 테스트하세요." +
            "\n게임 시작 버튼의 실제 전투 씬 연결은 팀 프로젝트 상태를 확인한 뒤 진행하세요.", "확인");
    }

    private static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static Color ColorFromHex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color color);
        return color;
    }

    private static GameObject MakePanel(string name, Transform parent, Vector2 anchorMin,
        Vector2 anchorMax, Vector2 size, Vector2 position, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return go;
    }

    private static Text MakeLabel(string name, Transform parent, string value, Vector2 size,
        Vector2 position, int fontSize, Color color, FontStyle style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f);
        rect.pivot = new Vector2(.5f,.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Text text = go.GetComponent<Text>();
        text.text = value;
        text.font = GetFont();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        Shadow shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0,0,0,.7f);
        shadow.effectDistance = new Vector2(2,-3);
        return text;
    }

    private static Font GetFont()
    {
        if (_font == null)
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        return _font;
    }

    private static Button MakeButton(string name, Transform parent, string label,
        Vector2 size, Vector2 position, Color normal, Color textColor)
    {
        GameObject go = MakePanel(name, parent, new Vector2(.5f,.5f),
            new Vector2(.5f,.5f), size, position, normal);
        Image image = go.GetComponent<Image>();
        image.raycastTarget = true;
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.2f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(.7f, .7f, .7f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = Color.gray;
        colors.colorMultiplier = 1f;
        button.colors = colors;
        MakeLabel("Text", go.transform, label, size, Vector2.zero, 34,
            textColor, FontStyle.Bold);
        return button;
    }
}
