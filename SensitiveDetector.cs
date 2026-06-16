using System;
using System.Text.RegularExpressions;

namespace ClipFlow;

public static class SensitiveDetector
{
    // Common patterns for sensitive data
    private static readonly Regex CreditCardPattern =
        new(@"^\d{4}[\s-]?\d{4}[\s-]?\d{4}[\s-]?\d{4}$", RegexOptions.Compiled);

    private static readonly Regex ApiKeyPattern =
        new(@"^(sk_|pk_|api[_-]?key|bearer\s+|ghp_|ghs_|github_pat_)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex JwtPattern =
        new(@"^eyJ[A-Za-z0-9_-]+\.eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$", RegexOptions.Compiled);

    private static readonly Regex Base64KeyPattern =
        new(@"^[A-Za-z0-9+/]{40,}={0,2}$", RegexOptions.Compiled);

    public enum SensitiveType
    {
        None,
        CreditCard,
        ApiKey,
        JsonWebToken,
        LikelyPassword,
        Base64Key
    }

    // Check if text appears to be sensitive
    public static SensitiveType Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return SensitiveType.None;

        string trimmed = text.Trim();

        // Credit card numbers
        if (CreditCardPattern.IsMatch(trimmed))
            return SensitiveType.CreditCard;

        // JSON Web Tokens
        if (JwtPattern.IsMatch(trimmed))
            return SensitiveType.JsonWebToken;

        // API keys (common prefixes)
        if (trimmed.Length >= 20 && ApiKeyPattern.IsMatch(trimmed))
            return SensitiveType.ApiKey;

        // Likely password — short, no spaces, mixed case, has numbers/symbols
        if (LooksLikePassword(trimmed))
            return SensitiveType.LikelyPassword;

        // Long Base64 strings often = secrets
        if (trimmed.Length >= 40 && trimmed.Length <= 100 &&
            !trimmed.Contains(' ') && Base64KeyPattern.IsMatch(trimmed))
            return SensitiveType.Base64Key;

        return SensitiveType.None;
    }

    private static bool LooksLikePassword(string text)
    {
        // Heuristic: 8-30 chars, no spaces, mix of types
        if (text.Length < 8 || text.Length > 30) return false;
        if (text.Contains(' ')) return false;
        if (text.Contains('\n')) return false;

        bool hasUpper   = false;
        bool hasLower   = false;
        bool hasDigit   = false;
        bool hasSpecial = false;

        foreach (char c in text)
        {
            if (char.IsUpper(c)) hasUpper = true;
            else if (char.IsLower(c)) hasLower = true;
            else if (char.IsDigit(c)) hasDigit = true;
            else if (!char.IsLetterOrDigit(c)) hasSpecial = true;
        }

        // Needs at least 3 character types — looks like a password
        int types = (hasUpper ? 1 : 0) + (hasLower ? 1 : 0) +
                    (hasDigit ? 1 : 0) + (hasSpecial ? 1 : 0);

        return types >= 3;
    }

    public static string GetLabel(SensitiveType type) => type switch
    {
        SensitiveType.CreditCard     => "Credit card number",
        SensitiveType.ApiKey         => "API key",
        SensitiveType.JsonWebToken   => "JSON Web Token",
        SensitiveType.LikelyPassword => "Possible password",
        SensitiveType.Base64Key      => "Encoded secret",
        _                            => ""
    };
}