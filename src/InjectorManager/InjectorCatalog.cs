using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using JsonType;
using UnityEngine;
using BuffSettings = EFT.HealthSystem.EffectsSettings.StimulatorSettings.StimulatorBuffSettings;

namespace InjectorManager;

internal sealed class InjectorCatalog
{
    private readonly List<InjectorInfo> _items = new();

    internal IReadOnlyList<InjectorInfo> Items => _items;
    internal bool IsLoaded { get; private set; }

    internal void Load(Func<string, bool> isInjectorHandbookItem)
    {
        var factory = Singleton<ItemFactory>.Instance;
        var knownTemplateIds = _items
            .Select(x => x.TemplateId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var pair in factory.ItemTemplates.OrderBy(x => x.Key.ToString(), StringComparer.Ordinal))
        {
            var template = pair.Value;
            var templateId = pair.Key.ToString();
            if (knownTemplateIds.Contains(templateId) ||
                template._type != NodeType.Item ||
                (template is not StimulatorTemplate && !isInjectorHandbookItem(templateId)))
            {
                continue;
            }

            var item = factory.CreateItem(factory.NextId.ToString(), templateId, null);
            var name = ResolveItemName(templateId, template, item);
            var buffs = ReadBuffs(item);
            _items.Add(new InjectorInfo
            {
                TemplateId = templateId,
                Template = template,
                PreviewItem = item,
                Name = name,
                Buffs = buffs
            });
            knownTemplateIds.Add(templateId);
        }

        _items.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase));
        for (var index = 0; index < _items.Count; index++)
        {
            var injector = _items[index];
            injector.Blacklisted ??= PluginConfig.EnsureBlacklistEntry(
                injector.TemplateId,
                injector.Name,
                _items.Count - index);
        }

        IsLoaded = true;
    }

    internal async Task LoadIconsAsync()
    {
        if (!IsLoaded)
        {
            return;
        }

        foreach (var injector in _items.Where(x => x.Icon == null && x.PreviewItem != null))
        {
            try
            {
                injector.Icon = await ItemViewFactory.GetItemSpriteAsync(injector.PreviewItem, 1);
            }
            catch (Exception exception)
            {
                Plugin.Log.LogWarning($"Could not load icon for {injector.TemplateId}: {exception.Message}");
            }
        }
    }

    internal void RefreshOwned(IEftSession session)
    {
        if (!IsLoaded || session?.Profile?.Inventory == null)
        {
            return;
        }

        var counts = session.Profile.Inventory
            .GetPlayerItems(EPlayerItems.NonQuestItemsExceptHideoutStashes)
            .GroupBy(x => x.StringTemplateId, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Sum(item => item.StackObjectsCount), StringComparer.Ordinal);

        foreach (var injector in _items)
        {
            injector.Owned = counts.TryGetValue(injector.TemplateId, out var count) ? count : 0;
        }
    }

    private static string ResolveItemName(string templateId, ItemTemplate template, Item item)
    {
        try
        {
            var localized = item?.LocalizedName();
            if (!string.IsNullOrWhiteSpace(localized) && !string.Equals(localized, templateId, StringComparison.Ordinal))
            {
                return localized;
            }
        }
        catch
        {
            // Fall through to data shipped with the item template.
        }

        return string.IsNullOrWhiteSpace(template.Name) ? templateId : template.Name;
    }

    private static IReadOnlyList<BuffInfo> ReadBuffs(Item item)
    {
        if (item == null)
        {
            return Array.Empty<BuffInfo>();
        }

        var result = new List<BuffInfo>();
        var stimulatorComponent = item.GetItemComponent<StimulatorBuffsComponent>();
        if (stimulatorComponent?.BuffSettings != null)
        {
            foreach (var settings in stimulatorComponent.BuffSettings)
            {
                result.Add(CreateBuff(settings));
            }
        }

        var healthComponent = item.GetItemComponent<HealthEffectsComponent>();
        if (healthComponent?.HealthEffects != null)
        {
            foreach (var effect in healthComponent.HealthEffects)
            {
                result.Add(CreateHealthEffect(item, effect.Key, effect.Value));
            }
        }

        if (healthComponent?.DamageEffects != null)
        {
            foreach (var effect in healthComponent.DamageEffects)
            {
                result.Add(CreateDamageEffect(item, effect.Key, effect.Value));
            }
        }

        return result;
    }

    private static BuffInfo CreateBuff(BuffSettings settings)
    {
        string propertyName;
        string valueText;
        string displayText;
        float visualValue;
        try
        {
            propertyName = settings.BuffName.Localized();
            visualValue = StimulatorHelper.GetVisualBuffValue(settings);
            valueText = Math.Abs(visualValue) < float.Epsilon
                ? "—"
                : StimulatorHelper.BuffColoredStringValue(settings);
            displayText = StimulatorHelper.BuffName(settings);
        }
        catch
        {
            propertyName = HumanizeName(settings.BuffType == EStimulatorBuffType.SkillRate
                ? settings.SkillName
                : settings.BuffType.ToString());
            visualValue = settings.Value;
            valueText = Math.Abs(settings.Value) < float.Epsilon
                ? "—"
                : settings.Value.ToString("+0.##;-0.##;0", System.Globalization.CultureInfo.InvariantCulture);
            displayText = valueText == "—" ? propertyName : $"{propertyName} ({valueText})";
        }

        bool positive;
        try
        {
            positive = StimulatorHelper.IsBuff(settings.BuffType, settings.Value);
        }
        catch (ArgumentOutOfRangeException)
        {
            positive = settings.Value >= 0f;
        }

        propertyName = ToSentenceCase(propertyName);
        return new BuffInfo(
            propertyName,
            valueText,
            Math.Abs(visualValue) >= float.Epsilon,
            displayText,
            Classify(settings.BuffType),
            positive,
            Math.Abs(visualValue),
            settings.Duration,
            settings.Delay,
            settings.Chance);
    }

    private static BuffInfo CreateHealthEffect(
        Item item,
        EHealthFactorType type,
        HealthEffectSpecification settings)
    {
        var propertyName = ToSentenceCase(ResolvePropertyName(item, type));
        var positive = settings.Value >= 0f;
        var plainValue = FormatSignedValue(settings.Value);
        var valueText = plainValue == "—"
            ? plainValue
            : $"<color={(positive ? "#48bce7" : "#e44343")}>{plainValue}</color>";
        var displayText = plainValue == "—" ? propertyName : $"{propertyName} ({plainValue})";

        return new BuffInfo(
            propertyName,
            valueText,
            plainValue != "—",
            displayText,
            Classify(type),
            positive,
            Math.Abs(settings.Value),
            settings.Duration,
            settings.Delay,
            1f);
    }

    private static BuffInfo CreateDamageEffect(
        Item item,
        EDamageEffectType type,
        DamageEffectSpecification settings)
    {
        var propertyName = ToSentenceCase(ResolvePropertyName(item, type));
        return new BuffInfo(
            propertyName,
            "—",
            false,
            propertyName,
            BuffSection.Effects,
            true,
            1f,
            settings.Duration,
            settings.Delay,
            1f);
    }

    private static string ResolvePropertyName(Item item, Enum type)
    {
        if (type is EDamageEffectType.DestroyedPart)
        {
            return "Restores destroyed body part";
        }

        var attributeName = item.Attributes?
            .FirstOrDefault(x => Equals(x.Id, type))?
            .DisplayName;
        if (!string.IsNullOrWhiteSpace(attributeName))
        {
            return attributeName;
        }

        var rawName = type.ToString();
        var localizedName = rawName.Localized();
        return !string.IsNullOrWhiteSpace(localizedName) &&
               !string.Equals(localizedName, rawName, StringComparison.Ordinal)
            ? localizedName
            : HumanizeName(rawName);
    }

    private static string HumanizeName(string name)
    {
        var result = new System.Text.StringBuilder(name.Length + 4);
        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];
            if (character == '_')
            {
                result.Append(' ');
                continue;
            }

            if (index > 0 && char.IsUpper(character) && char.IsLower(name[index - 1]))
            {
                result.Append(' ');
            }

            result.Append(character);
        }

        return result.ToString();
    }

    private static string FormatSignedValue(float value)
    {
        return Math.Abs(value) < float.Epsilon
            ? "—"
            : value.ToString("+0.##;-0.##;0", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string ToSentenceCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var normalized = value.Trim().ToLower(System.Globalization.CultureInfo.CurrentCulture);
        return char.ToUpper(normalized[0], System.Globalization.CultureInfo.CurrentCulture) +
               normalized.Substring(1);
    }

    private static BuffSection Classify(EStimulatorBuffType type)
    {
        return type switch
        {
            EStimulatorBuffType.HealthRate or
            EStimulatorBuffType.EnergyRate or
            EStimulatorBuffType.HydrationRate or
            EStimulatorBuffType.StaminaRate => BuffSection.Regeneration,

            EStimulatorBuffType.SkillRate or
            EStimulatorBuffType.MaxStamina or
            EStimulatorBuffType.DamageModifier or
            EStimulatorBuffType.WeightLimit => BuffSection.AttributesAndSkills,

            _ => BuffSection.Effects
        };
    }

    private static BuffSection Classify(EHealthFactorType type)
    {
        return type is EHealthFactorType.Health or EHealthFactorType.Hydration or EHealthFactorType.Energy
            ? BuffSection.Regeneration
            : BuffSection.Effects;
    }
}
