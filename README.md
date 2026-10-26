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

Para abrir una tarea, abrí su escena y dale Play.

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
