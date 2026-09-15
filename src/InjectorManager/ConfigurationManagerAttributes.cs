using BepInEx.Configuration;

namespace InjectorManager;

// BepInEx Configuration Manager reads these optional tag members by name.
#pragma warning disable 0169, 0414, 0649
internal sealed class ConfigurationManagerAttributes
{
    public bool? Browsable;
    public string Category;
    public string DispName;
    public int? Order;
}
#pragma warning restore 0169, 0414, 0649
