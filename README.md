# ¿Quién es ese Pokémon? — Serialización & API

Tarea de **Juegos en Red**: una actividad donde el jugador adivina un Pokémon secreto usando la [PokeAPI](https://pokeapi.co/).
Con cada intento fallido se revela más información:

1. Generación
2. Tipos
3. Altura y Peso
4. Cantidad de letras del nombre
5. Imagen oscurecida (silueta)

Además es multijugador con **Photon PUN 2**: todos los jugadores de la sala compiten por adivinar el mismo Pokémon.

## Cómo jugar

1. Abrir el proyecto con **Unity 6.3 (6000.3.12f1)**.
2. Abrir la escena `Assets/Scenes/PokeGuess.unity` (o el menú **PokeGuess → Abrir escena**).
3. Play, escribir un nombre y elegir:
   - **Jugar Solo**: usa el `OfflineMode` de PUN, no necesita servidor.
   - **Jugar Online**: se conecta a Photon y entra a una sala (o la crea). Para probar con 2 jugadores, hacer un build y abrirlo junto al Editor.
4. Escribir el nombre del Pokémon y Enter (o **Adivinar**). Acepta el nombre en inglés o en español, sin importar mayúsculas, tildes o guiones.

Cada jugador tiene 6 intentos por ronda. El primero en acertar suma un punto y, a los 5 segundos, empieza una ronda nueva.

## Qué se aplicó de la materia

| Tema | Dónde |
|---|---|
| Deserialización JSON con **Newtonsoft** y `[JsonProperty]` para mapear la estructura de la API (root + objetos anidados como `official-artwork`) | `Assets/PokeGuess/Scripts/PokemonData.cs` |
| Consumo de API REST con `UnityWebRequest` en **corrutinas**, `using` para liberar el request y manejo de errores HTTP (404, 429, 5xx, sin conexión, JSON inválido) | `Assets/PokeGuess/Scripts/PokeApiClient.cs` |
| Descarga de textura y generación de la **silueta** pixel a pixel | `PokeApiClient.MakeSilhouette` |
| **Rooms**: `ConnectUsingSettings`, `JoinRandomOrCreateRoom`, `RoomOptions`, `NickName` | `Assets/PokeGuess/Scripts/PokeGuessGame.cs` |
| **Custom Room Properties**: el MasterClient guarda el Pokémon secreto y la ronda; sobrevive a un cambio de MasterClient y le llega a quien entra tarde | `PokeGuessGame.StartNewRound` / `OnRoomPropertiesUpdate` |
| **RPCs**: intentos fallidos a todos (`RpcTarget.All`), reclamo de victoria solo al MasterClient (`RpcTarget.MasterClient`), que decide quién llegó primero y lo anuncia | `RPC_WrongGuess`, `RPC_ClaimWin`, `RPC_RoundWon` |
| **Player Properties** para el puntaje | `ScoreKey` |
| `OnMasterClientSwitched`: si la ronda terminó y el Master se fue, el nuevo arranca la siguiente | `PokeGuessGame` |

## Estructura

```
Assets/PokeGuess/
├── Scripts/
│   ├── PokemonData.cs        # Modelos JSON de la PokeAPI
│   ├── PokeApiClient.cs      # Requests HTTP + silueta
│   ├── PokeGuessText.cs      # Normalización de nombres y traducciones
│   ├── PokeGuessGame.cs      # Lógica de red y rondas (PUN 2)
│   └── PokeGuessUI.cs        # Vista (uGUI)
└── Editor/
    └── PokeGuessSceneBuilder.cs  # Genera la escena desde código
```

## Dependencias

- Photon PUN 2 (v2.55), incluido en `Assets/Photon`
- `com.unity.nuget.newtonsoft-json` 3.2.1
- Input System (UI con `InputSystemUIInputModule`)
