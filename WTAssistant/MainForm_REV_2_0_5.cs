using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace WTVRSettingsAssistant;

public partial class MainForm
{
    private readonly CancellationTokenSource _revisionCancellation = new();
    private readonly GameServices _gameServices = new();
    private readonly System.Windows.Forms.Timer _trimStateTimer = new() { Interval = 350 };
    private HOTASTrimUtility.Form1? _vtrimForm;
    private bool _vtrimNavigationExpanded;
    private Control? _aircraftHomeFilter;
    private void LayoutAircraftHomeFilter()
    {
        _aviationHome?.SetAircraftProfilesVisible(_showHomeAircraft);
        LayoutControlProfileFooter();
        if (_aircraftHomeFilter is null) return;
        int S(float n) => Math.Max(1, (int)Math.Round(n * ThemeChromeScale));
        _aircraftHomeFilter.Visible = _showHomeAircraft && _mainPanel.Visible && _vtrimForm?.Visible != true &&
            _neckAssistForm?.Visible != true && _hiddenKeybindsForm?.Visible != true;
        _aircraftHomeFilter.SetBounds(Padding.Left + S(150), ClientSize.Height - S(51), S(240), S(38));
        _aircraftHomeFilter.BringToFront();
    }
    private GameInstallations _installations = new();
    private bool _launchBusy, _versionStatusBusy, _versionRefreshPending;
    private DateTime _gameLaunchPendingUntilUtc = DateTime.MinValue;
    private bool GameLaunchPending => DateTime.UtcNow < _gameLaunchPendingUntilUtc;
    private bool _selectedGameUpdateAvailable;
    private string _gameVersionStatusText = AppText.T("en", "Game.Status.Checking");
    private Color _gameVersionStatusColor = Color.FromArgb(236, 185, 71);
    private static string InstallationsFile => Path.Combine(SettingsFolder, "game_installations.json");

    private static void SetOwnedFont(Control control, float pixels, FontStyle style)
    {
        if (Math.Abs(control.Font.Size - pixels) < .05f && control.Font.Unit == GraphicsUnit.Pixel && control.Font.Style == style) return;
        ResponsiveFonts.Set(control, pixels, style, GraphicsUnit.Pixel);
    }

