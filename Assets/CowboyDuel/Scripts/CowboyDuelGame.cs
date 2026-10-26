using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Random = UnityEngine.Random;

// Patrón "Tiempo de gracia": Duelo de cowboys con PUN 2.
//  - Los dos jugadores ven un reloj con la hora del dispositivo (en UTC para no depender del huso horario).
//  - Solo se puede disparar cuando los segundos son múltiplo de 3, y una sola vez por ventana.
//  - El disparo se le envía al MasterClient, que decide en última instancia.
//  - Cuando le llega el primer disparo empieza a correr un tiempo de gracia. Si en ese lapso llega otro,
//    compara cuál sucedió primero usando el momento de envío (SentServerTimestamp), no el de llegada.
//  - "Practicar vs Bot" usa el OfflineMode de PUN con un rival simulado que tiene latencia.
[RequireComponent(typeof(PhotonView))]
public class CowboyDuelGame : MonoBehaviourPunCallbacks
{
    private const string RoundKey = "rnd";
    private const string ScoreKey = "pts";
    private const string BotScoreKey = "bot";
    private const int BotActor = -1;
    private const int WindowSeconds = 3;

    private struct Shot
    {
        public int Actor;
        public string Name;
        public int SentServerTime;      // PhotonMessageInfo.SentServerTimestamp (ms, reloj sincronizado)
        public int ArrivalServerTime;   // cuándo lo recibió el MasterClient
        public long ClientUtcMs;        // reloj del dispositivo que disparó
    }

    [SerializeField] private CowboyDuelUI ui;
    [SerializeField] private string gameVersion = "duelo-1";
    [SerializeField] private int graceMilliseconds = 300;
    [SerializeField] private float secondsBetweenDuels = 4f;

    [Header("Bot (solo práctica offline)")]
    [SerializeField] private string botName = "Bot Bandido";
    [SerializeField, Range(0f, 1f)] private float botFireChance = 0.8f;
    [SerializeField] private Vector2Int botReactionMs = new Vector2Int(80, 650);
    [SerializeField] private Vector2Int botLatencyMs = new Vector2Int(20, 350);

    private bool wantsToPlay;
    private int currentRound = -1;
    private int resolvedRound = -1;
    private long lastShotWindow = -1;

    // Solo los usa el MasterClient
    private readonly List<Shot> pendingShots = new List<Shot>();
    private int graceRound = -1;
    private int decidedRound = -1;
    private Coroutine graceRoutine;
    private Coroutine nextDuelRoutine;
    private long botWindow = -1;
    private int botShotRound = -1;

    private bool InRoom => PhotonNetwork.CurrentRoom != null;
    private bool HasRival => PhotonNetwork.OfflineMode || (InRoom && PhotonNetwork.CurrentRoom.PlayerCount >= 2);
    private bool DuelActive => InRoom && HasRival && currentRound >= 0 && resolvedRound != currentRound;

    private void Start()
    {
        ui.SetNickname("Vaquero" + Random.Range(100, 1000));
        ui.OnlineClicked += PlayOnline;
        ui.BotClicked += PlayVsBot;
        ui.ShootPressed += Shoot;
        ui.LeaveClicked += Leave;
        ui.SetInfo("Tiempo de gracia del MasterClient: " + graceMilliseconds + " ms");
    }

    private void Update()
    {
        if (!InRoom) return;

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ui.SetClock(DateTime.UtcNow.ToLocalTime().ToString("HH:mm:ss.f"));
        ui.SetWindow(IsWindowOpen(nowMs), WindowId(nowMs) == lastShotWindow);
        ui.SetShootInteractable(DuelActive);

        if (PhotonNetwork.OfflineMode && PhotonNetwork.IsMasterClient) UpdateBot(nowMs);
    }

    // La ventana es todo el segundo cuyos segundos son múltiplo de 3 (60 es múltiplo de 3, así que no se corta al cambiar de minuto).
    private static bool IsWindowOpen(long utcMs) => (utcMs / 1000) % WindowSeconds == 0;

    private static long WindowId(long utcMs) => utcMs / (WindowSeconds * 1000);

    private static string FormatClock(long utcMs) =>
        DateTimeOffset.FromUnixTimeMilliseconds(utcMs).ToLocalTime().ToString("HH:mm:ss.fff");

