using System.Drawing;
using System.Windows.Forms;
using Vortice.DirectInput;

namespace HOTASTrimUtility;

public partial class Form1
{
    // VTrim 1.8.8 no longer contains, builds, installs or manages a custom
    // kernel driver. The proven vJoy Device 1 path is used for virtual axes.
    private string _selectedOutputBackend = "vJoy";
    private bool _outputSetupBusy;

    public async Task<bool> SetupAndConnectOutputAsync()
    {
        if (_outputSetupBusy || _applicationClosing) return false;
        if (SetOutputEnabled(true)) return true;
        _outputSetupBusy = true;
        _vJoyAutoConnectTimer.Stop();
        _vJoyConnectButton.Enabled = false;
        try
        {
            await VJoySetup.EnsureAsync(message =>
            {
                if (!IsDisposed) { _vJoyStatusLabel.Text = message; SetInstruction(message, Theme.Warning); }
            });
            if (_applicationClosing || IsDisposed || !OutputEnabled || _manualVJoyDisconnect) return false;
            return ConnectVJoy(automatic: false);
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
            {
                _vJoyStatusLabel.Text = ex.Message;
                SetInstruction(ex.Message, Theme.Warning);
                MessageBox.Show(this, ex.Message, "vJoy setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return false;
        }
        finally
        {
            _outputSetupBusy = false;
            if (!IsDisposed)
            {
                _vJoyConnectButton.Enabled = true;
                if (OutputEnabled && !_vJoyConnected && !_applicationClosing) _vJoyAutoConnectTimer.Start();
            }
        }
    }

    private Control CreateVirtualOutputCard()
    {
        Panel panel = CreateSectionPanel(Point.Empty, Size.Empty);
        panel.Dock = DockStyle.Fill;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = new Padding(16, 10, 16, 10),
            BackColor = Theme.Panel
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var heading = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            BackColor = Theme.Panel
        };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));

        heading.Controls.Add(new Label
        {
            Text = VT("VJoy.Title"),
            Tag = "i18n:VJoy.Title",
            Dock = DockStyle.Fill,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 11F),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = false,
            UseMnemonic = false
        }, 0, 0);

        heading.Controls.Add(new Label
        {
            Text = VT("VJoy.Device1"),
            Tag = "i18n:VJoy.Device1",
            Dock = DockStyle.Fill,
            ForeColor = Theme.Success,
            Font = new Font("Segoe UI Semibold", 9.5F),
            TextAlign = ContentAlignment.MiddleRight,
            AutoEllipsis = false,
            UseMnemonic = false
        }, 1, 0);

        var connectionRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            BackColor = Theme.Panel
        };
        connectionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        connectionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23));
        connectionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 21));

        _vJoyStatusLabel = new Label
        {
            Text = VT("VJoy.StatusDisconnected"),
            Tag = "i18n:VJoy.StatusDisconnected",
            Dock = DockStyle.Fill,
            ForeColor = Theme.Warning,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = false,
            UseMnemonic = false
        };

        _vJoyConnectButton = I18n(CreatePrimaryButton("Set up / Connect"), "VJoy.SetupConnect");
        _vJoyConnectButton.Dock = DockStyle.Fill;
        _vJoyConnectButton.Margin = new Padding(4, 5, 4, 5);
        _vJoyConnectButton.Click += (_, _) => ToggleVJoyConnection();

        Button test = I18n(CreateSecondaryButton("Test Axes"), "VJoy.TestAxes");
        test.Dock = DockStyle.Fill;
        test.Margin = new Padding(4, 5, 4, 5);
        test.Click += (_, _) => OpenJoyControlPanel();

        connectionRow.Controls.Add(_vJoyStatusLabel, 0, 0);
        connectionRow.Controls.Add(_vJoyConnectButton, 1, 0);
        connectionRow.Controls.Add(test, 2, 0);

        var description = new Label
        {
            Text = VT("VJoy.Description"),
            Tag = "i18n:VJoy.Description",
            Dock = DockStyle.Fill,
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.TopLeft,
            AutoEllipsis = false,
            UseMnemonic = false
        };

        var options = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 4, 0, 0),
            BackColor = Theme.Panel
        };
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));

        _autoConnectToVJoyBox = new CheckBox
        {
            Text = VT("VJoy.AutoConnect"),
            Tag = "i18n:VJoy.AutoConnect",
            Checked = true,
            Dock = DockStyle.Fill,
            ForeColor = Theme.Text,
            AutoEllipsis = false,
            UseMnemonic = false
        };

        _startWithWindowsMinimizedBox = new CheckBox
        {
            Text = VT("VJoy.StartWithWindows"),
            Tag = "i18n:VJoy.StartWithWindows",
            Checked = false,
            Dock = DockStyle.Fill,
            ForeColor = Theme.Text,
            AutoEllipsis = false,
            UseMnemonic = false,
            Visible = !_embeddedMode
        };

        _autoConnectToVJoyBox.CheckedChanged += (_, _) =>
        {
            if (_loadingApplicationSettings) return;
            if (_embeddedMode)
            {
                SetOutputEnabled(_autoConnectToVJoyBox.Checked);
                return;
            }
            _vJoyAutoConnectTimer.Stop();
            _manualVJoyDisconnect = !_autoConnectToVJoyBox.Checked;
            SaveApplicationSettings();
            if (_autoConnectToVJoyBox.Checked && Visible && !_embeddedMode)
                StartAutomaticVJoyConnection();
        };
        _startWithWindowsMinimizedBox.CheckedChanged += (_, _) =>
        {
            if (!_loadingApplicationSettings) SaveApplicationSettings();
        };

        options.Controls.Add(_autoConnectToVJoyBox, 0, 0);
        options.Controls.Add(_startWithWindowsMinimizedBox, 1, 0);

        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(connectionRow, 0, 1);
        layout.Controls.Add(description, 0, 2);
        layout.Controls.Add(options, 0, 3);
        panel.Controls.Add(layout);
        return panel;
    }

    private static bool IsVirtualOutputDevice(DeviceInstance instance)
    {
        string name = CleanDeviceName(instance.ProductName);
        return name.Contains("vJoy", StringComparison.OrdinalIgnoreCase);
    }
}
