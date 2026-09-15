using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.Ragfair;
using HarmonyLib;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace InjectorManager;

internal sealed class PurchaseDialogService
{
    private HandoverRagfairMoneyWindow _window;

    internal bool Show(
        IEftSession session,
        PurchasePlan plan,
        string message,
        Action<CommoditiesToPurchase> accept,
        Action<bool> closed)
    {
        var itemUiContext = ItemUiContext.Instance;
        var parent = itemUiContext?._infoWindowsContainer;
        var inventoryController = AccessTools.Field(typeof(ItemUiContext), "_inventoryController")
            ?.GetValue(itemUiContext) as InventoryController;
        if (parent == null || inventoryController == null)
        {
            Plugin.Log.LogError(
                $"Cannot open the flea purchase dialog: info window container ready={parent != null}, inventory controller ready={inventoryController != null}.");
            return false;
        }

        if (_window == null)
        {
            var source = Resources.FindObjectsOfTypeAll<RagfairScreen>()
                .Select(x => AccessTools.Field(typeof(RagfairScreen), "_handoverMoneyWindow")?.GetValue(x) as HandoverRagfairMoneyWindow)
                .FirstOrDefault(x => x != null);
            if (source == null)
            {
                Plugin.Log.LogError("Cannot open the flea purchase dialog: no HandoverRagfairMoneyWindow template was found.");
                return false;
            }

            _window = Object.Instantiate(source, parent, false);
            _window.gameObject.name = "InjectorManagerPurchaseWindow";
        }
        else if (_window.transform.parent != parent)
        {
            _window.transform.SetParent(parent, false);
        }

        var autoExchange = new AutoExchange(inventoryController, session.Traders);
        var accepted = false;
        var context = _window.Show(
            session.Profile.Inventory,
            false,
            plan.Goods,
            purchases =>
            {
                accepted = true;
                accept(purchases);
            },
            true,
            autoExchange);
        if (context == null)
        {
            Plugin.Log.LogError("Cannot open the flea purchase dialog: EFT returned no dialog context.");
            return false;
        }

        if (AccessTools.Field(typeof(HandoverRagfairMoneyWindow), "_message")?.GetValue(_window) is TextMeshProUGUI text)
        {
            text.text = message;
        }

        itemUiContext.RegisterWindow(_window, context);
        context.OnClose += () =>
        {
            closed(accepted);
        };
        if (!_window.gameObject.activeInHierarchy)
        {
            Plugin.Log.LogError("Cannot open the flea purchase dialog: registered window is not active in the UI hierarchy.");
            context.Close();
            return false;
        }

        return true;
    }

    internal void Destroy()
    {
        if (_window != null)
        {
            Object.Destroy(_window.gameObject);
            _window = null;
        }
    }

    internal void CloseActive()
    {
        if (_window != null && _window.gameObject.activeSelf)
        {
            _window.Close();
        }
    }
}
