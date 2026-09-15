using BepInEx.Configuration;
using EFT;
using EFT.InventoryLogic;
using EFT.UI.Ragfair;
using UnityEngine;

namespace InjectorManager;

internal enum BuffSection
{
    Effects,
    Regeneration,
    AttributesAndSkills
}

internal enum MarketStatus
{
    NotLoaded,
    Loading,
    Available,
    NotSellable,
    NoRoubleOffers,
    FleaLocked,
    Error
}

internal enum InjectorSort
{
    Name,
    LongestDuration,
    PositiveCount,
    NegativeCount
}

internal sealed class BuffInfo
{
    internal BuffInfo(
        string propertyName,
        string valueText,
        bool hasValue,
        string displayText,
        BuffSection section,
        bool positive,
        float strength,
        float duration,
        float delay,
        float chance)
    {
        PropertyName = propertyName;
        ValueText = valueText;
        HasValue = hasValue;
        DisplayText = displayText;
        Section = section;
        IsPositive = positive;
        Strength = strength;
        Duration = duration;
        Delay = delay;
        Chance = chance;
    }

    internal string PropertyName { get; }
    internal string ValueText { get; }
    internal bool HasValue { get; }
    internal string DisplayText { get; }
    internal BuffSection Section { get; }
    internal bool IsPositive { get; }
    internal float Strength { get; }
    internal float Duration { get; }
    internal float Delay { get; }
    internal float Chance { get; }
}

internal sealed class InjectorInfo
{
    internal string TemplateId { get; set; }
    internal ItemTemplate Template { get; set; }
    internal Item PreviewItem { get; set; }
    internal string Name { get; set; }
    internal IReadOnlyList<BuffInfo> Buffs { get; set; }
    internal ConfigEntry<bool> Blacklisted { get; set; }
    internal Sprite Icon { get; set; }
    internal int Owned { get; set; }
    internal MarketSnapshot Market { get; set; } = MarketSnapshot.NotLoaded();

    internal float LongestDuration => Buffs.Count == 0 ? 0f : Buffs.Max(x => x.Duration);
    internal int PositiveCount => Buffs.Count(x => x.IsPositive);
    internal int NegativeCount => Buffs.Count - PositiveCount;
}

internal sealed class MarketSnapshot
{
    private MarketSnapshot(MarketStatus status, IReadOnlyList<Offer> offers, string message)
    {
        Status = status;
        Offers = offers;
        Message = message;
    }

    internal MarketStatus Status { get; }
    internal IReadOnlyList<Offer> Offers { get; }
    internal string Message { get; }
    internal long Stock => Offers.Sum(x => (long)MarketService.PurchasableCount(x));
    internal int LowestUnitPrice => Offers.Count == 0 ? 0 : Offers.Min(MarketService.UnitPrice);

    internal static MarketSnapshot NotLoaded() => new(MarketStatus.NotLoaded, Array.Empty<Offer>(), "Price not loaded");
    internal static MarketSnapshot Loading() => new(MarketStatus.Loading, Array.Empty<Offer>(), "Loading price…");
    internal static MarketSnapshot Available(IReadOnlyList<Offer> offers) => new(MarketStatus.Available, offers, string.Empty);
    internal static MarketSnapshot NotSellable() => new(MarketStatus.NotSellable, Array.Empty<Offer>(), "Not available on flea");
    internal static MarketSnapshot NoOffers() => new(MarketStatus.NoRoubleOffers, Array.Empty<Offer>(), "No rouble offers");
    internal static MarketSnapshot FleaLocked() => new(MarketStatus.FleaLocked, Array.Empty<Offer>(), "Flea market locked");
    internal static MarketSnapshot Error(string message) => new(MarketStatus.Error, Array.Empty<Offer>(), message);
}

internal sealed class PurchasePlan
{
    internal PurchasePlan(Dictionary<IExchangeable, int> goods, int requested, int quantity, long totalPrice)
    {
        Goods = goods;
        Requested = requested;
        Quantity = quantity;
        TotalPrice = totalPrice;
    }

    internal Dictionary<IExchangeable, int> Goods { get; }
    internal int Requested { get; }
    internal int Quantity { get; }
    internal long TotalPrice { get; }
    internal bool IsShort => Quantity < Requested;
}
