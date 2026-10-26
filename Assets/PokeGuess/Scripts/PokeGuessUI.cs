using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Vista: solo muestra datos y avisa cuando el jugador toca algo. La lógica vive en PokeGuessGame.
public class PokeGuessUI : MonoBehaviour
{
    public const int ClueCount = 5;

    [Header("Menú")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private InputField nicknameInput;
    [SerializeField] private Button onlineButton;
    [SerializeField] private Button offlineButton;
    [SerializeField] private Text menuStatusText;

    [Header("Juego")]
    [SerializeField] private GameObject gamePanel;
    [SerializeField] private Text roomText;
    [SerializeField] private Text[] clueTexts = new Text[ClueCount];
    [SerializeField] private RawImage pokemonImage;
    [SerializeField] private Text imageHintText;
    [SerializeField] private Text resultText;
    [SerializeField] private Text attemptsText;
    [SerializeField] private InputField guessInput;
    [SerializeField] private Button guessButton;
    [SerializeField] private Button nextRoundButton;
    [SerializeField] private Button leaveButton;
    [SerializeField] private Text scoreboardText;
    [SerializeField] private Text logText;

    [SerializeField] private int maxLogLines = 12;

    public event Action<string> OnlineClicked;
    public event Action<string> OfflineClicked;
    public event Action<string> GuessSubmitted;
    public event Action NextRoundClicked;
    public event Action LeaveClicked;

    private readonly Queue<string> logLines = new Queue<string>();

    private void Awake()
    {
        onlineButton.onClick.AddListener(() => OnlineClicked?.Invoke(nicknameInput.text));
        offlineButton.onClick.AddListener(() => OfflineClicked?.Invoke(nicknameInput.text));
        guessButton.onClick.AddListener(SubmitGuess);
        guessInput.onEndEdit.AddListener(_ => { if (EnterPressed()) SubmitGuess(); });
        nextRoundButton.onClick.AddListener(() => NextRoundClicked?.Invoke());
        leaveButton.onClick.AddListener(() => LeaveClicked?.Invoke());

        ShowMenu("Elegí un nombre y cómo querés jugar");
    }

    private static bool EnterPressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
    }

    private void SubmitGuess()
    {
        string guess = guessInput.text;
        if (string.IsNullOrWhiteSpace(guess)) return;
        guessInput.text = string.Empty;
        GuessSubmitted?.Invoke(guess);
        guessInput.ActivateInputField();
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
        offlineButton.interactable = interactable;
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
    }

    public void ShowLoading()
    {
        for (int i = 0; i < clueTexts.Length; i++)
            clueTexts[i].text = ClueLabel(i) + "???";
        pokemonImage.texture = null;
        pokemonImage.color = new Color(1f, 1f, 1f, 0f);
        imageHintText.text = "Buscando Pokémon...";
        resultText.text = string.Empty;
        attemptsText.text = string.Empty;
        SetGuessInteractable(false);
    }

    // Muestra las primeras 'revealed' pistas; si el round terminó se muestra todo.
    public void ShowClues(SecretPokemon secret, int revealed, bool roundOver)
    {
        if (roundOver) revealed = ClueCount;

        string[] values =
        {
            "Gen " + secret.Generation,
            string.Join(" / ", secret.Types),
            secret.HeightMeters.ToString("0.0") + " m  -  " + secret.WeightKg.ToString("0.0") + " kg",
            PokeGuessText.LetterHint(secret),
            "(ver imagen)"
        };

        for (int i = 0; i < clueTexts.Length; i++)
            clueTexts[i].text = ClueLabel(i) + (i < revealed ? values[i] : "???");

        if (roundOver && secret.Artwork != null)
        {
            pokemonImage.texture = secret.Artwork;
            pokemonImage.color = Color.white;
            imageHintText.text = string.Empty;
        }
        else if (revealed >= ClueCount && secret.Silhouette != null)
        {
            pokemonImage.texture = secret.Silhouette;
            pokemonImage.color = Color.white;
            imageHintText.text = string.Empty;
        }
        else
        {
            pokemonImage.texture = null;
            pokemonImage.color = new Color(1f, 1f, 1f, 0f);
            imageHintText.text = "?";
        }
    }

    private static string ClueLabel(int index)
    {
        switch (index)
        {
            case 0: return "1. Generación: ";
            case 1: return "2. Tipos: ";
            case 2: return "3. Altura y Peso: ";
            case 3: return "4. Nombre: ";
            default: return "5. Imagen: ";
        }
    }

    public void SetAttempts(int used, int max) => attemptsText.text = "Intentos: " + used + " / " + max;

    public void SetResult(string text, Color color)
    {
        resultText.text = text;
        resultText.color = color;
    }

    public void SetGuessInteractable(bool interactable)
    {
        guessInput.interactable = interactable;
        guessButton.interactable = interactable;
        if (interactable) guessInput.ActivateInputField();
    }

    public void SetNextRoundVisible(bool visible) => nextRoundButton.gameObject.SetActive(visible);

    public void SetScoreboard(string text) => scoreboardText.text = text;

    public void Log(string line)
    {
        logLines.Enqueue(line);
        while (logLines.Count > maxLogLines) logLines.Dequeue();
        logText.text = string.Join("\n", logLines);
    }
}
