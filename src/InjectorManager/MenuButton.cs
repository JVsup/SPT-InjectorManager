using EFT.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace InjectorManager;

internal static class MenuButton
{
    private static GameObject _button;
    private static GameObject _blocker;
    private static MenuTaskBar _taskBar;

    internal static MenuTaskBar TaskBar => _taskBar;
    internal static bool IsMenuActive => _taskBar != null && _taskBar.gameObject.activeInHierarchy;

    internal static void TryInject(Action onClick)
    {
        if (_button != null)
        {
            return;
        }

        _taskBar = Object.FindObjectOfType<MenuTaskBar>(true);
        if (_taskBar == null)
        {
            return;
        }

        var parent = _taskBar
            .GetComponentsInChildren<HorizontalLayoutGroup>(true)
            .OrderByDescending(x => x.transform.childCount)
            .Select(x => x.transform)
            .FirstOrDefault();
        if (parent == null)
        {
            Plugin.Log.LogWarning("Could not find the MenuTaskBar navigation container.");
            return;
        }

        var template = parent.GetComponentInChildren<TextMeshProUGUI>(true);
        var normalColor = template != null ? template.color : new Color(0.85f, 0.85f, 0.85f);

        _button = new GameObject("InjectorManagerMenuButton", typeof(RectTransform));
        _button.transform.SetParent(parent, false);
        var layout = _button.AddComponent<LayoutElement>();
        layout.preferredWidth = 145f;
        layout.flexibleWidth = 0f;

        var background = _button.AddComponent<Image>();
        background.color = Color.clear;
        var button = _button.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => onClick());

        var labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(_button.transform, false);
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(4f, 0f);
        labelRect.offsetMax = new Vector2(-4f, 0f);
        var label = labelObject.AddComponent<TextMeshProUGUI>();
        label.raycastTarget = false;
        label.text = "INJECTORS";
        label.fontSize = template != null ? template.fontSize : 12f;
        label.fontStyle = FontStyles.Bold;
        label.color = normalColor;
        label.alignment = TextAlignmentOptions.Center;
        label.characterSpacing = template != null ? template.characterSpacing : 2f;
        label.overflowMode = TextOverflowModes.Overflow;
        if (template?.font != null)
        {
            label.font = template.font;
        }

        var hoverColor = new Color(1f, 0.84f, 0f);
        var pressedColor = new Color(
            normalColor.r * 0.6f,
            normalColor.g * 0.6f,
            normalColor.b * 0.6f);
        var eventTrigger = _button.AddComponent<EventTrigger>();
        AddTrigger(eventTrigger, EventTriggerType.PointerEnter, _ => label.color = hoverColor);
        AddTrigger(eventTrigger, EventTriggerType.PointerExit, _ => label.color = normalColor);
        AddTrigger(eventTrigger, EventTriggerType.PointerDown, _ => label.color = pressedColor);
        AddTrigger(eventTrigger, EventTriggerType.PointerUp, _ => label.color = hoverColor);
    }

    private static void AddTrigger(
        EventTrigger eventTrigger,
        EventTriggerType eventType,
        UnityAction<BaseEventData> action)
    {
        var entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(action);
        eventTrigger.triggers.Add(entry);
    }

    internal static void Destroy()
    {
        SetOverlayBlocker(false);
        if (_button != null)
        {
            Object.Destroy(_button);
        }

        if (_blocker != null)
        {
            Object.Destroy(_blocker);
        }

        _button = null;
        _blocker = null;
        _taskBar = null;
    }

    internal static void SetButtonVisible(bool visible)
    {
        if (_button != null)
        {
            _button.SetActive(visible);
        }
    }

    internal static void SetOverlayBlocker(bool active)
    {
        if (!active)
        {
            if (_blocker != null)
            {
                _blocker.SetActive(false);
            }

            return;
        }

        var canvas = _taskBar?.GetComponentInParent<Canvas>(true);
        var parent = canvas?.rootCanvas.transform ?? _taskBar?.transform.root;
        if (parent == null)
        {
            Plugin.Log.LogWarning("Could not create the Injector Manager input blocker because the menu canvas is unavailable.");
            return;
        }

        if (_blocker == null)
        {
            _blocker = new GameObject(
                "InjectorManagerInputBlocker",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            _blocker.transform.SetParent(parent, false);
            var rect = _blocker.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = _blocker.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.72f);
            image.raycastTarget = true;
        }

        _blocker.transform.SetAsLastSibling();
        _blocker.SetActive(true);
    }
}
