using System.Collections.Concurrent;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text;

using AntdUI;

using FbSample.Localization;
using FbSample.Views;
using FbSample.Views.Controls;
using FbSample.Views.Pages;

[assembly: DoNotParallelize]

namespace FbSample.Tests;

/// <summary>Exercises the desktop shell on STA threads without showing application windows.</summary>
[STATestClass]
[TestCategory("UIIntegration")]
public sealed class DesktopUiTests
{
    private static readonly string[] s_parentRoutes = ["Dashboard", "Devices", "Monitor", "Logs", "Debug", "Settings"];
    private static readonly string[] s_leafRoutes =
        ["Dashboard", "Devices", "Monitor", "LogsPage1", "LogsPage2", "DebugPage1", "Settings"];
    private static readonly int[] s_childCounts = [0, 0, 0, 2, 1, 0];
    private static readonly string[] s_supportedLanguages = ["en-US", "zh-CN", "zh-TW"];

    private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;
    private readonly CultureInfo? _originalDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;
    private readonly CultureInfo? _originalDefaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
    private readonly ILocalization? _originalLocalizationProvider = AntdUI.Localization.Provider;
    private readonly IThemeConfig? _originalThemeConfiguration = Config.ThemeConfig;
    private readonly TextRenderingHint? _originalTextRenderingHint = Config.TextRenderingHint;
    private readonly bool _originalTextRenderingQuality = Config.TextRenderingHighQuality;
    private readonly bool _originalAnimation = Config.Animation;
    private readonly bool _originalLightTheme = Config.IsLight;
    private readonly bool _originalShadows = Config.ShadowEnabled;
    private readonly bool _originalScrollbars = Config.ScrollBarHide;
    private readonly bool _originalShowInWindow = Config.ShowInWindow;
    private readonly int _originalWindowOffset = Config.NoticeWindowOffsetXY;

    /// <summary>Uses the production DPI and font configuration before any test creates controls.</summary>
    /// <param name="context">The test assembly context.</param>
    [AssemblyInitialize]
    public static void InitializeAssembly(TestContext context)
    {
        global::ApplicationConfiguration.Initialize();
        context.WriteLine("The test host uses the production PerMonitorV2 and default font configuration.");
    }

    /// <summary>Resets mutable UI configuration before each isolated test.</summary>
    [TestInitialize]
    public void Initialize()
    {
        AppLocalization.Initialize();
        AppLocalization.SetLanguage("en-US");
        Config.Animation = false;
        Config.IsLight = true;
        Config.ShadowEnabled = true;
        Config.ScrollBarHide = true;
        Config.ShowInWindow = false;
        Config.NoticeWindowOffsetXY = 0;
        Config.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        Config.TextRenderingHighQuality = true;
        Config.ThemeConfig = new IThemeConfig().Dark("#000", "#fff").Light("#fff", "#000").FormBorderColor();
    }

    /// <summary>Restores the host configuration and culture after each test.</summary>
    [TestCleanup]
    public void Cleanup()
    {
        Config.Animation = _originalAnimation;
        Config.IsLight = _originalLightTheme;
        Config.ShadowEnabled = _originalShadows;
        Config.ScrollBarHide = _originalScrollbars;
        Config.ShowInWindow = _originalShowInWindow;
        Config.NoticeWindowOffsetXY = _originalWindowOffset;
        Config.ThemeConfig = _originalThemeConfiguration;
        Config.TextRenderingHint = _originalTextRenderingHint;
        Config.TextRenderingHighQuality = _originalTextRenderingQuality;
        CultureInfo.CurrentCulture = _originalCulture;
        CultureInfo.CurrentUICulture = _originalUiCulture;
        CultureInfo.DefaultThreadCurrentCulture = _originalDefaultCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _originalDefaultUiCulture;
        AntdUI.Localization.SetLanguage(_originalUiCulture.Name);
        AntdUI.Localization.Provider = _originalLocalizationProvider;
    }

    /// <summary>Ensures the designer constructor contains the complete shell without runtime setup.</summary>
    [TestMethod]
    public void ConstructorWithoutRuntimeInitializationCreatesEditableShellAndBlankDashboard()
    {
        using var form = new MainForm();
        AntdUI.Menu menu = GetControl<AntdUI.Menu>(form, "_navigationMenu");
        AntdUI.Panel contentPanel = GetControl<AntdUI.Panel>(form, "_contentPanel");

        CollectionAssert.AreEqual(
            s_parentRoutes,
            menu.Items.Select(static item => item.Name).ToArray());
        CollectionAssert.AreEqual(
            s_childCounts,
            menu.Items.Select(static item => item.Sub.Count).ToArray());
        Assert.AreEqual("LogsPage1", menu.Items[3].Sub[0].Name);
        Assert.AreEqual("LogsPage2", menu.Items[3].Sub[1].Name);
        Assert.AreEqual("DebugPage1", menu.Items[4].Sub[0].Name);
        Assert.AreEqual("Settings", menu.Items[5].Name);
        Assert.AreEqual("SettingOutlined", menu.Items[5].IconSvg);
        Assert.AreEqual("InfoCircleOutlined", GetControl<AntdUI.Button>(form, "_aboutButton").IconSvg);
        Assert.AreEqual(1, contentPanel.Controls.Count);
        Assert.AreEqual("Dashboard", contentPanel.Controls[0].Name);
        Assert.AreEqual(DockStyle.Fill, contentPanel.Dock);
        AssertBlankPage(contentPanel.Controls[0]);
        Assert.IsFalse(form.Controls.OfType<Tabs>().Any());
        Assert.IsFalse(GetControl<AntdUI.Button>(form, "_backButton").Enabled);
        Avatar logo = GetControl<Avatar>(form, "_navigationLogo");
        Assert.IsFalse(logo.TabStop);
        Assert.AreEqual(0, logo.BackColor.A);
        Assert.IsInstanceOfType<Bitmap>(logo.Image);
        var logoImage = (Bitmap)logo.Image;
        bool hasTransparentPixel = false;
        bool hasVisiblePixel = false;
        for (int y = 0; y < logoImage.Height; y++)
        {
            for (int x = 0; x < logoImage.Width; x++)
            {
                byte alpha = logoImage.GetPixel(x, y).A;
                hasTransparentPixel |= alpha == 0;
                hasVisiblePixel |= alpha > 0;
            }
        }

        Assert.IsTrue(hasTransparentPixel, "The navigation logo must retain the source image's transparent background.");
        Assert.IsTrue(hasVisiblePixel, "The navigation logo must contain visible artwork.");
        PageHeader titleBar = GetControl<PageHeader>(form, "_titleBar");
        Assert.IsTrue(string.IsNullOrEmpty(titleBar.Text));
        Assert.IsTrue(string.IsNullOrEmpty(titleBar.SubText));
        Assert.IsFalse(titleBar.ShowIcon);
        Assert.AreEqual("FbSample", form.Text);
        Assert.IsNotNull(form.Icon);
        Assert.AreSame(GetControl<AntdUI.Panel>(form, "_workspacePanel"), contentPanel.Parent);
        AntdUI.Panel flyoutPanel = GetControl<AntdUI.Panel>(form, "_navigationFlyoutPanel");
        NavigationMenu flyoutMenu = GetControl<NavigationMenu>(form, "_navigationFlyoutMenu");
        Assert.IsInstanceOfType<UserControl>(flyoutPanel.Parent);
        Assert.AreSame(form, flyoutPanel.Parent.Parent);
        Assert.AreEqual(AutoScaleMode.Dpi, ((UserControl)flyoutPanel.Parent).AutoScaleMode);
        Assert.AreEqual(DockStyle.Fill, flyoutPanel.Dock);
        Assert.AreSame(flyoutPanel, flyoutMenu.Parent);
        Assert.AreEqual(DockStyle.Fill, flyoutMenu.Dock);
        Assert.AreEqual(Color.FromArgb(225, 230, 234), GetControl<AntdUI.Panel>(form, "_navigationDivider").Back);
        Assert.AreEqual(Color.FromArgb(204, 204, 204), flyoutPanel.BorderColor);
        Assert.IsFalse(flyoutPanel.Visible);
        Assert.IsFalse(form.Visible);
    }

    /// <summary>Checks the standalone flyout designer owns its controls, editable styles and DPI scaling.</summary>
    [TestMethod]
    public void NavigationFlyoutDesignerKeepsItsOwnDpiLayoutAndEditableStyles()
    {
        using var source = new NavigationFlyoutControl();
        Assert.AreEqual(AutoScaleMode.Dpi, source.AutoScaleMode);
        Assert.IsTrue(source.Visible, "The standalone design canvas must display the drawing source.");
        Assert.AreSame(source, source.Surface.Parent);
        Assert.AreSame(source.Surface, source.Menu.Parent);
        Assert.AreEqual(DockStyle.Fill, source.Surface.Dock);
        Assert.AreEqual(DockStyle.Fill, source.Menu.Dock);
        source.PerformLayout();
        Assert.AreEqual(source.ClientRectangle, source.Surface.Bounds);

        Size initialSize = source.Size;
        source.Surface.Radius = 10;
        source.Surface.Padding = new Padding(6);
        source.Scale(new SizeF(1.5F, 1.5F));
        source.PerformLayout();
        Assert.AreEqual(new Size((int)Math.Round(initialSize.Width * 1.5F),
            (int)Math.Round(initialSize.Height * 1.5F)), source.Size);
        Assert.AreEqual(source.ClientRectangle, source.Surface.Bounds);
        Assert.AreEqual(new Padding(9), source.Surface.Padding);
        Assert.AreEqual(10, source.Surface.Radius, "AntdUI radius stays logical while Windows Forms scales layout padding.");
        Assert.IsTrue(source.Surface.ClientRectangle.Contains(source.Menu.Bounds));
        Assert.IsFalse(source.IsHandleCreated);
        Assert.IsFalse(source.Surface.IsHandleCreated);
        Assert.IsFalse(source.Menu.IsHandleCreated);
    }

    /// <summary>Checks window and executable icons preserve the navigation artwork across resource reloads.</summary>
    [TestMethod]
    [TestCategory("Rendering")]
    public void ApplicationIconResourcesMatchNavigationLogoAcrossRepeatedLoads()
    {
        string executablePath = Path.ChangeExtension(typeof(MainForm).Assembly.Location, ".exe");
        for (int load = 0; load < 2; load++)
        {
            using var form = new MainForm();
            Avatar logo = GetControl<Avatar>(form, "_navigationLogo");
            Assert.IsInstanceOfType<Bitmap>(logo.Image);
            var logoImage = (Bitmap)logo.Image;
            Assert.IsNotNull(form.Icon);
            using var windowIcon = new Icon(form.Icon, logoImage.Size);
            using Bitmap windowImage = windowIcon.ToBitmap();
            AssertIconArtworkEqual(logoImage, windowImage,
                "The window icon must use the transparent navigation logo artwork.");
            using Icon? executableIcon = Icon.ExtractIcon(executablePath, 0, logoImage.Width);
            Assert.IsNotNull(executableIcon);
            using Bitmap executableImage = executableIcon.ToBitmap();
            AssertIconArtworkEqual(logoImage, executableImage,
                "The executable's embedded application icon must use the transparent navigation logo artwork.");
        }
    }

    /// <summary>Checks designer-time navigation sizing follows font changes without creating window handles.</summary>
    [TestMethod]
    public void ConstructorAndFontChangesSizeTopNavigationWithoutCreatingHandles()
    {
        using var form = new MainForm();
        AntdUI.Menu menu = GetControl<AntdUI.Menu>(form, "_navigationMenu");
        AntdUI.Button back = GetControl<AntdUI.Button>(form, "_backButton");
        AntdUI.Button collapse = GetControl<AntdUI.Button>(form, "_collapseButton");
        Avatar logo = GetControl<Avatar>(form, "_navigationLogo");
        Assert.IsFalse(form.IsHandleCreated);
        Assert.IsFalse(menu.IsHandleCreated);
        Assert.IsFalse(back.IsHandleCreated);
        Assert.IsFalse(collapse.IsHandleCreated);
        Assert.IsFalse(logo.IsHandleCreated);
        Assert.AreEqual(back.Size, collapse.Size, "The designer constructor must create equally sized navigation buttons.");
        Size originalButtonSize = back.Size;
        Size originalLogoSize = logo.Size;
        int originalTextHeight = menu.GDI(canvas => canvas.MeasureText(Config.NullText, menu.Font).Height);
        Font originalFont = menu.Font;
        using var enlargedFont = new Font(originalFont.FontFamily, originalFont.Size + 3F,
            originalFont.Style, originalFont.Unit);
        try
        {
            menu.Font = enlargedFont;
            int enlargedTextHeight = menu.GDI(canvas => canvas.MeasureText(Config.NullText, menu.Font).Height);
            Assert.IsGreaterThan(originalTextHeight, enlargedTextHeight);
            Assert.AreEqual(enlargedTextHeight - originalTextHeight, back.Height - originalButtonSize.Height,
                "With the menu gap unchanged, the button height must follow the native font height change.");
            Assert.AreEqual(back.Size, collapse.Size);
            Assert.AreEqual(originalButtonSize.Width, back.Width);
            Assert.AreEqual(enlargedTextHeight - originalTextHeight, logo.Height - originalLogoSize.Height,
                "The logo must scale with the menu's native icon height.");
            Assert.AreEqual(logo.Width, logo.Height);
            Assert.IsFalse(form.IsHandleCreated);
            Assert.IsFalse(menu.IsHandleCreated);
            Assert.IsFalse(back.IsHandleCreated);
            Assert.IsFalse(collapse.IsHandleCreated);
            Assert.IsFalse(logo.IsHandleCreated);
        }
        finally
        {
            menu.Font = originalFont;
        }
    }

    /// <summary>Checks every route displays one reusable page while the six placeholder pages remain blank.</summary>
    [TestMethod]
    public void NavigationSelectionEveryLeafDisplaysOneCachedPage()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        AntdUI.Menu menu = GetControl<AntdUI.Menu>(form, "_navigationMenu");
        AntdUI.Panel contentPanel = GetControl<AntdUI.Panel>(form, "_contentPanel");

        foreach (string route in s_leafRoutes)
        {
            AntdUI.MenuItem item = FindMenuItem(menu, route);
            if (item.ParentItem is AntdUI.MenuItem group)
            {
                InvokeClick(GetControl<AntdUI.Button>(form, "_collapseButton"));
                if (!group.Expand)
                {
                    InvokeMenuClick(menu, group);
                }
            }

            InvokeMenuClick(menu, item);
            Assert.AreEqual(route, GetVisiblePage(contentPanel).Name);
            if (route == "Settings")
            {
                Assert.IsInstanceOfType<SettingsPage>(GetVisiblePage(contentPanel));
                Assert.AreEqual(DockStyle.Fill, GetVisiblePage(contentPanel).Dock);
            }
            else
            {
                AssertBlankPage(GetVisiblePage(contentPanel));
            }
            Assert.IsTrue(menu.Collapsed, "Selecting a page must not expand the sidebar.");
        }

        Assert.AreEqual(7, contentPanel.Controls.Count);
        Control[] originalPages = [.. contentPanel.Controls.Cast<Control>()];
        foreach (string route in s_leafRoutes.Reverse())
        {
            form.NavigateTo(route);
            Assert.AreEqual(route, GetVisiblePage(contentPanel).Name);
        }

