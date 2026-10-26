using System.Collections.Generic;
using System.IO;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Arma la escena del juego (Canvas, UI, PhotonView, EventSystem) desde código.
// Se ejecuta sola la primera vez que compila si la escena no existe,
// y se puede regenerar desde el menú "PokeGuess/Regenerar escena".
[InitializeOnLoad]
public static class PokeGuessSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/PokeGuess.unity";

    private static readonly Color Background = new Color(0.09f, 0.11f, 0.18f);
    private static readonly Color PanelColor = new Color(0.15f, 0.18f, 0.28f);
    private static readonly Color Accent = new Color(1f, 0.8f, 0.1f);
    private static readonly Color ButtonBlue = new Color(0.23f, 0.45f, 0.85f);
    private static readonly Color ButtonGreen = new Color(0.2f, 0.65f, 0.35f);
    private static readonly Color ButtonRed = new Color(0.75f, 0.25f, 0.25f);

    private static Font font;
    private static DefaultControls.Resources resources;

    static PokeGuessSceneBuilder()
    {
        EditorApplication.delayCall += () =>
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(ScenePath)) return;
            Build();
        };
    }

    [MenuItem("PokeGuess/Regenerar escena")]
    private static void RebuildFromMenu()
    {
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("PokeGuess", "Esto reemplaza " + ScenePath + ". ¿Seguir?", "Sí", "No"))
            return;
        Build();
    }

    [MenuItem("PokeGuess/Abrir escena")]
    private static void OpenScene()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(ScenePath);
    }

    private static void Build()
    {
        LoadResources();

        Scene previousActive = SceneManager.GetActiveScene();
        bool openedPokeGuess = previousActive.path == ScenePath;
        if (openedPokeGuess && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // Escena aditiva para no tocar lo que el usuario tenga abierto.
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);

        CreateCamera();
        CreateEventSystem();
        PokeGuessUI ui = CreateUI();
        CreateGame(ui);

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings();

        if (openedPokeGuess || (previousActive.path.EndsWith("SampleScene.unity") && !previousActive.isDirty))
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        else
        {
            SceneManager.SetActiveScene(previousActive);
            EditorSceneManager.CloseScene(scene, true);
        }

        Debug.Log("[PokeGuess] Escena creada en " + ScenePath);
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

    private static void AddToBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ================= Objetos base =================

    private static void CreateCamera()
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        var camera = go.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Background;
        camera.orthographic = true;
        go.transform.position = new Vector3(0f, 0f, -10f);
    }

    private static void CreateEventSystem()
    {
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    private static void CreateGame(PokeGuessUI ui)
    {
        var go = new GameObject("PokeGuessGame");
        var api = go.AddComponent<PokeApiClient>();
        var game = go.AddComponent<PokeGuessGame>();   // RequireComponent agrega la PhotonView
        go.GetComponent<PhotonView>().sceneViewId = 1;

        var so = new SerializedObject(game);
        so.FindProperty("api").objectReferenceValue = api;
        so.FindProperty("ui").objectReferenceValue = ui;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ================= UI =================

    private static PokeGuessUI CreateUI()
    {
        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        Transform root = canvasGo.transform;

        var ui = canvasGo.AddComponent<PokeGuessUI>();
        var so = new SerializedObject(ui);

        // ---------- Menú ----------
        GameObject menu = CreatePanel(root, "MenuPanel", Background);
        CreateText(menu.transform, "Title", "¿Quién es ese Pokémon?", 96, new Vector2(0, 300), new Vector2(1600, 140), Accent, FontStyle.Bold);
        CreateText(menu.transform, "Subtitle", "Serialización & API  -  PokeAPI + Photon PUN 2", 34, new Vector2(0, 200), new Vector2(1600, 60), Color.white);
        CreateText(menu.transform, "NickLabel", "Tu nombre", 32, new Vector2(0, 70), new Vector2(600, 50), Color.white);
        InputField nickname = CreateInput(menu.transform, "NicknameInput", "Nombre de entrenador...", new Vector2(0, 0), new Vector2(600, 80));
        Button online = CreateButton(menu.transform, "OnlineButton", "Jugar Online", new Vector2(-210, -130), new Vector2(380, 90), ButtonBlue);
        Button offline = CreateButton(menu.transform, "OfflineButton", "Jugar Solo", new Vector2(210, -130), new Vector2(380, 90), ButtonGreen);
        Text menuStatus = CreateText(menu.transform, "StatusText", "", 30, new Vector2(0, -250), new Vector2(1400, 60), new Color(0.8f, 0.85f, 1f));
        CreateText(menu.transform, "Help", "Cada intento fallido revela una pista nueva: Generación, Tipos, Altura y Peso, Letras del nombre e Imagen oscurecida.\nAcepta el nombre en inglés o en español.",
            26, new Vector2(0, -400), new Vector2(1500, 100), new Color(1f, 1f, 1f, 0.6f));

        Set(so, "menuPanel", menu);
        Set(so, "nicknameInput", nickname);
        Set(so, "onlineButton", online);
        Set(so, "offlineButton", offline);
        Set(so, "menuStatusText", menuStatus);

        // ---------- Juego ----------
        GameObject game = CreatePanel(root, "GamePanel", Background);
        Transform g = game.transform;

        Text room = CreateText(g, "RoomText", "Sala", 28, new Vector2(-460, 500), new Vector2(980, 50), new Color(0.8f, 0.85f, 1f), FontStyle.Normal, TextAnchor.MiddleLeft);
        Button leave = CreateButton(g, "LeaveButton", "Salir", new Vector2(830, 495), new Vector2(200, 60), ButtonRed);

        // Imagen
        GameObject imageBox = CreatePanel(g, "ImageBox", PanelColor);
        SetRect(imageBox.GetComponent<RectTransform>(), new Vector2(-560, 70), new Vector2(560, 560));
        var rawGo = new GameObject("PokemonImage", typeof(RectTransform), typeof(RawImage));
        rawGo.transform.SetParent(imageBox.transform, false);
        SetRect(rawGo.GetComponent<RectTransform>(), Vector2.zero, new Vector2(520, 520));
        RawImage pokemonImage = rawGo.GetComponent<RawImage>();
        pokemonImage.raycastTarget = false;
        Text imageHint = CreateText(imageBox.transform, "ImageHint", "?", 300, Vector2.zero, new Vector2(520, 520), new Color(1f, 1f, 1f, 0.15f), FontStyle.Bold);
        Text result = CreateText(g, "ResultText", "", 40, new Vector2(-560, -290), new Vector2(760, 120), Color.white, FontStyle.Bold);

        // Pistas e intento
        CreateText(g, "CluesTitle", "Pistas", 44, new Vector2(170, 330), new Vector2(620, 60), Accent, FontStyle.Bold, TextAnchor.MiddleLeft);
        var clues = new Text[PokeGuessUI.ClueCount];
        for (int i = 0; i < clues.Length; i++)
            clues[i] = CreateText(g, "Clue" + (i + 1), "???", 34, new Vector2(170, 250 - i * 70), new Vector2(620, 60), Color.white, FontStyle.Normal, TextAnchor.MiddleLeft);
        Text attempts = CreateText(g, "AttemptsText", "", 30, new Vector2(170, -110), new Vector2(620, 50), new Color(0.8f, 0.85f, 1f), FontStyle.Normal, TextAnchor.MiddleLeft);
        InputField guess = CreateInput(g, "GuessInput", "¿Quién es?", new Vector2(70, -200), new Vector2(420, 80));
        Button guessButton = CreateButton(g, "GuessButton", "Adivinar", new Vector2(390, -200), new Vector2(200, 80), ButtonGreen);
        Button next = CreateButton(g, "NextRoundButton", "Nuevo Pokémon", new Vector2(170, -310), new Vector2(620, 70), ButtonBlue);

        // Puntajes y log
        GameObject scoreBox = CreatePanel(g, "ScoreBox", PanelColor);
        SetRect(scoreBox.GetComponent<RectTransform>(), new Vector2(720, 230), new Vector2(400, 360));
        Text scoreboard = CreateText(scoreBox.transform, "ScoreboardText", "Puntajes", 28, Vector2.zero, new Vector2(360, 330), Color.white, FontStyle.Normal, TextAnchor.UpperLeft);
        GameObject logBox = CreatePanel(g, "LogBox", PanelColor);
        SetRect(logBox.GetComponent<RectTransform>(), new Vector2(720, -230), new Vector2(400, 520));
        Text log = CreateText(logBox.transform, "LogText", "", 22, Vector2.zero, new Vector2(370, 490), new Color(0.85f, 0.88f, 1f), FontStyle.Normal, TextAnchor.LowerLeft);

        Set(so, "gamePanel", game);
        Set(so, "roomText", room);
        SerializedProperty clueArray = so.FindProperty("clueTexts");
        clueArray.arraySize = clues.Length;
        for (int i = 0; i < clues.Length; i++)
            clueArray.GetArrayElementAtIndex(i).objectReferenceValue = clues[i];
        Set(so, "pokemonImage", pokemonImage);
        Set(so, "imageHintText", imageHint);
        Set(so, "resultText", result);
        Set(so, "attemptsText", attempts);
        Set(so, "guessInput", guess);
        Set(so, "guessButton", guessButton);
        Set(so, "nextRoundButton", next);
        Set(so, "leaveButton", leave);
        Set(so, "scoreboardText", scoreboard);
        Set(so, "logText", log);
        so.ApplyModifiedPropertiesWithoutUndo();

        game.SetActive(false);
        return ui;
    }

    private static void Set(SerializedObject so, string field, Object value)
    {
        so.FindProperty(field).objectReferenceValue = value;
    }

    // ================= Helpers de UI =================

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color)
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

    private static Text CreateText(Transform parent, string name, string content, int size, Vector2 position, Vector2 rectSize,
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

    private static Button CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Color color)
    {
        GameObject go = DefaultControls.CreateButton(resources);
        go.name = name;
        go.transform.SetParent(parent, false);
        SetRect(go.GetComponent<RectTransform>(), position, size);
        go.GetComponent<Image>().color = color;

        Text text = go.GetComponentInChildren<Text>();
        text.font = font;
        text.text = label;
        text.fontSize = 34;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        return go.GetComponent<Button>();
    }

    private static InputField CreateInput(Transform parent, string name, string placeholder, Vector2 position, Vector2 size)
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
