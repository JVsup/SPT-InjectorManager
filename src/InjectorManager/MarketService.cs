using EFT;
using EFT.HandBook;
using EFT.InventoryLogic;
using EFT.UI.Ragfair;
using HarmonyLib;

namespace InjectorManager;

internal sealed class MarketService
{
    private const string InjectorHandbookCategory = "5b47574386f77428ca22b33a";
    private const int OfferLimit = 10000;
    private IEftSession _session;
    private Handbook _handbook;

    internal bool IsLoading { get; private set; }
    internal string LastError { get; private set; } = string.Empty;

    internal void SetSession(IEftSession session)
    {
        if (!ReferenceEquals(_session, session))
        {
            _session = session;
            LastError = string.Empty;
        }

        ResolveHandbook();
    }

    internal async Task RefreshAllAsync(IReadOnlyList<InjectorInfo> injectors)
    {
        if (IsLoading || _session == null)
        {
            return;
        }

        if (!_session.RagFair.Available)
        {
            foreach (var injector in injectors)
            {
                injector.Market = injector.Template.CanSellOnRagfair
                    ? MarketSnapshot.FleaLocked()
                    : MarketSnapshot.NotSellable();
            }

            return;
        }

        IsLoading = true;
        LastError = string.Empty;
        ResolveHandbook();
        foreach (var injector in injectors)
        {
            injector.Market = injector.Template.CanSellOnRagfair
                ? MarketSnapshot.Loading()
                : MarketSnapshot.NotSellable();
        }

        try
        {
            var categoryOffers = await QueryAsync(InjectorHandbookCategory);
            var grouped = GroupOffers(categoryOffers);

            foreach (var injector in injectors.Where(x => x.Template.CanSellOnRagfair))
            {
                if (grouped.TryGetValue(injector.TemplateId, out var offers))
                {
                    injector.Market = MarketSnapshot.Available(offers);
                }
                else
                {
                    injector.Market = MarketSnapshot.NoOffers();
                }
            }

            if (_handbook == null)
            {
                Plugin.Log.LogWarning("The flea handbook is not ready; outside-category injector queries were skipped.");
            }

            foreach (var injector in injectors.Where(x =>
                         _handbook != null &&
                         x.Template.CanSellOnRagfair &&
                         !IsInjectorHandbookItem(x.TemplateId)))
            {
                var offers = EligibleOffers(await QueryAsync(injector.TemplateId))
                    .Where(x => string.Equals(x.Item.StringTemplateId, injector.TemplateId, StringComparison.Ordinal))
                    .OrderBy(UnitPrice)
                    .ToArray();
                injector.Market = offers.Length == 0
                    ? MarketSnapshot.NoOffers()
                    : MarketSnapshot.Available(offers);
            }
        }
        catch (Exception exception)
        {
            LastError = exception.Message;
            Plugin.Log.LogError($"Flea refresh failed: {exception}");
            foreach (var injector in injectors.Where(x => x.Market.Status == MarketStatus.Loading))
            {
                injector.Market = MarketSnapshot.Error("Flea request failed");
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    internal async Task RefreshTemplateAsync(InjectorInfo injector)
    {
        if (_session == null || !injector.Template.CanSellOnRagfair)
        {
            return;
        }

        if (!_session.RagFair.Available)
        {
            injector.Market = MarketSnapshot.FleaLocked();
            return;
        }

        injector.Market = MarketSnapshot.Loading();
        try
        {
            var offers = EligibleOffers(await QueryAsync(injector.TemplateId))
                .Where(x => string.Equals(x.Item.StringTemplateId, injector.TemplateId, StringComparison.Ordinal))
                .OrderBy(UnitPrice)
                .ToArray();
            injector.Market = offers.Length == 0
                ? MarketSnapshot.NoOffers()
                : MarketSnapshot.Available(offers);
        }
        catch (Exception exception)
        {
            Plugin.Log.LogError($"Flea refresh failed for {injector.TemplateId}: {exception}");
            injector.Market = MarketSnapshot.Error("Flea request failed");
        }
    }

    internal static PurchasePlan BuildPurchasePlan(MarketSnapshot snapshot, int requested)
    {
        var goods = new Dictionary<IExchangeable, int>();
        var remaining = Math.Max(0, requested);
        var purchased = 0;
        var total = 0L;

        foreach (var offer in snapshot.Offers.OrderBy(UnitPrice))
        {
            if (remaining == 0)
            {
                break;
            }

            var stock = PurchasableCount(offer);
            if (stock <= 0)
            {
                continue;
            }

            if (offer.SellInOnePiece)
            {
                if (stock > remaining)
                {
                    continue;
                }

                goods.Add(offer, stock);
                purchased += stock;
                remaining -= stock;
                total += RequirementPrice(offer);
                continue;
            }

            var count = Math.Min(stock, remaining);
            goods.Add(offer, count);
            purchased += count;
            remaining -= count;
            total += RequirementPrice(offer) * count;
        }

        return new PurchasePlan(goods, requested, purchased, total);
    }

    internal static int PurchasableCount(Offer offer)
    {
        return Math.Max(0, offer.CurrentItemCount);
    }

    internal static int UnitPrice(Offer offer)
    {
        var price = RequirementPrice(offer);
        return offer.SellInOnePiece
            ? (int)Math.Ceiling(price / (double)Math.Max(1, PurchasableCount(offer)))
            : price;
    }

    private async Task<Offer[]> QueryAsync(string handbookId)
    {
        var queryType = string.Equals(handbookId, InjectorHandbookCategory, StringComparison.Ordinal)
            ? "category"
            : "template";
        var result = await _session.GetOffers(
            0,
            OfferLimit,
            (int)ESortType.Price,
            true,
            (int)ECurrencyType.RUB,
            0,
            0,
            0,
            0,
            0,
            100,
            false,
            true,
            0,
            true,
            handbookId,
            string.Empty,
            string.Empty,
            new Dictionary<string, int>(),
            0,
            false);

        if (result.Failed)
        {
            Plugin.Log.LogWarning(
                $"Flea {queryType} query {handbookId} failed with error {result.ErrorCode}: {result.Error}");
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Error)
                ? $"Flea request failed with error {result.ErrorCode}."
                : result.Error);
        }

        return result.Value?.offers ?? Array.Empty<Offer>();
    }

    private static Dictionary<string, IReadOnlyList<Offer>> GroupOffers(IEnumerable<Offer> offers)
    {
        return EligibleOffers(offers)
            .GroupBy(x => x.Item.StringTemplateId, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<Offer>)x.OrderBy(UnitPrice).ToArray(),
                StringComparer.Ordinal);
    }

    private static IEnumerable<Offer> EligibleOffers(IEnumerable<Offer> offers)
    {
        return offers.Where(x =>
            x != null &&
            x.Item != null &&
            x.OnlyMoney &&
            x.MoneyType == ECurrencyType.RUB &&
            x.CanBeBought &&
            !x.Expired &&
            PurchasableCount(x) > 0 &&
            x.Requirements is { Length: > 0 });
    }

    internal bool IsInjectorHandbookItem(string templateId)
    {
        if (_handbook == null || !_handbook.AllNodes.TryGetValue(templateId, out var node))
        {
            return false;
        }

        while (node != null)
        {
            if (string.Equals(node.Id, InjectorHandbookCategory, StringComparison.Ordinal))
            {
                return true;
            }

            node = node.Parent;
        }

        return false;
    }

    private void ResolveHandbook()
    {
        _handbook = _session?.RagFair == null
            ? null
            : AccessTools.Field(typeof(RagFair), "_handbook")?.GetValue(_session.RagFair) as Handbook;
    }

    private static int RequirementPrice(Offer offer)
    {
        return offer.Requirements.Length == 0 ? 0 : Math.Max(0, offer.Requirements[0].IntCount);
    }
}
