using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace WTVRSettingsAssistant;

internal sealed class AviationHomePage : UserControl
{
    private readonly AlphaPictureBox _logo;
    private WebView2? _webView;
    private readonly Panel _webLoadingCover;
    private bool _webReady;
    private string? _lastWebState;
    private double[]? _webHeroMetrics;
    private bool _webFallbackLocked;
    private System.Windows.Forms.Timer? _webInitWatchdog;
    private Control? _desktopProfileSelector;
    private Control? _vrProfileSelector;
    private bool _profileSelectorsVisible;
    private bool _desktopProfileSelectorEnabled;
    private bool _vrProfileSelectorEnabled;
    public event Action<bool>? ControlProfileMenuRequested;
    private string _languageCode = "en";
    private bool _monitorActive;
    private bool _vrActive;
    private bool _neckEnabled;
    private bool _keysEnabled;
    private bool _vtrimEnabled;
    private GameServerChannel _serverChannel;
    private bool _canLaunch;
    private bool _gameRunning;
    private bool _launchBusy;
    private bool _updateAvailable;
    private Version? _liveVersion;
    private Version? _testVersion;

    public void AttachControlProfileSelectors(Control desktop, Control vr)
    {
        _desktopProfileSelector = desktop;
        _vrProfileSelector = vr;
        AttachProfileSelectorsToSurface();
        Arrange();
    }

    private void AttachProfileSelectorsToSurface()
    {
        if (_desktopProfileSelector is null || _vrProfileSelector is null) return;
        // Keep the WinForms selectors parented to the native cards only. A native
        // transparent control cannot reveal a WebView2 HWND beneath it; doing so
        // produced the dark square seen around the arrows. Enhanced Home draws its
        // arrows inside HTML instead.
        if (!ReferenceEquals(_desktopProfileSelector.Parent, _monitor)) _monitor.Controls.Add(_desktopProfileSelector);
        if (!ReferenceEquals(_vrProfileSelector.Parent, _vr)) _vr.Controls.Add(_vrProfileSelector);
        _desktopProfileSelector.Visible = !_webReady && _profileSelectorsVisible;
        _vrProfileSelector.Visible = !_webReady && _profileSelectorsVisible;
        PushWebState();
    }

    public void SetControlProfileSelectorState(bool visible, bool desktopEnabled, bool vrEnabled)
    {
        _profileSelectorsVisible = visible;
        _desktopProfileSelectorEnabled = desktopEnabled;
        _vrProfileSelectorEnabled = vrEnabled;
        if (_desktopProfileSelector is not null)
        {
            _desktopProfileSelector.Enabled = desktopEnabled;
            _desktopProfileSelector.Visible = !_webReady && visible;
        }
        if (_vrProfileSelector is not null)
        {
            _vrProfileSelector.Enabled = vrEnabled;
            _vrProfileSelector.Visible = !_webReady && visible;
        }
        PushWebState();
    }
    private Control? _aircraftProfiles;
    private bool _showAircraftProfiles = true;
    public void SetAircraftProfilesVisible(bool visible)
    {
        if (_showAircraftProfiles == visible) return;
        _showAircraftProfiles = visible;
        Arrange();
    }
    public void SetAircraftProfiles(Control browser)
    {
        _aircraftProfiles?.Dispose();
        _aircraftProfiles = browser;
        browser.Dock = DockStyle.None;
        Controls.Add(browser);
        Arrange();
        if (_webReady) browser.BringToFront();
        else if (_webLoadingCover.Visible) _webLoadingCover.BringToFront();
    }
    private readonly Label _playMode;
    private readonly AviationActionCard _monitor;
    private readonly AviationActionCard _vr;
    private readonly AviationActionCard _neck;
    private readonly AviationActionCard _keys;
    private readonly AviationActionCard _trim;
    private readonly AviationServerSelector _server;
    private readonly AviationLaunchButton _launch;
    private readonly TableLayoutPanel _links;
    private bool _arranging;

