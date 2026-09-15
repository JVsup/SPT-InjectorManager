using BepInEx.Configuration;

namespace InjectorManager;

internal static class PluginConfig
{
    private static ConfigFile _config;
    private static readonly Dictionary<string, ConfigEntry<bool>> Blacklist = new(StringComparer.Ordinal);
    private static readonly HashSet<string> Favorites = new(StringComparer.Ordinal);
    private static ConfigEntry<string> _favoriteTemplateIds;

    internal static ConfigEntry<KeyboardShortcut> OpenShortcut { get; private set; }
    internal static ConfigEntry<bool> ShowTaskbarButton { get; private set; }
    internal static ConfigEntry<bool> ShowDuration { get; private set; }
    internal static ConfigEntry<bool> ShowDelay { get; private set; }
    internal static ConfigEntry<bool> ShowChance { get; private set; }
    internal static ConfigEntry<int> EntriesPerPolarity { get; private set; }

    internal static void Bind(ConfigFile config)
    {
        _config = config;
        OpenShortcut = config.Bind(
            "General",
            "Open shortcut",
            KeyboardShortcut.Empty,
            new ConfigDescription("Open Injector Manager while an out-of-raid menu is active."));
        ShowTaskbarButton = config.Bind(
            "General",
            "Show taskbar button",
            true,
            new ConfigDescription("Show the INJECTORS button on the main-menu taskbar."));
        ShowDuration = config.Bind(
            "Display",
            "Show duration",
            true,
            new ConfigDescription("Show how long an injector effect lasts."));
        ShowDelay = config.Bind(
            "Display",
            "Show delayed start",
            true,
            new ConfigDescription("Show an effect's delayed start when it is greater than one second."));
        ShowChance = config.Bind(
            "Display",
            "Show chance",
            true,
            new ConfigDescription("Show the activation chance of random effects."));
        EntriesPerPolarity = config.Bind(
            "Display",
            "Entries per polarity and section",
            2,
            new ConfigDescription(
                "Maximum properties shown for each beneficial and harmful group in a section.",
                new AcceptableValueRange<int>(1, 10),
                new ConfigurationManagerAttributes
                {
                    DispName = "Property limit"
                }));
        _favoriteTemplateIds = config.Bind(
            "Internal",
            "Favorite template IDs",
            string.Empty,
            new ConfigDescription(
                "Injector template IDs marked as favorites.",
                null,
                new ConfigurationManagerAttributes
                {
                    Browsable = false
                }));

        Favorites.Clear();
        foreach (var templateId in _favoriteTemplateIds.Value.Split(','))
        {
            var trimmed = templateId.Trim();
            if (trimmed.Length > 0)
            {
                Favorites.Add(trimmed);
            }
        }
    }

    internal static ConfigEntry<bool> EnsureBlacklistEntry(string templateId, string displayName, int order)
    {
        if (Blacklist.TryGetValue(templateId, out var existing))
        {
            return existing;
        }

        var entry = _config.Bind(
            "Injector blacklist",
            templateId,
            false,
            new ConfigDescription(
                "Hide this injector from Injector Manager.",
                null,
                new ConfigurationManagerAttributes
                {
                    DispName = displayName,
                    Order = order
                }));
        Blacklist.Add(templateId, entry);
        return entry;
    }

    internal static bool IsBlacklisted(string templateId)
    {
        return Blacklist.TryGetValue(templateId, out var entry) && entry.Value;
    }

    internal static bool IsFavorite(string templateId)
    {
        return Favorites.Contains(templateId);
    }

    internal static void ToggleFavorite(string templateId)
    {
        if (!Favorites.Add(templateId))
        {
            Favorites.Remove(templateId);
        }

        _favoriteTemplateIds.Value = string.Join(",", Favorites.OrderBy(x => x, StringComparer.Ordinal));
    }
}
