using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

// Todo lo que el juego necesita saber del Pokémon secreto, ya armado a partir de la API.
public class SecretPokemon
{
    public int Id;
    public string Name;          // nombre "oficial" de la API (ej: "mr-mime")
    public string SpanishName;   // nombre en español si existe
    public string Generation;    // "I", "II", ...
    public List<string> Types = new List<string>();
    public float HeightMeters;
    public float WeightKg;
    public Texture2D Artwork;
    public Texture2D Silhouette;

    public int LetterCount => PokeGuessText.Normalize(Name).Length;
}

// Consume la PokeAPI de forma asincrónica (corrutinas + UnityWebRequest) para no frenar el juego.
public class PokeApiClient : MonoBehaviour
{
    private const string BaseUrl = "https://pokeapi.co/api/v2/";

    [SerializeField] private int timeoutSeconds = 10;

    public IEnumerator FetchPokemon(int id, Action<SecretPokemon> onSuccess, Action<string> onError)
    {
        PokemonData pokemon = null;
        PokemonSpeciesData species = null;
        string error = null;

        // 1. Datos base: tipos, altura, peso, sprites
        yield return GetJson<PokemonData>(BaseUrl + "pokemon/" + id, data => pokemon = data, e => error = e);
        if (error != null) { onError?.Invoke(error); yield break; }

        // 2. Especie: generación y nombres traducidos
        yield return GetJson<PokemonSpeciesData>(BaseUrl + "pokemon-species/" + id, data => species = data, e => error = e);
        if (error != null) { onError?.Invoke(error); yield break; }

        SecretPokemon secret = BuildSecret(pokemon, species);

        // 3. Imagen: preferimos el artwork oficial, si no el sprite chico
        string imageUrl = pokemon.Sprites?.Other?.OfficialArtwork?.FrontDefault ?? pokemon.Sprites?.FrontDefault;
        if (!string.IsNullOrEmpty(imageUrl))
        {
            yield return GetTexture(imageUrl, tex => secret.Artwork = tex, e => Debug.LogWarning("Sin imagen: " + e));
            if (secret.Artwork != null)
                secret.Silhouette = MakeSilhouette(secret.Artwork);
        }

        onSuccess?.Invoke(secret);
    }

    private IEnumerator GetJson<T>(string url, Action<T> onSuccess, Action<string> onError)
    {
        // "using" asegura que el WebRequest se descarte al terminar, liberando memoria y recursos de red.
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = timeoutSeconds;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(DescribeError(request));
                yield break;
            }

            try
            {
                T data = JsonConvert.DeserializeObject<T>(request.downloadHandler.text);
                onSuccess?.Invoke(data);
            }
            catch (JsonException e)
            {
                onError?.Invoke("JSON inválido: " + e.Message);
            }
        }
    }

    private IEnumerator GetTexture(string url, Action<Texture2D> onSuccess, Action<string> onError)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url, false))
        {
            request.timeout = timeoutSeconds;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(DescribeError(request));
                yield break;
            }

            onSuccess?.Invoke(DownloadHandlerTexture.GetContent(request));
        }
    }

    private static string DescribeError(UnityWebRequest request)
    {
        switch (request.result)
        {
            case UnityWebRequest.Result.ConnectionError:
                return "Sin conexión con la API (" + request.error + ")";
            case UnityWebRequest.Result.ProtocolError:
                switch (request.responseCode)
                {
                    case 404: return "404: Pokémon no encontrado";
                    case 429: return "429: Demasiadas consultas, esperá un momento";
                    case 500:
                    case 503: return request.responseCode + ": La API no responde, probá de nuevo";
                    default: return "HTTP " + request.responseCode + ": " + request.error;
                }
            case UnityWebRequest.Result.DataProcessingError:
                return "Error procesando la respuesta: " + request.error;
            default:
                return request.error;
        }
    }

    private static SecretPokemon BuildSecret(PokemonData pokemon, PokemonSpeciesData species)
    {
        var secret = new SecretPokemon
        {
            Id = pokemon.Id,
            Name = species?.Name ?? pokemon.Name,
            HeightMeters = pokemon.Height / 10f,
            WeightKg = pokemon.Weight / 10f,
            Generation = PokeGuessText.GenerationNumber(species?.Generation?.Name)
        };

        if (pokemon.Types != null)
        {
            pokemon.Types.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            foreach (PokemonTypeSlot slot in pokemon.Types)
                secret.Types.Add(PokeGuessText.TypeToSpanish(slot.Type.Name));
        }

        if (species?.Names != null)
        {
            foreach (LocalizedName localized in species.Names)
            {
                if (localized.Language?.Name == "es")
                {
                    secret.SpanishName = localized.Name;
                    break;
                }
            }
        }

        return secret;
    }

    // Copia la imagen dejando cada píxel negro pero respetando su transparencia.
    private static Texture2D MakeSilhouette(Texture2D source)
    {
        Color32[] pixels = source.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(0, 0, 0, pixels[i].a);

        var silhouette = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        silhouette.SetPixels32(pixels);
        silhouette.Apply();
        return silhouette;
    }
}
