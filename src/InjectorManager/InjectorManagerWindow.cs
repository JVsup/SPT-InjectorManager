using System.Globalization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace InjectorManager;

internal sealed class InjectorManagerWindow
{
    private const int WindowId = 0x49A315;
    private const float MaximumWindowWidth = 1760f;
    private const float MaximumWindowHeight = 960f;
    private const float BuffPropertyMinimumWidth = 120f;
    private const float BuffPropertyMaximumWidth = 220f;
    private const float BuffSignWidth = 10f;
    private const float BuffSignGap = 2f;
    private const float BuffValueWidth = 52f;
    private const float BuffDurationWidth = 52f;
    private const float BuffDelayWidth = 44f;
    private const float BuffChanceWidth = 46f;
    private const float BuffColumnGap = 3f;
    private const float BuffHeaderHeight = 14f;
    private const float BuffRowHeight = 16f;
    private const float ToolbarControlHeight = 26f;
    private const float ToolbarRowHeight = 28f;
    private const float DurationSliderTrackHeight = 4f;
    private const float DurationSliderThumbWidth = 10f;
    private const float DurationSliderThumbHeight = 16f;
    private const float FavoriteButtonWidth = 24f;
    private const float InjectorIconSize = 66f;
    private const float IdentityBlockWidth = 220f;
    private const float PurchaseBlockWidth = 180f;

    private static readonly string[] SortOptions =
    {
        "Name",
        "Longest duration",
        "Positive count",
        "Negative count"
    };

    private readonly Dictionary<string, string> _quantities = new(StringComparer.Ordinal);
    private Vector2 _scroll;
    private Vector2 _dropdownScroll;
    private string _search = string.Empty;
    private float _minimumDuration;
    private float _maximumDuration = DurationScale.MaximumSeconds;
    private InjectorSort _sort = InjectorSort.Name;
    private bool _descending;
    private bool _showUnavailable = true;
    private bool _favoritesOnly;
    private int _affectedIndex;
    private string[] _affectedProperties = { "All properties" };
    private DropdownKind _openDropdown;
    private Rect _propertyButtonRect;
    private Rect _sortButtonRect;
    private Rect _windowRect;
    private GUIStyle _windowStyle;
    private GUIStyle _titleStyle;
    private GUIStyle _headingStyle;
    private GUIStyle _bodyStyle;
    private GUIStyle _mutedStyle;
    private GUIStyle _toolbarLabelStyle;
    private GUIStyle _toolbarValueStyle;
    private GUIStyle _quantityLabelStyle;
    private GUIStyle _textFieldStyle;
    private GUIStyle _durationSliderTrackStyle;
    private GUIStyle _durationSliderStyle;
    private GUIStyle _durationSliderThumbStyle;
    private GUIStyle _tableHeaderStyle;
    private GUIStyle _tablePropertyStyle;
    private GUIStyle _tableCellStyle;
    private GUIStyle _tableEmptyStyle;
    private GUIStyle _panelStyle;
    private GUIStyle _rowStyle;
    private GUIStyle _popupStyle;
    private GUIStyle _neutralButtonStyle;
    private GUIStyle _selectedButtonStyle;
    private GUIStyle _primaryButtonStyle;
    private GUIStyle _disabledButtonStyle;
    private GUIStyle _favoriteButtonStyle;
    private GUIStyle _popupButtonStyle;
    private GUIStyle _popupSelectedButtonStyle;
    private Texture2D _windowTexture;
    private Texture2D _panelTexture;
    private Texture2D _rowTexture;
    private Texture2D _popupTexture;
    private Texture2D _neutralButtonTexture;
    private Texture2D _hoverButtonTexture;
    private Texture2D _pressedButtonTexture;
    private Texture2D _selectedButtonTexture;
    private Texture2D _selectedHoverButtonTexture;
    private Texture2D _primaryButtonTexture;
    private Texture2D _primaryHoverButtonTexture;
    private Texture2D _disabledButtonTexture;

    internal string StatusMessage { get; set; } = string.Empty;

    internal void Draw(
        IReadOnlyList<InjectorInfo> injectors,
        MarketService market,
        bool purchaseBusy,
        Action close,
        Action refresh,
        Action<InjectorInfo, int> buy)
    {
        EnsureStyles();
        RefreshAffectedProperties(injectors);

        var oldMatrix = GUI.matrix;
        var scale = Math.Min(Screen.width / 1920f, Screen.height / 1080f);
        scale = Math.Max(0.65f, scale);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

        var availableWidth = Screen.width / scale;
        var availableHeight = Screen.height / scale;
        var width = Math.Min(availableWidth * 0.94f, MaximumWindowWidth);
        var height = Math.Min(availableHeight * 0.92f, MaximumWindowHeight);
        _windowRect = new Rect(
            (availableWidth - width) * 0.5f,
            (availableHeight - height) * 0.5f,
            width,
            height);
        _windowRect = GUI.ModalWindow(
            WindowId,
            _windowRect,
            _ => DrawContents(injectors, market, purchaseBusy, close, refresh, buy),
            GUIContent.none,
            _windowStyle);
        GUI.matrix = oldMatrix;
    }

    internal void Destroy()
    {
        DestroyTexture(ref _windowTexture);
        DestroyTexture(ref _panelTexture);
        DestroyTexture(ref _rowTexture);
        DestroyTexture(ref _popupTexture);
        DestroyTexture(ref _neutralButtonTexture);
        DestroyTexture(ref _hoverButtonTexture);
        DestroyTexture(ref _pressedButtonTexture);
        DestroyTexture(ref _selectedButtonTexture);
        DestroyTexture(ref _selectedHoverButtonTexture);
        DestroyTexture(ref _primaryButtonTexture);
        DestroyTexture(ref _primaryHoverButtonTexture);
        DestroyTexture(ref _disabledButtonTexture);
        _windowStyle = null;
    }

