namespace ContextKey.Core;

public static class SecureField
{
    public static bool IsSecure(string? role, string? subrole, string? description = null)
    {
        if (LooksSecure(role) || LooksSecure(subrole))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        return description.Contains("password", StringComparison.OrdinalIgnoreCase) ||
               description.Contains("secure text", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksSecure(string? value) =>
        !string.IsNullOrEmpty(value) &&
        (value.Equals("AXSecureTextField", StringComparison.OrdinalIgnoreCase) ||
         value.Equals("AXSecureTextArea", StringComparison.OrdinalIgnoreCase) ||
         value.Contains("SecureText", StringComparison.OrdinalIgnoreCase));
}