        CollectionAssert.AreEquivalent(originalPages, contentPanel.Controls);
        Assert.AreEqual(7, originalPages.Select(static page => page.GetType()).Distinct().Count());
        Assert.IsTrue(originalPages.All(static page => !page.IsDisposed));
    }

    /// <summary>Checks real grouping clicks only expand or collapse and preserve the active page.</summary>
    [TestMethod]
    public void NavigationGroupClicksOnlyToggleExpansionAndPreserveCurrentPage()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        form.NavigateTo("Monitor");
        AntdUI.Menu menu = GetControl<AntdUI.Menu>(form, "_navigationMenu");
        AntdUI.Panel contentPanel = GetControl<AntdUI.Panel>(form, "_contentPanel");
        Control currentPage = GetVisiblePage(contentPanel);
        AntdUI.MenuItem logs = FindMenuItem(menu, "Logs");
        AntdUI.MenuItem debug = FindMenuItem(menu, "Debug");
        InvokeClick(GetControl<AntdUI.Button>(form, "_collapseButton"));
        Assert.IsFalse(menu.Collapsed);
        Assert.IsTrue(menu.Unique);
        logs.Expand = false;
        debug.Expand = false;

        InvokeMenuClick(menu, logs);
        Assert.IsTrue(logs.Expand);
        Assert.IsFalse(menu.Collapsed, "A group click must keep the expanded sidebar open.");
        Assert.AreSame(currentPage, GetVisiblePage(contentPanel));
        InvokeMenuClick(menu, debug);
        Assert.IsTrue(debug.Expand);
        Assert.IsFalse(logs.Expand, "Unique navigation keeps only one group expanded.");
        Assert.IsFalse(menu.Collapsed);
        Assert.AreSame(currentPage, GetVisiblePage(contentPanel));
        InvokeMenuClick(menu, debug);
        Assert.IsFalse(debug.Expand);
        Assert.IsFalse(menu.Collapsed);

        InvokeClick(GetControl<AntdUI.Button>(form, "_collapseButton"));
        int collapsedWidth = menu.Width;
        InvokeMenuClick(menu, logs);
        _ = GetReadyNavigationFlyout((NavigationMenu)menu);
        Assert.IsTrue(menu.Collapsed, "Clicking an icon-mode group must not expand the sidebar.");
        Assert.AreEqual(collapsedWidth, menu.Width);
        Assert.IsFalse(logs.Expand, "The collapsed parent must not also expand through the native Menu path.");

        Assert.AreEqual(2, contentPanel.Controls.Count);
        Assert.AreSame(currentPage, GetVisiblePage(contentPanel));
        Assert.IsTrue(FindMenuItem(menu, "Monitor").Select);
        AntdUI.Button back = GetControl<AntdUI.Button>(form, "_backButton");
        InvokeClick(back);
        Assert.AreEqual("Dashboard", GetVisiblePage(contentPanel).Name);
        Assert.IsFalse(back.Enabled, "Group interactions must not add navigation history.");
    }

    /// <summary>Checks repeated real leaf clicks preserve state, collapse the sidebar, and add no history.</summary>
    [TestMethod]
    public void RepeatedLeafClicksReusePageStateCollapseSidebarAndDoNotAddHistory()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        AntdUI.Menu menu = GetControl<AntdUI.Menu>(form, "_navigationMenu");
        AntdUI.Panel contentPanel = GetControl<AntdUI.Panel>(form, "_contentPanel");
        AntdUI.MenuItem monitor = FindMenuItem(menu, "Monitor");
        InvokeMenuClick(menu, monitor);
        Control monitorPage = GetVisiblePage(contentPanel);
        object state = new();
        monitorPage.Tag = state;

        InvokeMenuClick(menu, monitor);
        Assert.AreSame(monitorPage, GetVisiblePage(contentPanel));
        InvokeClick(GetControl<AntdUI.Button>(form, "_collapseButton"));
        Assert.IsFalse(menu.Collapsed);
        InvokeMenuClick(menu, monitor);

        Assert.AreSame(monitorPage, GetVisiblePage(contentPanel));
        Assert.AreSame(state, monitorPage.Tag);
        Assert.IsTrue(monitor.Select);
        Assert.IsFalse(FindMenuItem(menu, "Dashboard").Select);
        Assert.IsTrue(menu.Collapsed);
        Assert.AreEqual(2, contentPanel.Controls.Count);

        AntdUI.Button back = GetControl<AntdUI.Button>(form, "_backButton");
        InvokeClick(back);
        Assert.AreEqual("Dashboard", GetVisiblePage(contentPanel).Name);
        Assert.IsFalse(back.Enabled, "Repeated selection must not introduce duplicate history entries.");
        Assert.IsFalse(monitorPage.IsDisposed);
    }

    /// <summary>Checks a blank-origin release cannot reuse the native menu's preceding pressed item.</summary>
    [TestMethod]
    public void NavigationBlankMouseDownDoesNotReusePreviousClickAfterBack()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        NavigationMenu menu = GetControl<NavigationMenu>(form, "_navigationMenu");
        AntdUI.Panel contentPanel = GetControl<AntdUI.Panel>(form, "_contentPanel");
        AntdUI.Button back = GetControl<AntdUI.Button>(form, "_backButton");
        AntdUI.MenuItem devices = FindMenuItem(menu, "Devices");
        InvokeMenuClick(menu, devices);
        InvokeClick(back);
        Assert.AreEqual("Dashboard", GetVisiblePage(contentPanel).Name);
        Assert.IsFalse(back.Enabled);

        Rectangle row = devices.Rect();
        var down = new Point(row.Left - 1, row.Top + row.Height / 2);
        var release = new Point(down.X + 2, down.Y);
        Assert.IsNull(menu.HitTest(down.X, down.Y));
        Assert.AreSame(devices, menu.HitTest(release.X, release.Y));
        MethodInfo? mouseDown = typeof(NavigationMenu).GetMethod("OnMouseDown",
            BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo? mouseUp = typeof(NavigationMenu).GetMethod("OnMouseUp",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(mouseDown);
        Assert.IsNotNull(mouseUp);
        int selections = 0;
        int releases = 0;
        menu.ItemClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                selections++;
            }
        };
        menu.MouseUp += (_, _) => releases++;

        mouseDown.Invoke(menu, [new MouseEventArgs(MouseButtons.Left, 1, down.X, down.Y, 0)]);
        InvokeMouseMove(menu, release);
        mouseUp.Invoke(menu, [new MouseEventArgs(MouseButtons.Left, 1, release.X, release.Y, 0)]);

        Assert.AreEqual("Dashboard", GetVisiblePage(contentPanel).Name);
        Assert.IsFalse(back.Enabled, "A blank-origin gesture must not create navigation history.");
        Assert.AreEqual(0, selections);
        Assert.AreEqual(1, releases, "Cancelling navigation must preserve the normal mouse-up event.");
        InvokeMenuClick(menu, devices);
        Assert.AreEqual("Devices", GetVisiblePage(contentPanel).Name);
        Assert.AreEqual(1, selections);
        InvokeClick(back);
        Assert.AreEqual("Dashboard", GetVisiblePage(contentPanel).Name);
        Assert.IsFalse(back.Enabled);
    }

    /// <summary>Checks completed clicks clear their state without cancelling an in-progress leave and return.</summary>
    [TestMethod]
    public void NavigationMouseReleaseClearsGestureAndPreservesLeaveThenReturnClick()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        NavigationMenu menu = GetControl<NavigationMenu>(form, "_navigationMenu");
        AntdUI.Panel contentPanel = GetControl<AntdUI.Panel>(form, "_contentPanel");
        AntdUI.Button back = GetControl<AntdUI.Button>(form, "_backButton");
        AntdUI.MenuItem devices = FindMenuItem(menu, "Devices");
        MethodInfo? mouseDown = typeof(NavigationMenu).GetMethod("OnMouseDown",
            BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo? mouseUp = typeof(NavigationMenu).GetMethod("OnMouseUp",
            BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo? mouseLeave = typeof(NavigationMenu).GetMethod("OnMouseLeave",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(mouseDown);
        Assert.IsNotNull(mouseUp);
        Assert.IsNotNull(mouseLeave);
        Rectangle row = devices.Rect();
        var click = new MouseEventArgs(MouseButtons.Left, 1, row.Left + 1, row.Top + row.Height / 2, 0);

        mouseDown.Invoke(menu, [click]);
        mouseLeave.Invoke(menu, [EventArgs.Empty]);
        InvokeMouseMove(menu, click.Location);
        mouseUp.Invoke(menu, [click]);
        Assert.AreEqual("Devices", GetVisiblePage(contentPanel).Name,
            "Leaving and returning during a valid main-menu press must retain its original click behavior.");
        InvokeClick(back);
        Assert.AreEqual("Dashboard", GetVisiblePage(contentPanel).Name);
        Assert.IsFalse(back.Enabled);

        mouseUp.Invoke(menu, [click]);
        Assert.AreEqual("Dashboard", GetVisiblePage(contentPanel).Name);
        Assert.IsFalse(back.Enabled, "A second release without a new press must not reuse the completed gesture.");
    }

    /// <summary>Checks a blank-origin touch drag releases normally and retains native inertial scrolling.</summary>
    [TestMethod]
    public void NavigationBlankTouchDragPreservesInertiaAndReleasesGesture()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        NavigationMenu menu = GetControl<NavigationMenu>(form, "_navigationMenu");
        AntdUI.Panel contentPanel = GetControl<AntdUI.Panel>(form, "_contentPanel");
        InvokeClick(GetControl<AntdUI.Button>(form, "_collapseButton"));
        menu.Dock = DockStyle.None;
        menu.Height = FindMenuItem(menu, "Devices").Rect().Height * 3;
        Assert.IsTrue(menu.ScrollBar.ShowY);
        Assert.IsTrue(Config.TouchEnabled);
        int step = (int)(Config.TouchThreshold * menu.DeviceDpi / 96F) + 5;
        int x = FindMenuItem(menu, "Devices").Rect().Left - 1;
        int y = menu.ClientSize.Height - 5;
        Assert.IsNull(menu.HitTest(x, y));
        MethodInfo? mouseDown = typeof(NavigationMenu).GetMethod("OnMouseDown",
            BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo? mouseUp = typeof(NavigationMenu).GetMethod("OnMouseUp",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(mouseDown);
        Assert.IsNotNull(mouseUp);
        mouseDown.Invoke(menu, [new MouseEventArgs(MouseButtons.Left, 1, x, y, 0)]);
        InvokeMouseMove(menu, new Point(x, y - step));
        InvokeMouseMove(menu, new Point(x, y - step * 2));
        int draggedOffset = menu.ScrollBar.ValueY;
        Assert.IsGreaterThan(0, draggedOffset);
        mouseUp.Invoke(menu, [new MouseEventArgs(MouseButtons.Left, 1, x, y - step * 2, 0)]);

        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        while (menu.ScrollBar.ValueY == draggedOffset && elapsed.Elapsed < TimeSpan.FromSeconds(2))
        {
            Application.DoEvents();
        }

        Assert.IsGreaterThan(draggedOffset, menu.ScrollBar.ValueY,
            "Releasing a blank-origin drag must still start the native inertia animation.");
        Assert.AreEqual("Dashboard", GetVisiblePage(contentPanel).Name);
        Assert.IsFalse(GetControl<AntdUI.Button>(form, "_backButton").Enabled);

        // A new stationary gesture stops inertia, then its release must stop further pointer scrolling.
        mouseDown.Invoke(menu, [new MouseEventArgs(MouseButtons.Left, 1, x, y, 0)]);
        mouseUp.Invoke(menu, [new MouseEventArgs(MouseButtons.Left, 1, x, y, 0)]);
        int releasedOffset = menu.ScrollBar.ValueY;
        InvokeMouseMove(menu, new Point(x, y - step));
        InvokeMouseMove(menu, new Point(x, y - step * 2));
        Assert.AreEqual(releasedOffset, menu.ScrollBar.ValueY);
    }

    /// <summary>Checks mouse-up still releases a scrollbar drag before later pointer movement.</summary>
    [TestMethod]
    public void NavigationScrollbarMouseReleaseStopsDraggingWithoutNavigation()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        NavigationMenu menu = GetControl<NavigationMenu>(form, "_navigationMenu");
        AntdUI.Panel contentPanel = GetControl<AntdUI.Panel>(form, "_contentPanel");
        InvokeClick(GetControl<AntdUI.Button>(form, "_collapseButton"));
        menu.Dock = DockStyle.None;
        menu.Height = FindMenuItem(menu, "Devices").Rect().Height * 3;
        Assert.IsTrue(menu.ScrollBar.ShowY);
        var drag = new Point(menu.ClientSize.Width - 1, menu.ClientSize.Height / 2);
        Assert.IsTrue(menu.ScrollBar.Contains(drag));
        MethodInfo? mouseDown = typeof(NavigationMenu).GetMethod("OnMouseDown",
            BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo? mouseUp = typeof(NavigationMenu).GetMethod("OnMouseUp",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(mouseDown);
        Assert.IsNotNull(mouseUp);
        mouseDown.Invoke(menu, [new MouseEventArgs(MouseButtons.Left, 1, drag.X, drag.Y, 0)]);
        InvokeMouseMove(menu, new Point(drag.X, drag.Y + 10));
        int draggedOffset = menu.ScrollBar.ValueY;
        Assert.IsGreaterThan(0, draggedOffset);
        mouseUp.Invoke(menu, [new MouseEventArgs(MouseButtons.Left, 1, drag.X, drag.Y + 10, 0)]);
        InvokeMouseMove(menu, new Point(drag.X, 0));

        Assert.AreEqual(draggedOffset, menu.ScrollBar.ValueY);
        Assert.AreEqual("Dashboard", GetVisiblePage(contentPanel).Name);
        Assert.IsFalse(GetControl<AntdUI.Button>(form, "_backButton").Enabled);
        menu.ScrollBar.ValueY = 0;
        InvokeMenuClick(menu, FindMenuItem(menu, "Devices"));
        Assert.AreEqual("Devices", GetVisiblePage(contentPanel).Name);
    }

    /// <summary>Checks that filtering changes visibility without destroying navigation or page state.</summary>
    [TestMethod]
    public void SearchChildMatchAndClearPreservesMenuInstancesAndCachedPages()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        form.NavigateTo("Monitor");
        AntdUI.Menu menu = GetControl<AntdUI.Menu>(form, "_navigationMenu");
        AntdUI.Panel contentPanel = GetControl<AntdUI.Panel>(form, "_contentPanel");
        Control monitorPage = GetVisiblePage(contentPanel);
        Input search = GetControl<Input>(form, "_searchInput");
        AntdUI.MenuItem[] originalItems = [.. menu.Items];

        search.Text = "logspage1";

        Assert.IsTrue(FindMenuItem(menu, "Logs").Visible);
        Assert.IsTrue(FindMenuItem(menu, "LogsPage1").Visible);
        Assert.IsFalse(FindMenuItem(menu, "LogsPage2").Visible);
        Assert.IsTrue(FindMenuItem(menu, "Logs").Expand);
        Assert.IsFalse(FindMenuItem(menu, "Dashboard").Visible);
        Assert.IsFalse(FindMenuItem(menu, "Debug").Visible);

        search.Text = "devices";
        Assert.IsTrue(FindMenuItem(menu, "Devices").Visible);
        Assert.AreEqual(1, menu.Items.Count(static item => item.Visible));

        search.Text = "unmatched-navigation-query";
        Assert.IsTrue(menu.Items.All(static item => !item.Visible));
        search.Text = string.Empty;

        Assert.IsTrue(menu.Items.All(static item => item.Visible));
        CollectionAssert.AreEqual(originalItems, menu.Items.ToArray());
        Assert.AreEqual(2, contentPanel.Controls.Count);
        Assert.AreSame(monitorPage, GetVisiblePage(contentPanel));
        Assert.AreEqual("Monitor", monitorPage.Name);
        Assert.IsTrue(FindMenuItem(menu, "LogsPage2").Visible);
    }

    /// <summary>Checks keyboard navigation cannot leave the filtered results or act on an empty result set.</summary>
    [TestMethod]
    [DataRow(Keys.Up)]
    [DataRow(Keys.Down)]
    public void SearchKeyboardNavigationKeepsVisibleSelectionAndEmptyResultsUnchanged(Keys direction)
    {
        using var form = new OffscreenMainForm();
        form.Show();
        form.NavigateTo("Monitor");
        NavigationMenu menu = GetControl<NavigationMenu>(form, "_navigationMenu");
        Input search = GetControl<Input>(form, "_searchInput");
        AntdUI.Panel contentPanel = GetControl<AntdUI.Panel>(form, "_contentPanel");
        Control currentPage = GetVisiblePage(contentPanel);
        search.Text = "Monitor";
        Assert.IsTrue(menu.Focus());

        InvokeNavigationKey(menu, direction);

        Assert.AreEqual("Monitor", menu.SelectItem?.Name);
        Assert.AreSame(currentPage, GetVisiblePage(contentPanel));
        search.Text = "unmatched-navigation-query";
        int selections = 0;
        menu.ItemClick += (_, _) => selections++;
        foreach (Keys key in new[] { Keys.Up, Keys.Down, Keys.Enter, Keys.Right })
        {
            InvokeNavigationKey(menu, key);
            Assert.AreSame(currentPage, GetVisiblePage(contentPanel));
            Assert.IsNull(menu.FlyoutWindow);
        }

        Assert.AreEqual(0, selections, "An empty result set must not dispatch any selection callbacks.");
        Assert.AreEqual(2, contentPanel.Controls.Count);
        InvokeClick(GetControl<AntdUI.Button>(form, "_backButton"));
        Assert.AreEqual("Dashboard", GetVisiblePage(contentPanel).Name);
        Assert.IsFalse(GetControl<AntdUI.Button>(form, "_backButton").Enabled);
    }

    /// <summary>Checks a hidden retained group cannot open and arrow keys restart inside visible results.</summary>
    [TestMethod]
    [DataRow(Keys.Up)]
    [DataRow(Keys.Down)]
    public void SearchKeyboardNavigationRestartsFromVisibleResultsWhenSelectionIsHidden(Keys direction)
    {
        using var form = new OffscreenMainForm();
        form.Show();
        NavigationMenu menu = GetControl<NavigationMenu>(form, "_navigationMenu");
        menu.Select(FindMenuItem(menu, "Logs"));
        GetControl<Input>(form, "_searchInput").Text = "Monitor";
        Assert.IsTrue(menu.Focus());
        foreach (Keys key in new[] { Keys.Enter, Keys.Right })
        {
            InvokeNavigationKey(menu, key);
            Assert.IsNull(menu.FlyoutWindow);
            Assert.AreEqual("Dashboard", GetVisiblePage(GetControl<AntdUI.Panel>(form, "_contentPanel")).Name);
        }

        InvokeNavigationKey(menu, direction);

        Assert.AreEqual("Monitor", menu.SelectItem?.Name);
        Assert.AreEqual("Monitor", GetVisiblePage(GetControl<AntdUI.Panel>(form, "_contentPanel")).Name);
    }

    /// <summary>Checks filtering out the current child cannot reactivate it through its visible parent.</summary>
    [TestMethod]
    [DataRow(Keys.Up, "Debug")]
    [DataRow(Keys.Down, "Logs")]
    public void SearchKeyboardNavigationRejectsHiddenChildAndStartsAtVisibleBoundary(Keys direction, string expected)
    {
        using var form = new OffscreenMainForm();
        form.Show();
        form.NavigateTo("LogsPage2");
        NavigationMenu menu = GetControl<NavigationMenu>(form, "_navigationMenu");
        Input search = GetControl<Input>(form, "_searchInput");
        search.Text = "LogsPage1";
        Assert.IsTrue(menu.Focus());
        int selections = 0;
        menu.ItemClick += (_, _) => selections++;
        foreach (Keys key in new[] { Keys.Enter, Keys.Right })
        {
            InvokeNavigationKey(menu, key);
            Assert.IsNull(menu.FlyoutWindow);
        }

        Assert.AreEqual(0, selections);
        search.Text = "Page1";
        InvokeNavigationKey(menu, direction);
        Assert.AreEqual(expected, menu.SelectItem?.Name);
        Assert.AreEqual("LogsPage2", GetVisiblePage(GetControl<AntdUI.Panel>(form, "_contentPanel")).Name);
    }

    /// <summary>Checks expanded keyboard order skips hidden children and collapsed order stays on roots.</summary>
    [TestMethod]
    public void SearchKeyboardNavigationUsesDisplayedChildrenAndSkipsDisabledRows()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        form.NavigateTo("Monitor");
        NavigationMenu menu = GetControl<NavigationMenu>(form, "_navigationMenu");
        Input search = GetControl<Input>(form, "_searchInput");
        search.Text = "LogsPage2";
        InvokeClick(GetControl<AntdUI.Button>(form, "_collapseButton"));
        Assert.IsFalse(menu.Collapsed);
        Assert.IsTrue(menu.Focus());
        InvokeNavigationKey(menu, Keys.Down);
        Assert.AreEqual("Logs", menu.SelectItem?.Name);
        InvokeNavigationKey(menu, Keys.Down);
        Assert.AreEqual("LogsPage2", GetVisiblePage(GetControl<AntdUI.Panel>(form, "_contentPanel")).Name);
        Assert.IsTrue(menu.Collapsed);

        search.Text = string.Empty;
        menu.Select(FindMenuItem(menu, "Logs"));
        FindMenuItem(menu, "Logs").Expand = true;
        FindMenuItem(menu, "Debug").Enabled = false;
        InvokeNavigationKey(menu, Keys.Down);
        Assert.AreEqual("Settings", menu.SelectItem?.Name);
        InvokeNavigationKey(menu, Keys.Down);
        Assert.AreEqual("Settings", menu.SelectItem?.Name, "The final visible row must not wrap to the first row.");
    }

    /// <summary>Checks a child hidden only by collapsing its group keeps the parent's keyboard position.</summary>
    [TestMethod]
    public void KeyboardNavigationUsesParentForCollapsedCurrentChildWithoutReopeningItsPage()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        form.NavigateTo("LogsPage2");
        NavigationMenu menu = GetControl<NavigationMenu>(form, "_navigationMenu");
        AntdUI.MenuItem logs = FindMenuItem(menu, "Logs");
        InvokeClick(GetControl<AntdUI.Button>(form, "_collapseButton"));
        logs.Expand = false;
        Assert.IsTrue(menu.Focus());
        int selections = 0;
        menu.ItemClick += (_, _) => selections++;

        InvokeNavigationKey(menu, Keys.Right);

        Assert.IsTrue(logs.Expand);
        Assert.IsFalse(menu.Collapsed);
        Assert.AreEqual(0, selections, "Opening the group must not reselect its hidden current child.");
        InvokeClick(GetControl<AntdUI.Button>(form, "_collapseButton"));
        InvokeNavigationKey(menu, Keys.Down);
        Assert.AreEqual("Debug", menu.SelectItem?.Name);
        Assert.AreEqual("LogsPage2", GetVisiblePage(GetControl<AntdUI.Panel>(form, "_contentPanel")).Name);
    }

    /// <summary>Checks language dropdown events while retaining the active route and existing pages.</summary>
    [TestMethod]
    public void LanguageDropdownChangesThreeLanguagesPreservesSelectedPageAndRouteIdentity()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        form.NavigateTo("Monitor");
        form.NavigateTo("LogsPage2");
        AntdUI.Menu menu = GetControl<AntdUI.Menu>(form, "_navigationMenu");
        AntdUI.Panel contentPanel = GetControl<AntdUI.Panel>(form, "_contentPanel");
        Dropdown languages = GetControl<Dropdown>(form, "_languageDropdown");
        Control logsPage = GetVisiblePage(contentPanel);
        Control[] originalPages = [.. contentPanel.Controls.Cast<Control>()];
        object state = new();
        logsPage.Tag = state;
        MethodInfo? selectValue = typeof(Dropdown).GetMethod(
            "DropDownChange", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(selectValue);

        foreach ((string language, string selection) in new[]
        {
            ("zh-CN", "简体中文"), ("zh-TW", "繁體中文"), ("en-US", "English")
        })
        {
            // AntdUI's SelectedValue setter is passive; this is its actual item-selection entry point.
            selectValue.Invoke(languages, [selection]);
            Assert.AreEqual(language, CultureInfo.CurrentUICulture.Name);
            Assert.AreEqual(language, CultureInfo.CurrentCulture.Name);
            Assert.AreSame(logsPage, GetVisiblePage(contentPanel));
            Assert.AreSame(state, logsPage.Tag);
            Assert.AreEqual("LogsPage2", logsPage.Name);
            Assert.AreEqual(AppLocalization.GetText("LogsPage2"), FindMenuItem(menu, "LogsPage2").Text);
            Assert.AreEqual(AppLocalization.GetText("Dashboard"), menu.Items[0].Text);
            Assert.AreEqual(AppLocalization.GetText("Devices"), menu.Items[1].Text);
            Assert.AreEqual(AppLocalization.GetText("Settings"), menu.Items[5].Text);
            Assert.AreEqual(AppLocalization.GetText("About"), GetControl<AntdUI.Button>(form, "_aboutButton").AccessibleName);
            Assert.AreEqual(AppLocalization.GetText("Back"), GetControl<AntdUI.Button>(form, "_backButton").AccessibleName);
            Assert.IsTrue(FindMenuItem(menu, "LogsPage2").Select);
            form.NavigateTo("LogsPage2");
            Assert.AreEqual(3, contentPanel.Controls.Count);
            CollectionAssert.AreEquivalent(originalPages, contentPanel.Controls);
            Assert.IsTrue(originalPages.All(static page => !page.IsDisposed));
        }

        AntdUI.Button back = GetControl<AntdUI.Button>(form, "_backButton");
        InvokeClick(back);
        Assert.AreEqual("Monitor", GetVisiblePage(contentPanel).Name);
        InvokeClick(back);
        Assert.AreEqual("Dashboard", GetVisiblePage(contentPanel).Name);
        Assert.IsFalse(back.Enabled, "Changing language must not add navigation history.");
    }

    /// <summary>Checks localized navigation keeps root icons aligned and preserves shell geometry when toggled.</summary>
    /// <param name="language">The application language exercised by the navigation actions.</param>
    [TestMethod]
    [DataRow("en-US")]
    [DataRow("zh-CN")]
    [DataRow("zh-TW")]
    public void CollapseButtonClickedTwiceRestoresInitiallyCollapsedNavigation(string language)
    {
        using var form = new OffscreenMainForm();
        form.Show();
        form.SetLanguage(language);
        AntdUI.Menu menu = GetControl<AntdUI.Menu>(form, "_navigationMenu");
        AntdUI.Button button = GetControl<AntdUI.Button>(form, "_collapseButton");
        AntdUI.Button back = GetControl<AntdUI.Button>(form, "_backButton");
        Avatar logo = GetControl<Avatar>(form, "_navigationLogo");
        AntdUI.Label brand = GetControl<AntdUI.Label>(form, "_brandLabel");
        Assert.IsNotNull(menu.Parent);
        Assert.IsTrue(menu.Collapsed);
        Assert.IsTrue(logo.Visible);
        Assert.IsFalse(back.Visible);
        Assert.IsFalse(back.Enabled);
        Assert.IsFalse(brand.Visible);
        int collapsedWidth = menu.Parent.Width;
        AntdUI.Panel workspace = GetControl<AntdUI.Panel>(form, "_workspacePanel");
        Rectangle originalWorkspace = workspace.Bounds;
        Assert.AreEqual(GetControl<AntdUI.Panel>(form, "_navigationDivider").Left, menu.Right,
            "The initial menu must finish docking before its icon positions are measured.");
        Rectangle[] icons = [.. menu.Items.Select(static item => item.Rect("Icon"))];
        int[] rowHeights = [.. menu.Items.Select(static item => item.Rect().Height)];
        Size collapsedRowSize = menu.Items[0].Rect().Size;
        AssertContentFillsShell(form);
        AssertTopNavigationGeometry(form, collapsedRowSize);

        InvokeClick(button);

        Assert.IsFalse(menu.Collapsed);
        Assert.IsTrue(logo.Visible);
        Assert.IsFalse(back.Visible);
        Assert.IsFalse(back.Enabled);
        Assert.IsTrue(brand.Visible);
        Assert.AreEqual("FbSample", brand.Text);
        Assert.IsGreaterThan(collapsedWidth, menu.Parent.Width);
        Assert.AreEqual(AppLocalization.GetText("CollapseNavigation"), button.AccessibleName);
        Assert.AreEqual(originalWorkspace, workspace.Bounds, "Expanding navigation must cover the workspace.");
        AssertContentFillsShell(form);
        AssertRootMenuGeometry(menu, icons, rowHeights);
        AssertTopNavigationGeometry(form, collapsedRowSize);
        foreach (AntdUI.MenuItem group in menu.Items.Where(static item => item.CanExpand))
        {
            if (!group.Expand)
            {
                InvokeMenuClick(menu, group);
            }

            menu.Refresh();
            foreach (AntdUI.MenuItem child in group.Sub)
            {
                Rectangle text = child.Rect("Text");
                Size measured = menu.GDI(canvas => canvas.MeasureText(child.Text, child.Font ?? menu.Font));
                Assert.IsTrue(text.Left > child.Rect("Icon").Right, "Inline child icons and names must have a positive gap.");
                Assert.IsTrue(text.Width >= measured.Width && text.Height >= measured.Height,
                    $"The inline name '{child.Text}' must fit its text area.");
            }
        }

        InvokeClick(button);

        Assert.IsTrue(menu.Collapsed);
        Assert.IsTrue(logo.Visible);
        Assert.IsFalse(back.Visible);
        Assert.IsFalse(brand.Visible);
        Assert.AreEqual(collapsedWidth, menu.Parent.Width);
        Assert.AreEqual(AppLocalization.GetText("ExpandNavigation"), button.AccessibleName);
        Assert.AreEqual(originalWorkspace, workspace.Bounds);
        AssertContentFillsShell(form);
        AssertRootMenuGeometry(menu, icons, rowHeights);
        AssertTopNavigationGeometry(form, collapsedRowSize);
        Assert.IsFalse(back.Enabled, "Language changes, group expansion and sidebar toggles must not create history.");
    }

    /// <summary>Checks changing the font while expanded preserves root icon alignment in both navigation states.</summary>
    [TestMethod]
    public void NavigationFontChangedWhileExpandedKeepsRootIconsAlignedWhenToggled()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        AntdUI.Menu menu = GetControl<AntdUI.Menu>(form, "_navigationMenu");
        AntdUI.Button button = GetControl<AntdUI.Button>(form, "_collapseButton");
        Rectangle workspace = GetControl<AntdUI.Panel>(form, "_workspacePanel").Bounds;
        int collapsedRowWidth = menu.Items[0].Rect().Width;
        InvokeClick(button);
        Assert.IsFalse(menu.Collapsed);
        Font originalFont = menu.Font;
        using var enlargedFont = new Font(originalFont.FontFamily, originalFont.Size + 3F,
            originalFont.Style, originalFont.Unit);
        try
        {
            menu.Font = enlargedFont;
            menu.Refresh();
            AssertContentFillsShell(form);
            Rectangle[] icons = [.. menu.Items.Select(static item => item.Rect("Icon"))];
            int[] rowHeights = [.. menu.Items.Select(static item => item.Rect().Height)];
            var collapsedRowSize = new Size(collapsedRowWidth, rowHeights[0]);
            AssertTopNavigationGeometry(form, collapsedRowSize);
            AssertRootMenuGeometry(menu, icons, rowHeights);

            InvokeClick(button);
            Assert.IsTrue(menu.Collapsed);
            AssertContentFillsShell(form);
            AssertRootMenuGeometry(menu, icons, rowHeights);
            Assert.AreEqual(collapsedRowSize, menu.Items[0].Rect().Size);
            AssertTopNavigationGeometry(form, collapsedRowSize);
            InvokeClick(button);
            Assert.IsFalse(menu.Collapsed);
            AssertContentFillsShell(form);
            AssertRootMenuGeometry(menu, icons, rowHeights);
            AssertTopNavigationGeometry(form, collapsedRowSize);
            Assert.AreEqual(workspace, GetControl<AntdUI.Panel>(form, "_workspacePanel").Bounds);
        }
        finally
        {
            menu.Font = originalFont;
        }
    }

    /// <summary>Checks that the title-bar theme action changes the application theme.</summary>
    [TestMethod]
    public void ThemeButtonClickedTwiceTogglesAndRestoresApplicationTheme()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        form.NavigateTo("LogsPage2");
        AntdUI.Panel contentPanel = GetControl<AntdUI.Panel>(form, "_contentPanel");
        Control page = GetVisiblePage(contentPanel);
        AntdUI.Button button = GetControl<AntdUI.Button>(form, "_themeButton");
        bool originalTheme = Config.IsLight;

        InvokeClick(button);
        Assert.AreEqual(!originalTheme, Config.IsLight);
        InvokeClick(button);
        Assert.AreEqual(originalTheme, Config.IsLight);
        Assert.AreSame(page, GetVisiblePage(contentPanel));
        AntdUI.Button back = GetControl<AntdUI.Button>(form, "_backButton");
        InvokeClick(back);
        Assert.AreEqual("Dashboard", GetVisiblePage(contentPanel).Name);
        Assert.IsFalse(back.Enabled, "Theme changes must not add navigation history.");
    }

    /// <summary>Checks sampled text color follows the visible current route without recoloring ancestors, icons or keyboard focus.</summary>
    /// <param name="language">The application language rendered by the navigation menu.</param>
    [TestMethod]
    [TestCategory("Rendering")]
    [DataRow("en-US")]
    [DataRow("zh-CN")]
    [DataRow("zh-TW")]
    public void NavigationTextColorFollowsCurrentRouteAcrossLanguages(string language)
    {
        using var form = new OffscreenMainForm();
        form.Show();
        form.SetLanguage(language);
        form.NavigateTo("LogsPage2");
        Config.IsLight = true;
        NavigationMenu menu = GetControl<NavigationMenu>(form, "_navigationMenu");
        AntdUI.MenuItem logs = FindMenuItem(menu, "Logs");
        NavigationMenu flyout = OpenNavigationFlyout(menu, logs);
        using (Bitmap popup = CaptureControl(flyout))
        {
            Assert.AreEqual(36, GetDarkestGray(popup, flyout.Items[1].Rect("Text")));
            Assert.AreEqual(38, GetDarkestGray(popup, flyout.Items[0].Rect("Text")));
            Assert.AreEqual(38, GetDarkestGray(popup, flyout.Items[1].Rect("Icon")));
        }

        menu.CloseFlyout();
        InvokeClick(GetControl<AntdUI.Button>(form, "_collapseButton"));
        logs.Expand = true;
        using (Bitmap expanded = CaptureControl(menu))
        {
            Assert.AreEqual(36, GetDarkestGray(expanded, logs.Sub[1].Rect("Text")));
            Assert.AreEqual(38, GetDarkestGray(expanded, logs.Rect("Text")),
                "A selected descendant must not tint its expanded ancestor's text.");
        }

        logs.Expand = false;
        using (Bitmap expanded = CaptureControl(menu))
        {
            Assert.AreEqual(36, GetDarkestGray(expanded, logs.Rect("Text")),
                "A closed group represents its hidden current descendant.");
        }

        form.NavigateTo("Monitor");
        MethodInfo? processKey = typeof(NavigationMenu).GetMethod(
            "ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(processKey);
        var key = System.Windows.Forms.Message.Create(menu.Handle, 0x0100, (nint)Keys.Down, 0);
        processKey.Invoke(menu, [key, Keys.Down]);
        processKey.Invoke(menu, [key, Keys.Right]);
        flyout = GetReadyNavigationFlyout(menu);
        Assert.AreSame(flyout.Items[0], flyout.SelectItem);
        Assert.AreEqual("Monitor", menu.CurrentPageKey);
        using Bitmap focused = CaptureControl(flyout);
        Assert.AreEqual(38, GetDarkestGray(focused, flyout.Items[0].Rect("Text")),
            "Keyboard focus alone must not apply the current-page text color.");
    }

    /// <summary>Preserves native arrows and single-pass text pixels at an expansion's final frame and after scrolling.</summary>
    [TestMethod]
    [TestCategory("Rendering")]
    public void NavigationTextRenderingPreservesArrowsAndPixelsAcrossAnimationAndScrolling()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        form.NavigateTo("LogsPage2");
        Config.IsLight = true;
        NavigationMenu menu = GetControl<NavigationMenu>(form, "_navigationMenu");
        InvokeClick(GetControl<AntdUI.Button>(form, "_collapseButton"));
        AntdUI.MenuItem logs = FindMenuItem(menu, "Logs");
        AntdUI.MenuItem page = FindMenuItem(menu, "LogsPage2");
        logs.Expand = false;
        using (Bitmap closed = CaptureControl(menu))
        {
            Assert.AreEqual(36, GetDarkestGray(closed, logs.Rect("Text")));
            Assert.AreEqual(38, GetDarkestGray(closed, logs.Rect("Arrow")),
                "Tinting the current group's text must not erase its native expansion arrow.");
        }

        logs.Expand = true;
        Assert.AreEqual(1F, logs.GetArrowProg());
        Rectangle text = page.Rect("Text");
        int textWidth = menu.GDI(canvas => canvas.MeasureText(page.Text, page.Font ?? menu.Font).Width);
        Assert.IsTrue(text.Width >= textWidth);
        text.Width = textWidth;
        using Bitmap settled = CaptureControl(menu);
        Assert.AreEqual(36, GetDarkestGray(settled, text));

        PropertyInfo? expanding = typeof(AntdUI.MenuItem).GetProperty(
            "ExpandThread", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(expanding);
        // Native animation reaches ArrowProg=1 one interval before clearing ExpandThread.
        expanding.SetValue(logs, true);
        try
        {
            using Bitmap finalFrame = CaptureControl(menu);
            AssertBitmapRegionEqual(settled, text, finalFrame, text,
                "The final expansion frame must draw current-page text exactly once.");
        }
        finally
        {
            expanding.SetValue(logs, false);
        }

        menu.Dock = DockStyle.None;
        menu.Width = form.ClientSize.Width / 2;
        menu.Height = page.Rect().Bottom - page.Rect().Height;
        menu.ScrollBar.ValueY = page.Rect().Height;
        Assert.IsTrue(menu.ScrollBar.ShowY);
        Assert.IsGreaterThan(0, menu.ScrollBar.ValueY);
        Rectangle scrolledText = page.Rect("Text", 0, menu.ScrollBar.ValueY);
        Assert.IsTrue(scrolledText.Width >= textWidth);
        scrolledText.Width = textWidth;
        using Bitmap scrolled = CaptureControl(menu);
        AssertBitmapRegionEqual(settled, text, scrolled, scrolledText,
            "Scrolling must translate the same text pixels without duplicate glyphs or a changed font.");
    }

    /// <summary>Checks navigation button hover frames stay between the sidebar and hover colors without flashing.</summary>
    /// <param name="dark">Whether to use the dark navigation palette.</param>
    /// <param name="expanded">Whether to expand the navigation before hovering.</param>
    [TestMethod]
    [TestCategory("Rendering")]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void NavigationButtonHoverAnimationKeepsPaletteColors(bool dark, bool expanded)
    {
        using var form = new OffscreenMainForm();
        form.Show();
        Config.IsLight = !dark;
        form.NavigateTo("Devices");
        AntdUI.Button back = GetControl<AntdUI.Button>(form, "_backButton");
        AntdUI.Button collapse = GetControl<AntdUI.Button>(form, "_collapseButton");
        if (expanded)
        {
            InvokeClick(collapse);
        }

        Color background = GetControl<AntdUI.Panel>(form, "_navigationPanel").BackColor;
        Color hover = dark ? Color.FromArgb(51, 54, 59) : Color.FromArgb(239, 239, 239);
        FieldInfo? animation = typeof(AntdUI.Button).GetField("AnimationHover",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo? opacity = typeof(AntdUI.Button).GetField("AnimationHoverValue",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(animation);
        Assert.IsNotNull(opacity);
        foreach (AntdUI.Button button in new[] { back, collapse })
        {
            button.ExtraMouseHover = true;
            // Drive native drawing frames directly so timer scheduling cannot hide a brief color flash.
            animation.SetValue(button, true);
            try
            {
                foreach (int alpha in new[] { 0, 64, 128, 192, 255 })
                {
                    opacity.SetValue(button, alpha);
                    using Bitmap frame = CaptureControl(button);
                    Color pixel = GetRowBackground(frame, button.ClientRectangle, button.DeviceDpi);
                    Assert.IsTrue(
                        pixel.R >= Math.Min(background.R, hover.R) - 1 && pixel.R <= Math.Max(background.R, hover.R) + 1
                        && pixel.G >= Math.Min(background.G, hover.G) - 1 && pixel.G <= Math.Max(background.G, hover.G) + 1
                        && pixel.B >= Math.Min(background.B, hover.B) - 1 && pixel.B <= Math.Max(background.B, hover.B) + 1,
                        $"{button.Name} hover alpha {alpha} rendered {pixel} outside the navigation palette.");
                }
            }
            finally
            {
                animation.SetValue(button, false);
            }

            using Bitmap settled = CaptureControl(button);
            Assert.AreEqual(hover.ToArgb(),
                GetRowBackground(settled, button.ClientRectangle, button.DeviceDpi).ToArgb());
            button.ExtraMouseHover = false;
        }
    }

    /// <summary>Checks that returning visits prior pages in order, reuses their state, and collapses navigation.</summary>
    [TestMethod]
    public void BackButtonReturnsThroughCachedPagesInOrderAndDisablesAtInitialPage()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        AntdUI.Panel contentPanel = GetControl<AntdUI.Panel>(form, "_contentPanel");
        AntdUI.Menu menu = GetControl<AntdUI.Menu>(form, "_navigationMenu");
        AntdUI.Button back = GetControl<AntdUI.Button>(form, "_backButton");
        Avatar logo = GetControl<Avatar>(form, "_navigationLogo");
        AntdUI.Label brand = GetControl<AntdUI.Label>(form, "_brandLabel");
        Size collapsedRowSize = menu.Items[0].Rect().Size;
        Control dashboard = GetVisiblePage(contentPanel);
        Assert.IsFalse(back.Enabled);
        Assert.IsFalse(back.Visible);
        Assert.IsTrue(logo.Visible);
        form.NavigateTo("LogsPage1");
        Control firstLogsPage = GetVisiblePage(contentPanel);
        form.NavigateTo("LogsPage2");
        Control secondLogsPage = GetVisiblePage(contentPanel);
        object state = new();
        secondLogsPage.Tag = state;
        form.NavigateTo("Devices");
        Control[] previousPages = [secondLogsPage, firstLogsPage, dashboard];

        foreach (Control previousPage in previousPages)
        {
            Assert.IsTrue(back.Visible);
            Assert.IsFalse(logo.Visible);
            Assert.IsFalse(brand.Visible);
            AssertTopNavigationGeometry(form, collapsedRowSize);
            InvokeClick(GetControl<AntdUI.Button>(form, "_collapseButton"));
            Assert.IsFalse(menu.Collapsed);
            Assert.IsTrue(back.Enabled);
            Assert.IsTrue(back.Visible);
            Assert.IsTrue(logo.Visible);
            Assert.IsTrue(brand.Visible);
            Assert.AreEqual("FbSample", brand.Text);
            AssertTopNavigationGeometry(form, collapsedRowSize);

            InvokeClick(back);

            Assert.AreSame(previousPage, GetVisiblePage(contentPanel));
            Assert.IsTrue(menu.Collapsed, "A successful return must collapse the sidebar.");
            Assert.IsTrue(FindMenuItem(menu, previousPage.Name).Select);
        }

        Assert.IsFalse(back.Enabled);
        Assert.IsFalse(back.Visible);
        Assert.IsTrue(logo.Visible, "The final return must restore the logo in the left navigation slot.");
        Assert.IsFalse(brand.Visible);
        AssertTopNavigationGeometry(form, collapsedRowSize);
        Assert.AreSame(state, secondLogsPage.Tag);
        Assert.AreEqual(4, contentPanel.Controls.Count);
        Assert.IsTrue(contentPanel.Controls.Cast<Control>().All(static page => !page.IsDisposed));
    }

    /// <summary>Checks navigation after returning records the current route without restoring an abandoned branch.</summary>
    [TestMethod]
    public void SelectingNewPageAfterBackUsesCurrentRouteAsPreviousPage()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        AntdUI.Panel contentPanel = GetControl<AntdUI.Panel>(form, "_contentPanel");
        AntdUI.Button back = GetControl<AntdUI.Button>(form, "_backButton");
        form.NavigateTo("Devices");
        Control devicesPage = GetVisiblePage(contentPanel);
        form.NavigateTo("Monitor");
        Control monitorPage = GetVisiblePage(contentPanel);
        InvokeClick(back);
        Assert.AreSame(devicesPage, GetVisiblePage(contentPanel));

        form.NavigateTo("DebugPage1");
        InvokeClick(back);

        Assert.AreSame(devicesPage, GetVisiblePage(contentPanel));
        InvokeClick(back);
        Assert.AreEqual("Dashboard", GetVisiblePage(contentPanel).Name);
        Assert.IsFalse(back.Enabled);
        Assert.IsFalse(monitorPage.IsDisposed);
        Assert.IsFalse(monitorPage.Visible);
        form.NavigateTo("Devices");
        form.NavigateTo("Dashboard");
        Assert.AreEqual("Dashboard", GetVisiblePage(contentPanel).Name);
        Assert.IsTrue(back.Enabled);
        Assert.IsTrue(back.Visible, "Dashboard must retain the return arrow when navigation history exists.");
        Assert.IsFalse(GetControl<Avatar>(form, "_navigationLogo").Visible);
        InvokeClick(back);
        Assert.AreSame(devicesPage, GetVisiblePage(contentPanel));
        InvokeClick(back);
        Assert.AreEqual("Dashboard", GetVisiblePage(contentPanel).Name);
        Assert.IsFalse(back.Visible);
        Assert.IsTrue(GetControl<Avatar>(form, "_navigationLogo").Visible);
    }

    /// <summary>Checks route synchronization leaves the user-selected expanded group unchanged.</summary>
    [TestMethod]
    public void LanguageAndCurrentRouteSynchronizationPreserveExpandedGroup()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        form.NavigateTo("LogsPage2");
        AntdUI.Menu menu = GetControl<AntdUI.Menu>(form, "_navigationMenu");
        AntdUI.MenuItem logs = FindMenuItem(menu, "Logs");
        AntdUI.MenuItem debug = FindMenuItem(menu, "Debug");
        InvokeClick(GetControl<AntdUI.Button>(form, "_collapseButton"));
        logs.Expand = false;
        debug.Expand = false;
        InvokeMenuClick(menu, debug);
        Assert.IsTrue(debug.Expand);

        form.SetLanguage("zh-TW");
        InvokeClick(GetControl<AntdUI.Button>(form, "_themeButton"));

        Assert.IsFalse(menu.Collapsed);
        Assert.IsTrue(debug.Expand);
        Assert.IsFalse(logs.Expand);
        Assert.AreEqual("LogsPage2", GetVisiblePage(GetControl<AntdUI.Panel>(form, "_contentPanel")).Name);
        form.NavigateTo("LogsPage2");
        Assert.IsTrue(menu.Collapsed);
        InvokeClick(GetControl<AntdUI.Button>(form, "_collapseButton"));
        Assert.IsTrue(debug.Expand);
        Assert.IsFalse(logs.Expand, "Synchronizing the current route must not open its ancestor group.");
    }

    /// <summary>Checks the first click submits a complete transparent child frame without queued work or activation.</summary>
    [TestMethod]
    public void FlyoutFirstClickWithAnimationDisplaysTextAndIconsInTheTransparentWindowImmediately()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        NavigationMenu menu = GetControl<NavigationMenu>(form, "_navigationMenu");
        AntdUI.MenuItem logs = FindMenuItem(menu, "Logs");
        logs.Expand = false;
        Config.Animation = true;
        Form[] existingWindows = [.. Application.OpenForms.Cast<Form>()];

        NavigationMenu flyout = OpenNavigationFlyout(menu, logs);

        // No message pump or paint helper may make delayed content ready before these assertions.
        Assert.AreSame(GetControl<NavigationMenu>(form, "_navigationFlyoutMenu"), flyout);
        NavigationFlyoutWindow? window = menu.FlyoutWindow;
        Assert.IsNotNull(window);
        Assert.IsTrue(window.RenderCache, "The opening handler must already have submitted its first layered frame.");
        Assert.IsFalse(window.ShowInTaskbar);
        Assert.IsFalse(window.ContainsFocus);
        Assert.IsTrue(menu.Focused, "A pointer-opened child window must retain focus in the main menu.");
        using Bitmap frame = window.PrintBit();
        Assert.AreEqual(window.TargetRect.Size, frame.Size);
        Assert.AreEqual(0, frame.GetPixel(0, 0).A);
        Assert.IsTrue(flyout.IsHandleCreated);
        Assert.AreEqual(DockStyle.Fill, flyout.Dock);
        Assert.AreEqual(2, flyout.Items.Count);
        foreach (AntdUI.MenuItem item in flyout.Items)
        {
            Rectangle row = item.Rect();
            Rectangle text = item.Rect("Text");
            Rectangle icon = item.Rect("Icon");
            Assert.IsTrue(flyout.ClientRectangle.Contains(row));
            Assert.IsGreaterThan(0, text.Width);
            Assert.IsGreaterThan(0, text.Height);
            Assert.IsGreaterThan(0, icon.Width);
            Assert.IsGreaterThan(0, icon.Height);
            Assert.IsTrue(row.Contains(text));
            Assert.IsTrue(row.Contains(icon));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.Text));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.IconSvg));
            text.Offset(flyout.Location);
            icon.Offset(flyout.Location);
            Assert.IsLessThan(100, GetDarkestGray(frame, text), "The first frame must contain the child's text pixels.");
            Assert.IsLessThan(100, GetDarkestGray(frame, icon), "The first frame must contain the child's icon pixels.");
        }

        CollectionAssert.AreEquivalent(existingWindows.Append(window).ToArray(), Application.OpenForms.Cast<Form>().ToArray(),
            "Child navigation must create only its transparent window, without a separate content host.");
        Assert.IsNull(menu.SubForm());
        Assert.IsFalse(logs.Expand);
        Assert.IsTrue(menu.Collapsed);
        Assert.AreEqual("Dashboard", GetVisiblePage(GetControl<AntdUI.Panel>(form, "_contentPanel")).Name);

        int uiThread = Environment.CurrentManagedThreadId;
        var drawingThreads = new ConcurrentQueue<int>();
        flyout.Draw += (_, _) => drawingThreads.Enqueue(Environment.CurrentManagedThreadId);
        Task.Run(() => flyout.Invalidate()).GetAwaiter().GetResult();
        Application.DoEvents();

        int[] observedThreads = [.. drawingThreads];
        Assert.IsGreaterThan(0, observedThreads.Length, "A worker invalidation must repaint through the owning UI thread's dispatch.");
        foreach (int drawingThread in observedThreads)
        {
            Assert.AreEqual(uiThread, drawingThread, "The child frame must only draw on its owning UI thread.");
        }
    }

    /// <summary>Checks alpha composition is independent of changing Settings content and runtime windows are released.</summary>
    /// <param name="dark">Whether to render the dark palette.</param>
    /// <param name="shadow">Whether to include the translucent shadow.</param>
    [TestMethod]
    [TestCategory("Rendering")]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void FlyoutAlphaPreservesDynamicSettingsBackgroundAndReleasesEveryRuntimeWindow(bool dark, bool shadow)
    {
        Config.IsLight = !dark;
        Config.ShadowEnabled = shadow;
        using var form = new OffscreenMainForm();
        form.Show();
        form.NavigateTo("Settings");
        NavigationMenu menu = GetControl<NavigationMenu>(form, "_navigationMenu");
        AntdUI.MenuItem logs = FindMenuItem(menu, "Logs");
        var settings = (SettingsPage)GetVisiblePage(GetControl<AntdUI.Panel>(form, "_contentPanel"));
        AntdUI.Panel card = GetControl<AntdUI.Panel>(settings, "_basicSettingsPanel");
        NavigationMenu content = OpenNavigationFlyout(menu, logs);
        NavigationFlyoutWindow? window = menu.FlyoutWindow;
        Assert.IsNotNull(window);
        AntdUI.Panel source = GetControl<AntdUI.Panel>(menu, "_flyoutPanel");
        using Bitmap original = window.PrintBit();

        Assert.AreEqual(0, original.GetPixel(0, 0).A);
        Assert.AreEqual(0, original.GetPixel(original.Width - 1, original.Height - 1).A);
        Rectangle cardBounds = source.ReadRectangle;
        Color cardPixel = original.GetPixel(cardBounds.Left + cardBounds.Width / 2, cardBounds.Top + 2);
        Assert.AreEqual(byte.MaxValue, cardPixel.A, "The card interior must remain opaque.");
        if (shadow)
        {
            Color shadowPixel = original.GetPixel(original.Width / 2, cardBounds.Top / 2);
            Assert.IsTrue(shadowPixel.A is > 0 and < byte.MaxValue,
                "The shadow must carry partial alpha instead of a copied page color.");
        }
        else
        {
            Assert.AreEqual(0, source.Shadow);
        }

        for (int y = content.Top; y < content.Bottom; y++)
        {
            for (int x = content.Left; x < content.Right; x++)
            {
                Assert.AreEqual(byte.MaxValue, original.GetPixel(x, y).A,
                    "Menu rows, text and icons must be fully composed over the opaque card.");
            }
        }

        card.Back = Color.Magenta;
        GetControl<AntdUI.Label>(settings, "_basicSettingsLabel").Text = "Updated beneath navigation";
        settings.Refresh();
        Assert.AreSame(window, menu.FlyoutWindow);
        using (Bitmap updated = window.PrintBit())
        {
            AssertBitmapRegionEqual(original, new Rectangle(Point.Empty, original.Size),
                updated, new Rectangle(Point.Empty, updated.Size),
                "Changing the underlying page must not be copied into the transparent navigation frame.");
        }

        menu.CloseFlyout();
        Assert.IsNull(menu.FlyoutWindow);
        Assert.IsTrue(window.IsDisposed);
        Assert.IsFalse(Application.OpenForms.Cast<Form>().Contains(window));
        Assert.AreSame(settings, GetVisiblePage(GetControl<AntdUI.Panel>(form, "_contentPanel")));
        Assert.AreSame(content, OpenNavigationFlyout(menu, logs));
        NavigationFlyoutWindow? reopened = menu.FlyoutWindow;
        Assert.IsNotNull(reopened);
        Assert.AreNotSame(window, reopened);
        using (Bitmap fresh = reopened.PrintBit())
        {
            AssertBitmapRegionEqual(original, new Rectangle(Point.Empty, original.Size),
                fresh, new Rectangle(Point.Empty, fresh.Size), "Reopening must not retain the changed page background.");
        }

        form.Close();
        Assert.IsTrue(reopened.IsDisposed);
        Assert.IsTrue(content.IsDisposed);
        Assert.IsTrue(source.IsDisposed);
    }

    /// <summary>Checks localized child menus share one measured width across repeated changes in row count.</summary>
    [TestMethod]
    public void FlyoutWidthFitsEveryLanguageAndStaysStableAcrossBidirectionalGroupSwitches()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        NavigationMenu menu = GetControl<NavigationMenu>(form, "_navigationMenu");
        AntdUI.Panel panel = GetControl<AntdUI.Panel>(form, "_navigationFlyoutPanel");
        AntdUI.MenuItem logs = FindMenuItem(menu, "Logs");
        AntdUI.MenuItem debug = FindMenuItem(menu, "Debug");
        var widths = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string language in s_supportedLanguages)
        {
            form.SetLanguage(language);
            NavigationMenu flyout = OpenNavigationFlyout(menu, debug);
            AssertFlyoutItemsFit(menu, flyout);
            int width = panel.Width;
            int singleRowHeight = panel.Height;
            widths.Add(language, width);
            for (int repeat = 0; repeat < 3; repeat++)
            {
                menu.CloseFlyout();
                Assert.AreSame(flyout, OpenNavigationFlyout(menu, logs));
                Assert.AreEqual(width, panel.Width, "Changing from one row to two must not retain a scrollbar inset.");
                Assert.AreEqual(2, flyout.Items.Count);
                Assert.IsGreaterThan(singleRowHeight, panel.Height);
                AssertFlyoutItemsFit(menu, flyout);

                menu.CloseFlyout();
                Assert.AreSame(flyout, OpenNavigationFlyout(menu, debug));
                Assert.AreEqual(width, panel.Width, "Returning to one row must not change the shared width.");
                Assert.AreEqual(singleRowHeight, panel.Height);
                Assert.AreEqual(1, flyout.Items.Count);
                AssertFlyoutItemsFit(menu, flyout);
            }

            menu.CloseFlyout();
            AntdUI.Button collapse = GetControl<AntdUI.Button>(form, "_collapseButton");
            InvokeClick(collapse);
            InvokeClick(collapse);
            Assert.AreSame(flyout, OpenNavigationFlyout(menu, debug));
            Assert.AreEqual(width, panel.Width, "Main-menu text spacing must not change the compact child-panel width.");
            AssertFlyoutItemsFit(menu, flyout);
            menu.CloseFlyout();
        }

        Assert.IsGreaterThan(widths["zh-CN"], widths["en-US"]);
        Assert.IsGreaterThan(widths["zh-TW"], widths["en-US"]);
        form.SetLanguage("en-US");
        AssertFlyoutItemsFit(menu, OpenNavigationFlyout(menu, logs));
        Assert.AreEqual(widths["en-US"], panel.Width, "Returning to English must restore its measured width.");
    }

    /// <summary>Checks search-hidden child labels still determine the common width of visible groups.</summary>
    [TestMethod]
    public void FlyoutWidthIncludesTheLongestChildWhenSearchHidesItsGroup()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        NavigationMenu menu = GetControl<NavigationMenu>(form, "_navigationMenu");
        AntdUI.MenuItem logs = FindMenuItem(menu, "Logs");
        AntdUI.MenuItem debug = FindMenuItem(menu, "Debug");
        NavigationMenu flyout = OpenNavigationFlyout(menu, logs);
        AntdUI.Panel panel = GetControl<AntdUI.Panel>(form, "_navigationFlyoutPanel");
        int originalWidth = panel.Width;
        int logsHeight = panel.Height;
        flyout.GDI(canvas => Assert.IsGreaterThan(
            canvas.MeasureText(logs.Sub[0].Text, flyout.Font).Width,
            canvas.MeasureText(debug.Sub[0].Text, flyout.Font).Width,
            "The English Debug child must exercise the longest hidden label."));

        Input search = GetControl<Input>(form, "_searchInput");
        search.Text = "Logs";
        Assert.IsFalse(debug.Visible);
        Assert.IsFalse(debug.Sub[0].Visible);
        Assert.IsNull(menu.FlyoutWindow);
        Assert.AreSame(flyout, OpenNavigationFlyout(menu, logs));
        Assert.AreEqual(originalWidth, panel.Width, "Filtering out the longest child must not shrink another group.");
        Assert.AreEqual(logsHeight, panel.Height);
        AssertFlyoutItemsFit(menu, flyout);

        search.Text = string.Empty;
        Assert.AreSame(flyout, OpenNavigationFlyout(menu, debug));
        Assert.AreEqual(originalWidth, panel.Width);
        AssertFlyoutItemsFit(menu, flyout);
    }

    /// <summary>Checks reopening navigation measures the current font instead of retaining a previous width.</summary>
    [TestMethod]
    public void FlyoutFontChangeRemeasuresTheSharedWidthAndKeepsEveryChildFullyVisible()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        NavigationMenu menu = GetControl<NavigationMenu>(form, "_navigationMenu");
        AntdUI.MenuItem logs = FindMenuItem(menu, "Logs");
        AntdUI.MenuItem debug = FindMenuItem(menu, "Debug");
        NavigationMenu flyout = OpenNavigationFlyout(menu, logs);
        AntdUI.Panel panel = GetControl<AntdUI.Panel>(form, "_navigationFlyoutPanel");
        int originalWidth = panel.Width;
        menu.CloseFlyout();
        Font originalFont = menu.Font;
        using var enlargedFont = new Font(originalFont.FontFamily, originalFont.Size + 6F,
            originalFont.Style, originalFont.Unit);
        try
        {
            menu.Font = enlargedFont;
            Assert.AreSame(flyout, OpenNavigationFlyout(menu, debug));
            Assert.AreEqual(enlargedFont, flyout.Font);
            Assert.IsGreaterThan(originalWidth, panel.Width);
            int enlargedWidth = panel.Width;
            AssertFlyoutItemsFit(menu, flyout);
            menu.CloseFlyout();
            Assert.AreSame(flyout, OpenNavigationFlyout(menu, logs));
            Assert.AreEqual(enlargedWidth, panel.Width);
            AssertFlyoutItemsFit(menu, flyout);
        }
        finally
        {
            menu.CloseFlyout();
            menu.Font = originalFont;
        }

        AssertFlyoutItemsFit(menu, OpenNavigationFlyout(menu, logs));
        Assert.AreEqual(originalWidth, panel.Width, "Restoring the font must restore its measured width.");
    }

    /// <summary>Checks hovering never opens, switches or closes a child panel, while the same parent toggles it.</summary>
    [TestMethod]
    public void FlyoutHoverAndLeavePreserveStateAndRepeatedParentClicksToggleVisibility()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        NavigationMenu menu = GetControl<NavigationMenu>(form, "_navigationMenu");
        AntdUI.MenuItem logs = FindMenuItem(menu, "Logs");
        AntdUI.MenuItem debug = FindMenuItem(menu, "Debug");
        logs.Expand = false;
        debug.Expand = false;
        MethodInfo? hover = typeof(NavigationMenu).GetMethod("OnMouseHover",
            BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(int), typeof(int)], null);
        MethodInfo? leave = typeof(NavigationMenu).GetMethod("OnMouseLeave", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(hover);
        Assert.IsNotNull(leave);
        Rectangle logsRow = logs.Rect();
        hover.Invoke(menu, [logsRow.Left + logsRow.Width / 2, logsRow.Top + logsRow.Height / 2]);
        Assert.IsNull(menu.FlyoutWindow, "Hovering a collapsed parent must not open child navigation.");

        NavigationMenu flyout = OpenNavigationFlyout(menu, logs);
        AntdUI.MenuItem[] items = [.. flyout.Items];
        Rectangle debugRow = debug.Rect();
        var debugCenter = new Point(debugRow.Left + debugRow.Width / 2, debugRow.Top + debugRow.Height / 2);
        InvokeMouseMove(menu, debugCenter);
        hover.Invoke(menu, [debugCenter.X, debugCenter.Y]);
        leave.Invoke(menu, [EventArgs.Empty]);
        NavigationFlyoutWindow? window = menu.FlyoutWindow;
        Assert.IsNotNull(window);
        MethodInfo? windowLeave = typeof(NavigationFlyoutWindow).GetMethod("OnMouseLeave",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(windowLeave);
        windowLeave.Invoke(window, [EventArgs.Empty]);
        var moveOutside = System.Windows.Forms.Message.Create(form.Handle, 0x0200, 0, 0);
        Assert.IsFalse(((IMessageFilter)menu).PreFilterMessage(ref moveOutside));
        Application.DoEvents();

        Assert.IsTrue(menu.FlyoutWindow is { Visible: true }, "Hovering another parent or leaving both menus must preserve the open panel.");
        CollectionAssert.AreEqual(items, flyout.Items.ToArray());
        var padding = new Point(menu.ClientSize.Width - 1, debugCenter.Y);
        Assert.IsNull(menu.HitTest(padding.X, padding.Y));
        InvokeMenuClick(menu, debug, padding);
        Assert.IsTrue(menu.FlyoutWindow is { Visible: true }, "Pressing another parent and releasing outside its row must cancel the click.");
        CollectionAssert.AreEqual(items, flyout.Items.ToArray());
        InvokeMenuClick(menu, logs);
        Assert.IsNull(menu.FlyoutWindow, "Clicking the same parent must close its panel.");
        Assert.IsTrue(window.IsDisposed);
        Assert.IsFalse(flyout.IsDisposed);
        InvokeMenuClick(menu, logs, padding);
        Assert.IsNull(menu.FlyoutWindow, "A cancelled click must not open a closed child panel.");
        Assert.AreSame(flyout, OpenNavigationFlyout(menu, logs));
        Assert.IsFalse(logs.Expand);
        Assert.IsFalse(debug.Expand);
        Assert.IsFalse(GetControl<AntdUI.Button>(form, "_backButton").Enabled);
    }

    /// <summary>Checks padding clicks stay inside navigation and window changes release only the runtime host.</summary>
    [TestMethod]
    public void FlyoutBlankClicksPreserveVisibilityAndWindowChangesHideReusableControls()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        NavigationMenu menu = GetControl<NavigationMenu>(form, "_navigationMenu");
        AntdUI.MenuItem logs = FindMenuItem(menu, "Logs");
        NavigationMenu flyout = OpenNavigationFlyout(menu, logs);
        AntdUI.Panel panel = GetControl<AntdUI.Panel>(form, "_navigationFlyoutPanel");
        NavigationFlyoutWindow? window = menu.FlyoutWindow;
        Assert.IsNotNull(window);
        int paddingX = panel.ReadRectangle.Left + panel.ReadRectangle.Width / 2;
        int paddingY = panel.ReadRectangle.Top + 2;
        int location = (paddingY << 16) | paddingX;
        var blankClick = System.Windows.Forms.Message.Create(window.Handle, 0x0201, 0, location);

        Assert.IsFalse(((IMessageFilter)menu).PreFilterMessage(ref blankClick));
        _ = SendMessage(window.Handle, 0x0201, 1, location);
        _ = SendMessage(window.Handle, 0x0202, 0, location);
        Assert.IsTrue(menu.FlyoutWindow is { Visible: true }, "A click within the child panel's padding must keep it open.");
        Assert.AreEqual("Dashboard", GetVisiblePage(GetControl<AntdUI.Panel>(form, "_contentPanel")).Name);

        form.ClientSize = new Size(form.ClientSize.Width + 40, form.ClientSize.Height + 20);
        Application.DoEvents();
        Assert.IsNull(menu.FlyoutWindow, "Resizing the owner must close child navigation.");
        Assert.IsTrue(window.IsDisposed);
        Assert.IsFalse(flyout.IsDisposed);
        Assert.AreSame(flyout, OpenNavigationFlyout(menu, logs));
        window = menu.FlyoutWindow;
        Assert.IsNotNull(window);
        MethodInfo? deactivate = typeof(Form).GetMethod("OnDeactivate", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(deactivate);
        deactivate.Invoke(form, [EventArgs.Empty]);
        Application.DoEvents();
        Assert.IsNull(menu.FlyoutWindow, "Deactivating the owner must close child navigation.");
        Assert.IsTrue(window.IsDisposed);
        Assert.IsFalse(flyout.IsDisposed);
        Assert.IsFalse(GetControl<AntdUI.Button>(form, "_backButton").Enabled);
    }

    /// <summary>Checks current rows, pointer feedback and shell borders retain their agreed colors in both themes.</summary>
    /// <param name="dark">Whether to use the dark navigation palette.</param>
    [TestMethod]
    [TestCategory("Rendering")]
    [DataRow(false)]
    [DataRow(true)]
    public void NavigationRowsRenderSelectedHoverAndPressedColorsForBothThemes(bool dark)
    {
        using var form = new OffscreenMainForm();
        form.Show();
        Config.IsLight = !dark;
        NavigationMenu menu = GetControl<NavigationMenu>(form, "_navigationMenu");
        AntdUI.Panel panel = GetControl<AntdUI.Panel>(form, "_navigationFlyoutPanel");
        Assert.AreEqual(dark ? Color.FromArgb(62, 65, 70) : Color.FromArgb(225, 230, 234),
            GetControl<AntdUI.Panel>(form, "_navigationDivider").Back);
        Assert.AreEqual(dark ? Color.FromArgb(62, 65, 70) : Color.FromArgb(204, 204, 204), panel.BorderColor);
        Color selected = dark ? Color.FromArgb(54, 56, 60) : Color.FromArgb(238, 238, 238);
        Color hover = dark ? Color.FromArgb(51, 54, 59) : Color.FromArgb(239, 239, 239);
        Color pressed = dark ? Color.FromArgb(65, 68, 73) : Color.FromArgb(226, 226, 226);
        MethodInfo? mouseDown = typeof(NavigationMenu).GetMethod("OnMouseDown",
            BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo? mouseUp = typeof(NavigationMenu).GetMethod("OnMouseUp",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(mouseDown);
        Assert.IsNotNull(mouseUp);
        foreach (string route in s_leafRoutes)
        {
            form.NavigateTo(route);
            InvokeMouseMove(menu, new Point(-1, -1));
            AntdUI.MenuItem current = FindMenuItem(menu, route);
            AntdUI.MenuItem root = current.ParentItem ?? current;
            using (Bitmap idle = CaptureControl(menu))
            {
                Assert.AreEqual((root.CanExpand ? menu.BackColor : selected).ToArgb(),
                    GetRowBackground(idle, root.Rect(), menu.DeviceDpi).ToArgb(),
                    $"The current collapsed {root.Name} row must distinguish groups from standalone pages.");
            }

            Rectangle row = root.Rect();
            var center = new Point(row.Left + row.Width / 2, row.Top + row.Height / 2);
            InvokeMouseMove(menu, center);
            using (Bitmap hovered = CaptureControl(menu))
            {
                Assert.AreEqual((root.CanExpand ? hover : selected).ToArgb(),
                    GetRowBackground(hovered, row, menu.DeviceDpi).ToArgb(),
                    "Hover feedback must preserve the selected background of a current standalone page.");
            }

            mouseDown.Invoke(menu, [new MouseEventArgs(MouseButtons.Left, 1, center.X, center.Y, 0)]);
            using (Bitmap down = CaptureControl(menu))
            {
                Assert.AreEqual(pressed.ToArgb(), GetRowBackground(down, row, menu.DeviceDpi).ToArgb());
            }

            InvokeMouseMove(menu, new Point(-1, -1));
            mouseUp.Invoke(menu, [new MouseEventArgs(MouseButtons.Left, 1, -1, -1, 0)]);
            Assert.IsNull(menu.FlyoutWindow, "Releasing outside the pressed row must not open a child panel.");
        }

        form.NavigateTo("LogsPage2");
        NavigationMenu flyout = OpenNavigationFlyout(menu, FindMenuItem(menu, "Logs"));
        using (Bitmap popup = CaptureControl(flyout))
        {
            Rectangle row = FindMenuItem(flyout, "LogsPage2").Rect();
            Assert.AreEqual((dark ? selected : Color.FromArgb(237, 237, 237)).ToArgb(),
                GetRowBackground(popup, row, flyout.DeviceDpi).ToArgb());
            Rectangle indicator = GetIndicatorBounds(popup, new Rectangle(Point.Empty, popup.Size));
            Assert.IsFalse(indicator.IsEmpty);
            float scale = flyout.DeviceDpi / 96F;
            Assert.AreEqual(row.Left + 3 * scale, indicator.Left, 2F,
                "The child indicator must keep its inset from the selected row in both themes.");
            Assert.AreEqual(row.Top + (row.Height - 16 * scale) / 2, indicator.Top, 2F,
                "The child indicator must stay vertically centered without a host-coordinate offset.");
            Assert.AreEqual(16 * scale, indicator.Height, 2F);
        }

        menu.CloseFlyout();
        InvokeMouseMove(menu, new Point(-1, -1));
        InvokeClick(GetControl<AntdUI.Button>(form, "_collapseButton"));
        FindMenuItem(menu, "Logs").Expand = true;
        using Bitmap expanded = CaptureControl(menu);
        Assert.AreEqual(selected.ToArgb(),
            GetRowBackground(expanded, FindMenuItem(menu, "LogsPage2").Rect(), menu.DeviceDpi).ToArgb());
    }

    /// <summary>Checks parent clicks close an open group before a later click opens another, preserving route and indicators.</summary>
    [TestMethod]
    [TestCategory("Rendering")]
    public void FlyoutGroupsUseIndependentItemsAndKeepIndicatorsOnTheCurrentRoute()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        form.NavigateTo("LogsPage2");
        Config.IsLight = true;
        NavigationMenu menu = GetControl<NavigationMenu>(form, "_navigationMenu");
        AntdUI.MenuItem logs = FindMenuItem(menu, "Logs");
        AntdUI.MenuItem debug = FindMenuItem(menu, "Debug");
        using (Bitmap collapsed = CaptureControl(menu))
        {
            Assert.IsFalse(GetIndicatorBounds(collapsed, logs.Rect()).IsEmpty);
            Assert.AreEqual(menu.BackColor.ToArgb(), GetRowBackground(collapsed, logs.Rect(), menu.DeviceDpi).ToArgb(),
                "The current collapsed group must not have a persistent selected background.");
        }

        NavigationMenu logsFlyout = OpenNavigationFlyout(menu, logs);
        Assert.AreEqual(2, logsFlyout.Items.Count);
        Assert.AreEqual("LogsPage1", logsFlyout.Items[0].Name);
        Assert.AreEqual("LogsPage2", logsFlyout.Items[1].Name);
        Assert.AreNotSame(logs.Sub[0], logsFlyout.Items[0]);
        Assert.AreNotSame(logs.Sub[1], logsFlyout.Items[1]);
        Assert.AreSame(logs, logs.Sub[1].ParentItem);
        using (Bitmap rail = CaptureControl(menu))
        using (Bitmap popup = CaptureControl(logsFlyout))
        {
            Assert.IsTrue(GetIndicatorBounds(rail, logs.Rect()).IsEmpty);
            Assert.IsTrue(GetIndicatorBounds(popup, logsFlyout.Items[0].Rect()).IsEmpty);
            Rectangle indicator = GetIndicatorBounds(popup, logsFlyout.Items[1].Rect());
            Assert.IsFalse(indicator.IsEmpty);
            Assert.AreEqual(16 * (logsFlyout.DeviceDpi / 96F), indicator.Height, 2F,
                "The rendered indicator must retain its 16 logical-pixel height.");
            Assert.AreEqual(Color.FromArgb(237, 237, 237).ToArgb(),
                GetRowBackground(popup, logsFlyout.Items[1].Rect(), logsFlyout.DeviceDpi).ToArgb());
        }

        RenderControl(logsFlyout, "navigation-logs-flyout-light.png");
        Assert.AreSame(logsFlyout.Items[1], logsFlyout.SelectItem);
        AntdUI.Panel contentPanel = GetControl<AntdUI.Panel>(form, "_contentPanel");
        Control currentPage = GetVisiblePage(contentPanel);
        InvokeMenuClick(menu, debug);
        Assert.IsNull(menu.FlyoutWindow, "The first click on Debug must only close the open Logs panel.");
        Assert.AreSame(currentPage, GetVisiblePage(contentPanel));
        Assert.AreEqual(2, contentPanel.Controls.Count);
        NavigationMenu debugFlyout = OpenNavigationFlyout(menu, debug);
        Assert.AreSame(logsFlyout, debugFlyout, "Changing groups must reuse the designer-owned child menu.");
        Assert.IsFalse(logsFlyout.IsDisposed);
        Assert.AreEqual(1, debugFlyout.Items.Count);
        Assert.AreEqual("DebugPage1", debugFlyout.Items[0].Name);
        Assert.AreNotSame(debug.Sub[0], debugFlyout.Items[0]);
        Assert.IsNull(debugFlyout.SelectItem, "Switching groups must clear the removed LogsPage2 selection.");
        using (Bitmap rail = CaptureControl(menu))
        using (Bitmap popup = CaptureControl(debugFlyout))
        {
            Assert.IsFalse(GetIndicatorBounds(rail, logs.Rect()).IsEmpty);
            Assert.IsTrue(GetIndicatorBounds(rail, debug.Rect()).IsEmpty);
            Assert.IsTrue(GetIndicatorBounds(popup, debugFlyout.Items[0].Rect()).IsEmpty);
        }

        InvokeMenuClick(menu, logs);
        Assert.IsNull(menu.FlyoutWindow, "The first click on Logs must only close the open Debug panel.");
        Assert.AreSame(currentPage, GetVisiblePage(contentPanel));
        Assert.AreSame(logsFlyout, OpenNavigationFlyout(menu, logs));
        Assert.AreEqual(2, logsFlyout.Items.Count);
        InvokeMenuClick(menu, debug);
        Assert.IsNull(menu.FlyoutWindow);
        Assert.AreSame(debugFlyout, OpenNavigationFlyout(menu, debug));
        Assert.IsNull(debugFlyout.SelectItem);
        Assert.AreSame(currentPage, GetVisiblePage(contentPanel));
        Assert.IsTrue(menu.Collapsed);
        MethodInfo? processKey = typeof(NavigationMenu).GetMethod(
            "ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(processKey);
        var up = System.Windows.Forms.Message.Create(menu.Handle, 0x0100, (nint)Keys.Up, 0);
        processKey.Invoke(menu, [up, Keys.Up]);
        Assert.AreSame(debugFlyout.Items[0], debugFlyout.SelectItem);
        Assert.AreEqual("DebugPage1", GetVisiblePage(GetControl<AntdUI.Panel>(form, "_contentPanel")).Name);
        Assert.IsNull(menu.FlyoutWindow, "Keyboard selection must navigate using the new group's item and close its panel.");
        AntdUI.Button back = GetControl<AntdUI.Button>(form, "_backButton");
        InvokeClick(back);
        Assert.AreSame(currentPage, GetVisiblePage(contentPanel));
        InvokeClick(back);
        Assert.AreEqual("Dashboard", GetVisiblePage(contentPanel).Name);
        Assert.IsFalse(back.Enabled, "Opening and closing either group must not add navigation history.");
    }

    /// <summary>Checks repeated flyout selection preserves focus, history and the previously expanded group.</summary>
    [TestMethod]
    public void FlyoutLeafSelectionReusesCurrentPageAndPreservesGroupState()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        form.NavigateTo("LogsPage2");
        NavigationMenu menu = GetControl<NavigationMenu>(form, "_navigationMenu");
        AntdUI.Panel contentPanel = GetControl<AntdUI.Panel>(form, "_contentPanel");
        Control currentPage = GetVisiblePage(contentPanel);
        AntdUI.MenuItem logs = FindMenuItem(menu, "Logs");
        AntdUI.MenuItem debug = FindMenuItem(menu, "Debug");
        logs.Expand = false;
        debug.Expand = true;
        NavigationMenu flyout = OpenNavigationFlyout(menu, logs);
        Rectangle parentRow = logs.Rect();
        var gap = new Point(menu.ClientSize.Width - 1, parentRow.Top + parentRow.Height / 2);
        Assert.IsNull(menu.HitTest(gap.X, gap.Y));

        InvokeMouseMove(menu, gap);

        Assert.IsFalse(flyout.IsDisposed, "Crossing the rail padding must not close the child popup.");
        Assert.AreSame(flyout, GetControl<NavigationMenu>(menu, "_flyoutMenu"));
        NavigationFlyoutWindow? window = menu.FlyoutWindow;
        Assert.IsNotNull(window);
        Assert.IsTrue(menu.Focused, "The main menu retains keyboard focus while its transparent child is open.");
        Assert.IsFalse(flyout.CanFocus, "The hidden drawing source must not acquire native focus.");
        Assert.IsFalse(window.ContainsFocus);
        int popupFocusAcquisitions = 0;
        flyout.GotFocus += (_, _) => popupFocusAcquisitions++;

        InvokeMenuClick(flyout, FindMenuItem(flyout, "LogsPage1"), new Point(-1, -1));
        Assert.AreSame(window, menu.FlyoutWindow, "Dragging a child press into panel padding must cancel navigation.");
        Assert.IsFalse(window.Capture);
        Assert.AreSame(currentPage, GetVisiblePage(contentPanel));
        InvokeMenuClick(flyout, FindMenuItem(flyout, "LogsPage2"));

        Assert.AreEqual(0, popupFocusAcquisitions, "A pointer selection must not move keyboard focus into the popup.");
        Assert.AreSame(currentPage, GetVisiblePage(contentPanel));
        Assert.IsNull(menu.FlyoutWindow);
        Assert.IsTrue(window.IsDisposed);
        Assert.IsFalse(flyout.IsDisposed, "Closing the child panel must preserve its designer-owned menu.");
        Assert.IsTrue(menu.Collapsed);
        Assert.IsFalse(logs.Expand);
        Assert.IsTrue(debug.Expand);
        Assert.AreEqual(2, contentPanel.Controls.Count);
        AntdUI.Button back = GetControl<AntdUI.Button>(form, "_backButton");
        InvokeClick(back);
        Assert.AreEqual("Dashboard", GetVisiblePage(contentPanel).Name);
        Assert.IsFalse(back.Enabled);
    }

    /// <summary>Checks keyboard focus, grouping and Enter selection through the shared child popup.</summary>
    [TestMethod]
    public void KeyboardNavigationCanFocusMenuAndOpenTheSharedFlyoutWithoutChangingRoute()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        form.NavigateTo("Monitor");
        NavigationMenu menu = GetControl<NavigationMenu>(form, "_navigationMenu");
        AntdUI.MenuItem logs = FindMenuItem(menu, "Logs");
        logs.Expand = false;
        Assert.IsTrue(menu.CanSelect, "Tab navigation must be able to reach the main menu.");
        Assert.AreEqual(TFocusMode.None, menu.FocusMode);
        MethodInfo? processKey = typeof(NavigationMenu).GetMethod(
            "ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(processKey);
        var key = System.Windows.Forms.Message.Create(menu.Handle, 0x0100, (nint)Keys.Down, 0);
        Assert.IsTrue(menu.Focus());

        processKey.Invoke(menu, [key, Keys.Down]);

        Assert.AreSame(logs, menu.SelectItem);
        Assert.AreEqual("Monitor", GetVisiblePage(GetControl<AntdUI.Panel>(form, "_contentPanel")).Name);
        processKey.Invoke(menu, [key, Keys.Right]);
        NavigationMenu flyout = GetReadyNavigationFlyout(menu);
        Assert.IsTrue(menu.Focused, "Keyboard focus must stay with the main menu while it forwards child keys.");
        Assert.IsFalse(flyout.CanFocus);
        Assert.IsNotNull(menu.FlyoutWindow);
        Assert.IsFalse(menu.FlyoutWindow.ContainsFocus);
        Assert.AreEqual("LogsPage1", flyout.SelectItem?.Name);
        using (Bitmap focused = CaptureControl(flyout))
        {
            Rectangle row = flyout.Items[0].Rect();
            bool hasDashedOutline = false;
            // The upper edge excludes text and icons; the focus stroke is blue-gray, not neutral gray.
            for (int y = row.Top; y < row.Top + 4; y++)
            {
                bool hasStroke = false;
                bool hasGap = false;
                for (int x = row.Left + 10; x < row.Right - 10; x++)
                {
                    Color pixel = focused.GetPixel(x, y);
                    hasStroke |= pixel.R < pixel.G && pixel.G < pixel.B && pixel.B < 200;
                    hasGap |= pixel.ToArgb() == flyout.BackColor.ToArgb();
                }

                hasDashedOutline |= hasStroke && hasGap;
            }

            Assert.IsTrue(hasDashedOutline,
                "The logically focused child must retain its visible dashed outline without taking native focus.");
            int left = focused.Width;
            int top = focused.Height;
            int right = -1;
            int bottom = -1;
            for (int y = 0; y < focused.Height; y++)
            {
                for (int x = 0; x < focused.Width; x++)
                {
                    Color pixel = focused.GetPixel(x, y);
                    if (pixel.R < pixel.G && pixel.G < pixel.B && pixel.B < 200)
                    {
                        left = Math.Min(left, x);
                        top = Math.Min(top, y);
                        right = Math.Max(right, x);
                        bottom = Math.Max(bottom, y);
                    }
                }
            }

            Rectangle expectedOutline = row;
            expectedOutline.Inflate(-2, -2);
            Assert.AreEqual(expectedOutline.Left, left, 2F, "The focus outline's left edge must follow its menu row.");
            Assert.AreEqual(expectedOutline.Top, top, 2F, "The focus outline's top edge must follow its menu row.");
            Assert.AreEqual(expectedOutline.Right, right + 1, 2F,
                "The focus outline must reach the row's right edge instead of crossing its text.");
            Assert.AreEqual(expectedOutline.Bottom, bottom + 1, 2F,
                "The focus outline must reach the row's bottom edge instead of shifting toward its text.");
        }

        Assert.IsNull(menu.SubForm(), "Keyboard navigation must use the same popup as pointer navigation.");
        processKey.Invoke(menu, [key, Keys.Left]);
        Assert.IsNull(menu.FlyoutWindow);
        Assert.IsTrue(menu.Focused, "Left must return focus to the main menu without navigating.");
        Assert.AreEqual("Monitor", GetVisiblePage(GetControl<AntdUI.Panel>(form, "_contentPanel")).Name);
        processKey.Invoke(menu, [key, Keys.Enter]);
        Assert.AreSame(flyout, GetReadyNavigationFlyout(menu));

        Assert.IsFalse(menu.RectangleToScreen(menu.ClientRectangle).Contains(Control.MousePosition));
        Assert.IsNotNull(menu.FlyoutWindow);
        Assert.IsFalse(menu.FlyoutWindow.TargetRect.Contains(Control.MousePosition));
        var stationaryMove = System.Windows.Forms.Message.Create(form.Handle, 0x0200, 0, 0);
        Assert.IsFalse(((IMessageFilter)menu).PreFilterMessage(ref stationaryMove));
        Assert.IsTrue(menu.FlyoutWindow is { Visible: true },
            "Pointer movement must not close a keyboard-opened child panel.");

        processKey.Invoke(menu, [key, Keys.Enter]);

        Assert.IsNull(menu.FlyoutWindow);
        Assert.IsFalse(flyout.IsDisposed);
        Assert.AreEqual("LogsPage1", GetVisiblePage(GetControl<AntdUI.Panel>(form, "_contentPanel")).Name);
        AntdUI.Button back = GetControl<AntdUI.Button>(form, "_backButton");
        InvokeClick(back);
        Assert.AreEqual("Monitor", GetVisiblePage(GetControl<AntdUI.Panel>(form, "_contentPanel")).Name);
        InvokeClick(back);
        Assert.IsFalse(back.Enabled, "Opening the keyboard flyout must not add navigation history.");
    }

    /// <summary>Checks Escape and outside clicks dismiss a flyout without navigating or consuming the outside click.</summary>
    [TestMethod]
    public void FlyoutDismissalPreservesRouteAndHistoryAndMainFormDisposesOpenFlyout()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        form.NavigateTo("LogsPage2");
        NavigationMenu menu = GetControl<NavigationMenu>(form, "_navigationMenu");
        AntdUI.MenuItem logs = FindMenuItem(menu, "Logs");
        NavigationMenu escapedFlyout = OpenNavigationFlyout(menu, logs);
        AntdUI.Panel flyoutPanel = GetControl<AntdUI.Panel>(menu, "_flyoutPanel");
        NavigationFlyoutWindow? escapedWindow = menu.FlyoutWindow;
        Assert.IsNotNull(escapedWindow);
        var escape = System.Windows.Forms.Message.Create(menu.Handle, 0x0100, (nint)Keys.Escape, 0);

        Assert.IsTrue(((IMessageFilter)menu).PreFilterMessage(ref escape));

        Assert.IsNull(menu.FlyoutWindow);
        Assert.IsTrue(escapedWindow.IsDisposed);
        Assert.IsFalse(escapedFlyout.IsDisposed);
        Assert.IsTrue(menu.Focused, "Escape must return keyboard focus to the main menu.");
        NavigationMenu outsideFlyout = OpenNavigationFlyout(menu, logs);
        NavigationFlyoutWindow? outsideWindow = menu.FlyoutWindow;
        Assert.IsNotNull(outsideWindow);
        Assert.AreSame(escapedFlyout, outsideFlyout);
        Assert.IsFalse(menu.RectangleToScreen(menu.ClientRectangle).Contains(Control.MousePosition));
        Assert.IsFalse(outsideWindow.TargetRect.Contains(Control.MousePosition));
        AntdUI.Button back = GetControl<AntdUI.Button>(form, "_backButton");
        var outsideClick = System.Windows.Forms.Message.Create(back.Handle, 0x0201, 0, 0x00140014);

        Assert.IsFalse(((IMessageFilter)menu).PreFilterMessage(ref outsideClick),
            "The outside click must remain available to its original target.");

        Assert.IsNull(menu.FlyoutWindow);
        Assert.IsTrue(outsideWindow.IsDisposed);
        Assert.IsFalse(outsideFlyout.IsDisposed);
        Assert.AreEqual("LogsPage2", GetVisiblePage(GetControl<AntdUI.Panel>(form, "_contentPanel")).Name);
        _ = SendMessage(back.Handle, 0x0201, 1, 0x00140014);
        _ = SendMessage(back.Handle, 0x0202, 0, 0x00140014);
        Assert.IsFalse(back.Enabled, "Opening and dismissing flyouts must not add history.");
        Assert.AreEqual("Dashboard", GetVisiblePage(GetControl<AntdUI.Panel>(form, "_contentPanel")).Name);
        NavigationMenu disposedFlyout = OpenNavigationFlyout(menu, logs);
        NavigationFlyoutWindow? disposedWindow = menu.FlyoutWindow;
        Assert.IsNotNull(disposedWindow);

        form.Close();

        Assert.IsTrue(disposedFlyout.IsDisposed);
        Assert.IsTrue(flyoutPanel.IsDisposed);
        Assert.IsTrue(disposedWindow.IsDisposed);
    }

    /// <summary>Checks filtering out the current child retains its indicator on the visible parent.</summary>
    [TestMethod]
    public void FilteringCurrentChildRetainsParentIndicatorWhenFlyoutCannotShowCurrentPage()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        form.NavigateTo("LogsPage2");
        Config.IsLight = true;
        NavigationMenu menu = GetControl<NavigationMenu>(form, "_navigationMenu");
        GetControl<Input>(form, "_searchInput").Text = "LogsPage1";
        AntdUI.MenuItem logs = FindMenuItem(menu, "Logs");
        Assert.IsTrue(logs.Visible);
        Assert.IsFalse(FindMenuItem(menu, "LogsPage2").Visible);
        NavigationMenu flyout = OpenNavigationFlyout(menu, logs);
        Assert.AreEqual(1, flyout.Items.Count);
        Assert.AreEqual("LogsPage1", flyout.Items[0].Name);

        using Bitmap rail = CaptureControl(menu);
        using Bitmap popup = CaptureControl(flyout);

        Assert.IsFalse(GetIndicatorBounds(rail, logs.Rect()).IsEmpty);
        Assert.IsTrue(GetIndicatorBounds(popup, flyout.Items[0].Rect()).IsEmpty);
        Assert.AreEqual("LogsPage2", GetVisiblePage(GetControl<AntdUI.Panel>(form, "_contentPanel")).Name);
    }

    /// <summary>Checks closing disposes cached controls and reopening preserves the logo and window icon resources.</summary>
    [TestMethod]
    public void ClosingMainFormDisposesEveryVisibleAndHiddenCachedPage()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        foreach (string route in s_leafRoutes)
        {
            form.NavigateTo(route);
        }

        AntdUI.Panel contentPanel = GetControl<AntdUI.Panel>(form, "_contentPanel");
        Control[] cachedPages = [.. contentPanel.Controls.Cast<Control>()];
        Assert.AreEqual(7, cachedPages.Length);
        Assert.AreEqual(1, cachedPages.Count(static page => page.Visible));
        Assert.IsTrue(cachedPages.All(static page => !page.IsDisposed));
        Avatar logo = GetControl<Avatar>(form, "_navigationLogo");
        Assert.IsInstanceOfType<Bitmap>(logo.Image);
        using var expectedLogo = (Bitmap)logo.Image.Clone();

        form.Close();

        Assert.IsTrue(form.IsDisposed);
        Assert.IsTrue(contentPanel.IsDisposed);
        Assert.IsTrue(cachedPages.All(static page => page.IsDisposed));
        Assert.IsTrue(logo.IsDisposed);
        using var reopened = new OffscreenMainForm();
        reopened.Show();
        Avatar reopenedLogo = GetControl<Avatar>(reopened, "_navigationLogo");
        Assert.IsTrue(reopenedLogo.Visible);
        Assert.IsFalse(reopenedLogo.TabStop);
        Assert.IsInstanceOfType<Bitmap>(reopenedLogo.Image);
        var actualLogo = (Bitmap)reopenedLogo.Image;
        Assert.AreEqual(expectedLogo.Size, actualLogo.Size);
        var imageBounds = new Rectangle(Point.Empty, expectedLogo.Size);
        AssertBitmapRegionEqual(expectedLogo, imageBounds, actualLogo, imageBounds,
            "Closing a form must not invalidate the image resource used by the next form.");
        using Bitmap renderedLogo = CaptureControl(reopenedLogo);
        Assert.AreEqual(reopenedLogo.ClientSize, renderedLogo.Size);
        Assert.IsNotNull(reopened.Icon);
        using Bitmap windowIcon = reopened.Icon.ToBitmap();
        Assert.IsGreaterThan(0, windowIcon.Width);
    }

    /// <summary>Checks content fills the available shell after resizing and changing navigation width at real DPI.</summary>
    [TestMethod]
    public void ResizingAndCollapsingNavigationKeepsWorkspaceFixedAndCurrentPageFillingContentArea()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        form.NavigateTo("Devices");
        Size originalSize = form.ClientSize;
        AssertContentFillsShell(form);
        form.ClientSize = originalSize + new Size(200, 120);
        AssertContentFillsShell(form);
        InvokeClick(GetControl<AntdUI.Button>(form, "_collapseButton"));
        AssertContentFillsShell(form);
        form.NavigateTo("Dashboard");
        AssertContentFillsShell(form);
        form.ClientSize = originalSize;
        InvokeClick(GetControl<AntdUI.Button>(form, "_collapseButton"));

        AssertContentFillsShell(form);
    }

    /// <summary>Ensures an invalid route fails explicitly without mutating the page collection.</summary>
    [TestMethod]
    public void NavigateToUnknownRouteFailsWithoutChangingCurrentPageOrCache()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        form.NavigateTo("Monitor");
        AntdUI.Panel contentPanel = GetControl<AntdUI.Panel>(form, "_contentPanel");
        Control currentPage = GetVisiblePage(contentPanel);
        Control[] cachedPages = [.. contentPanel.Controls.Cast<Control>()];

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => form.NavigateTo("UnknownPage"));
        Assert.AreSame(currentPage, GetVisiblePage(contentPanel));
        CollectionAssert.AreEqual(cachedPages, contentPanel.Controls);
        Assert.IsTrue(cachedPages.All(static page => !page.IsDisposed));
        Assert.IsTrue(FindMenuItem(GetControl<AntdUI.Menu>(form, "_navigationMenu"), "Monitor").Select);
        AntdUI.Button back = GetControl<AntdUI.Button>(form, "_backButton");
        InvokeClick(back);
        Assert.AreEqual("Dashboard", GetVisiblePage(contentPanel).Name);
        Assert.IsFalse(back.Enabled, "An invalid route must not add navigation history.");
    }

    /// <summary>Checks standalone page canvases match the light designer palette and cached pages follow theme changes.</summary>
    [TestMethod]
    public void PageBackgroundsMatchDesignerDefaultsAndFollowTheActiveTheme()
    {
        using var dashboard = new DashboardPage();
        using var devices = new DevicesPage();
        using var monitor = new MonitorPage();
        using var logsPage1 = new LogsPage1Page();
        using var logsPage2 = new LogsPage2Page();
        using var debugPage1 = new DebugPage1Page();
        foreach (UserControl page in new UserControl[] { dashboard, devices, monitor, logsPage1, logsPage2, debugPage1 })
        {
            Assert.AreEqual(Color.FromArgb(243, 243, 243), page.BackColor,
                "The standalone designer canvas must use the application's light page background.");
            Assert.IsFalse(page.IsHandleCreated);
        }

        using var owner = new OffscreenMainForm();
        owner.Show();
        Config.IsLight = false;
        AntdUI.Panel contentPanel = GetControl<AntdUI.Panel>(owner, "_contentPanel");
        foreach (string route in s_leafRoutes)
        {
            owner.NavigateTo(route);
            Assert.AreEqual(Color.FromArgb(30, 30, 30), GetVisiblePage(contentPanel).BackColor,
                "Pages first opened in the dark theme must match the content host.");
        }

        foreach (bool dark in new[] { false, true })
        {
            Config.IsLight = !dark;
            foreach (Control page in contentPanel.Controls)
            {
                Assert.AreEqual(dark ? Color.FromArgb(30, 30, 30) : Color.FromArgb(243, 243, 243), page.BackColor,
                    "Theme changes must update cached pages even while they are hidden.");
            }
        }
    }

    /// <summary>Checks designer construction and resizing create editable controls without runtime side effects.</summary>
    [TestMethod]
    public void SettingsAndAboutConstructorsCreateEditableControlsWithoutHandlesOrRuntimeChanges()
    {
        using var settings = new SettingsPage();
        using var about = new AboutControl();
        Assert.IsFalse(settings.IsHandleCreated);
        Assert.IsFalse(about.IsHandleCreated);
        Assert.AreEqual(Color.White, about.BackColor);
        Assert.AreEqual(0, about.Controls.Count);
        Assert.AreEqual(AutoScaleMode.Inherit, about.AutoScaleMode);
        Assert.AreEqual(new Size(598, 396), about.Size);
        Assert.AreEqual(AutoScaleMode.Dpi, settings.AutoScaleMode);
        Assert.IsTrue(GetControl<AntdUI.Switch>(settings, "_showInWindowSwitch").Checked,
            "The design canvas must match the application's initial ShowInWindow setting without changing Config.");
        settings.Size = new Size(360, 300);
        Assert.IsFalse(settings.IsHandleCreated);
        Assert.IsFalse(GetControl<StackPanel>(settings, "_settingsStack").IsHandleCreated);
        foreach (string field in new[]
        {
            "_animationSwitch", "_shadowSwitch", "_scrollbarSwitch", "_showInWindowSwitch", "_windowOffsetInput"
        })
        {
            Assert.IsFalse(GetControl<Control>(settings, field).IsHandleCreated);
        }

        Assert.IsFalse(Config.Animation);
        Assert.IsTrue(Config.ShadowEnabled);
        Assert.IsTrue(Config.ScrollBarHide);
        Assert.IsFalse(Config.ShowInWindow);
        Assert.AreEqual(0, Config.NoticeWindowOffsetXY);
    }

    /// <summary>Checks designer card scaling preserves an editable height without the page's runtime layout handlers.</summary>
    [TestMethod]
    public void SettingsCardsPreserveUnboundedHeightAcrossDesignerScalingAndEdits()
    {
        using var settings = new SettingsPage();
        foreach (string field in new[] { "_basicSettingsPanel", "_messageSettingsPanel" })
        {
            using var card = (AntdUI.Panel)Activator.CreateInstance(GetControl<AntdUI.Panel>(settings, field).GetType())!;
            card.MaximumSize = new Size(760, 0);
            card.Size = new Size(760, 136);

            card.Scale(new SizeF(1.5F, 1.5F));
            Assert.AreEqual(new Size(1140, 204), card.Size,
                "A maximum width with an unlimited height must not collapse the designer card during DPI scaling.");
            Assert.AreEqual(new Size(1140, 0), card.MaximumSize);

            card.Scale(new SizeF(2F / 3F, 2F / 3F));
            Assert.AreEqual(new Size(760, 136), card.Size);
            card.Scale(new SizeF(2F, 2F));
            Assert.AreEqual(new Size(1520, 272), card.Size);
            card.Scale(new SizeF(0.5F, 0.5F));
            Assert.AreEqual(new Size(760, 136), card.Size);

            card.Size = new Size(700, 180);
            Assert.AreEqual(new Size(700, 180), card.Size);
            card.Scale(new SizeF(1.5F, 1.5F));
            Assert.AreEqual(new Size(1050, 270), card.Size,
                "An edited card height must scale normally instead of reverting to a fixed minimum.");
            Assert.AreEqual(Size.Empty, card.MinimumSize);
            Assert.IsFalse(card.IsHandleCreated);
        }
    }

    /// <summary>Checks cached settings preserve their values, update appearance and retain navigation history.</summary>
    [TestMethod]
    public void SettingsChangesBasicSwitchesAndWindowOffsetUpdatesAntdUiConfiguration()
    {
        using var owner = new OffscreenMainForm();
        owner.Show();
        owner.NavigateTo("Settings");
        AntdUI.Panel contentPanel = GetControl<AntdUI.Panel>(owner, "_contentPanel");
        Assert.IsInstanceOfType<SettingsPage>(GetVisiblePage(contentPanel));
        var settings = (SettingsPage)GetVisiblePage(contentPanel);
        Switch animation = GetControl<Switch>(settings, "_animationSwitch");
        Switch shadows = GetControl<Switch>(settings, "_shadowSwitch");
        Switch scrollbars = GetControl<Switch>(settings, "_scrollbarSwitch");
        InputNumber offset = GetControl<InputNumber>(settings, "_windowOffsetInput");

        animation.Checked = true;
        shadows.Checked = false;
        scrollbars.Checked = false;
        offset.Value = 24;

        Assert.IsTrue(Config.Animation);
        Assert.IsFalse(Config.ShadowEnabled);
        Assert.IsFalse(Config.ScrollBarHide);
        Assert.AreEqual(24, Config.NoticeWindowOffsetXY);
        Assert.AreEqual(Config.ShowInWindow, GetControl<Switch>(settings, "_showInWindowSwitch").Checked);
        NavigationMenu menu = GetControl<NavigationMenu>(owner, "_navigationMenu");
        AntdUI.MenuItem logs = FindMenuItem(menu, "Logs");
        _ = OpenNavigationFlyout(menu, logs);
        AntdUI.Panel panel = GetControl<AntdUI.Panel>(owner, "_navigationFlyoutPanel");
        Assert.AreEqual(0, panel.Shadow, "Disabling shadows in settings must also disable the child panel's shadow.");
        Assert.AreEqual((int)Math.Round(66 * (owner.DeviceDpi / 96F)),
            owner.PointToClient(panel.PointToScreen(Point.Empty)).X);
        menu.CloseFlyout();

        shadows.Checked = true;
        _ = OpenNavigationFlyout(menu, logs);
        Assert.IsTrue(Config.ShadowEnabled);
        Assert.AreEqual(8, panel.Shadow);
        Assert.AreEqual((int)Math.Round(58 * (owner.DeviceDpi / 96F)),
            owner.PointToClient(panel.PointToScreen(Point.Empty)).X,
            "The shadow's extra bounds must preserve the card's 66-pixel logical left edge.");
        menu.CloseFlyout();
        foreach (string language in s_supportedLanguages)
        {
            owner.NavigateTo("Settings");
            Assert.AreSame(settings, GetVisiblePage(contentPanel));
            owner.NavigateTo("Devices");
            owner.SetLanguage(language);
            foreach (bool dark in new[] { true, false })
            {
                Config.IsLight = !dark;
                Assert.AreEqual(dark ? Color.FromArgb(30, 30, 30) : Color.FromArgb(243, 243, 243), settings.BackColor);
                Assert.AreEqual(dark ? Color.FromArgb(40, 40, 40) : Color.White,
                    GetControl<AntdUI.Panel>(settings, "_basicSettingsPanel").Back);
                Assert.AreEqual(dark ? Color.FromArgb(40, 40, 40) : Color.White,
                    GetControl<AntdUI.Panel>(settings, "_messageSettingsPanel").Back);
                Assert.IsTrue(animation.Checked);
                Assert.IsTrue(shadows.Checked);
                Assert.IsFalse(scrollbars.Checked);
                Assert.AreEqual(24M, offset.Value);
                Assert.IsFalse(GetControl<Switch>(settings, "_showInWindowSwitch").Checked);
            }

            InvokeClick(GetControl<AntdUI.Button>(owner, "_backButton"));
            Assert.AreSame(settings, GetVisiblePage(contentPanel));
            Assert.AreEqual(AppLocalization.GetText("BasicSettings"),
                GetControl<AntdUI.Label>(settings, "_basicSettingsLabel").Text);
            Assert.AreEqual(AppLocalization.GetText("MessageSettings"),
                GetControl<AntdUI.Label>(settings, "_messageSettingsLabel").Text);
        }

        InvokeClick(GetControl<AntdUI.Button>(owner, "_backButton"));
        Assert.AreEqual("Dashboard", GetVisiblePage(contentPanel).Name);
        Assert.IsFalse(GetControl<AntdUI.Button>(owner, "_backButton").Enabled,
            "Repeated settings selection, language and theme refreshes must not add navigation history.");
        Assert.IsFalse(settings.IsDisposed);
    }

    /// <summary>Checks native numeric commits report overflow without changing the active configuration.</summary>
    [TestMethod]
    [DataRow("2147483648")]
    [DataRow("-2147483649")]
    public void WindowOffsetOutOfRangeCommitShowsErrorAndRetainsActiveValue(string invalidText)
    {
        using var owner = new OffscreenMainForm();
        owner.Show();
        owner.NavigateTo("Settings");
        AntdUI.Panel contentPanel = GetControl<AntdUI.Panel>(owner, "_contentPanel");
        var settings = (SettingsPage)GetVisiblePage(contentPanel);
        InputNumber offset = GetControl<InputNumber>(settings, "_windowOffsetInput");
        AntdUI.Label description = GetControl<AntdUI.Label>(settings, "_windowOffsetDescription");
        offset.Value = 24;
        offset.Text = invalidText;
        // Exercise the actual focus-loss message: AntdUI's WndProc can swallow callback exceptions.
        _ = SendMessage(offset.Handle, 0x0008, 0, 0);

        Assert.AreEqual(TType.Error, offset.Status);
        Assert.AreEqual(24, Config.NoticeWindowOffsetXY);
        Assert.AreEqual(decimal.Parse(invalidText, CultureInfo.InvariantCulture), offset.Value);
        foreach (string language in s_supportedLanguages)
        {
            owner.NavigateTo("Devices");
            owner.SetLanguage(language);
            CompositeFormat errorFormat = CompositeFormat.Parse(AppLocalization.GetText("WindowOffsetOutOfRange"));
            foreach (bool dark in new[] { true, false })
            {
                Config.IsLight = !dark;
                Assert.AreEqual(TType.Error, offset.Status);
                StringAssert.Contains(description.Text, invalidText, StringComparison.Ordinal);
                StringAssert.Contains(description.Text, "24", StringComparison.Ordinal);
                Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, errorFormat, offset.Value, 24), description.Text);
                Assert.AreEqual(description.Text, offset.AccessibleDescription);
                Assert.IsTrue(description.ForeColor is { } color && color.R > color.G,
                    "The explanation must retain error coloring across theme changes.");
            }

            InvokeClick(GetControl<AntdUI.Button>(owner, "_backButton"));
            Assert.AreSame(settings, GetVisiblePage(contentPanel));
            owner.MinimumSize = Size.Empty;
            float scale = owner.DeviceDpi / 96F;
            foreach (int width in new[] { 980, 360 })
            {
                owner.ClientSize = new Size((int)Math.Round((58 + width) * scale), (int)Math.Round(600 * scale));
                AssertSettingsLayout(settings);
            }
        }

        foreach (int value in new[] { int.MinValue, int.MaxValue, 12 })
        {
            offset.Text = value.ToString(CultureInfo.CurrentCulture);
            _ = SendMessage(offset.Handle, 0x0008, 0, 0);
            Assert.AreEqual(value, Config.NoticeWindowOffsetXY);
            Assert.AreEqual(TType.None, offset.Status);
            Assert.AreEqual(AppLocalization.GetText("WindowOffsetDescription"), description.Text);
        }

        offset.Text = string.Empty;
        _ = SendMessage(offset.Handle, 0x0008, 0, 0);
        Assert.AreEqual(0, Config.NoticeWindowOffsetXY, "Clearing the native input must keep its original zero behavior.");
        Assert.AreEqual(TType.None, offset.Status);
    }

    /// <summary>Checks the native input menu's localization IDs resolve in every supported language.</summary>
    [TestMethod]
    [DataRow("en-US", "Cut|Copy|Paste|Delete|Select all|Undo|Redo")]
    [DataRow("zh-CN", "剪切|复制|粘贴|删除|全选|撤销|重做")]
    [DataRow("zh-TW", "剪下|複製|貼上|刪除|全選|復原|重做")]
    public void NativeInputContextMenuResolvesAllCommandsInCurrentLanguage(string language, string translations)
    {
        AppLocalization.SetLanguage(language);
        string[] ids = ["Cut", "Copy", "Paste", "Delete", "SelectAll", "Undo", "Redo"];
        string[] defaults = ["剪切", "复制", "粘贴", "删除", "全选", "撤销", "重做"];
        string[] expected = translations.Split('|');
        for (int index = 0; index < ids.Length; index++)
        {
            var item = new ContextMenuStripItem().SetText(defaults[index], "{id}").SetID(ids[index]);
            Assert.AreEqual(expected[index], item.Text, $"{language}: native command {ids[index]}.");
        }
    }

    /// <summary>Checks the message-setting event and both real previews anchored to an offscreen owner.</summary>
    [TestMethod]
    public void ShowInWindowSwitchEnablesBothMessagePreviewsInsideOffscreenOwner()
    {
        using var owner = new OffscreenMainForm();
        owner.Show();
        owner.NavigateTo("Settings");
        AntdUI.Panel contentPanel = GetControl<AntdUI.Panel>(owner, "_contentPanel");
        Assert.IsInstanceOfType<SettingsPage>(GetVisiblePage(contentPanel));
        var settings = (SettingsPage)GetVisiblePage(contentPanel);
        foreach (string language in s_supportedLanguages)
        {
            owner.NavigateTo("Settings");
            owner.NavigateTo("Devices");
            owner.SetLanguage(language);
            Config.IsLight = false;
            Config.IsLight = true;
            InvokeClick(GetControl<AntdUI.Button>(owner, "_backButton"));
            Assert.AreSame(settings, GetVisiblePage(contentPanel));
        }

        Switch showInWindow = GetControl<Switch>(settings, "_showInWindowSwitch");
        Assert.IsFalse(showInWindow.Checked);
        try
        {
            // Only false-to-true is exercised: screen-relative previews would otherwise become visible.
            showInWindow.Checked = true;
            ILayeredForm[] previews;
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            do
            {
                Application.DoEvents();
                previews = [.. Application.OpenForms.OfType<ILayeredForm>().Where(
                    static form => form.GetType().Name is "MessageFrm" or "NotificationFrm")];
            }
            // Layered windows render at TargetRect; WinForms Bounds can retain the initial host position.
            while ((previews.Length != 2 || previews.Any(static preview =>
                !preview.Visible || preview.TargetRect.Right >= 0 || preview.TargetRect.Bottom >= 0))
                && elapsed.Elapsed < TimeSpan.FromSeconds(5));

            Assert.IsTrue(Config.ShowInWindow);
            Assert.AreEqual(2, previews.Length,
                "Repeated settings navigation must still create exactly one Message and one Notification preview.");
            foreach (ILayeredForm preview in previews)
            {
                Assert.IsTrue(preview.Visible, "The preview must be shown before its position is checked.");
                Assert.IsTrue(preview.TargetRect.Right < 0 && preview.TargetRect.Bottom < 0,
                    $"{preview.GetType().Name} bounds={preview.Bounds}; target={preview.TargetRect}; owner={owner.Bounds}; "
                    + $"owner disposed={owner.IsDisposed}; ShowInWindow={Config.ShowInWindow}.");
            }
        }
        finally
        {
            AntdUI.Message.close_all();
            Notification.close_all();
            Application.DoEvents();
        }
    }

    /// <summary>Checks satellite resources and the actual localized AntdUI control properties.</summary>
    /// <param name="language">Supported culture name.</param>
    /// <param name="devices">Expected translated devices page label.</param>
    /// <param name="search">Expected translated search prompt.</param>
    /// <param name="basicSettings">Expected translated basic settings heading.</param>
    /// <param name="messageSettings">Expected translated message settings heading.</param>
    [TestMethod]
    [DataRow("en-US", "Devices", "Search", "Basic Settings", "Message configuration")]
    [DataRow("zh-CN", "设备", "搜索", "基本设置", "消息配置")]
    [DataRow("zh-TW", "設備", "搜尋", "基本設定", "訊息設定")]
    public void LocalizationSupportedLanguageProvidesTranslatedNavigationAndSettings(
        string language, string devices, string search, string basicSettings, string messageSettings)
    {
        AppLocalization.SetLanguage(language);

        Assert.AreEqual(devices, AppLocalization.GetText("Devices"));
        Assert.AreEqual(search, AppLocalization.GetText("Search"));
        Assert.AreEqual(basicSettings, AppLocalization.GetText("BasicSettings"));
        Assert.AreEqual(messageSettings, AppLocalization.GetText("MessageSettings"));
        using var owner = new OffscreenMainForm();
        owner.Show();
        owner.SetLanguage(language);
        owner.NavigateTo("Settings");
        AntdUI.Panel contentPanel = GetControl<AntdUI.Panel>(owner, "_contentPanel");
        Assert.IsInstanceOfType<SettingsPage>(GetVisiblePage(contentPanel));
        var settings = (SettingsPage)GetVisiblePage(contentPanel);
        Assert.AreEqual(AppLocalization.GetText("Settings"),
            GetControl<AntdUI.Label>(settings, "_settingsTitleLabel").Text);
        Assert.AreEqual(basicSettings, GetControl<AntdUI.Label>(settings, "_basicSettingsLabel").Text);
        Assert.AreEqual(messageSettings, GetControl<AntdUI.Label>(settings, "_messageSettingsLabel").Text);
        Assert.AreEqual(
            AppLocalization.GetText("EnableAnimations"),
            GetControl<AntdUI.Label>(settings, "_animationLabel").Text);
        Assert.AreEqual(
            AppLocalization.GetText("ShowInWindow"),
            GetControl<AntdUI.Label>(settings, "_showInWindowLabel").Text);
        Assert.AreEqual(AppLocalization.GetText("ShowInWindowDescription"),
            GetControl<AntdUI.Label>(settings, "_showInWindowDescription").Text);
        Assert.AreEqual(AppLocalization.GetText("WindowOffsetDescription"),
            GetControl<AntdUI.Label>(settings, "_windowOffsetDescription").Text);
        Assert.AreEqual(AppLocalization.GetText("Minimize"), AntdUI.Localization.Get("Minimize", "untranslated"));
        owner.MinimumSize = Size.Empty;
        float scale = owner.DeviceDpi / 96F;
        int wideMessageHeight = 0;
        foreach (int width in new[] { 980, 360 })
        {
            owner.ClientSize = new Size((int)Math.Round((58 + width) * scale), (int)Math.Round(600 * scale));
            AssertContentFillsShell(owner);
            Assert.AreEqual(owner.DeviceDpi, settings.DeviceDpi);
            AssertSettingsLayout(settings);
            int messageHeight = GetControl<AntdUI.Panel>(settings, "_messageSettingsPanel").Height;
            if (width == 980)
            {
                wideMessageHeight = messageHeight;
            }
            else
            {
                Assert.IsGreaterThan(wideMessageHeight, messageHeight,
                    "Narrow message descriptions must increase row and card height without overlapping their controls.");
            }

            RenderControl(settings, $"settings-{language}-width-{width}-device-dpi-{owner.DeviceDpi}.png");
        }
    }

    /// <summary>Ensures invalid cultures and missing application resources remain observable failures.</summary>
    [TestMethod]
    public void LocalizationUnsupportedLanguageOrMissingResourceFailsExplicitly()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => AppLocalization.SetLanguage("fr-FR"));
        Assert.ThrowsExactly<MissingManifestResourceException>(() => AppLocalization.GetText("MissingResourceKey"));
        Assert.AreEqual("en-US", CultureInfo.CurrentUICulture.Name);
    }

    /// <summary>Produces layout evidence for each supported language without opening a visible form.</summary>
    /// <param name="language">Supported culture name.</param>
    [TestMethod]
    [TestCategory("Rendering")]
    [DataRow("en-US")]
    [DataRow("zh-CN")]
    [DataRow("zh-TW")]
    public void RenderThreeLanguagesProducesHiddenShellEvidence(string language)
    {
        using var form = new OffscreenMainForm();
        form.Show();
        form.SetLanguage(language);
        form.NavigateTo("LogsPage2");
        Config.IsLight = true;
        AssertContentFillsShell(form);
        RenderControl(form, $"main-{language}-light-collapsed.png");
        NavigationMenu navigation = GetControl<NavigationMenu>(form, "_navigationMenu");
        _ = OpenNavigationFlyout(navigation, FindMenuItem(navigation, "Logs"));
        Assert.IsNotNull(navigation.FlyoutWindow);
        using (Bitmap frame = navigation.FlyoutWindow.PrintBit())
        {
            Assert.AreEqual(0, frame.GetPixel(0, 0).A, "The light-theme outer corner must expose the underlying page.");
            frame.Save(Path.Combine(GetUiArtifactDirectory(), $"navigation-{language}-light-alpha.png"), ImageFormat.Png);
        }

        navigation.CloseFlyout();
        InvokeClick(GetControl<AntdUI.Button>(form, "_collapseButton"));
        AntdUI.Menu menu = GetControl<AntdUI.Menu>(form, "_navigationMenu");
        foreach (AntdUI.MenuItem item in menu.Items)
        {
            item.Expand = true;
        }

        FindMenuItem(menu, "Logs").Expand = true;

        AssertContentFillsShell(form);
        RenderControl(form, $"main-{language}-light.png");
        Assert.AreEqual(0D, form.Opacity);
        Assert.IsFalse(form.ShowInTaskbar);
    }

    /// <summary>Produces dark-theme layout evidence without opening a visible form.</summary>
    [TestMethod]
    [TestCategory("Rendering")]
    public void RenderDarkThemeProducesHiddenShellEvidence()
    {
        using var form = new OffscreenMainForm();
        form.Show();
        form.NavigateTo("Monitor");
        Config.IsLight = false;
        InvokeClick(GetControl<AntdUI.Button>(form, "_collapseButton"));
        AntdUI.Menu menu = GetControl<AntdUI.Menu>(form, "_navigationMenu");
        foreach (AntdUI.MenuItem item in menu.Items)
        {
            item.Expand = true;
        }

        AssertContentFillsShell(form);
        RenderControl(form, "main-en-US-dark.png");
        InvokeClick(GetControl<AntdUI.Button>(form, "_collapseButton"));
        NavigationMenu navigation = GetControl<NavigationMenu>(form, "_navigationMenu");
        _ = OpenNavigationFlyout(navigation, FindMenuItem(navigation, "Logs"));
        Assert.IsNotNull(navigation.FlyoutWindow);
        using (Bitmap frame = navigation.FlyoutWindow.PrintBit())
        {
            Assert.AreEqual(0, frame.GetPixel(0, 0).A, "The dark-theme outer corner must expose the page without a halo.");
            frame.Save(Path.Combine(GetUiArtifactDirectory(), "navigation-en-US-dark-alpha.png"), ImageFormat.Png);
        }

        Assert.AreEqual(0D, form.Opacity);
        Assert.IsFalse(form.ShowInTaskbar);
    }

    /// <summary>Checks shell captures preserve the foreground navigation across languages, themes and sidebar states.</summary>
    /// <param name="language">The application language shown in the navigation.</param>
    /// <param name="dark">Whether to capture the dark palette.</param>
    /// <param name="collapsed">Whether to capture the collapsed navigation rail.</param>
    [TestMethod]
    [TestCategory("Rendering")]
    [DataRow("en-US", false, true)]
    [DataRow("zh-CN", false, true)]
    [DataRow("zh-TW", false, true)]
    [DataRow("en-US", true, true)]
    [DataRow("zh-CN", true, true)]
    [DataRow("zh-TW", true, true)]
    [DataRow("en-US", false, false)]
    [DataRow("zh-CN", false, false)]
    [DataRow("zh-TW", false, false)]
    [DataRow("en-US", true, false)]
    [DataRow("zh-CN", true, false)]
    [DataRow("zh-TW", true, false)]
    public void MainFormCapturePreservesForegroundNavigationPixels(string language, bool dark, bool collapsed)
    {
        using var form = new OffscreenMainForm();
        form.Show();
        form.SetLanguage(language);
        form.NavigateTo("LogsPage2");
        Config.IsLight = !dark;
        AntdUI.Panel navigation = GetControl<AntdUI.Panel>(form, "_navigationPanel");
        AntdUI.Panel workspace = GetControl<AntdUI.Panel>(form, "_workspacePanel");
        NavigationMenu menu = GetControl<NavigationMenu>(form, "_navigationMenu");
        if (menu.Collapsed != collapsed)
        {
            InvokeClick(GetControl<AntdUI.Button>(form, "_collapseButton"));
        }

        Assert.AreEqual(collapsed, menu.Collapsed);
        Assert.IsTrue(form.Controls.GetChildIndex(navigation) < form.Controls.GetChildIndex(workspace),
            "The navigation must remain in front of the workspace in the actual control tree.");
        Assert.AreEqual(navigation.Handle, GetWindow(form.Handle, 5),
            "The navigation must also be the first child in the native window Z order.");
        using Bitmap expected = CaptureControl(navigation);
        using Bitmap actual = CaptureControl(form);
        actual.Save(Path.Combine(GetUiArtifactDirectory(),
            $"main-{language}-{(dark ? "dark" : "light")}-navigation-{(collapsed ? "collapsed" : "expanded")}.png"),
            ImageFormat.Png);
        AssertBitmapRegionEqual(expected, new Rectangle(Point.Empty, expected.Size), actual, navigation.Bounds,
            $"The shell capture must preserve navigation pixels: language={language}, dark={dark}, collapsed={collapsed}.");
    }

    /// <summary>Checks the blank About modal has one DPI scale and leaves the selected page and history unchanged.</summary>
    /// <param name="language">The application language exercised by the real About action.</param>
    /// <param name="dark">Whether the modal initially uses the dark theme.</param>
    [TestMethod]
    [TestCategory("Rendering")]
    [TestCategory("Modal")]
    [DataRow("en-US", false)]
    [DataRow("zh-CN", false)]
    [DataRow("zh-TW", false)]
    [DataRow("en-US", true)]
    [DataRow("zh-CN", true)]
    [DataRow("zh-TW", true)]
    public void AboutButtonOpensBlankModalWithSingleDpiScalingAndPreservesNavigation(string language, bool dark)
    {
        using var owner = new OffscreenMainForm();
        owner.Show();
        owner.SetLanguage(language);
        owner.NavigateTo("Settings");
        AntdUI.Panel contentPanel = GetControl<AntdUI.Panel>(owner, "_contentPanel");
        Control currentPage = GetVisiblePage(contentPanel);
        Control[] cachedPages = [.. contentPanel.Controls.Cast<Control>()];
        Config.IsLight = !dark;
        using var timer = new System.Windows.Forms.Timer { Interval = 30 };
        Exception? captureFailure = null;
        bool captured = false;
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        timer.Tick += (_, _) =>
        {
            Form? modal = Application.OpenForms.Cast<Form>().SingleOrDefault(
                static form => form.GetType().Name == "LayeredFormModal");
            if (modal is null)
            {
                if (elapsed.Elapsed >= TimeSpan.FromSeconds(5))
                {
                    timer.Stop();
                    captureFailure = new TimeoutException("The About action did not open an AntdUI modal.");
                    owner.Close();
                }

                return;
            }

            timer.Stop();
            try
            {
                modal.Opacity = 0;
                Assert.IsTrue(modal.Right < 0 && modal.Bottom < 0, "The modal must remain offscreen.");
                AboutControl about = modal.Controls.OfType<AboutControl>().Single();
                Assert.AreEqual(Style.Db.BgElevated, modal.BackColor);
                Assert.AreEqual(modal.BackColor, about.BackColor);
                Config.IsLight = dark;
                Assert.AreEqual(Style.Db.BgElevated, modal.BackColor);
                Assert.AreEqual(modal.BackColor, about.BackColor,
                    "About must inherit native modal colors when the theme changes while the dialog is open.");
                Config.IsLight = !dark;
                Assert.AreEqual(0, about.Controls.Count, "About must contain no settings, tabs or placeholder content.");
                Assert.AreEqual(AutoScaleMode.Inherit, about.AutoScaleMode);
                FieldInfo? configuration = modal.GetType().GetField("config", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(configuration);
                object? value = configuration.GetValue(modal);
                Assert.IsInstanceOfType<AntdUI.Modal.Config>(value);
                var modalConfiguration = (AntdUI.Modal.Config)value;
                Assert.AreEqual(AppLocalization.GetText("About"), modalConfiguration.Title);
                Assert.IsTrue(modalConfiguration.CloseIcon);
                Assert.AreEqual(0, modalConfiguration.BtnHeight);
                Assert.AreEqual(TType.None, modalConfiguration.Icon);
                Assert.IsNull(modalConfiguration.IconCustom);
                string prefix = $"modal-about-{language}-{(dark ? "dark" : "light")}-device-dpi-{owner.DeviceDpi}";
                var report = new StringBuilder();
                report.AppendLine(CultureInfo.InvariantCulture,
                    $"AntdUiScale={Config.Dpi}; OwnerDeviceDpi={owner.DeviceDpi}; ModalDeviceDpi={modal.DeviceDpi}");
                report.AppendLine("The test uses production PerMonitorV2; no library DPI override is applied.");
                RenderControl(modal, $"{prefix}.png");
                AppendControlLayout(report, modal, 0);
                File.WriteAllText(Path.Combine(GetUiArtifactDirectory(), $"{prefix}-layout.txt"),
                    report.ToString(), new UTF8Encoding(false));
                captured = true;
                float scale = owner.DeviceDpi / 96F;
                int expectedHeight = (int)Math.Round(396 * scale);
                Assert.AreEqual(expectedHeight, about.Height,
                    "The modal must scale its 396-pixel design height exactly once.");
                int expectedModalWidth = (int)Math.Round(598 * scale) + 2 * (int)Math.Round(24 * scale);
                Assert.AreEqual(expectedModalWidth, modal.ClientSize.Width,
                    "The modal must contain one scaled About width plus the existing horizontal padding.");
            }
            catch (Exception exception)
            {
                // Exceptions must escape the modal loop after its window has been closed.
                captureFailure = exception;
            }
            finally
            {
                modal.Close();
            }
        };

        timer.Start();
        InvokeClick(GetControl<AntdUI.Button>(owner, "_aboutButton"));
        timer.Stop();
        if (captureFailure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(captureFailure);
        }

        Assert.IsTrue(captured, "The real About modal must be captured before closing.");
        Assert.AreSame(currentPage, GetVisiblePage(contentPanel));
        CollectionAssert.AreEquivalent(cachedPages, contentPanel.Controls);
        Assert.IsTrue(FindMenuItem(GetControl<NavigationMenu>(owner, "_navigationMenu"), "Settings").Select);
        InvokeClick(GetControl<AntdUI.Button>(owner, "_backButton"));
        Assert.AreEqual("Dashboard", GetVisiblePage(contentPanel).Name);
        Assert.IsFalse(GetControl<AntdUI.Button>(owner, "_backButton").Enabled,
            "Opening and closing About must not add a navigation-history entry.");
    }

    private static AntdUI.MenuItem FindMenuItem(AntdUI.Menu menu, string route)
    {
        AntdUI.MenuItem? item = menu.FindName(route);
        Assert.IsNotNull(item, $"The navigation must contain route '{route}'.");
        return item;
    }

    private static Control GetVisiblePage(AntdUI.Panel contentPanel)
    {
        Control[] visiblePages = [.. contentPanel.Controls.Cast<Control>().Where(static page => page.Visible)];
        Assert.AreEqual(1, visiblePages.Length, "Exactly one cached page must be visible.");
        return visiblePages[0];
    }

    private static T GetControl<T>(Control owner, string fieldName)
        where T : Control
    {
        Type declaringType = owner is MainForm ? typeof(MainForm) : owner.GetType();
        FieldInfo? field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"The designer must declare {fieldName}.");
        object? value = field.GetValue(owner);
        Assert.IsInstanceOfType<T>(value);
        return (T)value;
    }

    private static void InvokeClick(Control control)
    {
        // A hidden window cannot use PerformClick's focus eligibility check.
        MethodInfo? onClick = typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(onClick);
        onClick.Invoke(control, [EventArgs.Empty]);
    }

    private static void InvokeMenuClick(AntdUI.Menu menu, AntdUI.MenuItem item, Point? releaseLocation = null)
    {
        Application.DoEvents();
        menu.Refresh();
        Rectangle bounds = item.Rect();
        Assert.IsGreaterThan(0, bounds.Width);
        Assert.IsGreaterThan(0, bounds.Height);
        var click = new MouseEventArgs(MouseButtons.Left, 1, bounds.Left + bounds.Width / 2,
            bounds.Top + bounds.Height / 2, 0);
        NavigationMenu root = GetControl<NavigationMenu>(menu.FindForm()!, "_navigationMenu");
        Control target = menu == root ? menu : root.FlyoutWindow!;
        if (menu != root)
        {
            click = new MouseEventArgs(click.Button, click.Clicks, click.X + menu.Left, click.Y + menu.Top, 0);
        }

        MethodInfo? mouseDown = target.GetType().GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo? mouseUp = target.GetType().GetMethod("OnMouseUp", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(mouseDown);
        Assert.IsNotNull(mouseUp);
        var down = System.Windows.Forms.Message.Create(target.Handle, 0x0201, 1,
            (click.Y << 16) | click.X);
        Assert.IsFalse(((IMessageFilter)root).PreFilterMessage(ref down),
            "Navigation's message filter must allow the target control to receive its pointer gesture.");
        mouseDown.Invoke(target, [click]);
        if (releaseLocation is Point release)
        {
            InvokeMouseMove((NavigationMenu)menu, release);
            if (menu != root)
            {
                release.Offset(menu.Location);
            }

            click = new MouseEventArgs(MouseButtons.Left, 1, release.X, release.Y, 0);
        }

        mouseUp.Invoke(target, [click]);
    }

    private static void InvokeMouseMove(NavigationMenu menu, Point location)
    {
        NavigationMenu root = GetControl<NavigationMenu>(menu.FindForm()!, "_navigationMenu");
        Control target = menu == root ? menu : root.FlyoutWindow!;
        if (menu != root)
        {
            location.Offset(menu.Location);
        }

        MethodInfo? mouseMove = target.GetType().GetMethod(
            "OnMouseMove", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(mouseMove);
        mouseMove.Invoke(target, [new MouseEventArgs(MouseButtons.None, 0, location.X, location.Y, 0)]);
    }

    private static NavigationMenu OpenNavigationFlyout(NavigationMenu menu, AntdUI.MenuItem parent)
    {
        InvokeMenuClick(menu, parent);
        return GetReadyNavigationFlyout(menu);
    }

    private static NavigationMenu GetReadyNavigationFlyout(NavigationMenu menu)
    {
        AntdUI.Panel panel = GetControl<AntdUI.Panel>(menu, "_flyoutPanel");
        NavigationMenu content = GetControl<NavigationMenu>(menu, "_flyoutMenu");
        Form owner = menu.FindForm()!;
        NavigationFlyoutWindow? window = menu.FlyoutWindow;
        Assert.IsNotNull(window);
        Assert.IsTrue(window.Visible, "The transparent child window must be visible when its opening handler returns.");
        Assert.IsFalse(panel.Visible, "The designer panel remains a hidden drawing source.");
        Assert.IsFalse(content.Visible);
        Assert.IsInstanceOfType<UserControl>(panel.Parent);
        Assert.AreSame(owner, panel.Parent.Parent);
        Assert.AreSame(owner, content.FindForm(), "The reusable drawing source must stay with the main window.");
        Assert.AreSame(owner, window.Owner);
        Assert.AreSame(panel, content.Parent);
        Assert.IsTrue(owner.RectangleToScreen(owner.ClientRectangle).Contains(window.TargetRect));
        Assert.AreEqual(panel.RectangleToScreen(panel.ClientRectangle), window.TargetRect);
        Assert.AreEqual(menu.DeviceDpi, content.DeviceDpi);
        Assert.AreEqual(menu.DeviceDpi, window.DeviceDpi);
        return content;
    }

    private static void AssertFlyoutItemsFit(NavigationMenu menu, NavigationMenu flyout)
    {
        Assert.IsNull(flyout.IconGap, "The independent child menu must keep AntdUI's default icon-to-text gap.");
        Assert.IsFalse(flyout.ScrollBar.ShowX, "Child navigation must not need a horizontal scrollbar.");
        Assert.IsFalse(flyout.ScrollBar.ShowY, "The current row count must fit without a vertical scrollbar.");
        int textCapacity = flyout.Items[0].Rect("Text").Width;
        flyout.GDI(canvas =>
        {
            foreach (AntdUI.MenuItem parent in menu.Items)
            {
                foreach (AntdUI.MenuItem child in parent.Sub)
                {
                    int requiredWidth = canvas.MeasureText(child.Text, flyout.Font).Width;
                    Assert.IsTrue(textCapacity >= requiredWidth,
                        $"The common text area ({textCapacity}px) must fit '{child.Text}' ({requiredWidth}px), including hidden items.");
                }
            }

            foreach (AntdUI.MenuItem item in flyout.Items)
            {
                Rectangle row = item.Rect();
                Rectangle text = item.Rect("Text");
                Rectangle icon = item.Rect("Icon");
                Size measured = canvas.MeasureText(item.Text, item.Font ?? flyout.Font);
                Assert.IsTrue(flyout.ClientRectangle.Contains(row));
                Assert.IsTrue(row.Contains(text));
                Assert.IsTrue(row.Contains(icon));
                Assert.IsGreaterThan(0, icon.Width);
                Assert.IsGreaterThan(0, icon.Height);
                Assert.IsTrue(text.Width >= measured.Width && text.Height >= measured.Height,
                    $"'{item.Text}' must fit its actual text rectangle {text} without clipping {measured}.");
            }
        });
    }

    private static Rectangle GetIndicatorBounds(Bitmap bitmap, Rectangle row)
    {
        Assert.IsTrue(new Rectangle(Point.Empty, bitmap.Size).Contains(row), "The menu row must fit its rendered control.");
        int left = bitmap.Width;
        int top = bitmap.Height;
        int right = -1;
        int bottom = -1;
        for (int y = row.Top; y < row.Bottom; y++)
        {
            for (int x = row.Left; x < row.Right; x++)
            {
                Color pixel = bitmap.GetPixel(x, y);
                if (pixel.R > 200 && pixel.G < 100 && pixel.B < 120)
                {
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
            }
        }

        return right < left ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    private static void AssertBitmapRegionEqual(Bitmap expected, Rectangle expectedBounds,
        Bitmap actual, Rectangle actualBounds, string message)
    {
        Assert.AreEqual(expectedBounds.Size, actualBounds.Size, message);
        Assert.IsTrue(new Rectangle(Point.Empty, expected.Size).Contains(expectedBounds));
        Assert.IsTrue(new Rectangle(Point.Empty, actual.Size).Contains(actualBounds));
        for (int y = 0; y < expectedBounds.Height; y++)
        {
            for (int x = 0; x < expectedBounds.Width; x++)
            {
                Assert.AreEqual(expected.GetPixel(expectedBounds.X + x, expectedBounds.Y + y).ToArgb(),
                    actual.GetPixel(actualBounds.X + x, actualBounds.Y + y).ToArgb(), message);
            }
        }
    }

    private static void AssertIconArtworkEqual(Bitmap expected, Bitmap actual, string message)
    {
        Assert.AreEqual(expected.Size, actual.Size, message);
        for (int y = 0; y < expected.Height; y++)
        {
            for (int x = 0; x < expected.Width; x++)
            {
                Color expectedPixel = expected.GetPixel(x, y);
                Color actualPixel = actual.GetPixel(x, y);
                Assert.AreEqual(expectedPixel.A, actualPixel.A, message);
                // HICON conversion can round premultiplied colors; transparent RGB does not affect the artwork.
                Assert.AreEqual(expectedPixel.R * expectedPixel.A / 255F,
                    actualPixel.R * actualPixel.A / 255F, 1F, message);
                Assert.AreEqual(expectedPixel.G * expectedPixel.A / 255F,
                    actualPixel.G * actualPixel.A / 255F, 1F, message);
                Assert.AreEqual(expectedPixel.B * expectedPixel.A / 255F,
                    actualPixel.B * actualPixel.A / 255F, 1F, message);
            }
        }
    }

    private static int GetDarkestGray(Bitmap bitmap, Rectangle bounds)
    {
        Assert.IsTrue(new Rectangle(Point.Empty, bitmap.Size).Contains(bounds));
        int darkest = 255;
        for (int y = bounds.Top; y < bounds.Bottom; y++)
        {
            for (int x = bounds.Left; x < bounds.Right; x++)
            {
                Color pixel = bitmap.GetPixel(x, y);
                if (pixel.R == pixel.G && pixel.G == pixel.B)
                {
                    darkest = Math.Min(darkest, pixel.R);
                }
            }
        }

        return darkest;
    }

    private static void InvokeNavigationKey(NavigationMenu menu, Keys key)
    {
        MethodInfo? processKey = typeof(NavigationMenu).GetMethod(
            "ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(processKey);
        var message = System.Windows.Forms.Message.Create(menu.Handle, 0x0100, (nint)key, 0);
        processKey.Invoke(menu, [message, key]);
    }

    private static Color GetRowBackground(Bitmap bitmap, Rectangle row, int deviceDpi)
    {
        return bitmap.GetPixel(row.Right - (int)Math.Round(6 * (deviceDpi / 96F)), row.Top + row.Height / 2);
    }

    private static void AssertSettingsLayout(SettingsPage settings)
    {
        settings.PerformLayout();
        float scale = settings.DeviceDpi / 96F;
        StackPanel stack = GetControl<StackPanel>(settings, "_settingsStack");
        Assert.AreEqual(DockStyle.Fill, stack.Dock);
        Assert.IsTrue(stack.Vertical);
        Assert.IsTrue(stack.AutoScroll);
        Assert.AreEqual(new Padding((int)Math.Round(24 * scale)), stack.Padding);
        foreach (string field in new[] { "_basicSettingsPanel", "_messageSettingsPanel" })
        {
            AntdUI.Panel card = GetControl<AntdUI.Panel>(settings, field);
            Assert.IsTrue(stack.Controls.Contains(card), "Settings cards must belong to the stack's content collection.");
            AntdUI.Label heading = GetControl<AntdUI.Label>(settings,
                field == "_basicSettingsPanel" ? "_basicSettingsLabel" : "_messageSettingsLabel");
            string headingContext = $"{heading.Name}: Text={heading.Text}; Bounds={heading.Bounds}; "
                + $"Visible={heading.Visible}; Parent={heading.Parent?.GetType().Name}; "
                + $"Font={heading.Font}; ReadRectangle={heading.ReadRectangle}.";
            Assert.IsTrue(stack.Controls.Contains(heading), headingContext);
            Assert.AreSame(card.Parent, heading.Parent, headingContext);
            Assert.IsTrue(heading.Visible, headingContext);
            Assert.IsFalse(string.IsNullOrWhiteSpace(heading.Text), headingContext);
            Rectangle headingText = heading.ReadRectangle;
            Assert.IsGreaterThan(0, headingText.Width, headingContext);
            Assert.IsGreaterThan(0, headingText.Height, headingContext);
            Size headingSize = heading.GDI(canvas => canvas.MeasureText(heading.Text, heading.Font, headingText.Width));
            Assert.IsTrue(headingText.Width >= headingSize.Width && headingText.Height >= headingSize.Height,
                headingContext);
            Assert.IsTrue(heading.Bottom <= card.Top, headingContext);
            foreach (AntdUI.Panel otherCard in stack.Controls.OfType<AntdUI.Panel>())
            {
                Assert.IsFalse(heading.Bounds.IntersectsWith(otherCard.Bounds), headingContext);
            }

            Rectangle cardBounds = card.Bounds;
            for (Control? ancestor = card.Parent; ancestor != stack; ancestor = ancestor.Parent)
            {
                Assert.IsNotNull(ancestor);
                cardBounds.Offset(ancestor.Location);
            }

            Assert.IsTrue(card.Width <= (int)Math.Round(760 * scale), "Settings cards must respect their content width cap.");
            Assert.AreEqual(stack.Padding.Left, cardBounds.Left);
            Assert.IsTrue(cardBounds.Right <= stack.ClientRectangle.Right - stack.Padding.Right);
            AntdUI.Panel[] rows = [.. card.Controls.OfType<AntdUI.Panel>().OrderBy(static row => row.Top)];
            Assert.AreEqual(field == "_basicSettingsPanel" ? 3 : 2, rows.Length);
            int previousBottom = 0;
            foreach (AntdUI.Panel row in rows)
            {
                Assert.IsTrue(row.Height >= (int)Math.Round((field == "_basicSettingsPanel" ? 44 : 60) * scale));
                Assert.IsTrue(card.ClientRectangle.Contains(row.Bounds), $"{row.Name} must fit inside its card.");
                Assert.IsTrue(row.Top >= previousBottom, "Settings rows must not overlap after wrapping or DPI scaling.");
                previousBottom = row.Bottom;
                Control[] children = [.. row.Controls.Cast<Control>()];
                for (int index = 0; index < children.Length; index++)
                {
                    Control child = children[index];
                    Assert.IsTrue(row.ClientRectangle.Contains(child.Bounds), $"{child.Name} must fit inside its row.");
                    for (int other = index + 1; other < children.Length; other++)
                    {
                        Assert.IsFalse(child.Bounds.IntersectsWith(children[other].Bounds),
                            $"{child.Name} and {children[other].Name} must not overlap.");
                    }

                    if (child is AntdUI.Label label)
                    {
                        Size text = label.GDI(canvas => canvas.MeasureText(label.Text, label.Font, label.Width));
                        Assert.IsTrue(label.Height >= text.Height, $"The wrapped text in {label.Name} must fit its height.");
                    }
                    else
                    {
                        Assert.AreEqual(new Size((int)Math.Round(60 * scale), (int)Math.Round(26 * scale)), child.Size,
                            "Narrow layouts must preserve the switch and number-input dimensions.");
                    }
                }
            }
        }
    }

    private static void AssertBlankPage(Control page)
    {
        Assert.IsInstanceOfType<UserControl>(page);
        Assert.AreEqual(0, page.Controls.Count);
        Assert.AreEqual(DockStyle.Fill, page.Dock);
    }

    private static void AssertTopNavigationGeometry(MainForm form, Size collapsedRowSize)
    {
        AntdUI.Menu menu = GetControl<AntdUI.Menu>(form, "_navigationMenu");
        AntdUI.Button[] buttons =
            [GetControl<AntdUI.Button>(form, "_backButton"), GetControl<AntdUI.Button>(form, "_collapseButton")];
        MethodInfo? getIcon = typeof(AntdUI.Button).GetMethod("GetIconRectCenter",
            BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(Size), typeof(Rectangle)], null);
        Assert.IsNotNull(getIcon);
        Rectangle rootIcon = menu.Items[0].Rect("Icon");
        Point rootIconScreen = menu.PointToScreen(rootIcon.Location);
        Point dashboard = menu.PointToScreen(menu.Items[0].Rect().Location);
        Point devices = menu.PointToScreen(menu.Items[1].Rect().Location);
        var buttonRows = new Point[buttons.Length];
        for (int index = 0; index < buttons.Length; index++)
        {
            AntdUI.Button button = buttons[index];
            Assert.AreEqual(collapsedRowSize, button.ClientRectangle.Size,
                $"The {button.Name} click area must match a collapsed root menu row.");
            Assert.AreEqual(collapsedRowSize, button.ReadRectangle.Size,
                $"The {button.Name} painted area must match its click area.");
            Size fontSize = button.GDI(canvas => canvas.MeasureText(Config.NullText, button.Font));
            object? result = getIcon.Invoke(button, [fontSize, button.ReadRectangle]);
            Assert.IsInstanceOfType<Rectangle>(result);
            var icon = (Rectangle)result;
            Assert.AreEqual(rootIcon.Size, icon.Size, "Buttons must use the same native icon box as root menu items.");
            Assert.AreEqual(rootIconScreen.X, button.PointToScreen(icon.Location).X,
                $"The {button.Name} icon must share the root menu's vertical line.");
            buttonRows[index] = button.PointToScreen(button.ReadRectangle.Location);
            if (menu.Collapsed)
            {
                Assert.AreEqual(dashboard.X, buttonRows[index].X);
            }
            else
            {
                Assert.IsTrue(button.ClientRectangle.Width < menu.Items[0].Rect().Width,
                    "Top navigation buttons must remain compact when the sidebar expands.");
            }
        }

        int rowStep = devices.Y - dashboard.Y;
        Assert.IsGreaterThan(0, rowStep);
        Assert.AreEqual(rowStep, buttonRows[1].Y - buttonRows[0].Y, "Back and menu must use the root row spacing.");
        Assert.AreEqual(rowStep, dashboard.Y - buttonRows[1].Y, "Menu and Dashboard must use the root row spacing.");
        Avatar logo = GetControl<Avatar>(form, "_navigationLogo");
        AntdUI.Label brand = GetControl<AntdUI.Label>(form, "_brandLabel");
        Assert.IsFalse(logo.TabStop);
        Assert.AreSame(buttons[0].Parent, logo.Parent);
        Assert.AreEqual(rootIcon.Size, logo.ReadRectangle.Size,
            "The logo must retain the same rendered size as a root navigation icon.");
        if (logo.Visible)
        {
            Point logoScreen = logo.PointToScreen(logo.ReadRectangle.Location);
            Assert.AreEqual(buttonRows[0].Y + buttons[0].Height / 2F,
                logoScreen.Y + logo.ReadRectangle.Height / 2F, 0.5F,
                "The logo must remain vertically centered in the first navigation row.");
            if (buttons[0].Enabled)
            {
                Assert.IsTrue(logo.Left >= buttons[0].Right,
                    "Expanded navigation with history must place the logo to the right of the return slot.");
            }
            else
            {
                Assert.AreEqual(rootIconScreen.X, logoScreen.X,
                    "Without history, the logo must share the root navigation icon axis.");
            }

            if (brand.Visible)
            {
                Assert.IsTrue(brand.Left >= logo.Right, "The application name must follow the logo without overlap.");
            }
        }

        PageHeader titleBar = GetControl<PageHeader>(form, "_titleBar");
        Assert.IsTrue(string.IsNullOrEmpty(titleBar.Text));
        Assert.IsTrue(string.IsNullOrEmpty(titleBar.SubText));
        Assert.IsFalse(titleBar.ShowIcon);
        Assert.AreEqual("FbSample", form.Text);
        Assert.IsNotNull(form.Icon);
    }

    private static void AssertRootMenuGeometry(AntdUI.Menu menu, Rectangle[] icons, int[] rowHeights)
    {
        Assert.AreEqual(s_parentRoutes.Length, menu.Items.Count);
        for (int index = 0; index < menu.Items.Count; index++)
        {
            AntdUI.MenuItem item = menu.Items[index];
            Rectangle icon = item.Rect("Icon");
            Assert.AreEqual(icons[index].X, icon.X, $"The {item.Name} icon must remain on the same vertical line.");
            Assert.AreEqual(icons[index].Size, icon.Size, $"Toggling must not resize the {item.Name} icon.");
            Assert.AreEqual(rowHeights[index], item.Rect().Height, "Horizontal alignment must not change row height.");
            if (!menu.Collapsed)
            {
                Form form = menu.FindForm()!;
                float scale = form.DeviceDpi / 96F;
                int boundary = GetControl<AntdUI.Panel>(form, "_workspacePanel").Left + (int)Math.Round(6 * scale);
                Rectangle text = item.Rect("Text");
                int textLeft = form.PointToClient(menu.PointToScreen(text.Location)).X;
                Assert.IsTrue(textLeft >= boundary,
                    $"The {item.Name} text at {textLeft}px must start after the icon rail and its gap ({boundary}px).");
                Assert.IsTrue(textLeft <= boundary + (int)Math.Ceiling(scale),
                    "The reserved text gap may differ only by one logical unit of pixel rounding.");
                Size measured = menu.GDI(canvas => canvas.MeasureText(item.Text, item.Font ?? menu.Font));
                Assert.IsTrue(text.Width >= measured.Width && text.Height >= measured.Height,
                    $"The root name '{item.Text}' must still fit after reserving the icon rail.");
            }
        }
    }

    private static void AssertContentFillsShell(MainForm form)
    {
        form.PerformLayout();
        Application.DoEvents();
        AntdUI.Panel contentPanel = GetControl<AntdUI.Panel>(form, "_contentPanel");
        AntdUI.Panel workspace = GetControl<AntdUI.Panel>(form, "_workspacePanel");
        AntdUI.Panel navigationPanel = GetControl<AntdUI.Panel>(form, "_navigationPanel");
        AntdUI.Menu menu = GetControl<AntdUI.Menu>(form, "_navigationMenu");
        PageHeader titleBar = GetControl<PageHeader>(form, "_titleBar");
        Control page = GetVisiblePage(contentPanel);
        int railWidth = (int)Math.Round(58 * (form.DeviceDpi / 96F));
        Assert.AreEqual(railWidth, workspace.Left);
        Assert.AreEqual(0, workspace.Top);
        Assert.AreEqual(form.ClientRectangle.Right, workspace.Right);
        Assert.AreEqual(form.ClientRectangle.Bottom, workspace.Bottom);
        Assert.AreEqual(DockStyle.Fill, workspace.Dock);
        Assert.AreSame(workspace, titleBar.Parent);
        Assert.AreSame(workspace, contentPanel.Parent);
        Assert.AreEqual(Point.Empty, navigationPanel.Location);
        Assert.AreEqual(form.ClientSize.Height, navigationPanel.Height);
        Assert.AreEqual((int)Math.Round((menu.Collapsed ? 58 : 250) * (form.DeviceDpi / 96F)), navigationPanel.Width);
        Assert.AreEqual(0, contentPanel.Left);
        Assert.AreEqual(titleBar.Bottom, contentPanel.Top, "The page must begin immediately below the title bar.");
        Assert.AreEqual(workspace.ClientRectangle.Right, contentPanel.Right);
        Assert.AreEqual(workspace.ClientRectangle.Bottom, contentPanel.Bottom);
        Assert.AreEqual(DockStyle.Fill, contentPanel.Dock);
        Assert.AreEqual(contentPanel.ClientRectangle, page.Bounds);
        Assert.AreEqual(form.DeviceDpi, page.DeviceDpi);
    }

    private static void RenderControl(Control control, string fileName)
    {
        string outputDirectory = GetUiArtifactDirectory();
        using Bitmap bitmap = CaptureControl(control);
        bitmap.Save(Path.Combine(outputDirectory, fileName), ImageFormat.Png);
    }

    private static Bitmap CaptureControl(Control control)
    {
        if (control is NavigationMenu && control.Name == "_navigationFlyoutMenu")
        {
            NavigationMenu root = GetControl<NavigationMenu>(control.FindForm()!, "_navigationMenu");
            Assert.IsNotNull(root.FlyoutWindow);
            using Bitmap frame = root.FlyoutWindow.PrintBit();
            return frame.Clone(control.Bounds, PixelFormat.Format32bppArgb);
        }

        CreateHandles(control);
        control.PerformLayout();
        // AntdUI activates tab content through BeginInvoke; flush its queued UI work before printing.
        Application.DoEvents();
        Bitmap? bitmap = new Bitmap(control.ClientSize.Width, control.ClientSize.Height);
        try
        {
            using Graphics graphics = Graphics.FromImage(bitmap);
            if (control is MainForm)
            {
                graphics.Clear(control.BackColor);
                // Whole-window WM_PRINT paints the workspace over the overlapping navigation; compose back to front.
                foreach (Control child in control.Controls.Cast<Control>().Where(static child => child.Visible).Reverse())
                {
                    using Bitmap childBitmap = CaptureControl(child);
                    graphics.DrawImageUnscaled(childBitmap, child.Location);
                }
            }
            else
            {
                nint deviceContext = graphics.GetHdc();
                try
                {
                    // WM_PRINT with PRF_CHECKVISIBLE excludes hidden tab pages; no native caption is requested.
                    _ = SendMessage(control.Handle, 0x0317, deviceContext, 0x001D);
                }
                finally
                {
                    graphics.ReleaseHdc(deviceContext);
                }
            }

            Bitmap result = bitmap;
            bitmap = null;
            return result;
        }
        finally
        {
            bitmap?.Dispose();
        }
    }

    private static string GetUiArtifactDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory.FullName, "FbSample.sln")))
        {
            directory = directory.Parent ?? throw new DirectoryNotFoundException("FbSample.sln was not found.");
        }

        string outputDirectory = Path.Combine(directory.FullName, "artifacts", "ui");
        Directory.CreateDirectory(outputDirectory);
        return outputDirectory;
    }

    private static void AppendControlLayout(StringBuilder report, Control control, int depth)
    {
        report.Append(' ', depth * 2);
        report.AppendLine(CultureInfo.InvariantCulture,
            $"{control.GetType().FullName} Name={control.Name} Text={control.Text} "
            + $"Bounds={control.Bounds} ClientSize={control.ClientSize} Visible={control.Visible} "
            + $"DeviceDpi={control.DeviceDpi} Font={control.Font.Name}/{control.Font.SizeInPoints}pt "
            + $"Padding={control.Padding} Margin={control.Margin} Dock={control.Dock}");
        if (control is ContainerControl container)
        {
            report.Append(' ', depth * 2);
            report.AppendLine(CultureInfo.InvariantCulture,
                $"AutoScaleMode={container.AutoScaleMode} AutoScaleDimensions={container.AutoScaleDimensions} "
                + $"CurrentAutoScaleDimensions={container.CurrentAutoScaleDimensions}");
        }

        foreach (Control child in control.Controls)
        {
            AppendControlLayout(report, child, depth + 1);
        }
    }

    private static void CreateHandles(Control control)
    {
        _ = control.Handle;
        foreach (Control child in control.Controls)
        {
            CreateHandles(child);
        }
    }

    private sealed class OffscreenMainForm : MainForm
    {
        internal OffscreenMainForm()
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-32000, -32000);
            ShowInTaskbar = false;
            Opacity = 0;
        }

        protected override bool ShowWithoutActivation => true;
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageW", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint SendMessage(nint window, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint GetWindow(nint window, uint command);
}