    private void DrawContents(
        IReadOnlyList<InjectorInfo> injectors,
        MarketService market,
        bool purchaseBusy,
        Action close,
        Action refresh,
        Action<InjectorInfo, int> buy)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("INJECTOR MANAGER", _titleStyle);
        GUILayout.FlexibleSpace();
        if (!string.IsNullOrWhiteSpace(StatusMessage))
        {
            GUILayout.Label(StatusMessage, _mutedStyle, GUILayout.MaxWidth(560f));
        }

        var previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && !market.IsLoading && !purchaseBusy;
        if (GUILayout.Button(
                market.IsLoading ? "LOADING…" : "REFRESH",
                GetButtonStyle(_neutralButtonStyle),
                GUILayout.Width(110f),
                GUILayout.Height(28f)))
        {
            refresh();
        }

        GUI.enabled = previousEnabled;
        if (GUILayout.Button(
                "CLOSE",
                GetButtonStyle(_neutralButtonStyle),
                GUILayout.Width(82f),
                GUILayout.Height(28f)))
        {
            close();
        }

        GUILayout.EndHorizontal();
        GUILayout.Space(5f);

        DrawFilters();
        GUILayout.Space(6f);

        var visible = ApplyFilters(injectors).ToArray();
        var normal = visible.Where(x => !IsUnavailable(x)).ToArray();
        var unavailable = visible.Where(IsUnavailable).ToArray();
        var rendered = _showUnavailable
            ? normal.Concat(unavailable).ToArray()
            : normal;
        var contentWidth = Math.Max(1000f, _windowRect.width - 48f);
        var fixedWidth = FavoriteButtonWidth + InjectorIconSize + IdentityBlockWidth + PurchaseBlockWidth + 50f;
        var sectionWidth = Math.Max(180f, (contentWidth - fixedWidth) / 3f);
        var effectsPropertyWidth = GetBuffPropertyWidth(rendered, BuffSection.Effects, sectionWidth);
        var regenerationPropertyWidth = GetBuffPropertyWidth(rendered, BuffSection.Regeneration, sectionWidth);
        var attributesPropertyWidth = GetBuffPropertyWidth(
            rendered,
            BuffSection.AttributesAndSkills,
            sectionWidth);

        var visibleCount = normal.Length + unavailable.Length;
        GUILayout.Label(
            _favoritesOnly ? $"{visibleCount} favorite injectors" : $"{visibleCount} injectors",
            _mutedStyle);
        var listEnabled = GUI.enabled;
        GUI.enabled = listEnabled && _openDropdown == DropdownKind.None;
        _scroll = GUILayout.BeginScrollView(_scroll, false, true);
        if (_favoritesOnly && visibleCount == 0)
        {
            GUILayout.Label("No favorite injectors", _mutedStyle);
        }

        foreach (var injector in normal)
        {
            DrawRow(
                injector,
                purchaseBusy,
                buy,
                sectionWidth,
                effectsPropertyWidth,
                regenerationPropertyWidth,
                attributesPropertyWidth);
        }

        if (unavailable.Length > 0)
        {
            GUILayout.Space(4f);
            if (GUILayout.Button(
                    $"CURRENTLY UNAVAILABLE ({unavailable.Length}) {(_showUnavailable ? "▲" : "▼")}",
                    GetButtonStyle(_neutralButtonStyle),
                    GUILayout.Height(28f)))
            {
                _showUnavailable = !_showUnavailable;
            }

            if (_showUnavailable)
            {
                foreach (var injector in unavailable)
                {
                    DrawRow(
                        injector,
                        purchaseBusy,
                        buy,
                        sectionWidth,
                        effectsPropertyWidth,
                        regenerationPropertyWidth,
                        attributesPropertyWidth);
                }
            }
        }