    public AviationHomePage(
        Image logo,
        Image neckImage,
        Image keysImage,
        Action monitor,
        Action vr,
        Action openNeck,
        Action toggleNeck,
        Action openKeys,
        Action toggleKeys,
        Action<GameServerChannel> serverChanged,
        Action refreshServers,
        Action launch,
        Action logoClick,
        Action discord,
        Action youtube,
        Action support,
        Action? openTrim = null,
        Action? toggleTrim = null)
    {
        Dock = DockStyle.Fill;
        BackColor = IllustratedTheme.Background;
        AutoScroll = false;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        _playMode = MakeLabel("PLAY MODE", 20, FontStyle.Bold, ContentAlignment.MiddleCenter);
        _logo = new AlphaPictureBox(logo)
        {
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Cursor = Cursors.Default
        };
        // The Home logo is decorative; no hover text or secret-action target.
        _logo.TabStop = false;

        Image? monitorImage = LoadOptionalAsset("Home_Monitor_New.png");
        Image? vrImage = LoadOptionalAsset("Home_VR_New.png");
        Image? trimImage = LoadOptionalAsset("Home_VTrim_New.png");
        Image? cleanNeckImage = LoadOptionalAsset("IllustratedHead.png") ?? neckImage;
        Image? cleanKeysImage = LoadOptionalAsset("IllustratedKeys.png") ?? keysImage;
        Image? redLaunchFrame = LoadOptionalAsset("PlayButton.png") ?? LoadOptionalAsset("Launch_Red_Frame.png");
        Image? updateLaunchFrame = LoadOptionalAsset("UpdateButton.png") ?? LoadOptionalAsset("Launch_Update_Gold.png");
        Image? runningLaunchFrame = LoadOptionalAsset("GameStarted.png") ?? redLaunchFrame;

        _monitor = new AviationActionCard("MONITOR", "monitor", monitorImage, monitor);
        _vr = new AviationActionCard("VR", "visor", vrImage, vr);
        _neck = new AviationActionCard("Neck Assistant", "head", cleanNeckImage, openNeck) { ToggleClick = toggleNeck };
        _keys = new AviationActionCard("KeyBind Assistant", "keys", cleanKeysImage, openKeys) { ToggleClick = toggleKeys };
        _trim = new AviationActionCard("VTrim Assistant", "trim", trimImage, openTrim ?? (() => { })) { ToggleClick = toggleTrim };
        _server = new AviationServerSelector(serverChanged, refreshServers);
        _launch = new AviationLaunchButton(launch, redLaunchFrame, updateLaunchFrame, runningLaunchFrame);

        _links = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _links.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        _links.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        _links.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334f));
        _links.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _links.Controls.Add(new AviationLinkButton("DISCORD", "discord", discord), 0, 0);
        _links.Controls.Add(new AviationLinkButton("YOUTUBE", "youtube", youtube), 1, 0);
        _links.Controls.Add(new AviationLinkButton("Buy me a Beer!", "beer", support), 2, 0);

        Controls.AddRange([_playMode, _monitor, _vr, _neck, _keys, _trim, _server, _launch, _links, _logo]);

        _webLoadingCover = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = IllustratedTheme.Background,
            Visible = ShouldUseEnhancedWebUi()
        };
        Label loading = new()
        {
            Dock = DockStyle.Fill,
            Text = "WT VR ASSISTANT",
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = IllustratedTheme.Muted,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            AutoSize = false
        };
        _webLoadingCover.Controls.Add(loading);
        Controls.Add(_webLoadingCover);
        if (_webLoadingCover.Visible)
        {
            SetNativeSurfaceVisible(false);
            _webLoadingCover.BringToFront();
        }

        void StartWebSurfaceWhenVisible()
        {
            if (IsDisposed || Disposing || !IsHandleCreated || !Visible ||
                FindForm()?.WindowState == FormWindowState.Minimized ||
                !ShouldUseEnhancedWebUi()) return;
            _ = InitializeWebSurfaceAsync(
                monitor, vr, openNeck, toggleNeck, openKeys, toggleKeys,
                serverChanged, refreshServers, launch, logoClick, discord, youtube, support,
                openTrim ?? (() => { }), toggleTrim ?? (() => { }));
        }

        Resize += (_, _) => { Arrange(); Invalidate(); };
        HandleCreated += (_, _) =>
        {
            BeginInvoke((Action)Arrange);
            BeginInvoke((Action)StartWebSurfaceWhenVisible);
        };
        VisibleChanged += (_, _) =>
        {
            if (Visible && IsHandleCreated)
                BeginInvoke((Action)StartWebSurfaceWhenVisible);
        };
    }

    public void SetState(bool monitorActive, bool vrActive, bool neckEnabled, bool keysEnabled, GameServerChannel server, bool canLaunch, bool gameRunning, bool launchBusy)
    {
        _monitorActive = monitorActive;
        _vrActive = vrActive;
        _neckEnabled = neckEnabled;
        _keysEnabled = keysEnabled;
        _serverChannel = server;
        _canLaunch = canLaunch;
        _gameRunning = gameRunning;
        _launchBusy = launchBusy;

        _monitor.Active = monitorActive;
        _vr.Active = vrActive;
        _neck.Active = neckEnabled;
        _keys.Active = keysEnabled;
        _server.Selected = server;
        _launch.GameRunning = gameRunning;
        _launch.CanLaunch = canLaunch && !gameRunning && !launchBusy;
        PushWebState();
    }

    public void SetVTrimState(bool enabled)
    {
        if (_vtrimEnabled == enabled) return;
        _vtrimEnabled = enabled;
        _trim.Active = enabled;
        PushWebState();
    }

    public void SetGameUpdateAvailable(bool updateAvailable)
    {
        _updateAvailable = updateAvailable;
        _launch.UpdateAvailable = updateAvailable;
        PushWebState();
    }

    public void SetServerVersions(Version? live, Version? test)
    {
        _liveVersion = live;
        _testVersion = test;
        _server.SetVersions(live, test);
        PushWebState();
    }
    public void SetServerStatuses(ServerSignal live, ServerSignal test) { /* Server availability indicators intentionally removed in v2.0. */ }

    public void RefreshLayoutAfterRestore()
    {
        Arrange();
        Invalidate(true);
    }

    public void SetLanguage(string languageCode)
    {
        string normalized = AppText.Normalize(languageCode);
        bool languageChanged = normalized != _languageCode;
        _languageCode = normalized;
        _playMode.Text = AppText.T(_languageCode, "Home.ModeHeading");
        _server.SetLanguage(languageCode);
        _launch.SetLabels(
            AppText.T(_languageCode, "Home.LaunchAction"),
            AppText.T(_languageCode, "Home.UpdateAction"),
            "RUNNING");
        _monitor.Text = AppText.T(_languageCode, "Home.Monitor");
        _monitor.AccessibleName = _monitor.Text;
        _vr.Text = AppText.T(_languageCode, "Home.VR");
        _vr.AccessibleName = _vr.Text;
        SetCardText(_neck, AppText.T(_languageCode, "Nav.Neck"));
        SetCardText(_keys, AppText.T(_languageCode, "Nav.Keybind"));
        SetCardText(_trim, AppText.T(_languageCode, "Nav.VTrim"));
        _neck.SetStatusText(AppText.T(_languageCode, "Common.On"), AppText.T(_languageCode, "Common.Off"));
        _keys.SetStatusText(AppText.T(_languageCode, "Common.On"), AppText.T(_languageCode, "Common.Off"));
        _trim.SetStatusText(AppText.T(_languageCode, "Common.On"), AppText.T(_languageCode, "Common.Off"));
        if (_links.Controls.Count >= 3)
        {
            _links.Controls[0].Text = AppText.T(_languageCode, "Home.Discord");
            _links.Controls[1].Text = AppText.T(_languageCode, "Home.YouTube");
            _links.Controls[2].Text = AppText.T(_languageCode, "Home.Support");
        }
        Invalidate();
        if (_webReady && languageChanged) PushWebState();
    }

    private static bool ShouldUseEnhancedWebUi()
    {
        try
        {
            if (Environment.GetCommandLineArgs().Any(arg => arg.Equals("--classic-ui", StringComparison.OrdinalIgnoreCase)))
                return false;
            string flag = Path.Combine(AppContext.BaseDirectory, "Settings", "classic_ui.flag");
            return !File.Exists(flag);
        }
        catch
        {
            return true;
        }
    }

    private async Task InitializeWebSurfaceAsync(
        Action monitor,
        Action vr,
        Action openNeck,
        Action toggleNeck,
        Action openKeys,
        Action toggleKeys,
        Action<GameServerChannel> serverChanged,
        Action refreshServers,
        Action launch,
        Action logoClick,
        Action discord,
        Action youtube,
        Action support,
        Action openTrim,
        Action toggleTrim)
    {
        if (_webView is not null || IsDisposed || Disposing || _webFallbackLocked) return;
        _webFallbackLocked = false;

        WebView2 view = new()
        {
            Dock = DockStyle.Fill,
            Visible = false,
            BackColor = IllustratedTheme.Background,
            TabStop = true
        };
        _webView = view;
        Controls.Add(view);

        try
        {
            // Never leave the Home page hidden behind the startup cover forever.
            // If WebView2 initialization or first navigation stalls, fall back to
            // the proven native dashboard instead of presenting an empty Home.
            _webInitWatchdog?.Stop();
            _webInitWatchdog?.Dispose();
            _webInitWatchdog = new System.Windows.Forms.Timer { Interval = 6000 };
            _webInitWatchdog.Tick += (_, _) =>
            {
                _webInitWatchdog?.Stop();
                if (!_webReady && CanUseWebView(view)) SwitchToNativeFallback();
            };
            _webInitWatchdog.Start();

            string userData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WTVRSettingsAssistant",
                "WebView2");
            Directory.CreateDirectory(userData);
            CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, userData);
            if (!CanUseWebView(view)) { DiscardWebView(view); return; }
            await view.EnsureCoreWebView2Async(environment);
            if (!CanUseWebView(view)) { DiscardWebView(view); return; }
            string webAssetFolder = PrepareWebAssetFolder();
            view.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "wtassets.local",
                webAssetFolder,
                CoreWebView2HostResourceAccessKind.Allow);

            CoreWebView2Settings settings = view.CoreWebView2.Settings;
            settings.AreDefaultContextMenusEnabled = false;
            settings.AreDevToolsEnabled = false;
            settings.IsStatusBarEnabled = false;
            settings.IsZoomControlEnabled = false;
            settings.AreBrowserAcceleratorKeysEnabled = false;
            settings.AreDefaultScriptDialogsEnabled = false;
            settings.AreHostObjectsAllowed = false;

            view.CoreWebView2.ProcessFailed += (_, _) =>
            {
                if (!CanUseWebView(view)) return;
                try { BeginInvoke((Action)(() => { if (CanUseWebView(view)) SwitchToNativeFallback(); })); } catch { }
            };

            view.CoreWebView2.WebMessageReceived += (_, e) =>
            {
                if (!CanUseWebView(view)) return;
                string message;
                try { message = e.TryGetWebMessageAsString(); }
                catch { return; }

                if (message.StartsWith("hero-bounds:", StringComparison.Ordinal))
                {
                    try
                    {
                        double[]? metrics = JsonSerializer.Deserialize<double[]>(message[12..]);
                        if (metrics is { Length: 4 } && metrics[3] > 0)
                        {
                            _webHeroMetrics = metrics;
                            if (_webReady) Arrange();
                        }
                    }
                    catch (JsonException) { }
                    return;
                }

                switch (message)
                {
                    case "monitor": monitor(); break;
                    case "vr": vr(); break;
                    case "neck": toggleNeck(); break;
                    case "keybind": toggleKeys(); break;
                    case "vtrim": toggleTrim(); break;
                    case "server-live": serverChanged(GameServerChannel.Live); break;
                    case "server-test": serverChanged(GameServerChannel.Test); break;
                    case "refresh-servers": refreshServers(); break;
                    case "launch": launch(); break;
                    case "logo": logoClick(); break;
                    case "discord": discord(); break;
                    case "youtube": youtube(); break;
                    case "support": support(); break;
                    case "open-neck": openNeck(); break;
                    case "open-keybind": openKeys(); break;
                    case "open-vtrim": openTrim(); break;
                    case "profile-monitor": ControlProfileMenuRequested?.Invoke(false); break;
                    case "profile-vr": ControlProfileMenuRequested?.Invoke(true); break;
                }
            };

            view.CoreWebView2.NavigationStarting += (_, e) =>
            {
                // Do not block the initial NavigateToString navigation. Once the
                // dashboard is live, prevent clicks from navigating the embedded
                // browser away from our app surface.
                if (_webReady && !e.Uri.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase))
                    e.Cancel = true;
            };
            view.CoreWebView2.NewWindowRequested += (_, e) => e.Handled = true;

            view.NavigationCompleted += (_, e) =>
            {
                if (!CanUseWebView(view)) return;
                if (!e.IsSuccess)
                {
                    SwitchToNativeFallback();
                    return;
                }
                _webReady = true;
                _lastWebState = null;
                _webInitWatchdog?.Stop();
                _webLoadingCover.Visible = false;
                view.Visible = true;
                view.BringToFront();
                SetNativeSurfaceVisible(false);
                AttachProfileSelectorsToSurface();
                Arrange();
                _aircraftProfiles?.BringToFront();
                PushWebState();
            };

            view.NavigateToString(BuildWebHtml());
        }
        catch
        {
            _webInitWatchdog?.Stop();
            DiscardWebView(view);
            if (!IsDisposed && !Disposing) SwitchToNativeFallback();
        }
    }

    private bool CanUseWebView(WebView2 view) =>
        !IsDisposed && !Disposing && !_webFallbackLocked &&
        ReferenceEquals(_webView, view) && !view.IsDisposed && !view.Disposing;

    private void DiscardWebView(WebView2 view)
    {
        if (ReferenceEquals(_webView, view)) _webView = null;
        try { Controls.Remove(view); } catch { }
        try { view.Dispose(); } catch { }
    }

    private void SwitchToNativeFallback()
    {
        if (IsDisposed || Disposing) return;
        _webInitWatchdog?.Stop();
        _webFallbackLocked = true;
        _webReady = false;
        _webLoadingCover.Visible = false;
        if (_webView is { IsDisposed: false } view)
            try { view.Visible = false; } catch (ObjectDisposedException) { }
        SetNativeSurfaceVisible(true);
        AttachProfileSelectorsToSurface();
        Arrange();
    }

    private void SetNativeSurfaceVisible(bool visible)
    {
        _playMode.Visible = false;
        _monitor.Visible = visible;
        _vr.Visible = visible;
        _neck.Visible = visible;
        _keys.Visible = visible;
        _trim.Visible = visible;
        _server.Visible = visible;
        _launch.Visible = visible;
        _links.Visible = visible;
        if (!visible) _logo.Visible = false;
    }

    private void ReloadWebUi()
    {
        if (_webView is not { } view || !CanUseWebView(view)) return;
        try { if (view.CoreWebView2 is not null) view.NavigateToString(BuildWebHtml()); } catch { }
    }

    private void PushWebState()
    {
        if (!_webReady || _webView is not { } view || !CanUseWebView(view)) return;
        string liveVersion = _liveVersion is null
            ? AppText.T(_languageCode, "Home.Version").Replace("{0}", "...")
            : string.Format(AppText.T(_languageCode, "Home.Version"), _liveVersion);
        string testVersion = _testVersion is null
            ? AppText.T(_languageCode, "Home.Version").Replace("{0}", "...")
            : string.Format(AppText.T(_languageCode, "Home.Version"), _testVersion);

        var state = new
        {
            monitor = _monitorActive,
            vr = _vrActive,
            neck = _neckEnabled,
            keybind = _keysEnabled,
            vtrim = _vtrimEnabled,
            server = _serverChannel == GameServerChannel.Test ? "test" : "live",
            canLaunch = _canLaunch,
            running = _gameRunning,
            busy = _launchBusy,
            update = _updateAvailable,
            liveVersion,
            testVersion,
            profiles = new
            {
                visible = _profileSelectorsVisible,
                monitorEnabled = _desktopProfileSelectorEnabled,
                vrEnabled = _vrProfileSelectorEnabled
            },
            labels = new
            {
                live = AppText.T(_languageCode, "Home.LiveServer"),
                test = AppText.T(_languageCode, "Home.TestServer"),
                monitor = AppText.T(_languageCode, "Home.Monitor"),
                vr = AppText.T(_languageCode, "Home.VR"),
                neck = AppText.T(_languageCode, "Nav.Neck"),
                keybind = AppText.T(_languageCode, "Nav.Keybind"),
                vtrim = AppText.T(_languageCode, "Nav.VTrim"),
                on = AppText.T(_languageCode, "Common.On"),
                off = AppText.T(_languageCode, "Common.Off"),
                launch = AppText.T(_languageCode, "Home.LaunchAction"),
                update = AppText.T(_languageCode, "Home.UpdateAction"),
                running = "RUNNING",
                discord = AppText.T(_languageCode, "Home.Discord"),
                youtube = AppText.T(_languageCode, "Home.YouTube"),
                support = AppText.T(_languageCode, "Home.Support")
            }
        };

        string payload = JsonSerializer.Serialize(state);
        if (payload == _lastWebState) return;
        SendWebStateAsync(payload);
    }

    private async void SendWebStateAsync(string payload)
    {
        if (!_webReady || _webView is not { } view || !CanUseWebView(view)) return;
        try
        {
            CoreWebView2? core = view.CoreWebView2;
            if (core is null) return;
            _lastWebState = payload;
            await core.ExecuteScriptAsync("window.wtApplyState(" + payload + ");");
        }
        catch { if (ReferenceEquals(_webView, view)) _lastWebState = null; }
    }

    private string BuildWebHtml()
    {
        const string assetRoot = "https://wtassets.local/";
        string logo = assetRoot + "logo.png";
        string monitor = assetRoot + "monitor.png";
        string vr = assetRoot + "vr.png";
        string neck = assetRoot + "neck.png";
        string keybind = assetRoot + "keybind.png";
        string trim = assetRoot + "vtrim.png";
        string launchNormal = assetRoot + "launch-normal.png";
        string launchUpdate = assetRoot + "launch-update.png";
        string launchRunning = assetRoot + "launch-running.png";
        string liveIcon = assetRoot + "live.png";
        string liveActiveIcon = assetRoot + "live-active.png";
        string testIcon = assetRoot + "test.png";
        string testActiveIcon = assetRoot + "test-active.png";
        string discordIcon = assetRoot + "discord.png";
        string youtubeIcon = assetRoot + "youtube.png";
        string supportIcon = assetRoot + "support.png";

        static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        string themeClass = AppThemeAssets.ActiveTheme.Equals("MiG29", StringComparison.OrdinalIgnoreCase) ? "mig" : "f16";

        return $$"""
<!doctype html>
<html>
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1,user-scalable=no">
<style>
:root {
  --bg: {{Hex(IllustratedTheme.Background)}};
  --panel: {{Hex(IllustratedTheme.Panel)}};
  --ink: {{Hex(IllustratedTheme.Ivory)}};
  --accent: {{Hex(IllustratedTheme.Gold)}};
  --muted: {{Hex(IllustratedTheme.Muted)}};
  --edge: rgba(235,238,240,.20);
}
* { box-sizing:border-box; user-select:none; -webkit-user-drag:none; }
html,body { width:100%; height:100%; margin:0; overflow:hidden; background:var(--bg); color:var(--ink); font-family:"Segoe UI",Arial,sans-serif; }
body::before { content:""; position:fixed; inset:0; pointer-events:none; opacity:.035; background:radial-gradient(circle at 66% 34%,rgba(255,255,255,.10),transparent 44%); }
#stage {
  position:absolute; inset:0; min-width:0; min-height:0;
  display:grid; grid-template-columns:minmax(380px,46%) minmax(0,54%);
  gap:clamp(10px,1.15vw,20px); padding:clamp(8px,.85vw,16px);
}
#hero { min-width:0; min-height:0; display:flex; align-items:center; justify-content:center; overflow:hidden; }
#logo {
  display:block; width:min(100%,840px); height:min(100%,840px); object-fit:contain; object-position:center;
  pointer-events:none; cursor:default; image-rendering:auto;
  filter:drop-shadow(0 8px 18px rgba(0,0,0,.28));
}
#right {
  min-width:0; min-height:0; display:grid;
  grid-template-rows:clamp(68px,9.7vh,92px) minmax(0,1fr) minmax(0,1.16fr) max-content clamp(38px,5.1vh,48px);
  gap:clamp(8px,.9vh,14px);
}
#servers,#modes,#assistants,#links { min-width:0; min-height:0; display:grid; gap:clamp(8px,.72vw,12px); }
#servers,#modes { grid-template-columns:minmax(0,1fr) minmax(0,1fr); }
#assistants,#links { grid-template-columns:repeat(3,minmax(0,1fr)); }
.card {
  position:relative; min-width:0; min-height:0; overflow:hidden;
  background:linear-gradient(180deg,rgba(255,255,255,.018),rgba(0,0,0,.08)),var(--panel);
  border:1px solid var(--edge); border-radius:4px; cursor:pointer;
  transition:border-color .14s ease,box-shadow .14s ease,background .14s ease;
}
@keyframes enabledFlash {
  0%{box-shadow:0 0 0 0 color-mix(in srgb,var(--accent) 70%,transparent)}
  48%{box-shadow:0 0 0 4px color-mix(in srgb,var(--accent) 22%,transparent),0 0 22px color-mix(in srgb,var(--accent) 34%,transparent)}
  100%{box-shadow:0 0 0 0 transparent}
}
.card.flash { animation:enabledFlash .48s ease-out; }
.card:hover { border-color:color-mix(in srgb,var(--accent) 66%,var(--edge)); box-shadow:0 7px 18px rgba(0,0,0,.20); }
.card.active { border-color:var(--accent); box-shadow:inset 0 0 0 1px color-mix(in srgb,var(--accent) 58%,transparent); background:linear-gradient(180deg,color-mix(in srgb,var(--accent) 12%,var(--panel)),var(--panel)); }
.card:focus-visible,.link:focus-visible { outline:2px solid var(--accent); outline-offset:2px; }
.icon-card img { display:block; width:100%; height:100%; object-fit:contain; padding:clamp(7px,.72vw,12px); image-rendering:auto; backface-visibility:hidden; }
.profileArrow {
  position:absolute; right:clamp(10px,.75vw,14px); bottom:clamp(9px,.7vw,13px);
  width:clamp(28px,2.15vw,38px); height:clamp(24px,1.8vw,32px);
  border:0; background:transparent; padding:0; cursor:pointer; z-index:5;
  display:flex; align-items:center; justify-content:center; opacity:.92;
}
.profileArrow::before { content:""; width:0; height:0; border-left:clamp(7px,.55vw,10px) solid transparent; border-right:clamp(7px,.55vw,10px) solid transparent; border-top:clamp(10px,.75vw,14px) solid #efe2c4; filter:drop-shadow(0 1px 1px rgba(0,0,0,.7)); }
.profileArrow:hover::before,.profileArrow:focus-visible::before { border-top-color:var(--accent); }
.profileArrow.disabled { opacity:.30; cursor:default; }
.profileArrow:focus-visible { outline:1px solid var(--accent); outline-offset:1px; }
.server { display:flex; align-items:center; justify-content:flex-start; gap:clamp(10px,1vw,16px); text-align:left; padding:clamp(7px,.7vw,12px) clamp(14px,1.4vw,24px); }
.serverIcon { width:clamp(38px,3.2vw,54px); height:clamp(38px,3.2vw,54px); object-fit:contain; flex:0 0 auto; image-rendering:auto; }
.serverCopy { min-width:0; flex:1; }
.server .title { font-size:clamp(16px,1.35vw,24px); font-weight:800; letter-spacing:.35px; line-height:1.04; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
.server .version { margin-top:clamp(3px,.42vh,7px); font-size:clamp(14px,.95vw,19px); font-weight:650; color:var(--muted); white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
.server.active .version { color:var(--ink); }
#launch { border:none; background:transparent; border-radius:6px; width:100%; height:auto; aspect-ratio:2070 / 432; align-self:end; }
#launch img { width:100%; height:auto; aspect-ratio:2070 / 432; object-fit:contain; display:block; image-rendering:auto; }
#launch .launchText { position:absolute; inset:0; display:flex; align-items:center; justify-content:center; color:#efe2c4; text-shadow:0 2px 5px rgba(0,0,0,.85); font-weight:900; font-size:40px; line-height:1; letter-spacing:.8px; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
#launch.disabled { opacity:.54; cursor:default; }
#launch.running { opacity:1; }
#launch.running .launchText { color:#c8c3b8; }
#launch.disabled:hover { box-shadow:none; }
.link { min-width:0; border:1px solid var(--edge); border-radius:5px; background:var(--panel); color:var(--ink); display:flex; align-items:center; justify-content:center; gap:clamp(5px,.55vw,9px); font-size:clamp(14px,.92vw,18px); font-weight:750; cursor:pointer; transition:border-color .14s ease,background .14s ease; overflow:hidden; white-space:nowrap; text-overflow:ellipsis; padding:0 clamp(6px,.65vw,10px); }
.link img { width:clamp(16px,1.3vw,22px); height:clamp(16px,1.3vw,22px); object-fit:contain; flex:0 0 auto; image-rendering:auto; }
.link:hover { border-color:var(--accent); background:color-mix(in srgb,var(--accent) 8%,var(--panel)); }
@media (max-width:1120px) {
  #stage { grid-template-columns:minmax(320px,42%) minmax(0,58%); gap:10px; }
  #right { gap:8px; }
  #servers,#modes,#assistants,#links { gap:8px; }
}
@media (max-height:720px) {
  #stage { padding:6px; }
  #right { grid-template-rows:62px minmax(0,1fr) minmax(0,1.1fr) max-content 34px; gap:6px; }
  .icon-card img { padding:5px; }
}
</style>
</head>
<body class="{{themeClass}}">
<div id="stage">
  <div id="hero"><img id="logo" src="{{logo}}" alt="" aria-hidden="true" draggable="false"></div>
  <div id="right">
    <div id="servers">
      <div id="serverLive" class="card server"><img class="serverIcon" id="liveIcon" src="{{liveIcon}}"><div class="serverCopy"><div class="title" id="liveTitle"></div><div class="version" id="liveVersion"></div></div></div>
      <div id="serverTest" class="card server"><img class="serverIcon" id="testIcon" src="{{testIcon}}"><div class="serverCopy"><div class="title" id="testTitle"></div><div class="version" id="testVersion"></div></div></div>
    </div>
    <div id="modes">
      <div id="monitor" class="card icon-card"><img src="{{monitor}}"><button id="monitorProfile" class="profileArrow" aria-label="Monitor control profiles"></button></div>
      <div id="vr" class="card icon-card"><img src="{{vr}}"><button id="vrProfile" class="profileArrow" aria-label="VR control profiles"></button></div>
    </div>
    <div id="assistants">
      <div id="neck" class="card icon-card assist"><img src="{{neck}}"></div>
      <div id="keybind" class="card icon-card assist"><img src="{{keybind}}"></div>
      <div id="vtrim" class="card icon-card assist"><img src="{{trim}}"></div>
    </div>
    <div id="launch" class="card"><img id="launchImage" src="{{launchNormal}}"><div class="launchText" id="launchText"></div></div>
    <div id="links"><div class="link" id="discord"><img src="{{discordIcon}}"><span></span></div><div class="link" id="youtube"><img src="{{youtubeIcon}}"><span></span></div><div class="link" id="support"><img src="{{supportIcon}}"><span></span></div></div>
  </div>
</div>
<script>
const launchImages={normal:'{{launchNormal}}',update:'{{launchUpdate}}',running:'{{launchRunning}}'};
const serverImages={live:'{{liveIcon}}',liveActive:'{{liveActiveIcon}}',test:'{{testIcon}}',testActive:'{{testActiveIcon}}'};
const post=x=>window.chrome.webview.postMessage(x);
const postHeroBounds=()=>{
  const hero=document.getElementById('hero').getBoundingClientRect();
  const right=document.getElementById('right').getBoundingClientRect();
  post('hero-bounds:'+JSON.stringify([hero.left,hero.right,right.left,window.innerWidth]));
};
window.addEventListener('resize',()=>requestAnimationFrame(postHeroBounds));
requestAnimationFrame(postHeroBounds);
const clickMap={serverLive:'server-live',serverTest:'server-test',monitor:'monitor',vr:'vr',neck:'neck',keybind:'keybind',vtrim:'vtrim',launch:'launch',discord:'discord',youtube:'youtube',support:'support'};
for(const [id,msg] of Object.entries(clickMap)) {
  const el=document.getElementById(id); el.tabIndex=0; el.setAttribute('role','button');
  const invoke=()=>{ if(id==='launch' && el.classList.contains('disabled')) return; post(msg); };
  el.addEventListener('click',invoke);
  el.addEventListener('keydown',e=>{ if(e.key==='Enter'||e.key===' '){ e.preventDefault(); invoke(); } });
}
const setImage=(id,source)=>{ const el=document.getElementById(id); if(el.getAttribute('src')!==source) el.setAttribute('src',source); };
const fitLaunchText=()=>{
  const launch=document.getElementById('launch'), text=document.getElementById('launchText');
  if(!launch||!text) return;
  const h=launch.clientHeight, w=launch.clientWidth;
  if(h<=0||w<=0) return;
  let size=Math.max(18,Math.round(h*.44));
  text.style.fontSize=size+'px';
  // The artwork's stars and metal trim occupy both ends of the button.
  const maxWidth=Math.max(80,Math.round(w*.60));
  // Measure the glyphs, not the full-width flex container (whose scrollWidth
  // can never be less than the button width).
  const range=document.createRange(); range.selectNodeContents(text);
  while(size>14 && range.getBoundingClientRect().width>maxWidth){ size--; text.style.fontSize=size+'px'; }
};
window.addEventListener('resize',()=>requestAnimationFrame(fitLaunchText));
for(const [id,msg] of [['monitorProfile','profile-monitor'],['vrProfile','profile-vr']]) {
  const el=document.getElementById(id);
  const invoke=e=>{ e.preventDefault(); e.stopPropagation(); if(el.classList.contains('disabled')) return; post(msg); };
  el.addEventListener('click',invoke);
  el.addEventListener('mousedown',e=>e.stopPropagation());
  el.addEventListener('keydown',e=>{ if(e.key==='Enter'||e.key===' '){ invoke(e); } });
}
window.wtApplyState=s=>{
  const active=(id,v)=>{
    const el=document.getElementById(id), next=!!v;
    el.classList.toggle('active',next);
    // Selection changes do not animate or flash the raster artwork.
  };
  active('monitor',s.monitor); active('vr',s.vr); active('neck',s.neck); active('keybind',s.keybind); active('vtrim',s.vtrim);
  active('serverLive',s.server==='live'); active('serverTest',s.server==='test');
  setImage('liveIcon',s.server==='live'?serverImages.liveActive:serverImages.live);
  setImage('testIcon',s.server==='test'?serverImages.testActive:serverImages.test);
  document.getElementById('liveTitle').textContent=s.labels.live; document.getElementById('testTitle').textContent=s.labels.test;
  document.getElementById('liveVersion').textContent=s.liveVersion; document.getElementById('testVersion').textContent=s.testVersion;
  for(const [id,label] of [['monitor',s.labels.monitor],['vr',s.labels.vr]]) { const el=document.getElementById(id); el.removeAttribute('title'); el.setAttribute('aria-label',label); }
  for(const [id,label] of [['neck',s.labels.neck],['keybind',s.labels.keybind],['vtrim',s.labels.vtrim]]) { const el=document.getElementById(id); const a=label+' · '+(s[id]?s.labels.on:s.labels.off); el.removeAttribute('title'); el.setAttribute('aria-label',a); }
  document.getElementById('serverLive').setAttribute('aria-label',s.labels.live+' '+s.liveVersion);
  document.getElementById('serverTest').setAttribute('aria-label',s.labels.test+' '+s.testVersion);
  const profileState=s.profiles||{};
  for(const [id,enabled] of [['monitorProfile',profileState.monitorEnabled],['vrProfile',profileState.vrEnabled]]) {
    const el=document.getElementById(id);
    el.style.display=profileState.visible?'flex':'none';
    el.classList.toggle('disabled',!enabled);
    el.tabIndex=profileState.visible&&enabled?0:-1;
  }
  const launch=document.getElementById('launch'); launch.classList.toggle('disabled',s.running || !!s.busy || (!s.canLaunch && !s.update)); launch.classList.toggle('running',!!s.running);
  setImage('launchImage',s.running?launchImages.running:(s.update?launchImages.update:launchImages.normal));
  const launchLabel=s.running?s.labels.running:(s.update?s.labels.update:s.labels.launch); document.getElementById('launchText').textContent=launchLabel; launch.setAttribute('aria-label',launchLabel); requestAnimationFrame(fitLaunchText);
  for(const [id,label] of [['discord',s.labels.discord],['youtube',s.labels.youtube],['support',s.labels.support]]) { document.querySelector('#'+id+' span').textContent=label; document.getElementById(id).setAttribute('aria-label',label); }
};
</script>
</body>
</html>
""";
    }

    private static string PrepareWebAssetFolder()
    {
        string version = typeof(AviationHomePage).Assembly.GetName().Version?.ToString(3) ?? "current";
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WTVRSettingsAssistant",
            "WebUiCache",
            version,
            AppThemeAssets.ActiveTheme);
        Directory.CreateDirectory(root);

        var assets = new Dictionary<string, string[]>
        {
            ["logo.png"] = ["VRA.png"],
            ["monitor.png"] = ["Home_Monitor_New.png"],
            ["vr.png"] = ["Home_VR_New.png"],
            ["neck.png"] = ["IllustratedHead.png"],
            ["keybind.png"] = ["IllustratedKeys.png"],
            ["vtrim.png"] = ["Home_VTrim_New.png"],
            ["launch-normal.png"] = ["PlayButton.png", "Launch_Red_Frame.png"],
            ["launch-update.png"] = ["UpdateButton.png", "Launch_Update_Gold.png"],
            ["launch-running.png"] = ["GameStarted.png", "PlayButton.png"]
        };

        foreach ((string targetName, string[] candidates) in assets)
        {
            string target = Path.Combine(root, targetName);
            bool written = false;
            foreach (string sourceName in candidates)
            {
                try
                {
                    using Image image = AssetManager.LoadImage(sourceName);
                    image.Save(target, ImageFormat.Png);
                    written = true;
                    break;
                }
                catch { }
            }

            if (!written && !File.Exists(target))
            {
                using Bitmap placeholder = new(1, 1);
                placeholder.SetPixel(0, 0, Color.Transparent);
                placeholder.Save(target, ImageFormat.Png);
            }
        }

        void SaveDrawnIcon(string fileName, string iconName, Color color)
        {
            string target = Path.Combine(root, fileName);
            using Bitmap bitmap = new(160, 160, PixelFormat.Format32bppArgb);
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            IllustratedTheme.DrawIcon(graphics, iconName, new Rectangle(10, 10, 140, 140), color);
            bitmap.Save(target, ImageFormat.Png);
        }

        try
        {
            SaveDrawnIcon("live.png", "live", IllustratedTheme.Ivory);
            SaveDrawnIcon("live-active.png", "live", IllustratedTheme.Gold);
            SaveDrawnIcon("test.png", "test", IllustratedTheme.Ivory);
            SaveDrawnIcon("test-active.png", "test", IllustratedTheme.Gold);
            SaveDrawnIcon("discord.png", "discord", IllustratedTheme.Ivory);
            SaveDrawnIcon("youtube.png", "youtube", IllustratedTheme.Ivory);
            SaveDrawnIcon("support.png", "beer", IllustratedTheme.Ivory);
        }
        catch
        {
            // Cosmetic icons are non-critical; the card text remains usable.
        }

        // Web UI cache is app-owned. Keep only the current assembly-version tree;
        // user settings/profiles live elsewhere and are never touched here.
        try
        {
            string versionsRoot = Directory.GetParent(Directory.GetParent(root)!.FullName)!.FullName;
            foreach (string directory in Directory.EnumerateDirectories(versionsRoot))
            {
                if (Path.GetFileName(directory).Equals(version, StringComparison.OrdinalIgnoreCase)) continue;
                try { Directory.Delete(directory, recursive: true); } catch { }
            }
        }
        catch { }

        return root;
    }

    private static void SetCardText(AviationActionCard card, string text)
    {
        card.Text = text;
        card.AccessibleName = text;
        card.UpdateToolTip();
    }

    private static Image? LoadOptionalAsset(string fileName)
    {
        try { return AssetManager.LoadImage(fileName); }
        catch { return null; }
    }

    private void Arrange()
    {
        if (_arranging || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        _arranging = true;
        SuspendLayout();
        try
        {
            // Design-space layout. Every Home-screen element uses ONE scale factor,
            // so maximizing the app enlarges cards, icons, text and the hero together
            // instead of stretching only the empty space between them.
            const int designWidth = 1240;
            const int designHeight = 820;
            const int logoAreaWidth = 520;
            const int designGap = 18;
            const int rightWidth = designWidth - logoAreaWidth - designGap;

            float scale = Math.Min(ClientSize.Width / (float)designWidth, ClientSize.Height / (float)designHeight);
            scale = Math.Clamp(scale, 0.35f, 2.25f);
            int S(float value) => Math.Max(1, (int)Math.Round(value * scale));

            int canvasWidth = S(designWidth);
            int canvasHeight = S(designHeight);
            int horizontalPad = S(10);
            int centeredX = Math.Max(0, (ClientSize.Width - canvasWidth) / 2);
            int rightAnchoredX = Math.Max(0, ClientSize.Width - canvasWidth - horizontalPad);
            int canvasX = Math.Max(centeredX, rightAnchoredX);
            int canvasY = Math.Max(0, (ClientSize.Height - canvasHeight) / 2);
            int rightX = canvasX + S(logoAreaWidth + designGap);
            int w = S(rightWidth);
            int gap = S(10);

            bool showHeroArea = scale >= 0.58f;
            _logo.Visible = !_webReady && showHeroArea;
            int profileStripHeight = Math.Max(64, S(68));
            if (!_webReady && _logo.Visible)
            {
                // Keep the hero centered in the real open area to the left of the
                // Monitor/VR/server stack. In fullscreen the right stack is anchored
                // to the right, so the old fixed design-space logo cell drifted away
                // from the visual center of the left panel.
                int logoAreaLeft = canvasX;
                int logoAreaRight = Math.Max(logoAreaLeft + S(360), rightX - S(18));
                int logoAreaWidthActual = Math.Max(S(360), logoAreaRight - logoAreaLeft);
                int side = Math.Min(
                    Math.Min(S(560), canvasHeight - S(36)),
                    Math.Max(S(360), logoAreaWidthActual - S(42)));
                int logoX = logoAreaLeft + (logoAreaWidthActual - side) / 2;
                side = Math.Min(side, canvasHeight - profileStripHeight - S(20));
                int logoY = canvasY + (canvasHeight - profileStripHeight - side) / 2;
                _logo.SetBounds(logoX, logoY, side, side);
            }
            if (_aircraftProfiles is not null)
            {
                _aircraftProfiles.Visible = _showAircraftProfiles && showHeroArea;
                int heroLeft = 0, heroRight = rightX - S(8);
                int heroCenter = _logo.Left + _logo.Width / 2;
                if (_webReady)
                {
                    // The enhanced Home hero is a CSS grid track, not the native
                    // design-space logo cell. Match its actual 46% (42% on narrow
                    // windows) track so the WinForms profile strip is centered
                    // under the HTML logo at both window sizes.
                    double padding = Math.Clamp(ClientSize.Width * .0085, 8, 16);
                    double fraction = ClientSize.Width <= 1120 ? .42 : .46;
                    double minimum = ClientSize.Width <= 1120 ? 320 : 380;
                    double heroWidth = Math.Max(minimum, (ClientSize.Width - padding * 2) * fraction);
                    heroLeft = (int)Math.Round(padding);
                    heroRight = (int)Math.Round(padding + heroWidth);
                    heroCenter = (heroLeft + heroRight) / 2;
                    if (_webHeroMetrics is { Length: 4 } metrics && metrics[3] > 0)
                    {
                        double webScale = ClientSize.Width / metrics[3];
                        heroLeft = (int)Math.Round(metrics[0] * webScale);
                        heroRight = (int)Math.Round(Math.Min(metrics[1], metrics[2] - 8) * webScale);
                        heroCenter = (heroLeft + heroRight) / 2;
                    }
                }
                int stripWidth = Math.Min(S(520), Math.Max(180, heroRight - heroLeft - S(16)));
                int stripX = Math.Clamp(heroCenter - stripWidth / 2, heroLeft, Math.Max(heroLeft, heroRight - stripWidth));
                _aircraftProfiles.SetBounds(stripX, canvasY + canvasHeight - profileStripHeight, stripWidth, profileStripHeight);
            }

            // Home composition follows the supplied reference: Live/Test at the top,
            // large Monitor/VR cards underneath, assistants below, then the wide
            // launch/update action and community links at the bottom.
            _playMode.Visible = false;
            int serverY = canvasY + S(8);
            int serverHeight = S(80);
            int modeY = canvasY + S(102);
            int modeHeight = S(220);
            int launchHeight = Math.Max(S(130), _launch.PreferredHeightForWidth(w));
            int linksHeight = S(44);
            int bottomPad = S(10);
            int launchGap = S(14);
            int sectionGap = S(18);
            int linksY = canvasY + canvasHeight - bottomPad - linksHeight;
            int launchY = linksY - launchGap - launchHeight;
            int featureY = modeY + modeHeight + sectionGap;
            int featureHeight = Math.Max(S(210), launchY - featureY - sectionGap);

            _server.SetBounds(rightX, serverY, w, serverHeight);

            int half = (w - gap) / 2;
            _monitor.SetBounds(rightX, modeY, half, modeHeight);
            _vr.SetBounds(rightX + half + gap, modeY, w - half - gap, modeHeight);

            if (!_webReady && _desktopProfileSelector is not null && _vrProfileSelector is not null)
            {
                int selectorSize = Math.Clamp(modeHeight / 7, S(26), S(42));
                _desktopProfileSelector.SetBounds(
                    Math.Max(0, _monitor.ClientSize.Width - selectorSize - S(9)),
                    Math.Max(0, _monitor.ClientSize.Height - selectorSize - S(9)),
                    selectorSize, selectorSize);
                _vrProfileSelector.SetBounds(
                    Math.Max(0, _vr.ClientSize.Width - selectorSize - S(9)),
                    Math.Max(0, _vr.ClientSize.Height - selectorSize - S(9)),
                    selectorSize, selectorSize);
                _desktopProfileSelector.BringToFront();
                _vrProfileSelector.BringToFront();
            }

            int third = (w - gap * 2) / 3;
            _neck.SetBounds(rightX, featureY, third, featureHeight);
            _keys.SetBounds(rightX + third + gap, featureY, third, featureHeight);
            _trim.SetBounds(rightX + (third + gap) * 2, featureY, w - (third + gap) * 2, featureHeight);

            _launch.SetBounds(rightX, launchY, w, launchHeight);
            _links.SetBounds(rightX, linksY, w, linksHeight);
            foreach (Control control in _links.Controls)
            {
                control.Dock = DockStyle.Fill;
                control.Margin = new Padding(S(4), 0, S(4), 0);
            }

            if (_webReady)
            {
                SetNativeSurfaceVisible(false);
                _aircraftProfiles?.BringToFront();
            }

            AutoScroll = false;
            AutoScrollMinSize = Size.Empty;
        }
        finally
        {
            ResumeLayout(false);
            _arranging = false;
        }
    }

    private static void SetLabelPixelFont(Label label, int pixels, FontStyle style)
    {
        if (Math.Abs(label.Font.Size - pixels) < .1f && label.Font.Unit == GraphicsUnit.Pixel && label.Font.Style == style) return;
        ResponsiveFonts.Set(label, pixels, style, GraphicsUnit.Pixel);
    }

    private static Label MakeLabel(string text, int px, FontStyle style, ContentAlignment alignment) => new()
    {
        Text = text,
        Font = new Font("Segoe UI", px, style, GraphicsUnit.Pixel),
        ForeColor = IllustratedTheme.Ivory,
        BackColor = Color.Transparent,
        TextAlign = alignment,
        AutoSize = false
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _webFallbackLocked = true;
            _webReady = false;
            _webInitWatchdog?.Stop();
            _webInitWatchdog?.Dispose();
            _webInitWatchdog = null;
            _webView = null;
        }
        base.Dispose(disposing);
    }
}

