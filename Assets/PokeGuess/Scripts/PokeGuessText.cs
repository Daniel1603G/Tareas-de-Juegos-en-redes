using System.Collections.Generic;
using System.Globalization;
using System.Text;

// Helpers de texto: normalizar intentos y traducir datos de la API.
public static class PokeGuessText
{
    private static readonly Dictionary<string, string> TypeNames = new Dictionary<string, string>
    {
        { "normal", "Normal" }, { "fire", "Fuego" }, { "water", "Agua" }, { "grass", "Planta" },
        { "electric", "Eléctrico" }, { "ice", "Hielo" }, { "fighting", "Lucha" }, { "poison", "Veneno" },
        { "ground", "Tierra" }, { "flying", "Volador" }, { "psychic", "Psíquico" }, { "bug", "Bicho" },
        { "rock", "Roca" }, { "ghost", "Fantasma" }, { "dragon", "Dragón" }, { "dark", "Siniestro" },
        { "steel", "Acero" }, { "fairy", "Hada" }, { "stellar", "Astral" }
    };

    public static string TypeToSpanish(string apiType)
    {
        return apiType != null && TypeNames.TryGetValue(apiType, out string name) ? name : apiType;
    }

    // "generation-iv" -> "IV"
    public static string GenerationNumber(string apiGeneration)
    {
        if (string.IsNullOrEmpty(apiGeneration)) return "?";
        int dash = apiGeneration.LastIndexOf('-');
        return dash >= 0 ? apiGeneration.Substring(dash + 1).ToUpperInvariant() : apiGeneration;
    }

    // Deja solo letras y números en minúscula y sin tildes: "Mr. Mime" == "mr-mime" == "mrmime"
    public static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        string decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) builder.Append(c);
        }
        return builder.ToString();
    }

    public static bool IsCorrectGuess(string guess, SecretPokemon secret)
    {
        string normalized = Normalize(guess);
        if (normalized.Length == 0 || secret == null) return false;
        return normalized == Normalize(secret.Name) || normalized == Normalize(secret.SpanishName);
    }

    // "pikachu" -> "_ _ _ _ _ _ _  (7 letras)"
    public static string LetterHint(SecretPokemon secret)
    {
        int count = secret.LetterCount;
        var builder = new StringBuilder();
        for (int i = 0; i < count; i++)
            builder.Append(i == 0 ? "_" : " _");
        builder.Append("  (").Append(count).Append(" letras)");
        return builder.ToString();
    }

    public static string DisplayName(SecretPokemon secret)
    {
        string name = string.IsNullOrEmpty(secret.SpanishName) ? secret.Name : secret.SpanishName;
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name);
    }
}
