using System.ComponentModel;
using System.Drawing.Drawing2D;

using AntdUI;

namespace FbSample.Views.Controls;

/// <summary>Draws the current page consistently in the navigation rail and its child flyout.</summary>
public sealed class NavigationMenu : AntdUI.Menu, IMessageFilter
{
    private static readonly Color s_indicatorColor = Color.FromArgb(250, 35, 59);

    private string _currentPageKey = "Dashboard";
    private bool _dark;
    private bool _isFlyout;
    private bool _showKeyboardFocus;
    private bool _mouseDownHitItem;
    private AntdUI.MenuItem? _hoveredItem;
    private AntdUI.MenuItem? _previousHoveredItem;
    private AntdUI.MenuItem? _pressedItem;
    private AntdUI.MenuItem? _tooltipItem;
    private Form? _tooltip;
    private AntdUI.MenuItem? _flyoutParent;
    private NavigationMenu? _flyoutOwner;
    private NavigationMenu? _flyoutMenu;
    private AntdUI.Panel? _flyoutPanel;
    private NavigationFlyoutWindow? _flyoutWindow;
    private System.Windows.Forms.Timer? _hoverTimer;
    private long _hoverStarted;
    private float _hoverProgress = 1F;

    /// <summary>Creates a menu without opening windows or registering runtime services.</summary>
    public NavigationMenu()
    {
        // Keep native immediate arrow-key selection while allowing Tab to reach this menu.
        SetStyle(ControlStyles.Selectable, true);
        Gap = 12;
        itemMargin = 2;
        Radius = 5;
        Padding = new Padding(4);
        IconRatio = 1F;
        // Collapsed groups belong to the click flyout; preserve their expanded-sidebar state.
        SelectChanging += (_, e) => !Collapsed || !e.Value.CanExpand;
        ApplyPalette(false);
    }

    internal void InitializeFlyout(AntdUI.Panel panel, NavigationMenu menu)
    {
        _flyoutPanel = panel;
        _flyoutMenu = menu;
        menu._isFlyout = true;
        menu._flyoutOwner = this;
    }

    internal NavigationFlyoutWindow? FlyoutWindow => _flyoutWindow;

    internal void DrawFlyout(Canvas canvas) => OnDraw(new DrawEventArgs(canvas, ClientRectangle));

    internal void FlyoutMouseDown(MouseEventArgs e) => OnMouseDown(e);

    internal void FlyoutMouseMove(MouseEventArgs e) => OnMouseMove(e);

    internal void FlyoutMouseUp(MouseEventArgs e)
    {
        if (_pressedItem is null)
        {
            _mouseDownHitItem = false;
            OnTouchCancel();
            ScrollBar.MouseUp();
            return;
        }

        OnMouseUp(e);
    }

    internal void FlyoutMouseLeave() => OnMouseLeave(EventArgs.Empty);

    internal void FlyoutMouseWheel(MouseEventArgs e) => OnMouseWheel(e);

