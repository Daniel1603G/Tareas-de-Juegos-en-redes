using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Helpers compartidos para generar desde código las escenas de cada tarea (cámara, EventSystem, uGUI).
public static class TaskSceneKit
{
    public static readonly Color Background = new Color(0.09f, 0.11f, 0.18f);
    public static readonly Color PanelColor = new Color(0.15f, 0.18f, 0.28f);
    public static readonly Color Accent = new Color(1f, 0.8f, 0.1f);
    public static readonly Color SoftText = new Color(0.8f, 0.85f, 1f);
    public static readonly Color ButtonBlue = new Color(0.23f, 0.45f, 0.85f);
    public static readonly Color ButtonGreen = new Color(0.2f, 0.65f, 0.35f);
    public static readonly Color ButtonRed = new Color(0.75f, 0.25f, 0.25f);

    private static Font font;
    private static DefaultControls.Resources resources;

    // Crea la escena en forma aditiva (sin tocar lo que el usuario tenga abierto), la guarda y la suma al Build Settings.
    // Si estaba abierta la SampleScene sin cambios, abre la escena nueva.
    public static void BuildScene(string scenePath, Action buildContent)
    {
        LoadResources();

        Scene previousActive = SceneManager.GetActiveScene();
        bool wasOpen = previousActive.path == scenePath;
        if (wasOpen && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);

        buildContent();

        Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
        EditorSceneManager.SaveScene(scene, scenePath);
        AddToBuildSettings(scenePath);

        if (wasOpen || (previousActive.path.EndsWith("SampleScene.unity") && !previousActive.isDirty))
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }
        else
        {
            SceneManager.SetActiveScene(previousActive);
            EditorSceneManager.CloseScene(scene, true);
        }

        Debug.Log("[Tareas] Escena creada en " + scenePath);
    }

    // Llama a BuildScene una sola vez después de compilar, si la escena todavía no existe.
    public static void BuildIfMissing(string scenePath, Action build)
    {
        EditorApplication.delayCall += () =>
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(scenePath)) return;
            build();
        };
    }

    public static bool ConfirmRebuild(string scenePath)
    {
        return !File.Exists(scenePath) ||
               EditorUtility.DisplayDialog("Tareas", "Esto reemplaza " + scenePath + ". ¿Seguir?", "Sí", "No");
    }

    public static void OpenScene(string scenePath)
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(scenePath);
    }

    private static void LoadResources()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        resources = new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd")
        };
    }

    private static void AddToBuildSettings(string scenePath)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == scenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ================= Objetos base =================

    public static void CreateCamera()
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        var camera = go.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Background;
        camera.orthographic = true;
        go.transform.position = new Vector3(0f, 0f, -10f);
    }

    public static void CreateEventSystem()
    {
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    public static Transform CreateCanvas()
    {
        var go = new GameObject("Canvas", typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return go.transform;
    }

    public static void Set(SerializedObject so, string field, Object value)
    {
        so.FindProperty(field).objectReferenceValue = value;
    }

    // ================= UI =================

    public static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    // Panel que ocupa todo el padre.
    public static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = color;
        return go;
    }

    // Caja de un tamaño fijo.
    public static Image CreateBox(Transform parent, string name, Color color, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        SetRect(go.GetComponent<RectTransform>(), position, size);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static Text CreateText(Transform parent, string name, string content, int size, Vector2 position, Vector2 rectSize,
        Color color, FontStyle style = FontStyle.Normal, TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        SetRect(go.GetComponent<RectTransform>(), position, rectSize);
        var text = go.GetComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    public static Button CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Color color, int fontSize = 34)
    {
        GameObject go = DefaultControls.CreateButton(resources);
        go.name = name;
        go.transform.SetParent(parent, false);
        SetRect(go.GetComponent<RectTransform>(), position, size);
        go.GetComponent<Image>().color = color;

        Text text = go.GetComponentInChildren<Text>();
        text.font = font;
        text.text = label;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        return go.GetComponent<Button>();
    }

    public static InputField CreateInput(Transform parent, string name, string placeholder, Vector2 position, Vector2 size)
    {
        GameObject go = DefaultControls.CreateInputField(resources);
        go.name = name;
        go.transform.SetParent(parent, false);
        SetRect(go.GetComponent<RectTransform>(), position, size);

        var input = go.GetComponent<InputField>();
        input.lineType = InputField.LineType.SingleLine;
        input.characterLimit = 24;

        input.textComponent.font = font;
        input.textComponent.fontSize = 34;
        input.textComponent.alignment = TextAnchor.MiddleLeft;

        var placeholderText = (Text)input.placeholder;
        placeholderText.font = font;
        placeholderText.fontSize = 34;
        placeholderText.alignment = TextAnchor.MiddleLeft;
        placeholderText.text = placeholder;
        return input;
    }
}
