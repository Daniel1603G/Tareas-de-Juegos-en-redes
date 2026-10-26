using System.Collections;
using System.Text;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// "¿Quién es ese Pokémon?" en red con PUN 2.
//  - El MasterClient elige el Pokémon secreto y lo guarda en las Custom Room Properties
//    (así sobrevive a un cambio de MasterClient y le llega a quien entre tarde).
//  - Cada cliente consulta la PokeAPI y arma sus pistas localmente.
//  - Cada intento fallido revela una pista más: Generación, Tipos, Altura/Peso, Letras, Imagen oscurecida.
//  - El primero en acertar avisa al MasterClient por RPC, y él decide quién ganó la ronda.
//  - "Jugar Solo" usa el OfflineMode de PUN: mismo código, sin servidor.
[RequireComponent(typeof(PhotonView))]
public class PokeGuessGame : MonoBehaviourPunCallbacks
{
    // Claves de Custom Properties
    private const string SecretIdKey = "sid";
    private const string RoundKey = "rnd";
    private const string WonRoundKey = "won";
    private const string ScoreKey = "pts";

    [SerializeField] private PokeApiClient api;
    [SerializeField] private PokeGuessUI ui;
    [SerializeField] private string gameVersion = "1";
    [SerializeField] private byte maxPlayersPerRoom = 8;
    [SerializeField] private int maxAttempts = 6;
    [SerializeField] private int maxPokemonId = 1025;
    [SerializeField] private float secondsBetweenRounds = 5f;

    private bool wantsToPlay;
    private int currentRound = -1;
    private SecretPokemon secret;
    private int wrongAttempts;
    private bool roundOver;
    private bool waitingWinConfirmation;
    private int lastWonRound = -1;      // solo lo usa el MasterClient
    private Coroutine nextRoundRoutine;

    private void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        ui.SetNickname("Entrenador" + Random.Range(100, 1000));