        GUILayout.EndScrollView();
        GUI.enabled = listEnabled;
        DrawOpenDropdown();
    }

    private void DrawFilters()
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.BeginHorizontal(GUILayout.Height(ToolbarRowHeight));
        GUILayout.Label(
            "Search",
            _toolbarLabelStyle,
            GUILayout.Width(52f),
            GUILayout.Height(ToolbarControlHeight));
        _search = GUILayout.TextField(
            _search,
            _textFieldStyle,
            GUILayout.MinWidth(180f),
            GUILayout.Height(ToolbarControlHeight));

        if (GUILayout.Button(
                _favoritesOnly ? "★ FAVORITES" : "☆ FAVORITES",
                GetButtonStyle(_favoritesOnly ? _selectedButtonStyle : _neutralButtonStyle),
                GUILayout.Width(112f),
                GUILayout.Height(ToolbarControlHeight)))
        {
            _favoritesOnly = !_favoritesOnly;
            _scroll = Vector2.zero;
        }

        GUILayout.Space(10f);
        GUILayout.Label(
            "Show only property",
            _toolbarLabelStyle,
            GUILayout.Width(126f),
            GUILayout.Height(ToolbarControlHeight));
        if (GUILayout.Button(
                $"{_affectedProperties[_affectedIndex]} ▾",
                GetButtonStyle(_neutralButtonStyle),
                GUILayout.Width(220f),
                GUILayout.Height(ToolbarControlHeight)))
        {
            ToggleDropdown(DropdownKind.Property);
        }

        if (Event.current.type == EventType.Repaint)
        {
            _propertyButtonRect = GUILayoutUtility.GetLastRect();
        }

        GUILayout.Space(10f);
        GUILayout.Label(
            "Sort by",
            _toolbarLabelStyle,
            GUILayout.Width(50f),
            GUILayout.Height(ToolbarControlHeight));
        if (GUILayout.Button(
                $"{SortOptions[(int)_sort]} ▾",
                GetButtonStyle(_neutralButtonStyle),
                GUILayout.Width(155f),
                GUILayout.Height(ToolbarControlHeight)))
        {
            ToggleDropdown(DropdownKind.Sort);
        }

        if (Event.current.type == EventType.Repaint)
        {
            _sortButtonRect = GUILayoutUtility.GetLastRect();
        }

        if (GUILayout.Button(
                _descending ? "DESC" : "ASC",
                GetButtonStyle(_neutralButtonStyle),
                GUILayout.Width(62f),
                GUILayout.Height(ToolbarControlHeight)))
        {
            _descending = !_descending;
        }

        if (GUILayout.Button(
                "RESET",
                GetButtonStyle(_neutralButtonStyle),
                GUILayout.Width(72f),
                GUILayout.Height(ToolbarControlHeight)))
        {
            ResetFilters();
        }

        GUILayout.EndHorizontal();
        GUILayout.Space(4f);
        var durationEnabled = GUI.enabled;
        GUI.enabled = durationEnabled && _openDropdown == DropdownKind.None;
        GUILayout.BeginHorizontal(GUILayout.Height(ToolbarRowHeight));
        GUILayout.Label(
            "Duration",
            _toolbarLabelStyle,
            GUILayout.Width(62f),
            GUILayout.Height(ToolbarControlHeight));
        GUILayout.Label(
            $"From {FormatDuration(_minimumDuration)}",
            _toolbarValueStyle,
            GUILayout.Width(92f),
            GUILayout.Height(ToolbarControlHeight));
        var minimumPosition = DrawDurationSlider(DurationScale.ToPosition(_minimumDuration));
        var newMinimum = DurationScale.FromPosition(minimumPosition);
        _minimumDuration = Math.Min(newMinimum, _maximumDuration);

        GUILayout.Space(18f);
        GUILayout.Label(
            $"To {FormatDuration(_maximumDuration)}",
            _toolbarValueStyle,
            GUILayout.Width(86f),
            GUILayout.Height(ToolbarControlHeight));
        var maximumPosition = DrawDurationSlider(DurationScale.ToPosition(_maximumDuration));
        var newMaximum = DurationScale.FromPosition(maximumPosition);
        _maximumDuration = Math.Max(newMaximum, _minimumDuration);
        GUILayout.EndHorizontal();
        GUI.enabled = durationEnabled;
        GUILayout.EndVertical();
    }

    private float DrawDurationSlider(float value)
    {
        var lane = GUILayoutUtility.GetRect(
            GUIContent.none,
            GUIStyle.none,
            GUILayout.MinWidth(230f),
            GUILayout.ExpandWidth(true),
            GUILayout.Height(ToolbarControlHeight));
        var track = new Rect(
            lane.x,
            lane.y + (lane.height - DurationSliderTrackHeight) * 0.5f,
            lane.width,
            DurationSliderTrackHeight);
        GUI.Box(track, GUIContent.none, _durationSliderTrackStyle);
        var control = new Rect(
            lane.x,
            lane.y + (lane.height - DurationSliderThumbHeight) * 0.5f,
            lane.width,
            DurationSliderThumbHeight);
        return GUI.HorizontalSlider(
            control,
            value,
            0f,
            1f,
            _durationSliderStyle,
            _durationSliderThumbStyle);
    }

    private void DrawOpenDropdown()
    {
        if (_openDropdown == DropdownKind.None)
        {
            return;
        }

        var anchor = _openDropdown == DropdownKind.Property ? _propertyButtonRect : _sortButtonRect;
        var options = _openDropdown == DropdownKind.Property ? _affectedProperties : SortOptions;
        var selected = _openDropdown == DropdownKind.Property ? _affectedIndex : (int)_sort;
        var visibleRows = Math.Min(options.Length, 10);
        var popupRect = new Rect(anchor.x, anchor.yMax + 2f, anchor.width, visibleRows * 27f + 8f);

        GUI.Box(popupRect, GUIContent.none, _popupStyle);
        var contentRect = new Rect(popupRect.x + 4f, popupRect.y + 4f, popupRect.width - 8f, popupRect.height - 8f);
        var viewHeight = options.Length * 27f;
        _dropdownScroll = GUI.BeginScrollView(
            contentRect,
            _dropdownScroll,
            new Rect(0f, 0f, contentRect.width - (options.Length > visibleRows ? 18f : 0f), viewHeight));
        for (var index = 0; index < options.Length; index++)
        {
            var optionRect = new Rect(0f, index * 27f, contentRect.width - 18f, 25f);
            var style = GetButtonStyle(index == selected ? _popupSelectedButtonStyle : _popupButtonStyle);
            if (GUI.Button(optionRect, options[index], style))
            {
                if (_openDropdown == DropdownKind.Property)
                {
                    _affectedIndex = index;
                }
                else
                {
                    _sort = (InjectorSort)index;
                }

                _openDropdown = DropdownKind.None;
                Event.current.Use();
            }
        }

        GUI.EndScrollView();

        if (Event.current.type == EventType.MouseDown &&
            !popupRect.Contains(Event.current.mousePosition) &&
            !anchor.Contains(Event.current.mousePosition))
        {
            _openDropdown = DropdownKind.None;
            Event.current.Use();
        }
    }

    private void DrawRow(
        InjectorInfo injector,
        bool purchaseBusy,
        Action<InjectorInfo, int> buy,
        float sectionWidth,
        float effectsPropertyWidth,
        float regenerationPropertyWidth,
        float attributesPropertyWidth)
    {
        GUILayout.BeginHorizontal(_rowStyle, GUILayout.MinHeight(94f));
        var favorite = PluginConfig.IsFavorite(injector.TemplateId);
        GUILayout.BeginVertical(
            GUILayout.Width(FavoriteButtonWidth),
            GUILayout.Height(InjectorIconSize));
        GUILayout.Space((InjectorIconSize - FavoriteButtonWidth) * 0.5f);
        if (GUILayout.Button(
                favorite ? "★" : "☆",
                _favoriteButtonStyle,
                GUILayout.Width(FavoriteButtonWidth),
                GUILayout.Height(FavoriteButtonWidth)))
        {
            PluginConfig.ToggleFavorite(injector.TemplateId);
        }

        GUILayout.EndVertical();
        GUILayout.Space(5f);
        DrawIcon(injector.Icon);
        GUILayout.Space(8f);

        GUILayout.BeginVertical(GUILayout.Width(IdentityBlockWidth), GUILayout.MinHeight(80f));
        GUILayout.Label(injector.Name, _headingStyle);
        GUILayout.Label($"OWNED: {injector.Owned}", _bodyStyle);
        GUILayout.EndVertical();

        DrawBuffSection(injector, BuffSection.Effects, "EFFECTS", sectionWidth, effectsPropertyWidth);
        DrawBuffSection(
            injector,
            BuffSection.Regeneration,
            "REGENERATION",
            sectionWidth,
            regenerationPropertyWidth);
        DrawBuffSection(
            injector,
            BuffSection.AttributesAndSkills,
            "ATTRIBUTES & SKILLS",
            sectionWidth,
            attributesPropertyWidth);
        DrawPurchaseBlock(injector, purchaseBusy, buy);
        GUILayout.EndHorizontal();
        GUILayout.Space(3f);
    }

    private void DrawBuffSection(
        InjectorInfo injector,
        BuffSection section,
        string heading,
        float width,
        float propertyWidth)
    {
        GUILayout.BeginVertical(GUILayout.Width(width), GUILayout.MinHeight(80f));
        GUILayout.Label(heading, _headingStyle);
        var buffs = GetVisibleBuffs(injector, section);

        if (buffs.Length == 0)
        {
            GUILayout.Label(
                "NONE",
                _tableEmptyStyle,
                GUILayout.Width(BuffPropertyMinimumWidth),
                GUILayout.Height(BuffRowHeight));
        }
        else
        {
            var showDuration = PluginConfig.ShowDuration.Value;
            var showValue = buffs.Any(x => x.HasValue);
            var showDelay = PluginConfig.ShowDelay.Value && buffs.Any(x => x.Delay > 1f);
            var showChance = PluginConfig.ShowChance.Value && buffs.Any(x => x.Chance < 1f);
            DrawBuffTableHeader(propertyWidth, showValue, showDuration, showDelay, showChance);
            foreach (var buff in buffs)
            {
                DrawBuffTableRow(buff, propertyWidth, showValue, showDuration, showDelay, showChance);
            }
        }

        GUILayout.EndVertical();
    }

    private void DrawBuffTableHeader(
        float propertyWidth,
        bool showValue,
        bool showDuration,
        bool showDelay,
        bool showChance)
    {
        GUILayout.BeginHorizontal(
            GUILayout.Width(GetBuffTableWidth(propertyWidth, showValue, showDuration, showDelay, showChance)),
            GUILayout.Height(BuffHeaderHeight));
        DrawBuffCell(
            "PROPERTY",
            _tableHeaderStyle,
            propertyWidth,
            BuffHeaderHeight,
            showValue || showDuration || showDelay || showChance);
        if (showValue)
        {
            DrawBuffCell(
                "VALUE",
                _tableHeaderStyle,
                BuffValueWidth,
                BuffHeaderHeight,
                showDuration || showDelay || showChance);
        }

        if (showDuration)
        {
            DrawBuffCell(
                "DURATION",
                _tableHeaderStyle,
                BuffDurationWidth,
                BuffHeaderHeight,
                showDelay || showChance);
        }

        if (showDelay)
        {
            DrawBuffCell(
                "DELAY",
                _tableHeaderStyle,
                BuffDelayWidth,
                BuffHeaderHeight,
                showChance);
        }

        if (showChance)
        {
            DrawBuffCell("CHANCE", _tableHeaderStyle, BuffChanceWidth, BuffHeaderHeight, false);
        }

        GUILayout.EndHorizontal();
    }

    private void DrawBuffTableRow(
        BuffInfo buff,
        float propertyWidth,
        bool showValue,
        bool showDuration,
        bool showDelay,
        bool showChance)
    {
        var rowHeight = Math.Max(
            BuffRowHeight,
            (float)Math.Ceiling(_tablePropertyStyle.CalcHeight(
                new GUIContent(buff.PropertyName),
                GetBuffPropertyTextWidth(propertyWidth))));

        GUILayout.BeginHorizontal(
            GUILayout.Width(GetBuffTableWidth(propertyWidth, showValue, showDuration, showDelay, showChance)),
            GUILayout.Height(rowHeight));
        DrawBuffProperty(
            buff,
            propertyWidth,
            rowHeight,
            showValue || showDuration || showDelay || showChance);
        if (showValue)
        {
            DrawBuffCell(
                buff.HasValue ? buff.ValueText : "—",
                _tableCellStyle,
                BuffValueWidth,
                rowHeight,
                showDuration || showDelay || showChance);
        }

        if (showDuration)
        {
            DrawBuffCell(
                buff.Duration > 0f ? FormatDuration(buff.Duration) : "INSTANT",
                _tableCellStyle,
                BuffDurationWidth,
                rowHeight,
                showDelay || showChance);
        }

        if (showDelay)
        {
            DrawBuffCell(
                buff.Delay > 1f ? FormatDuration(buff.Delay) : "—",
                _tableCellStyle,
                BuffDelayWidth,
                rowHeight,
                showChance);
        }

        if (showChance)
        {
            var chance = buff.Chance < 1f
                ? $"{Math.Round(buff.Chance * 100f).ToString(CultureInfo.InvariantCulture)}%"
                : "—";
            DrawBuffCell(chance, _tableCellStyle, BuffChanceWidth, rowHeight, false);
        }

        GUILayout.EndHorizontal();
    }

    private void DrawBuffProperty(BuffInfo buff, float propertyWidth, float height, bool addGap)
    {
        var prefix = buff.IsPositive ? "<color=#79c777>+</color>" : "<color=#d97474>−</color>";
        GUILayout.BeginHorizontal(GUILayout.Width(propertyWidth), GUILayout.Height(height));
        GUILayout.Label(prefix, _tableCellStyle, GUILayout.Width(BuffSignWidth), GUILayout.Height(height));
        GUILayout.Space(BuffSignGap);
        GUILayout.Label(
            buff.PropertyName,
            _tablePropertyStyle,
            GUILayout.Width(GetBuffPropertyTextWidth(propertyWidth)),
            GUILayout.Height(height));
        GUILayout.EndHorizontal();
        if (addGap)
        {
            GUILayout.Space(BuffColumnGap);
        }
    }

    private static BuffInfo[] GetVisibleBuffs(InjectorInfo injector, BuffSection section)
    {
        var limit = PluginConfig.EntriesPerPolarity.Value;
        var positive = injector.Buffs
            .Where(x => x.Section == section && x.IsPositive)
            .OrderByDescending(x => x.Duration)
            .ThenByDescending(x => x.Strength)
            .Take(limit);
        var negative = injector.Buffs
            .Where(x => x.Section == section && !x.IsPositive)
            .OrderByDescending(x => x.Duration)
            .ThenByDescending(x => x.Strength)
            .Take(limit);
        return positive.Concat(negative).ToArray();
    }

    private float GetBuffPropertyWidth(
        IEnumerable<InjectorInfo> injectors,
        BuffSection section,
        float sectionWidth)
    {
        var desiredWidth = BuffPropertyMinimumWidth;
        var maximumWidth = BuffPropertyMaximumWidth;

        foreach (var injector in injectors)
        {
            var buffs = GetVisibleBuffs(injector, section);
            if (buffs.Length == 0)
            {
                continue;
            }

            var textWidth = buffs.Max(x => _tablePropertyStyle.CalcSize(new GUIContent(x.PropertyName)).x);
            desiredWidth = Math.Max(
                desiredWidth,
                BuffSignWidth + BuffSignGap + (float)Math.Ceiling(textWidth) + 2f);

            var showDuration = PluginConfig.ShowDuration.Value;
            var showValue = buffs.Any(x => x.HasValue);
            var showDelay = PluginConfig.ShowDelay.Value && buffs.Any(x => x.Delay > 1f);
            var showChance = PluginConfig.ShowChance.Value && buffs.Any(x => x.Chance < 1f);
            var nonPropertyWidth = GetBuffTableWidth(0f, showValue, showDuration, showDelay, showChance);
            maximumWidth = Math.Min(
                maximumWidth,
                Math.Max(BuffPropertyMinimumWidth, sectionWidth - nonPropertyWidth));
        }

        return Math.Max(BuffPropertyMinimumWidth, Math.Min(desiredWidth, maximumWidth));
    }

    private static float GetBuffPropertyTextWidth(float propertyWidth)
    {
        return propertyWidth - BuffSignWidth - BuffSignGap;
    }

    private static float GetBuffTableWidth(
        float propertyWidth,
        bool showValue,
        bool showDuration,
        bool showDelay,
        bool showChance)
    {
        var width = propertyWidth;
        var columnCount = 1;

        if (showValue)
        {
            width += BuffValueWidth;
            columnCount++;
        }

        if (showDuration)
        {
            width += BuffDurationWidth;
            columnCount++;
        }

        if (showDelay)
        {
            width += BuffDelayWidth;
            columnCount++;
        }

        if (showChance)
        {
            width += BuffChanceWidth;
            columnCount++;
        }

        return width + BuffColumnGap * (columnCount - 1);
    }

    private static void DrawBuffCell(string text, GUIStyle style, float width, float height, bool addGap = true)
    {
        GUILayout.Label(text, style, GUILayout.Width(width), GUILayout.Height(height));
        if (addGap)
        {
            GUILayout.Space(BuffColumnGap);
        }
    }

    private void DrawPurchaseBlock(InjectorInfo injector, bool purchaseBusy, Action<InjectorInfo, int> buy)
    {
        GUILayout.BeginVertical(GUILayout.Width(PurchaseBlockWidth), GUILayout.MinHeight(80f));
        DrawMarketSummary(injector.Market);
        GUILayout.FlexibleSpace();

        if (!_quantities.TryGetValue(injector.TemplateId, out var quantity))
        {
            quantity = "1";
            _quantities.Add(injector.TemplateId, quantity);
        }

        GUILayout.BeginHorizontal(GUILayout.Width(PurchaseBlockWidth), GUILayout.Height(28f));
        GUILayout.Label("Qty", _quantityLabelStyle, GUILayout.Width(28f), GUILayout.Height(26f));
        quantity = GUILayout.TextField(
            quantity,
            _textFieldStyle,
            GUILayout.Width(50f),
            GUILayout.Height(26f));
        _quantities[injector.TemplateId] = quantity;
        var validQuantity = int.TryParse(quantity, NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count > 0;
        var previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && !purchaseBusy && validQuantity && injector.Market.Status == MarketStatus.Available;
        if (GUILayout.Button(
                "BUY",
                GetButtonStyle(_primaryButtonStyle),
                GUILayout.Width(80f),
                GUILayout.Height(26f)))
        {
            buy(injector, count);
        }

        GUI.enabled = previousEnabled;
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private void DrawMarketSummary(MarketSnapshot market)
    {
        if (market.Status == MarketStatus.Available)
        {
            GUILayout.Label($"₽{market.LowestUnitPrice.ToString("N0", CultureInfo.InvariantCulture)} each", _bodyStyle);
            GUILayout.Label($"{market.Stock} available", _mutedStyle);
            return;
        }

        GUILayout.Label(market.Message, _mutedStyle);
    }

    private static void DrawIcon(Sprite sprite)
    {
        var rect = GUILayoutUtility.GetRect(
            InjectorIconSize,
            InjectorIconSize,
            GUILayout.Width(InjectorIconSize),
            GUILayout.Height(InjectorIconSize));
        if (sprite == null || sprite.texture == null)
        {
            GUI.Box(rect, "ICON");
            return;
        }

        var source = sprite.rect;
        var texture = sprite.texture;
        var coordinates = new Rect(
            source.x / texture.width,
            source.y / texture.height,
            source.width / texture.width,
            source.height / texture.height);
        GUI.DrawTextureWithTexCoords(rect, texture, coordinates, true);
    }

    private IEnumerable<InjectorInfo> ApplyFilters(IEnumerable<InjectorInfo> injectors)
    {
        var query = injectors.Where(x => !PluginConfig.IsBlacklisted(x.TemplateId));
        if (_favoritesOnly)
        {
            query = query.Where(x => PluginConfig.IsFavorite(x.TemplateId));
        }

        if (!string.IsNullOrWhiteSpace(_search))
        {
            query = query.Where(x =>
                x.Name.IndexOf(_search, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                x.TemplateId.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                x.Buffs.Any(buff =>
                    buff.PropertyName.IndexOf(_search, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                    buff.DisplayText.IndexOf(_search, StringComparison.CurrentCultureIgnoreCase) >= 0));
        }

        var property = _affectedIndex == 0 ? null : _affectedProperties[_affectedIndex];
        var hasDurationBounds = _minimumDuration > 0f || _maximumDuration < DurationScale.MaximumSeconds;
        if (property != null || hasDurationBounds)
        {
            query = query.Where(x => x.Buffs.Any(buff =>
                (property == null || string.Equals(buff.PropertyName, property, StringComparison.CurrentCultureIgnoreCase)) &&
                buff.Duration >= _minimumDuration &&
                buff.Duration <= _maximumDuration));
        }

        IOrderedEnumerable<InjectorInfo> ordered = _sort switch
        {
            InjectorSort.LongestDuration => query.OrderBy(x => x.LongestDuration),
            InjectorSort.PositiveCount => query.OrderBy(x => x.PositiveCount),
            InjectorSort.NegativeCount => query.OrderBy(x => x.NegativeCount),
            _ => query.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
        };
        return _descending ? ordered.Reverse() : ordered;
    }

    private void RefreshAffectedProperties(IEnumerable<InjectorInfo> injectors)
    {
        var properties = injectors
            .SelectMany(x => x.Buffs)
            .Select(x => x.PropertyName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
            .Prepend("All properties")
            .ToArray();
        var selected = _affectedIndex < _affectedProperties.Length ? _affectedProperties[_affectedIndex] : "All properties";
        _affectedProperties = properties;
        _affectedIndex = Math.Max(
            0,
            Array.FindIndex(
                _affectedProperties,
                x => string.Equals(x, selected, StringComparison.CurrentCultureIgnoreCase)));
    }

    private void ResetFilters()
    {
        _search = string.Empty;
        _affectedIndex = 0;
        _minimumDuration = 0f;
        _maximumDuration = DurationScale.MaximumSeconds;
        _sort = InjectorSort.Name;
        _descending = false;
        _openDropdown = DropdownKind.None;
        _dropdownScroll = Vector2.zero;
    }

    private void ToggleDropdown(DropdownKind dropdown)
    {
        _openDropdown = _openDropdown == dropdown ? DropdownKind.None : dropdown;
        _dropdownScroll = Vector2.zero;
    }

    private void EnsureStyles()
    {
        if (_windowStyle != null)
        {
            return;
        }

        _windowTexture = CreateTexture(new Color(0.055f, 0.065f, 0.07f, 1f));
        _panelTexture = CreateTexture(new Color(0.075f, 0.085f, 0.09f, 1f));
        _rowTexture = CreateTexture(new Color(0.09f, 0.10f, 0.105f, 1f));
        _popupTexture = CreateTexture(new Color(0.04f, 0.045f, 0.05f, 1f));
        _neutralButtonTexture = CreateBorderedTexture(
            new Color32(0x23, 0x28, 0x2c, 0xff),
            new Color32(0x3a, 0x41, 0x46, 0xff));
        _hoverButtonTexture = CreateBorderedTexture(
            new Color32(0x2d, 0x34, 0x39, 0xff),
            new Color32(0x5a, 0x65, 0x6c, 0xff));
        _pressedButtonTexture = CreateBorderedTexture(
            new Color32(0x17, 0x1b, 0x1e, 0xff),
            new Color32(0x5a, 0x65, 0x6c, 0xff));
        _selectedButtonTexture = CreateBorderedTexture(
            new Color32(0x25, 0x47, 0x35, 0xff),
            new Color32(0x5a, 0x9a, 0x70, 0xff));
        _selectedHoverButtonTexture = CreateBorderedTexture(
            new Color32(0x2c, 0x59, 0x40, 0xff),
            new Color32(0x68, 0xb0, 0x7f, 0xff));
        _primaryButtonTexture = CreateBorderedTexture(
            new Color32(0x28, 0x54, 0x3c, 0xff),
            new Color32(0x5a, 0x9a, 0x70, 0xff));
        _primaryHoverButtonTexture = CreateBorderedTexture(
            new Color32(0x32, 0x68, 0x48, 0xff),
            new Color32(0x68, 0xb0, 0x7f, 0xff));
        _disabledButtonTexture = CreateBorderedTexture(
            new Color32(0x17, 0x1a, 0x1c, 0xff),
            new Color32(0x2a, 0x2e, 0x31, 0xff));

        _windowStyle = new GUIStyle(GUI.skin.window)
        {
            padding = new RectOffset(10, 10, 8, 10),
            normal = { background = _windowTexture }
        };
        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            richText = true
        };
        _headingStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            richText = true,
            wordWrap = true
        };
        _bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            richText = true,
            wordWrap = true
        };
        _mutedStyle = new GUIStyle(_bodyStyle)
        {
            fontSize = 11,
            normal = { textColor = new Color(0.68f, 0.68f, 0.68f, 1f) }
        };
        _toolbarLabelStyle = new GUIStyle(_headingStyle)
        {
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false,
            margin = new RectOffset(0, 0, 1, 1),
            padding = new RectOffset(0, 0, 0, 0)
        };
        _toolbarValueStyle = new GUIStyle(_bodyStyle)
        {
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false,
            margin = new RectOffset(0, 0, 1, 1),
            padding = new RectOffset(0, 0, 0, 0)
        };
        _quantityLabelStyle = new GUIStyle(_bodyStyle)
        {
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false,
            margin = new RectOffset(0, 0, 1, 1),
            padding = new RectOffset(0, 0, 0, 0)
        };
        _textFieldStyle = new GUIStyle(GUI.skin.textField)
        {
            alignment = TextAnchor.MiddleLeft,
            border = new RectOffset(1, 1, 1, 1),
            padding = new RectOffset(6, 6, 3, 3),
            margin = new RectOffset(2, 2, 1, 1),
            fontSize = 12,
            wordWrap = false
        };
        var fieldTextColor = new Color32(0xd5, 0xd9, 0xdc, 0xff);
        SetButtonState(_textFieldStyle.normal, _neutralButtonTexture, fieldTextColor);
        SetButtonState(_textFieldStyle.hover, _hoverButtonTexture, Color.white);
        SetButtonState(_textFieldStyle.active, _pressedButtonTexture, Color.white);
        SetButtonState(_textFieldStyle.focused, _hoverButtonTexture, Color.white);
        SetButtonState(_textFieldStyle.onNormal, _neutralButtonTexture, fieldTextColor);
        SetButtonState(_textFieldStyle.onHover, _hoverButtonTexture, Color.white);
        SetButtonState(_textFieldStyle.onActive, _pressedButtonTexture, Color.white);
        SetButtonState(_textFieldStyle.onFocused, _hoverButtonTexture, Color.white);
        _durationSliderTrackStyle = new GUIStyle(GUI.skin.box)
        {
            border = new RectOffset(1, 1, 1, 1),
            margin = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(0, 0, 0, 0),
            fixedHeight = DurationSliderTrackHeight
        };
        SetButtonState(_durationSliderTrackStyle.normal, _neutralButtonTexture, Color.white);
        SetButtonState(_durationSliderTrackStyle.hover, _neutralButtonTexture, Color.white);
        SetButtonState(_durationSliderTrackStyle.active, _neutralButtonTexture, Color.white);
        SetButtonState(_durationSliderTrackStyle.focused, _neutralButtonTexture, Color.white);
        _durationSliderStyle = new GUIStyle(GUI.skin.horizontalSlider)
        {
            border = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(0, 0, 0, 0),
            fixedHeight = DurationSliderThumbHeight
        };
        SetButtonState(_durationSliderStyle.normal, null, Color.white);
        SetButtonState(_durationSliderStyle.hover, null, Color.white);
        SetButtonState(_durationSliderStyle.active, null, Color.white);
        SetButtonState(_durationSliderStyle.focused, null, Color.white);
        _durationSliderThumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb)
        {
            border = new RectOffset(1, 1, 1, 1),
            margin = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(0, 0, 0, 0),
            fixedWidth = DurationSliderThumbWidth,
            fixedHeight = DurationSliderThumbHeight
        };
        SetButtonState(_durationSliderThumbStyle.normal, _neutralButtonTexture, Color.white);
        SetButtonState(_durationSliderThumbStyle.hover, _hoverButtonTexture, Color.white);
        SetButtonState(_durationSliderThumbStyle.active, _pressedButtonTexture, Color.white);
        SetButtonState(_durationSliderThumbStyle.focused, _hoverButtonTexture, Color.white);
        SetButtonState(_durationSliderThumbStyle.onNormal, _neutralButtonTexture, Color.white);
        SetButtonState(_durationSliderThumbStyle.onHover, _hoverButtonTexture, Color.white);
        SetButtonState(_durationSliderThumbStyle.onActive, _pressedButtonTexture, Color.white);
        SetButtonState(_durationSliderThumbStyle.onFocused, _hoverButtonTexture, Color.white);
        _tableHeaderStyle = new GUIStyle(_mutedStyle)
        {
            fontSize = 9,
            fontStyle = FontStyle.Bold,
            wordWrap = false,
            margin = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(0, 0, 0, 0)
        };
        _tablePropertyStyle = new GUIStyle(_bodyStyle)
        {
            fontSize = 11,
            wordWrap = true,
            margin = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(0, 0, 0, 0)
        };
        _tableCellStyle = new GUIStyle(_bodyStyle)
        {
            fontSize = 11,
            wordWrap = false,
            margin = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(0, 0, 0, 0)
        };
        _tableEmptyStyle = new GUIStyle(_tableCellStyle)
        {
            normal = { textColor = new Color(0.55f, 0.55f, 0.55f, 1f) }
        };
        _panelStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(8, 8, 6, 6),
            normal = { background = _panelTexture }
        };
        _rowStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(8, 8, 7, 7),
            normal = { background = _rowTexture }
        };
        _popupStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(0, 0, 0, 0),
            normal = { background = _popupTexture }
        };
        _neutralButtonStyle = CreateButtonStyle(
            _neutralButtonTexture,
            _hoverButtonTexture,
            _pressedButtonTexture,
            TextAnchor.MiddleCenter,
            11,
            new RectOffset(7, 7, 3, 3),
            new RectOffset(2, 2, 1, 1));
        _selectedButtonStyle = CreateButtonStyle(
            _selectedButtonTexture,
            _selectedHoverButtonTexture,
            _pressedButtonTexture,
            TextAnchor.MiddleCenter,
            11,
            new RectOffset(7, 7, 3, 3),
            new RectOffset(2, 2, 1, 1));
        _primaryButtonStyle = CreateButtonStyle(
            _primaryButtonTexture,
            _primaryHoverButtonTexture,
            _pressedButtonTexture,
            TextAnchor.MiddleCenter,
            12,
            new RectOffset(7, 7, 3, 3),
            new RectOffset(2, 2, 1, 1));
        _disabledButtonStyle = CreateButtonStyle(
            _disabledButtonTexture,
            _disabledButtonTexture,
            _disabledButtonTexture,
            TextAnchor.MiddleCenter,
            11,
            new RectOffset(7, 7, 3, 3),
            new RectOffset(2, 2, 1, 1),
            new Color32(0x6f, 0x77, 0x7c, 0xff));
        _favoriteButtonStyle = CreateButtonStyle(
            null,
            null,
            null,
            TextAnchor.MiddleCenter,
            18,
            new RectOffset(0, 0, 0, 0),
            new RectOffset(0, 0, 0, 0),
            new Color32(0xd5, 0xd9, 0xdc, 0xff));
        _popupButtonStyle = CreateButtonStyle(
            _neutralButtonTexture,
            _hoverButtonTexture,
            _pressedButtonTexture,
            TextAnchor.MiddleLeft,
            11,
            new RectOffset(8, 6, 3, 3),
            new RectOffset(0, 0, 0, 0));
        _popupSelectedButtonStyle = CreateButtonStyle(
            _selectedButtonTexture,
            _selectedHoverButtonTexture,
            _pressedButtonTexture,
            TextAnchor.MiddleLeft,
            11,
            new RectOffset(8, 6, 3, 3),
            new RectOffset(0, 0, 0, 0));
    }

    private GUIStyle CreateButtonStyle(
        Texture2D normal,
        Texture2D hover,
        Texture2D active,
        TextAnchor alignment,
        int fontSize,
        RectOffset padding,
        RectOffset margin,
        Color? textColor = null)
    {
        var style = new GUIStyle(GUI.skin.button)
        {
            alignment = alignment,
            border = new RectOffset(1, 1, 1, 1),
            padding = padding,
            margin = margin,
            fontSize = fontSize,
            fontStyle = FontStyle.Normal,
            richText = false,
            wordWrap = false,
            imagePosition = ImagePosition.TextOnly
        };

        var normalText = textColor ?? new Color32(0xd5, 0xd9, 0xdc, 0xff);
        var hoverText = textColor ?? Color.white;
        SetButtonState(style.normal, normal, normalText);
        SetButtonState(style.hover, hover, hoverText);
        SetButtonState(style.active, active, hoverText);
        SetButtonState(style.focused, normal, normalText);
        SetButtonState(style.onNormal, normal, normalText);
        SetButtonState(style.onHover, hover, hoverText);
        SetButtonState(style.onActive, active, hoverText);
        SetButtonState(style.onFocused, normal, normalText);
        return style;
    }

    private GUIStyle GetButtonStyle(GUIStyle enabled, GUIStyle disabled = null)
    {
        return GUI.enabled ? enabled : disabled ?? _disabledButtonStyle;
    }

    private static void SetButtonState(GUIStyleState state, Texture2D background, Color textColor)
    {
        state.background = background;
        state.textColor = textColor;
    }

    private static Texture2D CreateTexture(Color color)
    {
        var texture = new Texture2D(1, 1)
        {
            name = "InjectorManagerSolidColor",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixel(0, 0, color);
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D CreateBorderedTexture(Color fill, Color border)
    {
        var texture = new Texture2D(3, 3)
        {
            name = "InjectorManagerFlatButton",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        for (var y = 0; y < texture.height; y++)
        {
            for (var x = 0; x < texture.width; x++)
            {
                var isBorder = x == 0 || x == texture.width - 1 || y == 0 || y == texture.height - 1;
                texture.SetPixel(x, y, isBorder ? border : fill);
            }
        }

        texture.Apply(false, true);
        return texture;
    }

    private static void DestroyTexture(ref Texture2D texture)
    {
        if (texture != null)
        {
            Object.Destroy(texture);
            texture = null;
        }
    }

    private static bool IsUnavailable(InjectorInfo injector)
    {
        return injector.Market.Status is MarketStatus.NotSellable or MarketStatus.NoRoubleOffers;
    }

    private static string FormatDuration(float seconds)
    {
        var rounded = Math.Max(0, (int)Math.Round(seconds));
        if (rounded < 60)
        {
            return $"{rounded}s";
        }

        if (rounded < 3600)
        {
            var minutes = rounded / 60;
            var remainder = rounded % 60;
            return remainder == 0 ? $"{minutes}m" : $"{minutes}m {remainder}s";
        }

        var hours = rounded / 3600;
        var minutesRemainder = rounded % 3600 / 60;
        return minutesRemainder == 0 ? $"{hours}h" : $"{hours}h {minutesRemainder}m";
    }

    private enum DropdownKind
    {
        None,
        Property,
        Sort
    }
}
