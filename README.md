# Tareas de Juegos en Red

Proyecto de Unity con las tareas de la materia **Juegos en Red**. Cada tarea está en su propia escena.

- **Unity:** 6.3 (6000.3.12f1), URP
- **Red:** Photon PUN 2 (v2.55), en `Assets/Photon`
- **JSON:** `com.unity.nuget.newtonsoft-json` 3.2.1
- **Input:** Input System (la UI usa `InputSystemUIInputModule`)

## Tareas

| # | Tarea | Escena | Carpeta |
|---|---|---|---|
| 1 | [Serialización & API: ¿Quién es ese Pokémon?](#1-serialización--api--quién-es-ese-pokémon) | `Assets/Scenes/PokeGuess.unity` | `Assets/PokeGuess` |
| 2 | [Patrones de sincronización precisa: Duelo de cowboys (tiempo de gracia)](#2-patrones-de-sincronización-precisa--duelo-de-cowboys) | `Assets/Scenes/CowboyDuel.unity` | `Assets/CowboyDuel` |

Para abrir una tarea, abrí su escena y dale Play. Las escenas se generan desde código (`Assets/*/Editor/*SceneBuilder.cs`, con helpers compartidos en `Assets/Common/Editor`), así que también se pueden abrir o regenerar desde los menús **PokeGuess** y **Tareas** de Unity.

---

## 1. Serialización & API: ¿Quién es ese Pokémon?

> Usando la API de Pokémon ([pokeapi.co](https://pokeapi.co/)), crear una actividad donde el jugador deba adivinar el Pokémon secreto. Con cada intento revelamos más información.

Con cada intento fallido se revela una pista más:

1. Generación
2. Tipos
3. Altura y Peso
4. Cantidad de letras del nombre
5. Imagen oscurecida (silueta)

Además es multijugador con Photon PUN 2: todos los jugadores de la sala compiten por adivinar el mismo Pokémon.

### Cómo jugar

1. Abrir la escena `Assets/Scenes/PokeGuess.unity` (o el menú **PokeGuess → Abrir escena**).
2. Darle Play, escribir un nombre y elegir:
   - **Jugar Solo**: usa el `OfflineMode` de PUN y no necesita servidor.
   - **Jugar Online**: se conecta a Photon y entra a una sala, o la crea si no hay. Para probar con 2 jugadores, hacer un build y abrirlo junto al Editor.
3. Escribir el nombre del Pokémon y apretar Enter (o **Adivinar**). Acepta el nombre en inglés o en español, sin importar mayúsculas, tildes ni guiones.

Cada jugador tiene 6 intentos por ronda. El primero en acertar suma un punto y, a los 5 segundos, empieza una ronda nueva.

### Qué se aplicó de la materia

| Tema | Dónde |
|---|---|
| Deserialización JSON con **Newtonsoft** y `[JsonProperty]` para mapear la estructura de la API (el root y objetos anidados como `official-artwork`) | `Scripts/PokemonData.cs` |
| Consumo de la API REST con `UnityWebRequest` en **corrutinas**, `using` para liberar el request y manejo de errores HTTP (404, 429, 5xx, sin conexión, JSON inválido) | `Scripts/PokeApiClient.cs` |
| Descarga de la textura y generación de la **silueta** píxel a píxel | `PokeApiClient.MakeSilhouette` |
| **Rooms**: `ConnectUsingSettings`, `JoinRandomOrCreateRoom`, `RoomOptions`, `NickName` | `Scripts/PokeGuessGame.cs` |
| **Custom Room Properties**: el MasterClient guarda el Pokémon secreto y la ronda. Así no se pierden si cambia el MasterClient y le llegan a quien entra tarde | `PokeGuessGame.StartNewRound` / `OnRoomPropertiesUpdate` |
| **RPCs**: los intentos fallidos van a todos (`RpcTarget.All`) y el reclamo de victoria va solo al MasterClient (`RpcTarget.MasterClient`), que decide quién llegó primero y lo anuncia | `RPC_WrongGuess`, `RPC_ClaimWin`, `RPC_RoundWon` |
| **Player Properties** para el puntaje | `ScoreKey` |
| `OnMasterClientSwitched`: si la ronda terminó y el Master se fue, el nuevo arranca la siguiente | `PokeGuessGame` |

### Archivos

```
Assets/PokeGuess/
├── Scripts/
│   ├── PokemonData.cs            # Modelos JSON de la PokeAPI
│   ├── PokeApiClient.cs          # Requests HTTP y silueta
│   ├── PokeGuessText.cs          # Normalización de nombres y traducciones
│   ├── PokeGuessGame.cs          # Lógica de red y rondas (PUN 2)
│   └── PokeGuessUI.cs            # Vista (uGUI)
└── Editor/
    └── PokeGuessSceneBuilder.cs  # Genera la escena desde código
```

---

## 2. Patrones de sincronización precisa: Duelo de cowboys

> Subir lo hecho en clase: 1 de los 3 patrones. Elegido: **Tiempo de gracia (Duelo de cowboys)**.

Consigna (presentación *JenR-Sync2*):

- Ambos jugadores ven en pantalla un reloj con la hora del dispositivo.
- Solo pueden disparar cuando los segundos del reloj son múltiplo de 3, y una sola vez por ventana.
- El disparo se le envía al MasterClient, que decide en última instancia. Cuando le llega el primer mensaje empieza a correr un **tiempo de gracia**; si en ese lapso llega el disparo del otro, comprueba cuál sucedió primero.

### Cómo jugar

1. Abrir la escena `Assets/Scenes/CowboyDuel.unity` (o el menú **Tareas → Duelo de Cowboys → Abrir escena**).
2. Darle Play, escribir un nombre y elegir:
   - **Practicar vs Bot**: usa el `OfflineMode` de PUN. El bot reacciona entre 80 y 650 ms y su mensaje tiene una latencia simulada de 20 a 350 ms, así que a veces dispara antes que vos pero su disparo llega después. Sirve para ver el tiempo de gracia en acción.
   - **Duelo Online**: sala de 2 jugadores. Para probarlo, hacer un build y abrirlo junto al Editor.
3. Disparar con **Espacio** o el botón cuando el cartel dice **¡AHORA!**.

Cada duelo muestra en el log, para cada disparo, cuándo se disparó y cuándo llegó al MasterClient (en ms, relativo al ganador) y la hora del reloj de ese dispositivo. Si el que llegó primero no es el que disparó primero, aparece *"¡El tiempo de gracia cambió el resultado!"*.

### Cómo está resuelto

| Tema | Dónde |
|---|---|
| Reloj con `DateTime.UtcNow` (no depende del huso horario) y ventana = segundos múltiplo de 3. Una ventana por bloque de 3 s, así que no se puede disparar dos veces en la misma | `CowboyDuelGame.IsWindowOpen` / `WindowId` |
| El disparo va solo al MasterClient (`RpcTarget.MasterClient`) con la hora del reloj del que dispara | `Shoot` → `RPC_Shoot` |
| El MasterClient valida la regla (la hora del disparo tiene que caer en ventana) y descarta disparos repetidos o tardíos | `ReceiveShot` |
| **Tiempo de gracia**: el primer disparo arranca una corrutina de `graceMilliseconds` (300 ms por defecto). Al terminar, gana el de menor `PhotonMessageInfo.SentServerTimestamp`, o sea el que **disparó** primero según el reloj sincronizado de Photon, no el que llegó primero | `GraceThenDecide` / `Decide` |
| El resultado se anuncia a todos por RPC; la ronda en curso vive en las Custom Room Properties y el puntaje en las Player Properties | `RPC_DuelResult`, `RoundKey`, `ScoreKey` |
| Si el MasterClient se va, el nuevo arranca otro duelo (los disparos pendientes vivían en el viejo Master y se pierden) | `OnMasterClientSwitched` |
| `GameVersion` propia (`duelo-1`) para que el matchmaking no mezcle salas con otras tareas | `gameVersion` |

Detalle de diseño: el reloj de cada dispositivo puede estar corrido, por eso la ventana usa el reloj local (como pide la consigna), pero para decidir quién fue primero se usa `SentServerTimestamp`, que es el mismo para todos los clientes.

### Archivos

```
Assets/CowboyDuel/
├── Scripts/
│   ├── CowboyDuelGame.cs           # Ventanas, RPCs, tiempo de gracia y bot
│   └── CowboyDuelUI.cs             # Vista (uGUI)
└── Editor/
    └── CowboyDuelSceneBuilder.cs   # Genera la escena desde código
Assets/Common/Editor/
└── TaskSceneKit.cs                 # Helpers compartidos para armar escenas
```
