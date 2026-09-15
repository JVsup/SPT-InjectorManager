using System.Globalization;
using BepInEx;
using BepInEx.Logging;
using EFT;
using SPT.Reflection.Utils;
using UnityEngine;

namespace InjectorManager;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    internal const string PluginGuid = "com.jvsup.injectormanager";
    internal const string PluginName = "Injector Manager";
    internal const string PluginVersion = "4.1.0";

    internal static ManualLogSource Log { get; private set; }

    private readonly InjectorCatalog _catalog = new();
    private readonly MarketService _market = new();
    private readonly InjectorManagerWindow _window = new();
    private readonly PurchaseDialogService _purchaseDialog = new();
    private IEftSession _session;
    private bool _visible;
    private bool _purchaseBusy;
    private float _nextMenuCheck;
    private float _nextOwnedRefresh;

    private void Awake()
    {
        Log = Logger;
        PluginConfig.Bind(Config);
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded for SPT 4.1.5.");
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextMenuCheck)
        {
            _nextMenuCheck = Time.unscaledTime + 0.5f;
            MenuButton.TryInject(ToggleWindow);
            MenuButton.SetButtonVisible(PluginConfig.ShowTaskbarButton.Value);
            if (_visible && !MenuButton.IsMenuActive)
            {
                CloseWindow();
            }
        }

        if (MenuButton.IsMenuActive && PluginConfig.OpenShortcut.Value.IsDown())
        {
            ToggleWindow();
        }

        if (_visible && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseWindow();
        }

        if (_visible && _session != null && Time.unscaledTime >= _nextOwnedRefresh)
        {
            _nextOwnedRefresh = Time.unscaledTime + 1f;
            _catalog.RefreshOwned(_session);
        }
    }

    private void OnGUI()
    {
        if (!_visible)
        {
            return;
        }

        _window.Draw(
            _catalog.Items,
            _market,
            _purchaseBusy,
            CloseWindow,
            RefreshMarket,
            BeginPurchase);
    }

    private void ToggleWindow()
    {
        if (_visible)
        {
            CloseWindow();
            return;
        }

        OpenWindow();
    }

    private void OpenWindow()
    {
        if (!MenuButton.IsMenuActive)
        {
            return;
        }

        _session = ClientAppUtils.GetClientApp()?.GetClientBackEndSession();
        if (_session == null)
        {
            _window.StatusMessage = "Game session is not ready";
            return;
        }

        try
        {
            _market.SetSession(_session);
            _catalog.Load(_market.IsInjectorHandbookItem);
            _catalog.RefreshOwned(_session);
            _visible = true;
            MenuButton.SetOverlayBlocker(true);
            _nextOwnedRefresh = Time.unscaledTime + 1f;
            _ = LoadIconsAsync();
            if (_catalog.Items.Any(x => x.Market.Status == MarketStatus.NotLoaded))
            {
                RefreshMarket();
            }
        }
        catch (Exception exception)
        {
            Logger.LogError(exception);
            CloseWindow();
            _window.StatusMessage = "Could not build the injector catalog";
        }
    }

    private void CloseWindow()
    {
        _visible = false;
        MenuButton.SetOverlayBlocker(false);
    }

    private async Task LoadIconsAsync()
    {
        try
        {
            await _catalog.LoadIconsAsync();
        }
        catch (Exception exception)
        {
            Logger.LogError(exception);
        }
    }

    private async void RefreshMarket()
    {
        if (_session == null)
        {
            return;
        }

        _window.StatusMessage = "Loading flea offers…";
        await _market.RefreshAllAsync(_catalog.Items);
        _window.StatusMessage = string.IsNullOrWhiteSpace(_market.LastError)
            ? "Flea offers updated"
            : "Flea refresh failed";
    }

    private async void BeginPurchase(InjectorInfo injector, int requested)
    {
        if (_purchaseBusy || _session == null)
        {
            return;
        }

        _purchaseBusy = true;
        try
        {
            _window.StatusMessage = $"Refreshing {injector.Name}…";
            await _market.RefreshTemplateAsync(injector);
            if (injector.Market.Status != MarketStatus.Available || injector.Market.Offers.Count == 0)
            {
                _window.StatusMessage = injector.Market.Status == MarketStatus.NoRoubleOffers
                    ? "No rouble offers after refresh"
                    : injector.Market.Message;
                return;
            }

            var plan = MarketService.BuildPurchasePlan(injector.Market, requested);
            if (plan.Quantity == 0)
            {
                var onlyOversizedBundles = injector.Market.Offers.All(x =>
                    x.SellInOnePiece && MarketService.PurchasableCount(x) > requested);
                _window.StatusMessage = onlyOversizedBundles
                    ? "Only larger whole-bundle offers are available"
                    : "No compatible stock is available";
                return;
            }

            var message = plan.IsShort
                ? $"Insufficient stock — buy {plan.Quantity} for ₽{plan.TotalPrice.ToString("N0", CultureInfo.InvariantCulture)}?"
                : $"Buy {plan.Quantity} for ₽{plan.TotalPrice.ToString("N0", CultureInfo.InvariantCulture)}?";

            _visible = false;
            MenuButton.SetOverlayBlocker(false);
            var shown = _purchaseDialog.Show(
                _session,
                plan,
                message,
                purchases => CompletePurchase(injector, purchases),
                RestoreWindowAfterPurchaseDialog);
            if (!shown)
            {
                _purchaseDialog.CloseActive();
                RestoreWindowAfterPurchaseDialog(false);
                _window.StatusMessage = "The flea purchase dialog is not ready";
            }
        }
        catch (Exception exception)
        {
            Logger.LogError($"Could not open the flea purchase dialog for {injector.TemplateId}: {exception}");
            _purchaseDialog.CloseActive();
            RestoreWindowAfterPurchaseDialog(false);
            _window.StatusMessage = "Could not open the flea purchase dialog";
        }
        finally
        {
            _purchaseBusy = false;
        }
    }

    private async void CompletePurchase(InjectorInfo injector, CommoditiesToPurchase purchases)
    {
        _purchaseBusy = true;
        _window.StatusMessage = "Completing purchase…";
        try
        {
            var result = await _session.RagFair.Purchase(purchases);
            _window.StatusMessage = result.Succeed
                ? "Purchase completed"
                : string.IsNullOrWhiteSpace(result.Error) ? "Purchase failed" : result.Error;
            if (result.Succeed)
            {
                Logger.LogInfo(
                    $"Purchased {purchases.Sum(x => x.Count)} item(s) of {injector.TemplateId} across {purchases.Count} offer(s).");
            }
            else
            {
                Logger.LogWarning(
                    $"Purchase failed for {injector.TemplateId} with error {result.ErrorCode}: {result.Error}");
            }
        }
        catch (Exception exception)
        {
            Logger.LogError(exception);
            _window.StatusMessage = "Purchase failed";
        }

        try
        {
            await _market.RefreshTemplateAsync(injector);
            _catalog.RefreshOwned(_session);
        }
        catch (Exception exception)
        {
            Logger.LogError($"Post-purchase refresh failed for {injector.TemplateId}: {exception}");
        }
        finally
        {
            _purchaseBusy = false;
        }
    }

    private void RestoreWindowAfterPurchaseDialog(bool accepted)
    {
        _visible = MenuButton.IsMenuActive;
        MenuButton.SetOverlayBlocker(_visible);
        if (!accepted)
        {
            _window.StatusMessage = "Purchase cancelled";
        }

    }

    private void OnDestroy()
    {
        CloseWindow();
        _window.Destroy();
        _purchaseDialog.Destroy();
        MenuButton.Destroy();
    }
}