    private void InitializeRevisionServices()
    {
        EnsureControlProfileFooter();
        _mainPanel.VisibleChanged += (_, _) => LayoutControlProfileFooter();
        _trimStateTimer.Tick += (_, _) => _aviationHome?.SetVTrimState(_vtrimForm?.OutputEnabled == true);
        Shown += async (_, _) =>
        {
            UpdateAspectMaximizedBounds();
            Rectangle area = Screen.FromControl(this).WorkingArea;
            MinimumSize = new Size(Math.Min(MinimumSize.Width, area.Width), Math.Min(MinimumSize.Height, area.Height));
            if (!_fullScreen && WindowState == FormWindowState.Normal)
            {
                Size targetClient = FitClientSizeToLockedAspect(ClientSize, area);
                if (ClientSize != targetClient) ClientSize = targetClient;

                int x = Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - Width));
                int y = Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - Height));
                Location = new Point(x, y);
            }

            LayoutThemePages();
            try { EnsureVTrim(); }
            catch (Exception ex) { WriteRevisionLog("VTrim startup", ex); }
            _trimStateTimer.Start();
            await RefreshGameVersionStatusAsync();
        };
        DpiChanged += (_, _) =>
        {
            UpdateAspectMaximizedBounds();
            if (_fullScreen) Bounds = Screen.FromControl(this).Bounds;
            else if (WindowState == FormWindowState.Normal)
                ClientSize = FitClientSizeToLockedAspect(ClientSize, Screen.FromControl(this).WorkingArea);
            LayoutThemePages();
            Invalidate(true);
        };
        LocationChanged += (_, _) =>
        {
            if (WindowState == FormWindowState.Normal) UpdateAspectMaximizedBounds();
        };
        FormClosed += (_, _) =>
        {
            _revisionCancellation.Cancel();
            _trimStateTimer.Dispose();
            _vtrimForm?.Dispose();
            _gameServices.Dispose();
        };
    }

    private async Task RefreshGameVersionStatusAsync()
    {
        if (IsDisposed || _revisionCancellation.IsCancellationRequested) return;
        if (_versionStatusBusy)
        {
            // Do not lose a branch/profile refresh just because another version
            // request is still finishing. Run one fresh pass immediately after it.
            _versionRefreshPending = true;
            return;
        }

        do
        {
            _versionRefreshPending = false;
            _versionStatusBusy = true;
            try
            {
                await RefreshGameVersionStatusCoreAsync();
            }
            finally
            {
                _versionStatusBusy = false;
            }
        }
        while (_versionRefreshPending && !IsDisposed && !_revisionCancellation.IsCancellationRequested);
    }

    private async Task RefreshGameVersionStatusCoreAsync()
    {
        try
        {
            if (!TryGetCurrentGameRoot(out string root))
            {
                _selectedGameUpdateAvailable = false;
                _aviationHome?.SetGameUpdateAvailable(false);
                SetGameVersionStatus(T("Game.Status.SelectFolder"), Color.FromArgb(236, 185, 71));
                return;
            }

            // Capture the branch used for this request. If the user changes
            // LIVE/TEST while the network request is running, never apply the
            // old response to the newly selected branch.
            GameServerChannel channel = SelectedGameServerChannel;
            VersionCheck check = await _gameServices.CheckVersionAsync(root, channel, _revisionCancellation.Token);
            if (channel != SelectedGameServerChannel)
            {
                _versionRefreshPending = true;
                return;
            }
            ApplyVersionCheckToStatus(check, channel);

            try
            {
                // Use the same effective version logic for the Home cards that is
                // used for the selected branch status. This prevents the top Live/Test
                // cards from disagreeing with the footer when Gaijin's public version
                // endpoint briefly lags behind a build the official launcher has
                // already validated on that circuit.
                Task<VersionCheck> liveTask = _gameServices.CheckVersionAsync(root, GameServerChannel.Live, _revisionCancellation.Token);
                Task<VersionCheck> testTask = _gameServices.CheckVersionAsync(root, GameServerChannel.Test, _revisionCancellation.Token);
                await Task.WhenAll(liveTask, testTask);
                SetHomeServerVersions(liveTask.Result.Latest, testTask.Result.Latest);
            }
            catch { SetHomeServerVersions(null, null); }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _selectedGameUpdateAvailable = false;
            _aviationHome?.SetGameUpdateAvailable(false);
            SetGameVersionStatus(T("Game.Status.Unavailable"), Color.FromArgb(236, 185, 71));
            WriteRevisionLog("Version status", ex);
        }
    }

    private void ApplyVersionCheckToStatus(VersionCheck check, GameServerChannel channel)
    {
        if (IsDisposed || channel != SelectedGameServerChannel) return;
        _selectedGameUpdateAvailable = check.UpdateAvailable;
        _aviationHome?.SetGameUpdateAvailable(_selectedGameUpdateAvailable);
        string name = channel == GameServerChannel.Test ? T("Home.TestServer") : T("Home.LiveServer");
        if (check.Installed == null)
            SetGameVersionStatus(TF("Game.Status.UnknownVersion", name), Color.FromArgb(236, 185, 71));
        else if (check.Latest == null)
            SetGameVersionStatus(TF("Game.Status.UnknownUpdate", name, check.Installed), Color.FromArgb(236, 185, 71));
        else if (check.Current)
            SetGameVersionStatus(TF("Game.Status.UpToDate", name, check.Installed), Color.FromArgb(68, 215, 117));
        else
            SetGameVersionStatus(TF("Game.Status.Required", name, check.Installed, check.Latest), Color.FromArgb(240, 92, 92));
    }

    private void SetGameVersionStatus(string text, Color color)
    {
        _gameVersionStatusText = text;
        _gameVersionStatusColor = color;
        if (!IsDisposed)
            Invalidate(new Rectangle(0, Math.Max(0, ClientSize.Height - 90), ClientSize.Width, Math.Min(90, ClientSize.Height)));
    }

    private async void RefreshServerSignalsFromUi()
    {
        try { await RefreshGameVersionStatusAsync(); }
        catch (OperationCanceledException) { }
    }

    // -------------------- VTrim assistant --------------------

    private void EnsureVTrim()
    {
        if (_vtrimForm is { IsDisposed: false }) return;

        HOTASTrimUtility.Form1.SetHostVisualTheme(_uiTheme);
        _vtrimForm = new HOTASTrimUtility.Form1(Path.Combine(SettingsFolder, "VTrim"), true)
        {
            TopLevel = false,
            FormBorderStyle = FormBorderStyle.None,
            ShowInTaskbar = false,
            Icon = this.Icon,
            MinimumSize = Size.Empty,
            Bounds = ThemeContentBounds
        };

        _vtrimForm.ApplyLanguage(_languageCode);
        _vtrimForm.AircraftProfileEditorRequested += profileName =>
        {
            OpenVTrim();
            _vtrimForm?.OpenAircraftProfileEditor(profileName);
        };

        Controls.Add(_vtrimForm);
        _vtrimForm.Show(); // Restores the saved output preference even while Home is visible.
        _vtrimForm.Hide();
        var aircraftBrowser = _vtrimForm.CreateAircraftProfileBrowser(true);
        _aviationHome?.SetAircraftProfiles(aircraftBrowser);
        _aircraftHomeFilter = aircraftBrowser.Tag as Control;
        if (_aircraftHomeFilter is not null) Controls.Add(_aircraftHomeFilter);
        _mainPanel.VisibleChanged += (_, _) => LayoutAircraftHomeFilter();
        _vtrimForm.VisibleChanged += (_, _) => LayoutAircraftHomeFilter();
        LayoutAircraftHomeFilter();
    }

    private void OpenVTrim()
    {
        try
        {
            EnsureVTrim();
            _neckAssistForm?.Hide();
            _hiddenKeybindsForm?.Hide();
            _vtrimForm!.Bounds = ThemeContentBounds;
            _vtrimForm.Show();
            _vtrimForm.BringToFront();
            _themeNavigation?.BringToFront();
            SelectThemePage("VTrim Assistant");
        }
        catch (Exception ex)
        {
            WriteRevisionLog("VTrim open", ex);
            MessageBox.Show(this, ex.Message, "VTrim", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void ToggleVTrimFromHome()
    {
        try
        {
            EnsureVTrim();
            bool enable = !_vtrimForm!.OutputEnabled;
            if (!(enable ? await _vtrimForm.SetupAndConnectOutputAsync() : _vtrimForm.SetOutputEnabled(false)))
            {
                OpenVTrim();
            }
            _aviationHome?.SetVTrimState(_vtrimForm.OutputEnabled);
        }
        catch (Exception ex)
        {
            WriteRevisionLog("VTrim toggle", ex);
            MessageBox.Show(this, ex.Message, "VTrim", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // -------------------- War Thunder install / circuit / updater --------------------

    private void LoadGameInstallations()
    {
        try
        {
            _installations = GameInstallations.Load(InstallationsFile);

            string root = string.Empty;
            if (File.Exists(_configBlkPath) && File.Exists(_warThunderExePath))
                root = GameInstallations.CanonicalRoot(Path.GetDirectoryName(_configBlkPath)!);
            else if (Directory.Exists(_installations.LiveRoot) && File.Exists(Path.Combine(_installations.LiveRoot, "config.blk")))
                root = GameInstallations.CanonicalRoot(_installations.LiveRoot);
            else if (Directory.Exists(_installations.TestRoot) && File.Exists(Path.Combine(_installations.TestRoot, "config.blk")))
                root = GameInstallations.CanonicalRoot(_installations.TestRoot);
            else if (TryAutoDetectWarThunderRoot(out string detected))
                root = detected;

            if (root.Length == 0) return;
            root = ValidateGameFolder(root, GameServerChannel.Live);
            _configBlkPath = Path.Combine(root, "config.blk");
            _warThunderExePath = Path.Combine(root, "launcher.exe");
            _useTestServer = WarThunderServerConfig.Detect(File.ReadAllText(_configBlkPath)) == GameServerChannel.Test;
            _installations.SetBoth(root);
            SaveGameInstallations();
            SaveState();
        }
        catch (Exception ex) { WriteRevisionLog("Installation settings", ex); }
    }

    private static bool TryAutoDetectWarThunderRoot(out string root)
    {
        root = string.Empty;
        var candidates = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddCandidate(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                string full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')));
                if (seen.Add(full)) candidates.Add(full);
            }
            catch { }
        }

        try
        {
            using RegistryKey? steam = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            string? steamPath = steam?.GetValue("SteamPath") as string;
            if (!string.IsNullOrWhiteSpace(steamPath))
            {
                AddCandidate(Path.Combine(steamPath, "steamapps", "common", "War Thunder"));
                string libraries = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (File.Exists(libraries) && new FileInfo(libraries).Length < 2 * 1024 * 1024)
                {
                    string vdf = File.ReadAllText(libraries);
                    foreach (Match match in Regex.Matches(vdf, @"""path""\s+""(?<p>[^""]+)""", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(150)))
                    {
                        string library = match.Groups["p"].Value.Replace("\\\\", "\\");
                        AddCandidate(Path.Combine(library, "steamapps", "common", "War Thunder"));
                    }
                }
            }
        }
        catch { }

        string systemDrive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "C:\\";
        AddCandidate(Path.Combine(systemDrive, "WarThunder"));
        AddCandidate(Path.Combine(systemDrive, "Games", "WarThunder"));
        AddCandidate(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "WarThunder"));
        AddCandidate(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WarThunder"));

        foreach (string candidate in candidates)
        {
            try
            {
                if (!File.Exists(Path.Combine(candidate, "config.blk")) || !File.Exists(Path.Combine(candidate, "launcher.exe"))) continue;
                root = GameInstallations.CanonicalRoot(candidate);
                return true;
            }
            catch { }
        }
        return false;
    }

    private void SaveGameInstallations() =>
        AtomicFile.WriteTextWithBackup(InstallationsFile, JsonSerializer.Serialize(_installations, new JsonSerializerOptions { WriteIndented = true }));

    // Channel is retained in the signature for old call sites, but 1.8.4 deliberately
    // uses ONE War Thunder installation and switches only the yunetwork{} block.
    private string ValidateGameFolder(string path, GameServerChannel channel)
    {
        string root = GameInstallations.CanonicalRoot(path);
        if (!File.Exists(Path.Combine(root, "config.blk")) || !File.Exists(Path.Combine(root, "launcher.exe")))
            throw new IOException("Choose the War Thunder root containing config.blk and launcher.exe.");
        return root;
    }

    private bool ChooseGameFolder(GameServerChannel channel)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the main War Thunder installation. Live and Test use this same folder.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        if (TryGetCurrentGameRoot(out string current)) dialog.SelectedPath = current;
        if (dialog.ShowDialog(this) != DialogResult.OK) return false;
        try
        {
            string root = ValidateGameFolder(dialog.SelectedPath, channel);
            _installations.SetBoth(root);
            _configBlkPath = Path.Combine(root, "config.blk");
            _warThunderExePath = Path.Combine(root, "launcher.exe");
            SaveGameInstallations();
            SaveState();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, T("Profiles.FolderTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    private bool TryGetCurrentGameRoot(out string root)
    {
        root = string.Empty;
        try
        {
            if (File.Exists(_configBlkPath) && File.Exists(_warThunderExePath))
            {
                root = ValidateGameFolder(Path.GetDirectoryName(_configBlkPath)!, SelectedGameServerChannel);
                return true;
            }
            string saved = !string.IsNullOrWhiteSpace(_installations.LiveRoot) ? _installations.LiveRoot : _installations.TestRoot;
            if (!string.IsNullOrWhiteSpace(saved) && Directory.Exists(saved))
            {
                root = ValidateGameFolder(saved, SelectedGameServerChannel);
                _configBlkPath = Path.Combine(root, "config.blk");
                _warThunderExePath = Path.Combine(root, "launcher.exe");
                return true;
            }
            if (TryAutoDetectWarThunderRoot(out string detected))
            {
                root = ValidateGameFolder(detected, SelectedGameServerChannel);
                _configBlkPath = Path.Combine(root, "config.blk");
                _warThunderExePath = Path.Combine(root, "launcher.exe");
                _installations.SetBoth(root);
                SaveGameInstallations();
                SaveState();
                return true;
            }
        }
        catch { }
        return false;
    }

    private static string DevServerWorkspace(string root) => Path.Combine(root, "DevServer");

    private static void PrepareDevServerWorkspace(string root, string configPath, bool enteringTest)
    {
        string workspace = DevServerWorkspace(root);
        Directory.CreateDirectory(workspace);
        string liveBackup = Path.Combine(workspace, "config.live.backup.blk");
        if (enteringTest && !File.Exists(liveBackup)) File.Copy(configPath, liveBackup, false);
        string snapshot = Path.Combine(workspace, enteringTest ? "config.test.blk" : "config.last_live.blk");
        File.Copy(configPath, snapshot, true);
    }

    private async Task HandleServerSwitchAsync(GameServerChannel previous, GameServerChannel channel)
    {
        if (previous == channel || IsDisposed || _revisionCancellation.IsCancellationRequested) return;

        // Changing the circuit is a config selection. The installed and latest
        // versions decide whether the official updater is needed.
        _selectedGameUpdateAvailable = false;
        _aviationHome?.SetGameUpdateAvailable(false);
        UpdateVisualStates();

        try
        {
            if (!TryGetCurrentGameRoot(out string root)) return;
            VersionCheck check = await _gameServices.CheckVersionAsync(root, channel, _revisionCancellation.Token);
            if (channel != SelectedGameServerChannel) return;

            ApplyVersionCheckToStatus(check, channel);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            WriteRevisionLog("Server branch switch", ex);
            await RefreshGameVersionStatusAsync();
        }
    }

    private bool SwitchGameInstallation(GameServerChannel channel)
    {
        try
        {
            if (IsWarThunderRunningOrStarting()) throw new IOException(T("Warning.CloseGameBeforeServer"));
            string root = GameInstallations.CanonicalRoot(_installations.LiveRoot);
            if (string.IsNullOrWhiteSpace(root) || !File.Exists(Path.Combine(root, "config.blk")) || !File.Exists(Path.Combine(root, "launcher.exe")))
            {
                if (!TryGetCurrentGameRoot(out root) && !ChooseGameFolder(channel)) return false;
                if (!TryGetCurrentGameRoot(out root)) return false;
            }
            if (LauncherRunningIn(root)) throw new IOException(T("Warning.CloseLauncherBeforeServer"));

            string config = Path.Combine(root, "config.blk");
            PrepareDevServerWorkspace(root, config, channel == GameServerChannel.Test);
            WarThunderServerConfig.ApplyToFile(config, channel);
            if (channel == GameServerChannel.Test)
                File.Copy(config, Path.Combine(DevServerWorkspace(root), "config.test.blk"), true);

            _useTestServer = channel == GameServerChannel.Test;
            _installations.SetBoth(root);
            _configBlkPath = config;
            _warThunderExePath = Path.Combine(root, "launcher.exe");
            SaveGameInstallations();
            SaveState();
            UpdateVisualStates();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, T("Dialog.GameServerTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    private static bool GameIsRunning()
    {
        Process[] aces = Process.GetProcessesByName("aces");
        Process[] acesBe = Process.GetProcessesByName("aces_BE");
        try { return aces.Length != 0 || acesBe.Length != 0; }
        finally
        {
            foreach (var process in aces) process.Dispose();
            foreach (var process in acesBe) process.Dispose();
        }
    }

    private static bool LauncherRunningIn(string root)
    {
        Process[] processes = Process.GetProcessesByName("launcher");
        try
        {
            foreach (var process in processes)
            {
                try
                {
                    string? file = process.MainModule?.FileName;
                    if (file != null && string.Equals(Path.GetDirectoryName(file), root, StringComparison.OrdinalIgnoreCase)) return true;
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // An unrelated/elevated launcher process whose executable
                    // path cannot be inspected must not hold WT Assistant in a
                    // false "launcher still running" state. The updater path
                    // also waits for warthunder.yup / launcher-log convergence,
                    // so skipping an uninspectable process here is safe for the
                    // version-refresh flow.
                    continue;
                }
            }
            return false;
        }
        finally { foreach (var process in processes) process.Dispose(); }
    }

    private async Task<bool> RunOfficialUpdaterAsync(string root, CancellationToken token)
    {
        if (IsWarThunderRunningOrStarting()) throw new IOException(T("Warning.CloseGameBeforeServer"));
        if (LauncherRunningIn(root)) throw new IOException(T("Warning.CloseLauncherBeforeServer"));

        // The updater is update-only. WT Assistant starts the official launcher,
        // keeps the selected circuit in config.blk, then waits for the USER to
        // close the launcher. It must never close the launcher on its own and it
        // must never chain directly into aces.exe after an update.
        WarThunderServerConfig.ApplyToFile(Path.Combine(root, "config.blk"), SelectedGameServerChannel);
        DateTimeOffset launcherStartedAt = DateTimeOffset.UtcNow;
        using var process = Process.Start(new ProcessStartInfo(Path.Combine(root, "launcher.exe"))
        {
            WorkingDirectory = root,
            UseShellExecute = true
        }) ?? throw new IOException("The official launcher did not start.");

        // launcher.exe can hand off to another launcher/updater instance. Wait
        // until every launcher belonging to THIS War Thunder installation has
        // been closed for a short quiet window before refreshing version state.
        int quiet = 0;
        while (quiet < 4)
        {
            token.ThrowIfCancellationRequested();
            bool primaryRunning;
            try { primaryRunning = !process.HasExited; }
            catch { primaryRunning = false; }
            bool anyLauncher = LauncherRunningIn(root);
            quiet = primaryRunning || anyLauncher ? 0 : quiet + 1;
            await Task.Delay(500, token);
        }

        // The user may have started the game from the official launcher.
        // Do not rewrite its configuration while it is running.
        if (GameIsRunning()) return false;
        WarThunderServerConfig.ApplyToFile(Path.Combine(root, "config.blk"), SelectedGameServerChannel);

        return !GameIsRunning();
    }

    private async Task<VersionCheck> WaitForGameVersionConvergenceAsync(
        string root,
        GameServerChannel channel,
        Version? expectedLatest,
        CancellationToken token)
    {
        // The official launcher stores the installed build in warthunder.yup.
        // Poll that local manifest first; do not hammer the version service and
        // do not require aces.exe's PE version to change on a content-only patch.
        Version? latest = expectedLatest;
        if (latest == null)
        {
            try { latest = await _gameServices.LatestAsync(channel, token); }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException) { }
        }

        VersionCheck last = new(GameServices.ReadInstalledVersion(root), latest, "Waiting for local War Thunder manifest.");
        for (int attempt = 0; attempt < 60 && !last.Current; attempt++)
        {
            ApplyVersionCheckToStatus(last, channel);
            await Task.Delay(500, token);
            Version? installed = GameServices.ReadInstalledVersion(root);
            if (installed != null && GameServices.IsLauncherValidatedBuild(root, channel, installed))
            {
                last = new(installed, installed, "Official launcher validated the selected branch.");
                break;
            }
            last = new(installed, latest, "Local warthunder.yup / launcher metadata.");
        }

        // One final online check catches a version that changed again while the
        // launcher was open. Do NOT throw away a successful local convergence
        // merely because this last network request times out: the version that
        // triggered the updater is already a valid comparison target.
        try
        {
            VersionCheck online = await _gameServices.CheckVersionAsync(root, channel, token);
            if (online.Latest != null)
                last = online;
            else if (online.Installed != null)
                last = new(online.Installed, latest, "Local manifest refreshed; final online recheck was unavailable.");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            // Preserve the locally converged result and the pre-update latest
            // version instead of manufacturing a false update-verification failure.
        }
        ApplyVersionCheckToStatus(last, channel);
        return last;
    }

    private void HomePrimaryAction()
    {
        // The large Home button is mode-aware: UPDATE only updates, LAUNCH only
        // launches. Keeping those operations separate prevents a completed update
        // from unexpectedly starting War Thunder.
        if (_selectedGameUpdateAvailable)
            _ = UpdateSelectedGameAsync();
        else
            LaunchWarThunder();
    }

    private async Task UpdateSelectedGameAsync()
    {
        if (_launchBusy) return;
        _launchBusy = true;
        UpdateVisualStates();
        try
        {
            if (GameIsRunning() || GameLaunchPending)
            {
                MessageBox.Show(this, T("Game.AlreadyRunning"));
                return;
            }
            if (!ApplySelectedGameServer(true)) return;
            if (!TryGetCurrentGameRoot(out string root))
            {
                MessageBox.Show(this, T("Game.SelectInstallFirst"));
                return;
            }
            if (LauncherRunningIn(root))
            {
                MessageBox.Show(this, T("Game.LauncherRunning"));
                return;
            }

            GameServerChannel channel = SelectedGameServerChannel;
            string branch = channel == GameServerChannel.Test ? T("Home.TestServer") : T("Home.LiveServer");
            VersionCheck check = await _gameServices.CheckVersionAsync(root, channel, _revisionCancellation.Token);
            ApplyVersionCheckToStatus(check, channel);

            if (check.Latest == null)
            {
                SetGameVersionStatus(TF("Game.Status.CannotVerify", branch), Color.FromArgb(236, 185, 71));
                MessageBox.Show(this, TF("Game.VerifyBranchFailed", branch),
                    T("Game.VersionCheckTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (check.Current)
            {
                ApplyVersionCheckToStatus(check, channel);
                return;
            }

            SetGameVersionStatus(TF("Game.Status.Switching", branch,
                check.Installed?.ToString() ?? T("Common.Unknown"), branch), Color.FromArgb(236, 185, 71));

            if (!await RunOfficialUpdaterAsync(root, _revisionCancellation.Token))
                return;

            // The launcher has been closed by the user. Refresh the local
            // manifest; the Home button becomes LAUNCH when versions match.
            check = await WaitForGameVersionConvergenceAsync(root, channel, check.Latest, _revisionCancellation.Token);
            ApplyVersionCheckToStatus(check, channel);
            await RefreshGameVersionStatusAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            WriteRevisionLog("Game update", ex);
            if (!IsDisposed) MessageBox.Show(this, ex.Message, T("Game.UpdateDialogTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _launchBusy = false;
            if (!IsDisposed)
            {
                UpdateVisualStates();
                _ = RefreshGameVersionStatusAsync();
            }
        }
    }

    private async void LaunchWarThunder()
    {
        if (_launchBusy) return;
        _launchBusy = true;
        UpdateVisualStates();
        try
        {
            if (GameIsRunning() || GameLaunchPending) { MessageBox.Show(this, T("Game.AlreadyRunning")); return; }
            if (!ApplySelectedGameServer(true)) return;
            if (!TryGetCurrentGameRoot(out string root)) { MessageBox.Show(this, T("Game.SelectInstallFirst")); return; }
            if (LauncherRunningIn(root)) { MessageBox.Show(this, T("Game.LauncherRunning")); return; }

            GameServerChannel channel = SelectedGameServerChannel;
            string branch = channel == GameServerChannel.Test ? T("Home.TestServer") : T("Home.LiveServer");
            VersionCheck check = await _gameServices.CheckVersionAsync(root, channel, _revisionCancellation.Token);
            ApplyVersionCheckToStatus(check, channel);

            if (check.Latest == null)
            {
                SetGameVersionStatus(TF("Game.Status.CannotVerify", branch), Color.FromArgb(236, 185, 71));
                MessageBox.Show(this, TF("Game.VerifyBranchFailed", branch),
                    T("Game.VersionCheckTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // LAUNCH never performs an update. If a fresh check discovers that the
            // selected branch is not ready, switch the Home button to UPDATE and
            // let the user explicitly run the updater on the next click.
            if (!check.Current)
            {
                _selectedGameUpdateAvailable = true;
                _aviationHome?.SetGameUpdateAvailable(true);
                ApplyVersionCheckToStatus(check, channel);
                UpdateVisualStates();
                return;
            }

            WarThunderServerConfig.ApplyToFile(Path.Combine(root, "config.blk"), channel);
            ApplyVersionCheckToStatus(check, channel);
            await RefreshGameVersionStatusAsync();
            if (_selectedGameUpdateAvailable) return;
            if (!IsDisposed && !_revisionCancellation.IsCancellationRequested && !IsWarThunderRunningOrStarting())
                LaunchWarThunderDirect();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            WriteRevisionLog("Launch", ex);
            if (!IsDisposed) MessageBox.Show(this, ex.Message, T("Game.LaunchTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _launchBusy = false;
            if (!IsDisposed) UpdateVisualStates();
        }
    }

    private void ShowGameOptions()
    {
        if (_launchBusy) { MessageBox.Show(this, T("Game.UpdateInProgress"), T("Game.UpdateDialogTitle")); return; }

        using Form dialog = new()
        {
            Text = T("Game.UpdateDialogTitle"), StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(720, 330), MinimumSize = new Size(650, 300),
            Padding = new Padding(14),
            BackColor = IllustratedTheme.Background, ForeColor = IllustratedTheme.Ivory, AutoScaleMode = AutoScaleMode.Dpi
        };
        dialog.HandleCreated += (_, _) => IllustratedTheme.ApplyWindowChrome(dialog);

        Label info = new()
        {
            Dock = DockStyle.Top, Height = 100, Padding = new Padding(22, 14, 22, 8),
            Font = new Font("Segoe UI", 19, FontStyle.Bold, GraphicsUnit.Pixel),
            ForeColor = IllustratedTheme.Ivory, TextAlign = ContentAlignment.MiddleCenter
        };
        ThemeButton update = new()
        {
            Dock = DockStyle.Bottom, Height = 92, Margin = Padding.Empty, Text = T("Game.UpdateButton"),
            Font = new Font("Segoe UI", 23, FontStyle.Bold, GraphicsUnit.Pixel)
        };
        Label result = new()
        {
            Dock = DockStyle.Fill, Padding = new Padding(22, 12, 22, 12),
            Font = new Font("Segoe UI", 16, FontStyle.Regular, GraphicsUnit.Pixel),
            ForeColor = IllustratedTheme.Muted, TextAlign = ContentAlignment.TopCenter
        };

        async Task RefreshInfo()
        {
            if (!TryGetCurrentGameRoot(out string root)) { info.Text = T("Game.InstallationNotFound"); return; }
            VersionCheck c = await _gameServices.CheckVersionAsync(root, SelectedGameServerChannel, _revisionCancellation.Token);
            info.Text = TF("Game.UpdateInfo", SelectedGameServerChannel == GameServerChannel.Test ? T("Home.TestServer") : T("Home.LiveServer"), c.Installed?.ToString() ?? T("Common.Unknown"), c.Latest?.ToString() ?? T("Common.Unknown"));
        }

        update.Click += async (_, _) =>
        {
            if (_launchBusy || !TryGetCurrentGameRoot(out string root)) return;
            _launchBusy = true; update.Enabled = false; UpdateVisualStates();
            try
            {
                WarThunderServerConfig.ApplyToFile(Path.Combine(root, "config.blk"), SelectedGameServerChannel);
                VersionCheck c = await _gameServices.CheckVersionAsync(root, SelectedGameServerChannel, _revisionCancellation.Token);
                ApplyVersionCheckToStatus(c, SelectedGameServerChannel);
                if (c.Latest is null)
                {
                    result.Text = T("Game.UpdateVersionVerifyFailed");
                }
                else if (c.Current) result.Text = T("Game.UpdateAlreadyInstalled");
                else
                {
                    result.Text = T("Game.UpdateOfficialLauncher");
                    if (await RunOfficialUpdaterAsync(root, _revisionCancellation.Token))
                    {
                        c = await WaitForGameVersionConvergenceAsync(root, SelectedGameServerChannel, c.Latest, _revisionCancellation.Token);
                        ApplyVersionCheckToStatus(c, SelectedGameServerChannel);
                        result.Text = c.Current ? TF("Game.UpdateComplete", c.Installed?.ToString() ?? T("Common.Unknown")) : T("Game.UpdateUnconfirmed");
                    }
                    else result.Text = T("Game.UpdateLauncherFailed");
                }
                await RefreshGameVersionStatusAsync(); await RefreshInfo();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { result.Text = ex.Message; WriteRevisionLog("Updater", ex); }
            finally { _launchBusy = false; if (!dialog.IsDisposed) update.Enabled = true; if (!IsDisposed) UpdateVisualStates(); }
        };

        dialog.Controls.Add(result); dialog.Controls.Add(update); dialog.Controls.Add(info);
        dialog.Shown += async (_, _) => { try { await RefreshInfo(); } catch { } };
        dialog.ShowDialog(this);
    }

    private static void WriteRevisionLog(string area, Exception exception)
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);
            string path = Path.Combine(SettingsFolder, "diagnostics.log");
            if (File.Exists(path) && new FileInfo(path).Length > 512000) File.Move(path, path + ".old", true);
            File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} {area}: {exception}{Environment.NewLine}");
        }
        catch { }
    }
}
