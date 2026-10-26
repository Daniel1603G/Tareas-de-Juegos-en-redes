using Photon.Pun;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static TaskSceneKit;

// Arma la escena del Duelo de cowboys desde código.
// Se crea sola la primera vez que compila si no existe, y se puede regenerar desde "Tareas/Duelo de Cowboys".
[InitializeOnLoad]
public static class CowboyDuelSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/CowboyDuel.unity";

    static CowboyDuelSceneBuilder()
    {
        BuildIfMissing(ScenePath, Build);
    }

    [MenuItem("Tareas/Duelo de Cowboys/Abrir escena")]
    private static void Open() => OpenScene(ScenePath);

    [MenuItem("Tareas/Duelo de Cowboys/Regenerar escena")]
    private static void Rebuild()
    {
        if (ConfirmRebuild(ScenePath)) Build();
    }

    private static void Build()
    {
        BuildScene(ScenePath, () =>
        {
            CreateCamera();
            CreateEventSystem();
            CowboyDuelUI ui = CreateUI();
            CreateGame(ui);
        });
    }

    private static void CreateGame(CowboyDuelUI ui)
    {
        var go = new GameObject("CowboyDuelGame");
        var game = go.AddComponent<CowboyDuelGame>();   // RequireComponent agrega la PhotonView
        go.GetComponent<PhotonView>().sceneViewId = 1;

        var so = new SerializedObject(game);
        Set(so, "ui", ui);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static CowboyDuelUI CreateUI()
    {
        Transform root = CreateCanvas();
        var ui = root.gameObject.AddComponent<CowboyDuelUI>();
        var so = new SerializedObject(ui);

        // ---------- Menú ----------
        GameObject menu = CreatePanel(root, "MenuPanel", Background);
        Transform m = menu.transform;
        CreateText(m, "Title", "Duelo de Cowboys", 96, new Vector2(0, 300), new Vector2(1600, 140), Accent, FontStyle.Bold);
        CreateText(m, "Subtitle", "Patrones de sincronización precisa  -  Tiempo de gracia", 34, new Vector2(0, 200), new Vector2(1600, 60), Color.white);
        CreateText(m, "NickLabel", "Tu nombre", 32, new Vector2(0, 70), new Vector2(600, 50), Color.white);
        InputField nickname = CreateInput(m, "NicknameInput", "Nombre de vaquero...", new Vector2(0, 0), new Vector2(600, 80));
        Button online = CreateButton(m, "OnlineButton", "Duelo Online", new Vector2(-210, -130), new Vector2(380, 90), ButtonBlue);
        Button bot = CreateButton(m, "BotButton", "Practicar vs Bot", new Vector2(210, -130), new Vector2(380, 90), ButtonGreen);
        Text menuStatus = CreateText(m, "StatusText", "", 30, new Vector2(0, -250), new Vector2(1400, 60), SoftText);
        CreateText(m, "Help", "Solo se puede disparar cuando los segundos del reloj son múltiplo de 3, una vez por ventana.\n" +
                              "El MasterClient decide: con el primer disparo abre un tiempo de gracia y gana quien disparó antes.",
            26, new Vector2(0, -400), new Vector2(1500, 100), new Color(1f, 1f, 1f, 0.6f));

        Set(so, "menuPanel", menu);
        Set(so, "nicknameInput", nickname);
        Set(so, "onlineButton", online);
        Set(so, "botButton", bot);
        Set(so, "menuStatusText", menuStatus);

        // ---------- Juego ----------
        GameObject game = CreatePanel(root, "GamePanel", Background);
        Transform g = game.transform;

        Text room = CreateText(g, "RoomText", "Sala", 28, new Vector2(-460, 500), new Vector2(980, 50), SoftText, FontStyle.Normal, TextAnchor.MiddleLeft);
        Button leave = CreateButton(g, "LeaveButton", "Salir", new Vector2(830, 495), new Vector2(200, 60), ButtonRed);

        // Reloj y ventana
        Text clock = CreateText(g, "ClockText", "00:00:00.0", 130, new Vector2(0, 360), new Vector2(900, 160), Color.white, FontStyle.Bold);
        Text window = CreateText(g, "WindowText", "Esperá...", 60, new Vector2(0, 245), new Vector2(900, 80), Color.white, FontStyle.Bold);
        Text info = CreateText(g, "InfoText", "", 26, new Vector2(0, 185), new Vector2(900, 40), SoftText);

        // Cowboys: vos a la izquierda, el rival a la derecha
        Text localName = CreateText(g, "LocalName", "Vos", 36, new Vector2(-600, 190), new Vector2(500, 50), Color.white, FontStyle.Bold);
        Image localCowboy = CreateBox(g, "LocalCowboy", Color.white, new Vector2(-600, -10), new Vector2(180, 300));
        Text localBang = CreateText(g, "LocalBang", "", 56, new Vector2(-440, 40), new Vector2(260, 80), Accent, FontStyle.Bold);
        Text rivalName = CreateText(g, "RivalName", "Rival", 36, new Vector2(600, 190), new Vector2(500, 50), Color.white, FontStyle.Bold);
        Image rivalCowboy = CreateBox(g, "RivalCowboy", Color.white, new Vector2(600, -10), new Vector2(180, 300));
        Text rivalBang = CreateText(g, "RivalBang", "", 56, new Vector2(440, 40), new Vector2(260, 80), Accent, FontStyle.Bold);

        Text result = CreateText(g, "ResultText", "", 40, new Vector2(0, 30), new Vector2(560, 200), Color.white, FontStyle.Bold);
        Button shoot = CreateButton(g, "ShootButton", "¡DISPARAR!  (Espacio)", new Vector2(0, -150), new Vector2(560, 120), ButtonRed, 44);

        // Puntajes y log
        GameObject scoreBox = CreatePanel(g, "ScoreBox", PanelColor);
        SetRect(scoreBox.GetComponent<RectTransform>(), new Vector2(-650, -390), new Vector2(520, 200));
        Text score = CreateText(scoreBox.transform, "ScoreText", "Puntajes", 28, Vector2.zero, new Vector2(480, 180), Color.white, FontStyle.Normal, TextAnchor.UpperLeft);
        GameObject logBox = CreatePanel(g, "LogBox", PanelColor);
        SetRect(logBox.GetComponent<RectTransform>(), new Vector2(330, -390), new Vector2(1060, 200));
        Text log = CreateText(logBox.transform, "LogText", "", 20, Vector2.zero, new Vector2(1030, 185), SoftText, FontStyle.Normal, TextAnchor.LowerLeft);

        Set(so, "gamePanel", game);
        Set(so, "roomText", room);
        Set(so, "leaveButton", leave);
        Set(so, "clockText", clock);
        Set(so, "windowText", window);
        Set(so, "infoText", info);
        Set(so, "localNameText", localName);
        Set(so, "rivalNameText", rivalName);
        Set(so, "localCowboy", localCowboy);
        Set(so, "rivalCowboy", rivalCowboy);
        Set(so, "localBangText", localBang);
        Set(so, "rivalBangText", rivalBang);
        Set(so, "resultText", result);
        Set(so, "shootButton", shoot);
        Set(so, "scoreText", score);
        Set(so, "logText", log);
        so.ApplyModifiedPropertiesWithoutUndo();

        game.SetActive(false);
        return ui;
    }
}