internal sealed class AlphaPictureBox : PictureBox
{
    public AlphaPictureBox(Image image)
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        Image = image;
    }

    protected override void OnPaint(PaintEventArgs pe)
    {
        pe.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        pe.Graphics.SmoothingMode = SmoothingMode.HighQuality;
        pe.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        base.OnPaint(pe);
    }
}

internal class AviationActionCard : Control
{
    private readonly string _icon;
    private readonly Image? _image;
    private readonly Rectangle _sourceBounds;
    private readonly Action _click;
    private bool _active;
    private readonly ToolTip _toolTip = new() { InitialDelay = 350, ReshowDelay = 100, AutoPopDelay = 6000 };
    private string _onText = "On";
    private string _offText = "Off";

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action? ToggleClick { get; init; }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Active { get => _active; set { if (_active == value) return; _active = value; UpdateToolTip(); Invalidate(); } }

    public void SetStatusText(string onText, string offText)
    {
        _onText = string.IsNullOrWhiteSpace(onText) ? "On" : onText;
        _offText = string.IsNullOrWhiteSpace(offText) ? "Off" : offText;
        UpdateToolTip();
    }

    public void UpdateToolTip() => AccessibleName = Text + (Active ? " · " + _onText : " · " + _offText);

    public AviationActionCard(string text, string icon, Image? image, Action click)
    {
        Text = text;
        _icon = icon;
        _image = image;
        _sourceBounds = image == null ? Rectangle.Empty : FindVisibleSourceBounds(image);
        _click = click;
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
        BackColor = IllustratedTheme.Background;
        AccessibleName = text;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        TabStop = true;
        MouseUp += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            if (ToggleClick != null) ToggleClick();
            else _click();
        };
        KeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.Enter or Keys.Space)
            {
                (ToggleClick ?? _click)();
                e.Handled = true;
            }
        };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using (Brush panelFill = new SolidBrush(IllustratedTheme.Panel))
            e.Graphics.FillRectangle(panelFill, 8, 8, Math.Max(1, Width - 16), Math.Max(1, Height - 16));
        IllustratedTheme.DrawFrame(e.Graphics, new Rectangle(2, 2, Width - 5, Height - 5), Active);
        if (Active)
        {
            using Brush tint = new SolidBrush(Color.FromArgb(26, IllustratedTheme.Gold));
            e.Graphics.FillRectangle(tint, 9, 9, Width - 18, Height - 18);
        }

        // Icon-only Home controls; names remain available on hover and to screen readers.
        bool playModeCard = _icon is "monitor" or "visor";
        if (_image != null)
        {
            Rectangle imageBox = playModeCard
                ? new Rectangle(Width / 10, Height / 12, Width - Width / 5, Height - Height / 6)
                : _icon == "trim"
                    ? new Rectangle(Width / 14, Height / 18, Math.Max(1, Width - Width / 7), Math.Max(1, Height - Height / 9))
                : new Rectangle(6, 6, Math.Max(1, Width - 12), Math.Max(1, Height - 12));
            Rectangle drawBounds = FitImageBounds(_sourceBounds, imageBox);
            e.Graphics.DrawImage(_image, drawBounds, _sourceBounds, GraphicsUnit.Pixel);
        }
        else
        {
            int iconTop = playModeCard ? Height / 14 : Math.Max(6, Height / 22);
            int iconBottom = playModeCard ? Height - Height / 12 : Height - Math.Max(6, Height / 24);
            int maxIcon = playModeCard ? (int)(Height * .80) : iconBottom - iconTop;
            int iconSize = Math.Min(maxIcon, Math.Min(Width - 12, Math.Max(32, iconBottom - iconTop)));
            Rectangle iconBounds = new(Width / 2 - iconSize / 2, iconTop, iconSize, iconSize);
            IllustratedTheme.DrawIcon(e.Graphics, _icon, iconBounds, Active ? IllustratedTheme.Gold : IllustratedTheme.Ivory);
        }

        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(5, 5, Width - 11, Height - 11));
    }

    protected override void Dispose(bool disposing) { if (disposing) _toolTip.Dispose(); base.Dispose(disposing); }

    private static int FitFontSize(Graphics graphics, string text, int width, int preferred, int minimum)
    {
        for (int size = preferred; size > minimum; size--)
        {
            using Font candidate = new("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Pixel);
            if (TextRenderer.MeasureText(graphics, text, candidate, Size.Empty, TextFormatFlags.NoPadding).Width <= width)
                return size;
        }
        return minimum;
    }

    private static Rectangle FitImageBounds(Rectangle source, Rectangle target)
    {
        if (source.Width <= 0 || source.Height <= 0 || target.Width <= 0 || target.Height <= 0)
            return target;

        float scale = Math.Min(target.Width / (float)source.Width, target.Height / (float)source.Height);
        int width = Math.Max(1, (int)Math.Round(source.Width * scale));
        int height = Math.Max(1, (int)Math.Round(source.Height * scale));
        return new Rectangle(
            target.X + (target.Width - width) / 2,
            target.Y + (target.Height - height) / 2,
            width,
            height);
    }

    private static Rectangle FindVisibleSourceBounds(Image image)
    {
        Rectangle full = new(0, 0, image.Width, image.Height);
        if (image.Width <= 0 || image.Height <= 0) return full;

        Bitmap? createdBitmap = image as Bitmap is null ? new Bitmap(image) : null;
        Bitmap bitmap = image as Bitmap ?? createdBitmap!;
        int left = bitmap.Width;
        int top = bitmap.Height;
        int right = -1;
        int bottom = -1;

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).A <= 8) continue;
                if (x < left) left = x;
                if (x > right) right = x;
                if (y < top) top = y;
                if (y > bottom) bottom = y;
            }
        }

        try
        {
            if (right < left || bottom < top) return full;

            int padX = Math.Max(1, (right - left + 1) / 40);
            int padY = Math.Max(1, (bottom - top + 1) / 40);
            left = Math.Max(0, left - padX);
            top = Math.Max(0, top - padY);
            right = Math.Min(bitmap.Width - 1, right + padX);
            bottom = Math.Min(bitmap.Height - 1, bottom + padY);
            return new Rectangle(left, top, right - left + 1, bottom - top + 1);
        }
        finally
        {
            createdBitmap?.Dispose();
        }
    }
}