    internal void AlignIcons(int collapsedWidth)
    {
        int left = Padding.Right;
        if (!Collapsed)
        {
            int iconWidth = this.GDI(canvas => (int)(canvas.MeasureString(Config.NullText, Font).Height * IconRatio));
            left += (collapsedWidth - (Padding.Right * 2) - iconWidth) / 2 - (int)(Gap!.Value * Dpi);
        }

        if (Padding.Left != left)
        {
            Padding = new Padding(left, Padding.Top, Padding.Right, Padding.Bottom);
            // Menu recalculates item rectangles on size changes, not on padding changes.
            base.OnSizeChanged(EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets the stable route name whose visible menu row carries the indicator.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string CurrentPageKey
    {
        get => _currentPageKey;
        set
        {
            _currentPageKey = value;
            if (_flyoutMenu is not null)
            {
                _flyoutMenu.CurrentPageKey = value;
            }

            Invalidate();
        }
    }

    /// <summary>Applies the agreed navigation colors for the current application theme.</summary>
    public void ApplyPalette(bool dark)
    {
        if (_dark != dark)
        {
            CloseFlyout();
        }

        _dark = dark;
        BackColor = Collapsed && !_isFlyout
            ? dark ? Color.FromArgb(32, 37, 44) : Color.FromArgb(239, 244, 249)
            : dark ? Color.FromArgb(40, 40, 40) : Color.FromArgb(247, 247, 247);
        ForeColor = dark ? Color.FromArgb(238, 238, 238) : Color.FromArgb(38, 38, 38);
        ForeActive = ForeColor;
        // Menu's dark hover and collapsed selection differ; paint these backgrounds together below.
        BackActive = Color.Transparent;
        BackHover = Color.Transparent;
        _flyoutMenu?.ApplyPalette(dark);
        Invalidate();
    }

    /// <summary>Closes the runtime flyout while retaining its designer-owned drawing controls.</summary>
    public void CloseFlyout()
    {
        CloseLeafTooltip();
        Application.RemoveMessageFilter(this);
        if (_flyoutMenu is not null)
        {
            _flyoutMenu.ItemClick -= FlyoutItemClicked;
            _flyoutMenu._hoverTimer?.Stop();
            _flyoutMenu._showKeyboardFocus = false;
        }

        _flyoutParent = null;
        NavigationFlyoutWindow? window = _flyoutWindow;
        _flyoutWindow = null;
        window?.Dispose();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnDraw(DrawEventArgs e)
    {
        AntdUI.MenuItem? current = GetVisibleItem(FindName(_currentPageKey));
        if (current is not null && (!Collapsed || !current.CanExpand))
        {
            DrawRow(e.Canvas, current, _dark ? Color.FromArgb(54, 56, 60)
                : _isFlyout ? Color.FromArgb(237, 237, 237) : Color.FromArgb(238, 238, 238));
        }

        DrawHover(e.Canvas, _previousHoveredItem, current, 1F - _hoverProgress);
        DrawHover(e.Canvas, _hoveredItem, current, _hoverProgress);

        if (_pressedItem is not null && _pressedItem == _hoveredItem)
        {
            DrawRow(e.Canvas, _pressedItem, _dark ? Color.FromArgb(65, 68, 73) : Color.FromArgb(226, 226, 226));
        }

        if (!_dark && !Collapsed && current is { Enabled: true })
        {
            using var canvas = new CurrentPageCanvas(e.Graphics!, e.Canvas.Dpi, current.Rect("Text"));
            base.OnDraw(new DrawEventArgs(canvas, e.Rect));
        }
        else
        {
            base.OnDraw(e);
        }

        bool keyboardFocus = _isFlyout ? _flyoutOwner!.Focused : Focused && _flyoutWindow is null;
        if (keyboardFocus && _showKeyboardFocus && GetVisibleItem(SelectItem) is { } focused)
        {
            Rectangle focusRow = focused.Rect(0, ScrollBar.ValueY);
            focusRow.Inflate(-2, -2);
            float diameter = Radius * 2 * (DeviceDpi / 96F);
            using var focusPath = new GraphicsPath();
            focusPath.AddArc(focusRow.Right - diameter, focusRow.Top,
                diameter, diameter, -90, 90);
            focusPath.AddArc(focusRow.Right - diameter, focusRow.Bottom - diameter,
                diameter, diameter, 0, 90);
            focusPath.AddArc(focusRow.Left, focusRow.Bottom - diameter,
                diameter, diameter, 90, 90);
            focusPath.AddArc(focusRow.Left, focusRow.Top,
                diameter, diameter, 180, 90);
            focusPath.CloseFigure();
            e.Canvas.Draw(_dark ? Color.FromArgb(161, 166, 174) : Color.FromArgb(110, 116, 124),
                DeviceDpi / 96F, DashStyle.Dash, focusPath);
        }

        if (current is null || (Collapsed && _flyoutWindow is not null && current == _flyoutParent
            && _flyoutMenu?.FindName(_currentPageKey) is { Visible: true }))
        {
            return;
        }

        Rectangle row = current.Rect(0, ScrollBar.ValueY);
        float scale = DeviceDpi / 96F;
        AntdUI.MenuItem root = current;
        while (root.ParentItem is { } parent)
        {
            root = parent;
        }

        int indent = Collapsed ? 0 : current.Rect("Icon").X - root.Rect("Icon").X;
        var indicator = new RectangleF(row.X + indent + (3 * scale),
            row.Y + ((row.Height - (16 * scale)) / 2), 3 * scale, 16 * scale);
        using GraphicsPath path = indicator.RoundPath(1.5F * scale);
        e.Canvas.Fill(s_indicatorColor, path);
    }

    /// <inheritdoc />
    protected override bool OnMouseHover(int x, int y)
    {
        if (DesignMode)
        {
            return false;
        }

        AntdUI.MenuItem? item = HitTest(x, y);
        if (Collapsed)
        {
            if (_flyoutParent is null && item is not null && _tooltipItem != item)
            {
                CloseLeafTooltip();
                _tooltipItem = item;
                _tooltip = AntdUI.Tooltip.open(new AntdUI.Tooltip.Config(this, item.Text
                    ?? throw new InvalidOperationException("The navigation item has no display text."))
                {
                    Font = item.Font ?? Font,
                    Offset = item.Rect(0, ScrollBar.ValueY),
                    ArrowAlign = TAlign.Right
                });
            }

            return item is not null;
        }

        return base.OnMouseHover(x, y);
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        SetHoveredItem(HitTest(e.X, e.Y));
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (!_isFlyout)
        {
            Focus();
        }

        _pressedItem = HitTest(e.X, e.Y);
        _mouseDownHitItem = _pressedItem is not null;
        SetHoveredItem(_pressedItem);
        _showKeyboardFocus = false;
        base.OnMouseDown(e);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseUp(MouseEventArgs e)
    {
        AntdUI.MenuItem? pressed = _pressedItem;
        base.OnMouseUp(e);
        _pressedItem = null;
        _mouseDownHitItem = false;
        if (e.Button == MouseButtons.Left && Collapsed && pressed is { CanExpand: true, Enabled: true }
            && pressed == HitTest(e.X, e.Y))
        {
            if (_flyoutParent is not null)
            {
                CloseFlyout();
            }
            else
            {
                OpenFlyout(pressed);
            }
        }

        Invalidate();
    }

    /// <inheritdoc />
    protected override bool OnTouchUp() => base.OnTouchUp() && _mouseDownHitItem;

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHoveredItem(null);
        CloseLeafTooltip();
        _pressedItem = null;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        _showKeyboardFocus = true;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    /// <inheritdoc />
    protected override bool ProcessCmdKey(ref System.Windows.Forms.Message msg, Keys keyData)
    {
        _showKeyboardFocus = true;
        if (keyData is Keys.Left or Keys.Escape && (_flyoutParent is not null || _flyoutOwner is not null))
        {
            (_flyoutOwner ?? this).CloseFlyoutAndFocus();
            return true;
        }

        if (_flyoutWindow is not null)
        {
            if (keyData is Keys.Up or Keys.Down or Keys.Enter)
            {
                _flyoutMenu!.ProcessCmdKey(ref msg, keyData);
                _flyoutMenu.Invalidate();
                return true;
            }

            if (keyData is Keys.Tab or (Keys.Shift | Keys.Tab))
            {
                CloseFlyout();
            }
        }

        if (!_isFlyout && keyData is Keys.Up or Keys.Down or Keys.Right or Keys.Enter)
        {
            var visibleItems = new List<AntdUI.MenuItem>();
            foreach (AntdUI.MenuItem item in Items)
            {
                if (!item.Visible || !item.Enabled)
                {
                    continue;
                }

                visibleItems.Add(item);
                if (!Collapsed && item.Expand)
                {
                    visibleItems.AddRange(item.Sub.Where(static child => child.Visible && child.Enabled));
                }
            }

            AntdUI.MenuItem? selected = SelectItem;
            if (selected is { Visible: true, Enabled: true, ParentItem: { } parent }
                && (Collapsed || !parent.Expand))
            {
                // A collapsed group represents its selected child; a search-hidden child does not.
                selected = parent;
            }

            int index = selected is null ? -1 : visibleItems.IndexOf(selected);
            if (keyData is Keys.Up or Keys.Down)
            {
                int next = index < 0
                    ? keyData == Keys.Down ? 0 : visibleItems.Count - 1
                    : index + (keyData == Keys.Down ? 1 : -1);
                if (next >= 0 && next < visibleItems.Count)
                {
                    Select(visibleItems[next]);
                }

                return true;
            }

            if (index < 0)
            {
                return true;
            }

            AntdUI.MenuItem target = visibleItems[index];
            if (target.CanExpand)
            {
                if (Collapsed)
                {
                    OpenFlyout(target, true);
                }
                else
                {
                    target.Expand = keyData == Keys.Right || !target.Expand;
                }
            }
            else if (keyData == Keys.Enter)
            {
                Select(target);
            }

            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CloseFlyout();
            _hoverTimer?.Dispose();
        }

        base.Dispose(disposing);
    }

    bool IMessageFilter.PreFilterMessage(ref System.Windows.Forms.Message m)
    {
        if (_flyoutParent is null)
        {
            return false;
        }

        const int keyDown = 0x0100;
        const int leftButtonDown = 0x0201;
        const int rightButtonDown = 0x0204;
        const int middleButtonDown = 0x0207;
        const int nonClientLeftButtonDown = 0x00A1;
        if (m.Msg == keyDown && (Keys)m.WParam == Keys.Escape)
        {
            CloseFlyoutAndFocus();
            return true;
        }

        if (m.Msg is leftButtonDown or rightButtonDown or middleButtonDown or nonClientLeftButtonDown)
        {
            var location = new Point(unchecked((int)m.LParam));
            // Let a completed parent click close the open menu in OnMouseUp.
            bool parentClicked = m.HWnd == Handle && HitTest(location.X, location.Y) is { } parent
                && (parent == _flyoutParent
                    || m.Msg == leftButtonDown && parent is { CanExpand: true, Enabled: true });
            if (m.HWnd != _flyoutWindow!.Handle && !parentClicked)
            {
                CloseFlyout();
            }
        }

        // Outside clicks must still reach their original target.
        return false;
    }

    private AntdUI.MenuItem? GetVisibleItem(AntdUI.MenuItem? item)
    {
        if (item is null)
        {
            return null;
        }

        AntdUI.MenuItem? visible = item.Visible ? item : null;
        for (AntdUI.MenuItem? parent = item.ParentItem; parent is not null; parent = parent.ParentItem)
        {
            if (!parent.Visible)
            {
                visible = null;
            }
            // Keep the indicator on a visible ancestor until its expansion animation has completed.
            else if (visible is null || Collapsed || !parent.Expand || parent.GetArrowProg() < 1F)
            {
                visible = parent;
            }
        }

        return visible;
    }

    private void DrawRow(Canvas canvas, AntdUI.MenuItem item, Color color)
    {
        if (!item.Visible || !item.Enabled)
        {
            return;
        }

        Rectangle row = item.Rect(0, ScrollBar.ValueY);
        using GraphicsPath path = row.RoundPath(Radius * (DeviceDpi / 96F));
        canvas.Fill(color, path);
    }

    private void DrawHover(Canvas canvas, AntdUI.MenuItem? item, AntdUI.MenuItem? current, float opacity)
    {
        if (item is not null && (item != current || Collapsed && item.CanExpand) && opacity > 0F)
        {
            Color color = _dark ? Color.FromArgb(51, 54, 59) : Color.FromArgb(239, 239, 239);
            DrawRow(canvas, item, Color.FromArgb((int)(255 * opacity), color));
        }
    }

    private void SetHoveredItem(AntdUI.MenuItem? item)
    {
        if (_hoveredItem == item)
        {
            return;
        }

        _previousHoveredItem = _hoveredItem;
        _hoveredItem = item;
        if (_tooltipItem != item)
        {
            CloseLeafTooltip();
        }

        _hoverStarted = Environment.TickCount64;
        _hoverProgress = Config.Animation ? 0F : 1F;
        if (Config.Animation)
        {
            if (_hoverTimer is null)
            {
                _hoverTimer = new System.Windows.Forms.Timer { Interval = 15 };
                _hoverTimer.Tick += HoverAnimationElapsed;
            }

            _hoverTimer.Start();
        }

        Invalidate();
    }

    private void HoverAnimationElapsed(object? sender, EventArgs e)
    {
        _hoverProgress = Config.Animation
            ? Math.Clamp((Environment.TickCount64 - _hoverStarted) / 100F, 0F, 1F)
            : 1F;
        if (_hoverProgress >= 1F)
        {
            _hoverTimer?.Stop();
            _previousHoveredItem = null;
        }

        Invalidate();
    }

    private void OpenFlyout(AntdUI.MenuItem parent, bool focus = false)
    {
        if (_flyoutParent == parent)
        {
            if (focus)
            {
                _flyoutMenu!._showKeyboardFocus = true;
                _flyoutMenu.Invalidate();
                Focus();
            }

            return;
        }

        CloseFlyout();
        CloseTip();
        AntdUI.Panel panel = _flyoutPanel!;
        NavigationMenu content = _flyoutMenu!;
        Control source = panel.Parent!;
        Form owner = FindForm()!;
        panel.Shadow = Config.ShadowEnabled ? 8 : 0;
        // Hidden designer controls provide the native layout before the layered window's first frame.
        _ = source.Handle;
        _ = panel.Handle;
        _ = content.Handle;
        content.Font = Font;
        content._hoverTimer?.Stop();
        content._hoveredItem = content._previousHoveredItem = content._pressedItem = null;
        content._hoverProgress = 1F;
        content._showKeyboardFocus = focus;
        content.USelect();
        content.Items.Clear();
        foreach (AntdUI.MenuItem child in parent.Sub)
        {
            if (child.Visible)
            {
                content.Items.Add(new AntdUI.MenuItem
                {
                    Name = child.Name,
                    Text = child.Text,
                    IconSvg = child.IconSvg,
                    Enabled = child.Enabled
                });
            }
        }

        content.CurrentPageKey = _currentPageKey;
        content.ApplyPalette(_dark);
        AntdUI.MenuItem? selected = content.FindName(_currentPageKey);
        if (selected is null && focus)
        {
            selected = content.Items[0];
        }

        if (selected is not null)
        {
            content.Select(selected, false);
        }

        float scale = DeviceDpi / 96F;
        int shadow = (int)(panel.Shadow * scale);
        int contentHeight = ((parent.Rect().Height + (int)(2 * scale)) * content.Items.Count)
            + content.Padding.Vertical;
        source.Height = contentHeight + panel.Height - panel.DisplayRectangle.Height;
        panel.PerformLayout();
        // Measure every group's labels, including those currently hidden by search.
        int textWidth = content.GDI(canvas => Items.SelectMany(item => item.Sub)
            .Max(child => canvas.MeasureText(child.Text, content.Font).Width));
        source.Width = textWidth + content.ClientSize.Width - content.Items[0].Rect("Text").Width
            + panel.Width - panel.DisplayRectangle.Width;
        panel.PerformLayout();
        Point anchor = owner.PointToClient(PointToScreen(new Point(Parent!.ClientSize.Width - Left,
            parent.Rect(0, ScrollBar.ValueY).Y)));
        source.Location = new Point(
            Math.Clamp(anchor.X + (int)Math.Round(8 * scale) - shadow, 0, owner.ClientSize.Width - panel.Width),
            Math.Clamp(anchor.Y - panel.DisplayRectangle.Top - content.Padding.Top,
                0, owner.ClientSize.Height - panel.Height));
        _flyoutParent = parent;
        content.ItemClick += FlyoutItemClicked;
        _flyoutWindow = new NavigationFlyoutWindow(owner, panel, content);
        _flyoutWindow.ShowFlyout();
        if (focus)
        {
            Focus();
        }

        Application.AddMessageFilter(this);
        Invalidate();
    }

    private void CloseFlyoutAndFocus()
    {
        CloseFlyout();
        _showKeyboardFocus = true;
        Focus();
    }

    private void CloseLeafTooltip()
    {
        _tooltip?.Dispose();
        _tooltip = null;
        _tooltipItem = null;
    }

    private void FlyoutItemClicked(object sender, MenuItemEventArgs e)
    {
        AntdUI.MenuItem item = FindName(e.Item.Name
            ?? throw new InvalidOperationException("The flyout item has no navigation route."))
            ?? throw new InvalidOperationException("The flyout route has no main navigation item.");
        var expandedGroups = Items.Where(group => group.Sub.Count > 0).Select(group => (Item: group, group.Expand)).ToArray();
        Select(item, false);
        foreach (var group in expandedGroups)
        {
            group.Item.Expand = group.Expand;
        }

        CloseFlyout();
    }

    // ForeActive also colors ancestors and keyboard selection. Keep native drawing and tint only current-page text.
    private sealed class CurrentPageCanvas(Graphics graphics, float dpi, Rectangle currentText)
        : AntdUI.Core.CanvasGDI(graphics, dpi), Canvas
    {
        private readonly GraphicsState _state = graphics.Save();

        // Restore this drawing scope without disposing the paint event's borrowed graphics.
        public new void Dispose() => g.Restore(_state);

        void Canvas.DrawText(string? text, Font font, Brush brush, Rectangle rect, FormatFlags format)
        {
            if (rect == currentText)
            {
                base.DrawText(text, font, Color.FromArgb(36, 36, 36), rect, format);
            }
            else
            {
                base.DrawText(text, font, brush, rect, format);
            }
        }
    }

}