        ui.OnlineClicked += PlayOnline;
        ui.OfflineClicked += PlayOffline;
        ui.GuessSubmitted += Guess;
        ui.NextRoundClicked += () => { if (PhotonNetwork.IsMasterClient) StartNewRound(); };
        ui.LeaveClicked += Leave;
    }

    // ================= Conexión y Rooms =================

    private void PlayOnline(string nickname)
    {
        if (!SetNickname(nickname)) return;
        wantsToPlay = true;
        ui.SetMenuInteractable(false);
        ui.SetMenuStatus("Conectando a Photon...");
        PhotonNetwork.GameVersion = gameVersion;
        PhotonNetwork.ConnectUsingSettings();
    }

    private void PlayOffline(string nickname)
    {
        if (!SetNickname(nickname)) return;
        wantsToPlay = true;
        ui.SetMenuInteractable(false);
        PhotonNetwork.OfflineMode = true;   // dispara OnConnectedToMaster en el acto
    }

    private bool SetNickname(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
        {
            ui.SetMenuStatus("Escribí un nombre primero");
            return false;
        }
        PhotonNetwork.NickName = nickname.Trim();
        return true;
    }

    private void Leave()
    {
        wantsToPlay = false;
        if (PhotonNetwork.OfflineMode)
        {
            PhotonNetwork.OfflineMode = false;
            ReturnToMenu("Partida terminada");
        }
        else
        {
            PhotonNetwork.Disconnect();     // OnDisconnected nos devuelve al menú
        }
    }

    public override void OnConnectedToMaster()
    {
        if (!wantsToPlay) return;

        if (PhotonNetwork.OfflineMode)
        {
            PhotonNetwork.CreateRoom("Offline");
            return;
        }

        ui.SetMenuStatus("Conectado (" + PhotonNetwork.CloudRegion + "). Buscando sala...");
        var options = new RoomOptions { MaxPlayers = maxPlayersPerRoom, PlayerTtl = 0, EmptyRoomTtl = 0 };
        PhotonNetwork.JoinRandomOrCreateRoom(roomOptions: options);
    }

    public override void OnJoinedRoom()
    {
        string mode = PhotonNetwork.OfflineMode ? "Modo solo" : "Sala: " + PhotonNetwork.CurrentRoom.Name;
        ui.ShowGame(mode + "   |   Vos: " + PhotonNetwork.NickName);
        ui.Log("Entraste a la sala");
        currentRound = -1;
        lastWonRound = -1;
        RefreshScoreboard();
        RefreshMasterControls();

        Hashtable props = PhotonNetwork.CurrentRoom.CustomProperties;
        if (props.ContainsKey(RoundKey))
            BeginRoundFromProperties(props);
        else if (PhotonNetwork.IsMasterClient)
            StartNewRound();
        else
            ui.ShowLoading();
    }

    public override void OnJoinRoomFailed(short returnCode, string message) => ReturnToMenu("No se pudo entrar: " + message);

    public override void OnCreateRoomFailed(short returnCode, string message) => ReturnToMenu("No se pudo crear la sala: " + message);

    public override void OnLeftRoom()
    {
        StopAllCoroutines();
        nextRoundRoutine = null;
        secret = null;
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (PhotonNetwork.OfflineMode) return;
        string status = cause == DisconnectCause.DisconnectByClientLogic ? "Desconectado" : "Desconectado: " + cause;
        wantsToPlay = false;
        ReturnToMenu(status);
    }

    private void ReturnToMenu(string status)
    {
        StopAllCoroutines();
        nextRoundRoutine = null;
        secret = null;
        ui.ShowMenu(status);
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        ui.Log(newPlayer.NickName + " se unió");
        RefreshScoreboard();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        ui.Log(otherPlayer.NickName + " salió");
        RefreshScoreboard();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        ui.Log("Nuevo MasterClient: " + newMasterClient.NickName);
        RefreshMasterControls();

        // Si la ronda ya terminó y el viejo Master no llegó a arrancar otra, la arrancamos nosotros.
        if (PhotonNetwork.IsMasterClient && RoundAlreadyWon())
            ScheduleNextRound();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps.ContainsKey(ScoreKey)) RefreshScoreboard();
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(RoundKey))
            BeginRoundFromProperties(PhotonNetwork.CurrentRoom.CustomProperties);
    }

    // ================= Rondas =================

    // Solo el MasterClient: elige un Pokémon y lo publica en las propiedades de la sala.
    private void StartNewRound()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null) return;

        if (nextRoundRoutine != null)
        {
            StopCoroutine(nextRoundRoutine);
            nextRoundRoutine = null;
        }

        int previousId = secret != null ? secret.Id : -1;
        int newId;
        do { newId = Random.Range(1, maxPokemonId + 1); } while (newId == previousId);

        int round = Mathf.Max(currentRound, GetRoomInt(RoundKey, 0)) + 1;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { SecretIdKey, newId }, { RoundKey, round } });
    }

    private void BeginRoundFromProperties(Hashtable props)
    {
        if (!props.TryGetValue(RoundKey, out object roundObj) || !props.TryGetValue(SecretIdKey, out object idObj)) return;

        int round = (int)roundObj;
        if (round == currentRound) return;

        currentRound = round;
        secret = null;
        wrongAttempts = 0;
        roundOver = false;
        waitingWinConfirmation = false;

        ui.ShowLoading();
        ui.Log("--- Ronda " + round + " ---");
        RefreshMasterControls();
        StartCoroutine(LoadSecret((int)idObj, round));
    }

    private IEnumerator LoadSecret(int id, int round)
    {
        SecretPokemon loaded = null;
        string error = null;
        yield return api.FetchPokemon(id, data => loaded = data, e => error = e);

        if (round != currentRound) yield break;     // llegó tarde: ya empezó otra ronda

        if (error != null)
        {
            ui.SetResult("Error con la PokeAPI: " + error, Color.red);
            ui.Log("Error de API: " + error);
            if (PhotonNetwork.IsMasterClient) ui.SetNextRoundVisible(true);
            yield break;
        }

        secret = loaded;
        RefreshRoundView();

        // Si entramos tarde y la ronda ya tenía ganador, solo mostramos la respuesta.
        if (RoundAlreadyWon()) EndRound("Esta ronda ya tiene ganador", Color.gray);
    }

    private void Guess(string text)
    {
        if (secret == null || roundOver || waitingWinConfirmation) return;

        if (PokeGuessText.IsCorrectGuess(text, secret))
        {
            // No decidimos solos: el MasterClient confirma quién llegó primero.
            waitingWinConfirmation = true;
            ui.SetGuessInteractable(false);
            ui.SetResult("¡Correcto! Confirmando con el MasterClient...", Color.yellow);
            photonView.RPC(nameof(RPC_ClaimWin), RpcTarget.MasterClient, currentRound);
            return;
        }

        wrongAttempts++;
        photonView.RPC(nameof(RPC_WrongGuess), RpcTarget.All, PhotonNetwork.NickName, text.Trim());

        if (wrongAttempts >= maxAttempts)
            EndRound("Sin intentos. Era " + PokeGuessText.DisplayName(secret), Color.red);
        else
            RefreshRoundView();
    }

    private void RefreshRoundView()
    {
        int revealed = Mathf.Min(1 + wrongAttempts, PokeGuessUI.ClueCount);
        ui.ShowClues(secret, revealed, roundOver);
        ui.SetAttempts(wrongAttempts, maxAttempts);
        ui.SetGuessInteractable(!roundOver && !waitingWinConfirmation);
        if (!roundOver) ui.SetResult(string.Empty, Color.white);
    }

    private void EndRound(string message, Color color)
    {
        roundOver = true;
        waitingWinConfirmation = false;
        if (secret != null) RefreshRoundView();
        ui.SetResult(message, color);
        RefreshMasterControls();
    }

    private void ScheduleNextRound()
    {
        if (nextRoundRoutine != null) StopCoroutine(nextRoundRoutine);
        nextRoundRoutine = StartCoroutine(NextRoundAfterDelay());
    }

    private IEnumerator NextRoundAfterDelay()
    {
        yield return new WaitForSeconds(secondsBetweenRounds);
        nextRoundRoutine = null;
        StartNewRound();
    }

    // ================= RPCs =================

    [PunRPC]
    private void RPC_WrongGuess(string nickname, string guess)
    {
        ui.Log(nickname + ": \"" + guess + "\" (x)");
    }

    // Lo recibe solo el MasterClient: el primer reclamo válido de la ronda gana.
    [PunRPC]
    private void RPC_ClaimWin(int round, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (round != currentRound || round == lastWonRound || RoundAlreadyWon()) return;

        lastWonRound = round;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { WonRoundKey, round } });

        int score = info.Sender.CustomProperties.TryGetValue(ScoreKey, out object pts) ? (int)pts : 0;
        info.Sender.SetCustomProperties(new Hashtable { { ScoreKey, score + 1 } });

        photonView.RPC(nameof(RPC_RoundWon), RpcTarget.All, info.Sender.ActorNumber, info.Sender.NickName, round);
        ScheduleNextRound();
    }

    [PunRPC]
    private void RPC_RoundWon(int winnerActor, string winnerName, int round)
    {
        if (round != currentRound) return;

        bool iWon = winnerActor == PhotonNetwork.LocalPlayer.ActorNumber;
        string answer = secret != null ? PokeGuessText.DisplayName(secret) : "?";
        ui.Log(">> " + winnerName + " adivinó: " + answer);
        EndRound(iWon ? "¡Ganaste! Era " + answer : winnerName + " ganó. Era " + answer, iWon ? Color.green : new Color(1f, 0.6f, 0.2f));
    }

    // ================= Helpers =================

    private bool RoundAlreadyWon() => currentRound >= 0 && GetRoomInt(WonRoundKey, -1) == currentRound;

    private static int GetRoomInt(string key, int fallback)
    {
        Room room = PhotonNetwork.CurrentRoom;
        return room != null && room.CustomProperties.TryGetValue(key, out object value) ? (int)value : fallback;
    }

    private void RefreshMasterControls()
    {
        ui.SetNextRoundVisible(PhotonNetwork.IsMasterClient && (roundOver || secret == null));
    }

    private void RefreshScoreboard()
    {
        if (PhotonNetwork.CurrentRoom == null) return;

        var builder = new StringBuilder("Puntajes\n");
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            int score = player.CustomProperties.TryGetValue(ScoreKey, out object pts) ? (int)pts : 0;
            builder.Append(player.IsMasterClient ? "[M] " : "     ")
                   .Append(player.NickName)
                   .Append(player.IsLocal ? " (vos)" : string.Empty)
                   .Append(": ").Append(score).Append('\n');
        }
        ui.SetScoreboard(builder.ToString());
    }
}
