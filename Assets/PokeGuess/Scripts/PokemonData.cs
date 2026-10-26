using System.Collections.Generic;
using Newtonsoft.Json;

// Estructuras que replican el JSON de la PokeAPI (https://pokeapi.co/docs/v2).
// Usamos [JsonProperty] para que la etiqueta del JSON sea la que tenga que coincidir,
// y no el nombre de nuestro campo.

// Root de GET https://pokeapi.co/api/v2/pokemon/{id}
public class PokemonData
{
    [JsonProperty("id")] public int Id;
    [JsonProperty("name")] public string Name;
    [JsonProperty("height")] public int Height;   // en decímetros
    [JsonProperty("weight")] public int Weight;   // en hectogramos
    [JsonProperty("types")] public List<PokemonTypeSlot> Types;
    [JsonProperty("sprites")] public PokemonSprites Sprites;
    [JsonProperty("species")] public NamedResource Species;
}

public class PokemonTypeSlot
{
    [JsonProperty("slot")] public int Slot;
    [JsonProperty("type")] public NamedResource Type;
}

public class PokemonSprites
{
    [JsonProperty("front_default")] public string FrontDefault;
    [JsonProperty("other")] public OtherSprites Other;
}

public class OtherSprites
{
    [JsonProperty("official-artwork")] public SpriteSet OfficialArtwork;
}

public class SpriteSet
{
    [JsonProperty("front_default")] public string FrontDefault;
}

// Root de GET https://pokeapi.co/api/v2/pokemon-species/{id}
public class PokemonSpeciesData
{
    [JsonProperty("name")] public string Name;
    [JsonProperty("generation")] public NamedResource Generation;
    [JsonProperty("names")] public List<LocalizedName> Names;
}

public class LocalizedName
{
    [JsonProperty("name")] public string Name;
    [JsonProperty("language")] public NamedResource Language;
}

// Objeto anidado que la API repite en todos lados: { "name": "...", "url": "..." }
public class NamedResource
{
    [JsonProperty("name")] public string Name;
    [JsonProperty("url")] public string Url;
}
