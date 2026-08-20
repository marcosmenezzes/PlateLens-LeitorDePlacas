namespace PlateLens.Domain.Rules;

/// <summary>Valida e normaliza placas brasileiras antigas e Mercosul.</summary>
public static class PlateNumberRule
{
    /// <summary>Remove separadores, converte para maiúsculas e valida o padrão de sete caracteres.</summary>
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = (value ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", string.Empty).Replace("-", string.Empty);
        if (normalized.Length != 7 || normalized.Any(character => character is not (>= 'A' and <= 'Z') and not (>= '0' and <= '9'))) return false;
        return IsLetter(normalized[0]) && IsLetter(normalized[1]) && IsLetter(normalized[2]) && IsDigit(normalized[3]) &&
               ((IsDigit(normalized[4]) && IsDigit(normalized[5]) && IsDigit(normalized[6])) ||
                (IsLetter(normalized[4]) && IsDigit(normalized[5]) && IsDigit(normalized[6])));
    }

    private static bool IsLetter(char value) => value is >= 'A' and <= 'Z';
    private static bool IsDigit(char value) => value is >= '0' and <= '9';
}
