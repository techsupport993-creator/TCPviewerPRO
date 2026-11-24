using System.Text.Json;

namespace TCPViewerPro;

/// <summary>
/// Persistence schema and deterministic mode rules. Keeping these separate
/// from Windows Forms allows headless regression tests.
/// </summary>
internal sealed class AppModePreferences
{
    public string Mode { get; set; } = AppModeProfile.Standard;
}

internal static class AppModeProfile
{
    internal const string Standard = "Standard";
    internal const string Advanced = "Advanced";

    internal static string Normalize(string? value) =>
        string.Equals(value, Advanced, StringComparison.OrdinalIgnoreCase)
            ? Advanced : Standard;

    internal static bool IsAdvanced(string? value) => Normalize(value) == Advanced;

    internal static AppModePreferences FromJson(string? text)
    {
        try
        {
            return new AppModePreferences
            {
                Mode = Normalize(text is null ? null :
                    JsonSerializer.Deserialize<AppModePreferences>(text)?.Mode)
            };
        }
        catch (JsonException)
        {
            return new AppModePreferences();
        }
    }
}