internal sealed class AviationServerSelector : Control
{
    private readonly Action<GameServerChannel> _changed;
    private GameServerChannel _selected;
    private string _liveVersion = "version ...";
    private string _testVersion = "version ...";
    private string _liveTitle = "LIVE SERVER";
    private string _testTitle = "TEST SERVER";
    private string _versionUnknown = "version unknown";

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public GameServerChannel Selected { get => _selected; set { if (_selected == value) return; _selected = value; Invalidate(); } }

    public AviationServerSelector(Action<GameServerChannel> changed, Action refresh)
    {
        _changed = changed;
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
        BackColor = IllustratedTheme.Background;
        AccessibleName = "War Thunder server selector";
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        TabStop = true;
        MouseUp += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            if (LeftCardBounds().Contains(e.Location)) _changed(GameServerChannel.Live);
            else if (RightCardBounds().Contains(e.Location)) _changed(GameServerChannel.Test);
        };
        KeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.Left or Keys.Right)
            {
                _changed(e.KeyCode == Keys.Left ? GameServerChannel.Live : GameServerChannel.Test);
                e.Handled = true;
            }
        };
    }

    public void SetSignals(ServerSignal live, ServerSignal test) { }

    public void SetVersions(Version? live, Version? test)
    {
        _liveVersion = live is null ? _versionUnknown : $"v {live}";
        _testVersion = test is null ? _versionUnknown : $"v {test}";
        Invalidate();
    }

    public void SetLanguage(string languageCode)
    {
        _liveTitle = AppText.T(languageCode, "Home.LiveServer");
        _testTitle = AppText.T(languageCode, "Home.TestServer");
        _versionUnknown = AppText.T(languageCode, "Home.VersionChecking");
        if (!_liveVersion.StartsWith("v ", StringComparison.OrdinalIgnoreCase)) _liveVersion = _versionUnknown;
        if (!_testVersion.StartsWith("v ", StringComparison.OrdinalIgnoreCase)) _testVersion = _versionUnknown;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (Width < 20 || Height < 20) return;
        DrawCard(e.Graphics, LeftCardBounds(), _liveTitle, _liveVersion, "live", Selected == GameServerChannel.Live);
        DrawCard(e.Graphics, RightCardBounds(), _testTitle, _testVersion, "test", Selected == GameServerChannel.Test);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(2, 2, Width - 5, Height - 5));
    }

    private Rectangle LeftCardBounds()
    {
        int gap = Math.Clamp(Height / 9, 8, 16);
        int w = Math.Max(1, (Width - gap) / 2);
        return new Rectangle(1, 1, w - 1, Math.Max(1, Height - 2));
    }

    private Rectangle RightCardBounds()
    {
        int gap = Math.Clamp(Height / 9, 8, 16);
        int leftWidth = Math.Max(1, (Width - gap) / 2);
        return new Rectangle(leftWidth + gap, 1, Math.Max(1, Width - leftWidth - gap - 1), Math.Max(1, Height - 2));
    }

    private static void DrawCard(Graphics g, Rectangle r, string titleText, string versionText, string icon, bool active)
    {
        using (Brush panelFill = new SolidBrush(IllustratedTheme.Panel))
            g.FillRectangle(panelFill, Rectangle.Inflate(r, -8, -8));
        IllustratedTheme.DrawFrame(g, Rectangle.Inflate(r, -2, -2), active);
        if (active)
        {
            using Brush b = new SolidBrush(Color.FromArgb(35, IllustratedTheme.Gold));
            g.FillRectangle(b, Rectangle.Inflate(r, -9, -9));
        }

        int iconSize = Math.Max(20, (int)Math.Round(r.Height * 0.64));
        int iconInset = Math.Max(6, (int)Math.Round(r.Height * 0.18));
        Rectangle iconBounds = new(r.X + iconInset, r.Y + (r.Height - iconSize) / 2, iconSize, iconSize);
        IllustratedTheme.DrawIcon(g, icon, iconBounds, active ? IllustratedTheme.Gold : IllustratedTheme.Ivory);

        int textX = iconBounds.Right + 10;
        int textW = Math.Max(40, r.Right - textX - 12);
        int titleSize = Math.Max(9, (int)Math.Round(r.Height * 0.22));
        int versionSize = Math.Max(12, (int)Math.Round(r.Height * 0.18));
        while (titleSize > 14)
        {
            using Font test = new("Segoe UI", titleSize, FontStyle.Bold, GraphicsUnit.Pixel);
            if (TextRenderer.MeasureText(g, titleText, test, Size.Empty, TextFormatFlags.NoPadding).Width <= textW) break;
            titleSize--;
        }
        while (versionSize > 12)
        {
            using Font test = new("Segoe UI", versionSize, FontStyle.Bold, GraphicsUnit.Pixel);
            if (TextRenderer.MeasureText(g, versionText, test, Size.Empty, TextFormatFlags.NoPadding).Width <= textW) break;
            versionSize--;
        }
        using Font title = new("Segoe UI", titleSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using Font version = new("Segoe UI", versionSize, FontStyle.Bold, GraphicsUnit.Pixel);
        Rectangle titleRect = new(textX, r.Y + 15, textW, Math.Max(26, r.Height / 2));
        Rectangle versionRect = new(textX, r.Bottom - Math.Max(38, r.Height / 3), textW, Math.Max(30, r.Height / 4));
        TextRenderer.DrawText(g, titleText, title, titleRect, active ? IllustratedTheme.Gold : IllustratedTheme.Ivory,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        TextRenderer.DrawText(g, versionText, version, versionRect, active ? IllustratedTheme.Gold : IllustratedTheme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}

internal sealed class AviationLaunchButton : Control
{
    private readonly Action _click;
    private readonly Image? _launchFrame;
    private readonly Image? _updateFrame;
    private readonly Image? _runningFrame;
    private bool _canLaunch;
    private bool _updateAvailable;
    private bool _gameRunning;
    private string _launchLabel = "LAUNCH";
    private string _updateLabel = "UPDATE";
    private string _runningLabel = "RUNNING";

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool CanLaunch
    {
        get => _canLaunch;
        set
        {
            if (_canLaunch == value) return;
            _canLaunch = value;
            Cursor = value ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool UpdateAvailable
    {
        get => _updateAvailable;
        set
        {
            if (_updateAvailable == value) return;
            _updateAvailable = value;
            Invalidate();
        }
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool GameRunning
    {
        get => _gameRunning;
        set
        {
            if (_gameRunning == value) return;
            _gameRunning = value;
            Cursor = value ? Cursors.Default : Cursor;
            Invalidate();
        }
    }

    public void SetLabels(string launch, string update, string running)
    {
        _launchLabel = string.IsNullOrWhiteSpace(launch) ? "LAUNCH" : launch;
        _updateLabel = string.IsNullOrWhiteSpace(update) ? "UPDATE" : update;
        _runningLabel = string.IsNullOrWhiteSpace(running) ? "RUNNING" : running;
        Text = _launchLabel;
        Invalidate();
    }

    public AviationLaunchButton(Action click, Image? launchFrame, Image? updateFrame, Image? runningFrame)
    {
        _click = click;
        _launchFrame = launchFrame;
        _updateFrame = updateFrame;
        _runningFrame = runningFrame;
        DoubleBuffered = true;
        BackColor = IllustratedTheme.Background;
        AccessibleName = "Launch War Thunder";
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        TabStop = true;
        MouseUp += (_, e) => { if (e.Button == MouseButtons.Left && CanLaunch && !GameRunning) _click(); };
        KeyDown += (_, e) =>
        {
            if (CanLaunch && !GameRunning && e.KeyCode is (Keys.Enter or Keys.Space))
            {
                _click();
                e.Handled = true;
            }
        };
    }

    public int PreferredHeightForWidth(int width)
    {
        Image? frame = GameRunning ? _runningFrame : UpdateAvailable ? _updateFrame : _launchFrame;
        if (frame == null || frame.Width <= 0 || frame.Height <= 0 || width <= 0) return 0;
        return Math.Max(1, (int)Math.Round(width * (frame.Height / (double)frame.Width)));
    }

    private static Rectangle FitFrame(Image frame, Rectangle bounds)
    {
        if (frame.Width <= 0 || frame.Height <= 0 || bounds.Width <= 0 || bounds.Height <= 0) return bounds;
        double scale = Math.Min(bounds.Width / (double)frame.Width, bounds.Height / (double)frame.Height);
        int width = Math.Max(1, (int)Math.Round(frame.Width * scale));
        int height = Math.Max(1, (int)Math.Round(frame.Height * scale));
        return new Rectangle(bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2, width, height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        bool running = GameRunning;
        bool update = !running && UpdateAvailable;
        Color color = running ? Color.FromArgb(200, 195, 184)
            : CanLaunch || update ? Color.FromArgb(239, 226, 196) : IllustratedTheme.Muted;
        Rectangle launchBounds = new(2, 2, Width - 5, Height - 5);
        if (launchBounds.Width <= 0 || launchBounds.Height <= 0) return;

        Image? frame = running ? _runningFrame : update ? _updateFrame : _launchFrame;
        if (frame != null)
        {
            e.Graphics.DrawImage(frame, FitFrame(frame, launchBounds));
        }
        else
        {
            using (var shape = new GraphicsPath())
            {
                int c = 10;
                shape.AddPolygon(new Point[] { new(c+2,2), new(Width-c-3,2), new(Width-3,c+2),
                    new(Width-3,Height-c-3), new(Width-c-3,Height-3), new(c+2,Height-3), new(2,Height-c-3), new(2,c+2) });
                using var fill = new LinearGradientBrush(launchBounds, update ? Color.FromArgb(204, 170, 0) : CanLaunch ? Color.FromArgb(172, 33, 39) : IllustratedTheme.Panel,
                    update ? Color.FromArgb(126, 98, 0) : CanLaunch ? Color.FromArgb(91, 15, 24) : IllustratedTheme.Background, 90f);
                using var edge = new Pen(update ? IllustratedTheme.Gold : CanLaunch ? Color.FromArgb(225, 76, 65) : IllustratedTheme.Muted, 2);
                e.Graphics.FillPath(fill, shape); e.Graphics.DrawPath(edge, shape);
                using var glint = new Pen(Color.FromArgb(100, 255, 219, 188), 1);
                e.Graphics.DrawLine(glint, 18, 6, Width - 18, 6);
            }
        }
        int preferred = Math.Max(18, (int)Math.Round(Height * 0.44));
        int labelGuard = Math.Max(12, (int)Math.Round(Width * 0.20));
        int available = Math.Max(80, Width - labelGuard * 2);
        string label = running ? _runningLabel : update ? _updateLabel : _launchLabel;
        while (preferred > 10)
        {
            using Font candidate = new("Segoe UI", preferred, FontStyle.Bold, GraphicsUnit.Pixel);
            if (TextRenderer.MeasureText(e.Graphics, label, candidate, Size.Empty, TextFormatFlags.NoPadding).Width <= available) break;
            preferred--;
        }
        using Font f = new("Segoe UI", preferred, FontStyle.Bold, GraphicsUnit.Pixel);
        TextRenderer.DrawText(e.Graphics, label, f, new Rectangle(labelGuard, 5, Math.Max(1, Width - labelGuard * 2), Height - 10), color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(5, 5, Width - 11, Height - 11));
    }
}

internal sealed class AviationLinkButton : Control
{
    private readonly string _icon;
    private readonly Action _click;
    private bool _hover;

    public AviationLinkButton(string text, string icon, Action click)
    {
        Text = text;
        _icon = icon;
        _click = click;
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
        BackColor = IllustratedTheme.Background;
        AccessibleName = text;
        TabStop = true;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        MouseEnter += (_, _) => { _hover = true; Invalidate(); };
        MouseLeave += (_, _) => { _hover = false; Invalidate(); };
        MouseUp += (_, e) => { if (e.Button == MouseButtons.Left) _click(); };
        KeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.Enter or Keys.Space)
            {
                _click();
                e.Handled = true;
            }
        };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        bool beer = _icon == "beer";
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        Color fillColor = beer ? Color.FromArgb(154, 113, 6) : IllustratedTheme.Panel;
        using (Brush panelFill = new LinearGradientBrush(ClientRectangle, beer ? Color.FromArgb(228, 177, 34) : fillColor, fillColor, 90f))
            e.Graphics.FillRectangle(panelFill, 6, 6, Math.Max(1, Width - 12), Math.Max(1, Height - 12));
        IllustratedTheme.DrawFrame(e.Graphics, new Rectangle(1, 1, Width - 3, Height - 3), beer || _hover || Focused);
        Color color = beer ? Color.FromArgb(255, 250, 232) : _hover || Focused ? IllustratedTheme.Gold : IllustratedTheme.Ivory;
        int iconSize = Math.Clamp(Height - 18, 22, 34);
        int textSize = Math.Clamp(Height / 3, 11, 18);
        int gap = beer ? 7 : 9;
        int textBudget = Math.Max(24, Width - iconSize - gap - 24);
        while (textSize > 9)
        {
            using Font test = new("Segoe UI", textSize, FontStyle.Bold, GraphicsUnit.Pixel);
            if (TextRenderer.MeasureText(Text, test, Size.Empty, TextFormatFlags.NoPadding).Width <= textBudget) break;
            textSize--;
        }
        using Font font = new("Segoe UI", textSize, FontStyle.Bold, GraphicsUnit.Pixel);
        int measuredTextWidth = TextRenderer.MeasureText(Text, font, Size.Empty, TextFormatFlags.NoPadding).Width + 4;
        int textWidth = Math.Min(textBudget, measuredTextWidth);
        int total = iconSize + gap + textWidth;
        int startX = Math.Max(8, (Width - total) / 2);
        IllustratedTheme.DrawIcon(e.Graphics, _icon, new Rectangle(startX, (Height - iconSize) / 2, iconSize, iconSize), color);
        TextRenderer.DrawText(e.Graphics, Text, font, new Rectangle(startX + iconSize + gap, 0, Math.Max(1, Width - startX - iconSize - gap - 8), Height),
            color, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(4, 4, Width - 9, Height - 9));
    }
}
