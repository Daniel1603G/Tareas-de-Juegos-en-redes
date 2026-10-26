using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Vista del duelo: solo muestra datos y avisa cuando el jugador toca algo. La lógica vive en CowboyDuelGame.
public class CowboyDuelUI : MonoBehaviour
{
    private static readonly Color WindowOpenColor = new Color(0.3f, 0.95f, 0.4f);
    private static readonly Color WindowClosedColor = new Color(0.75f, 0.75f, 0.8f);
    private static readonly Color CowboyColor = new Color(0.8f, 0.55f, 0.3f);
    private static readonly Color WinnerColor = new Color(1f, 0.82f, 0.2f);
    private static readonly Color LoserColor = new Color(0.35f, 0.35f, 0.38f);

    [Header("Menú")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private InputField nicknameInput;
    [SerializeField] private Button onlineButton;
    [SerializeField] private Button botButton;
    [SerializeField] private Text menuStatusText;

    [Header("Juego")]
    [SerializeField] private GameObject gamePanel;
    [SerializeField] private Text roomText;
    [SerializeField] private Button leaveButton;
    [SerializeField] private Text clockText;
    [SerializeField] private Text windowText;
    [SerializeField] private Text infoText;
    [SerializeField] private Text localNameText;
    [SerializeField] private Text rivalNameText;
    [SerializeField] private Image localCowboy;
    [SerializeField] private Image rivalCowboy;
    [SerializeField] private Text localBangText;
    [SerializeField] private Text rivalBangText;
    [SerializeField] private Text resultText;
    [SerializeField] private Button shootButton;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text logText;

    [SerializeField] private float bangSeconds = 0.6f;
    [SerializeField] private int maxLogLines = 9;

    public event Action<string> OnlineClicked;
    public event Action<string> BotClicked;
    public event Action ShootPressed;
    public event Action LeaveClicked;

    private readonly Queue<string> logLines = new Queue<string>();
    private Coroutine localBangRoutine;
    private Coroutine rivalBangRoutine;

    private void Awake()
    {
        onlineButton.onClick.AddListener(() => OnlineClicked?.Invoke(nicknameInput.text));
        botButton.onClick.AddListener(() => BotClicked?.Invoke(nicknameInput.text));
        shootButton.onClick.AddListener(() => ShootPressed?.Invoke());
        leaveButton.onClick.AddListener(() => LeaveClicked?.Invoke());

        ShowMenu("Elegí un nombre y cómo querés jugar");
    }

    private void Update()
    {
        // Espacio también dispara
        Keyboard keyboard = Keyboard.current;
        if (gamePanel.activeSelf && shootButton.interactable && keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            ShootPressed?.Invoke();
    }

    // ---------- Menú ----------

    public void ShowMenu(string status)
    {
        menuPanel.SetActive(true);
        gamePanel.SetActive(false);
        SetMenuStatus(status);
        SetMenuInteractable(true);
    }

    public void SetMenuStatus(string status) => menuStatusText.text = status;

    public void SetMenuInteractable(bool interactable)
    {
        onlineButton.interactable = interactable;
        botButton.interactable = interactable;
        nicknameInput.interactable = interactable;
    }

    public void SetNickname(string nickname) => nicknameInput.text = nickname;

    // ---------- Juego ----------

    public void ShowGame(string roomInfo)
    {
        menuPanel.SetActive(false);
        gamePanel.SetActive(true);
        roomText.text = roomInfo;
        logLines.Clear();
        logText.text = string.Empty;
        ResetCowboys();
    }

    public void SetClock(string time) => clockText.text = time;

    public void SetWindow(bool open, bool alreadyShot)
    {
        if (!open)
        {
            windowText.text = "Esperá...";
            windowText.color = WindowClosedColor;
        }
        else if (alreadyShot)
        {
            windowText.text = "Ya disparaste";
            windowText.color = WindowClosedColor;
        }
        else
        {
            windowText.text = "¡AHORA!";
            windowText.color = WindowOpenColor;
        }
    }

    public void SetInfo(string text) => infoText.text = text;

    public void SetPlayers(string localName, string rivalName)
    {
        localNameText.text = localName;
        rivalNameText.text = rivalName;
    }

    public void ResetCowboys()
    {
        ResetCowboy(localCowboy, localBangText);
        ResetCowboy(rivalCowboy, rivalBangText);
    }

    private static void ResetCowboy(Image cowboy, Text bang)
    {
        cowboy.color = CowboyColor;
        cowboy.rectTransform.localRotation = Quaternion.identity;
        bang.text = string.Empty;
    }

    public void ShowBang(bool local)
    {
        if (local)
        {
            if (localBangRoutine != null) StopCoroutine(localBangRoutine);
            localBangRoutine = StartCoroutine(Bang(localBangText));
        }
        else
        {
            if (rivalBangRoutine != null) StopCoroutine(rivalBangRoutine);
            rivalBangRoutine = StartCoroutine(Bang(rivalBangText));
        }
    }

    private IEnumerator Bang(Text bang)
    {
        bang.text = "¡BANG!";
        yield return new WaitForSecondsRealtime(bangSeconds);
        bang.text = string.Empty;
    }

    // El perdedor "cae" (se gira y se apaga) y el ganador brilla.
    public void ShowOutcome(bool localWon)
    {
        Image winner = localWon ? localCowboy : rivalCowboy;
        Image loser = localWon ? rivalCowboy : localCowboy;
        winner.color = WinnerColor;
        winner.rectTransform.localRotation = Quaternion.identity;
        loser.color = LoserColor;
        loser.rectTransform.localRotation = Quaternion.Euler(0f, 0f, localWon ? -90f : 90f);
    }

    public void SetResult(string text, Color color)
    {
        resultText.text = text;
        resultText.color = color;
    }

    public void SetShootInteractable(bool interactable) => shootButton.interactable = interactable;

    public void SetScore(string text) => scoreText.text = text;

    public void Log(string line)
    {
        logLines.Enqueue(line);
        while (logLines.Count > maxLogLines) logLines.Dequeue();
        logText.text = string.Join("\n", logLines);
    }
}