    // ================= Conexión y Rooms =================

    private void PlayOnline(string nickname)
    {
        if (!SetNickname(nickname)) return;
        wantsToPlay = true;
        ui.SetMenuInteractable(false);
        ui.SetMenuStatus("Conectando a Photon...");
        PhotonNetwork.GameVersion = gameVersion;   // versión propia: no se cruza con las salas de otras tareas
        PhotonNetwork.ConnectUsingSettings();
    }

    private void PlayVsBot(string nickname)
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
            ReturnToMenu("Duelo terminado");
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
            PhotonNetwork.CreateRoom("Práctica");
            return;
        }

        ui.SetMenuStatus("Conectado (" + PhotonNetwork.CloudRegion + "). Buscando rival...");
        var options = new RoomOptions { MaxPlayers = 2, PlayerTtl = 0, EmptyRoomTtl = 0 };
        PhotonNetwork.JoinRandomOrCreateRoom(roomOptions: options);
    }

    public override void OnJoinedRoom()
    {
        string mode = PhotonNetwork.OfflineMode ? "Práctica vs Bot" : "Sala: " + PhotonNetwork.CurrentRoom.Name;
        ui.ShowGame(mode + "   |   Vos: " + PhotonNetwork.NickName);
        ui.Log("Entraste a la sala");
        currentRound = -1;
        resolvedRound = -1;
        decidedRound = -1;
        graceRound = -1;
        lastShotWindow = -1;
        pendingShots.Clear();
        RefreshPlayers();

        Hashtable props = PhotonNetwork.CurrentRoom.CustomProperties;
        if (props.ContainsKey(RoundKey) && HasRival)
            BeginRound((int)props[RoundKey]);
        else
            TryStartDuel();
    }

    public override void OnJoinRoomFailed(short returnCode, string message) => ReturnToMenu("No se pudo entrar: " + message);

    public override void OnCreateRoomFailed(short returnCode, string message) => ReturnToMenu("No se pudo crear la sala: " + message);

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (PhotonNetwork.OfflineMode) return;
        wantsToPlay = false;
        ReturnToMenu(cause == DisconnectCause.DisconnectByClientLogic ? "Desconectado" : "Desconectado: " + cause);
    }

    private void ReturnToMenu(string status)
    {
        StopAllCoroutines();
        graceRoutine = null;
        nextDuelRoutine = null;
        pendingShots.Clear();
        ui.ShowMenu(status);
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        ui.Log(newPlayer.NickName + " entró al pueblo");
        RefreshPlayers();
        TryStartDuel();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        ui.Log(otherPlayer.NickName + " se fue");
        CancelMasterDuel();
        RefreshPlayers();
        ui.ResetCowboys();
        ui.SetResult("Esperando un rival...", Color.white);
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        // Los disparos pendientes vivían en el viejo Master y se perdieron: arrancamos un duelo nuevo.
        ui.Log("Nuevo MasterClient: " + newMasterClient.NickName);
        RefreshPlayers();
        if (PhotonNetwork.IsMasterClient) TryStartDuel();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps.ContainsKey(ScoreKey)) RefreshPlayers();
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(RoundKey)) BeginRound((int)propertiesThatChanged[RoundKey]);
        if (propertiesThatChanged.ContainsKey(BotScoreKey)) RefreshPlayers();
    }

    // ================= Duelos =================

    // Solo el MasterClient: arranca un duelo nuevo si hay rival.
    private void TryStartDuel()
    {
        if (!PhotonNetwork.IsMasterClient || !InRoom) return;

        if (!HasRival)
        {
            ui.SetResult("Esperando un rival...", Color.white);
            return;
        }

        CancelMasterDuel();
        int round = Mathf.Max(currentRound, GetRoomInt(RoundKey, 0)) + 1;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { RoundKey, round } });
    }

    private void BeginRound(int round)
    {
        if (round == currentRound) return;

        currentRound = round;
        lastShotWindow = -1;
        ui.ResetCowboys();
        ui.SetResult("Duelo " + round + ": disparen cuando los segundos sean múltiplo de 3", Color.white);
        ui.Log("--- Duelo " + round + " ---");
    }

    private void Shoot()
    {
        if (!DuelActive) return;

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (!IsWindowOpen(nowMs))
        {
            ui.SetResult("¡Todavía no! Solo cuando los segundos son múltiplo de 3", new Color(1f, 0.6f, 0.2f));
            return;
        }

        long window = WindowId(nowMs);
        if (window == lastShotWindow)
        {
            ui.SetResult("Ya disparaste en esta ventana", new Color(1f, 0.6f, 0.2f));
            return;
        }

        lastShotWindow = window;
        ui.ShowBang(true);
        ui.SetResult("¡Disparaste! El MasterClient decide...", Color.yellow);
        photonView.RPC(nameof(RPC_Shoot), RpcTarget.MasterClient, currentRound, nowMs);
    }

    // Lo recibe solo el MasterClient.
    [PunRPC]
    private void RPC_Shoot(int round, long clientUtcMs, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        ReceiveShot(round, new Shot
        {
            Actor = info.Sender.ActorNumber,
            Name = info.Sender.NickName,
            SentServerTime = info.SentServerTimestamp,
            ArrivalServerTime = PhotonNetwork.ServerTimestamp,
            ClientUtcMs = clientUtcMs
        });
    }

    private void ReceiveShot(int round, Shot shot)
    {
        if (round != currentRound || round == decidedRound)
        {
            ui.Log(shot.Name + " disparó tarde (duelo ya decidido)");
            return;
        }

        // El Master valida la regla: el reloj del que disparó tenía que estar en ventana.
        if (!IsWindowOpen(shot.ClientUtcMs))
        {
            ui.Log(shot.Name + " disparó fuera de ventana: no cuenta");
            return;
        }

        if (pendingShots.Exists(s => s.Actor == shot.Actor)) return;
        pendingShots.Add(shot);

        photonView.RPC(nameof(RPC_ShotHeard), RpcTarget.All, shot.Actor);

        // El primer disparo abre el tiempo de gracia; los que lleguen dentro de él también compiten.
        if (graceRound != round)
        {
            graceRound = round;
            graceRoutine = StartCoroutine(GraceThenDecide(round));
        }
    }

    private IEnumerator GraceThenDecide(int round)
    {
        yield return new WaitForSecondsRealtime(graceMilliseconds / 1000f);
        graceRoutine = null;
        Decide(round);
    }

    private void Decide(int round)
    {
        if (round != currentRound || pendingShots.Count == 0) return;
        decidedRound = round;

        // Gana quien DISPARÓ primero (momento de envío), aunque su mensaje haya llegado después.
        pendingShots.Sort((a, b) => unchecked(a.SentServerTime - b.SentServerTime));
        Shot winner = pendingShots[0];
        Shot firstArrived = pendingShots[0];
        foreach (Shot shot in pendingShots)
            if (unchecked(shot.ArrivalServerTime - firstArrived.ArrivalServerTime) < 0) firstArrived = shot;

        var summary = new StringBuilder();
        foreach (Shot shot in pendingShots)
        {
            summary.Append(shot.Name)
                   .Append(": disparó +").Append(unchecked(shot.SentServerTime - winner.SentServerTime)).Append(" ms")
                   .Append(", llegó +").Append(unchecked(shot.ArrivalServerTime - winner.SentServerTime)).Append(" ms")
                   .Append(" (su reloj: ").Append(FormatClock(shot.ClientUtcMs)).Append(")\n");
        }
        if (firstArrived.Actor != winner.Actor)
            summary.Append("¡El tiempo de gracia cambió el resultado! Llegó primero ").Append(firstArrived.Name).Append('\n');

        AddScore(winner);
        photonView.RPC(nameof(RPC_DuelResult), RpcTarget.All, round, winner.Actor, winner.Name, summary.ToString().TrimEnd());

        pendingShots.Clear();
        if (nextDuelRoutine != null) StopCoroutine(nextDuelRoutine);
        nextDuelRoutine = StartCoroutine(NextDuelAfterDelay());
    }

    private void AddScore(Shot winner)
    {
        if (winner.Actor == BotActor)
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { BotScoreKey, GetRoomInt(BotScoreKey, 0) + 1 } });
            return;
        }

        Player player = PhotonNetwork.CurrentRoom.GetPlayer(winner.Actor);
        if (player == null) return;
        int score = player.CustomProperties.TryGetValue(ScoreKey, out object pts) ? (int)pts : 0;
        player.SetCustomProperties(new Hashtable { { ScoreKey, score + 1 } });
    }

    private IEnumerator NextDuelAfterDelay()
    {
        yield return new WaitForSecondsRealtime(secondsBetweenDuels);
        nextDuelRoutine = null;
        TryStartDuel();
    }

    private void CancelMasterDuel()
    {
        if (graceRoutine != null) StopCoroutine(graceRoutine);
        if (nextDuelRoutine != null) StopCoroutine(nextDuelRoutine);
        graceRoutine = null;
        nextDuelRoutine = null;
        pendingShots.Clear();
    }

    // ================= RPCs =================

    [PunRPC]
    private void RPC_ShotHeard(int actor)
    {
        if (actor != PhotonNetwork.LocalPlayer.ActorNumber) ui.ShowBang(false);
    }

    [PunRPC]
    private void RPC_DuelResult(int round, int winnerActor, string winnerName, string summary)
    {
        if (round != currentRound) return;
        resolvedRound = round;

        bool iWon = winnerActor == PhotonNetwork.LocalPlayer.ActorNumber;
        ui.ShowOutcome(iWon);
        ui.SetResult(iWon ? "¡Ganaste el duelo!" : winnerName + " fue más rápido", iWon ? Color.green : new Color(1f, 0.4f, 0.35f));
        ui.Log(">> Ganó " + winnerName);
        foreach (string line in summary.Split('\n'))
            ui.Log("   " + line);
    }

    // ================= Bot (OfflineMode) =================

    // En cada ventana nueva el bot decide si dispara, con un tiempo de reacción y una latencia simulada.
    // Así se ve el tiempo de gracia en acción: puede disparar antes que vos pero llegar después.
    private void UpdateBot(long nowMs)
    {
        if (!DuelActive || botShotRound == currentRound || !IsWindowOpen(nowMs)) return;

        long window = WindowId(nowMs);
        if (window == botWindow) return;
        botWindow = window;

        if (Random.value <= botFireChance)
            StartCoroutine(BotShoot(currentRound, Random.Range(botReactionMs.x, botReactionMs.y)));
    }

    private IEnumerator BotShoot(int round, int reactionMs)
    {
        yield return new WaitForSecondsRealtime(reactionMs / 1000f);
        if (round != currentRound || round == decidedRound) yield break;

        botShotRound = round;
        int sentServerTime = PhotonNetwork.ServerTimestamp;
        long clientUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        yield return new WaitForSecondsRealtime(Random.Range(botLatencyMs.x, botLatencyMs.y) / 1000f);

        ReceiveShot(round, new Shot
        {
            Actor = BotActor,
            Name = botName,
            SentServerTime = sentServerTime,
            ArrivalServerTime = PhotonNetwork.ServerTimestamp,
            ClientUtcMs = clientUtcMs
        });
    }

    // ================= Helpers =================

    private static int GetRoomInt(string key, int fallback)
    {
        Room room = PhotonNetwork.CurrentRoom;
        return room != null && room.CustomProperties.TryGetValue(key, out object value) ? (int)value : fallback;
    }

    private void RefreshPlayers()
    {
        if (!InRoom) return;

        string rivalName = PhotonNetwork.OfflineMode ? botName : "Esperando rival...";
        var score = new StringBuilder("Puntajes\n");
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (!player.IsLocal) rivalName = player.NickName;
            int pts = player.CustomProperties.TryGetValue(ScoreKey, out object value) ? (int)value : 0;
            score.Append(player.IsMasterClient ? "[M] " : "     ")
                 .Append(player.NickName)
                 .Append(player.IsLocal ? " (vos)" : string.Empty)
                 .Append(": ").Append(pts).Append('\n');
        }
        if (PhotonNetwork.OfflineMode)
            score.Append("     ").Append(botName).Append(": ").Append(GetRoomInt(BotScoreKey, 0)).Append('\n');

        ui.SetPlayers(PhotonNetwork.NickName, rivalName);
        ui.SetScore(score.ToString());
    }
}
