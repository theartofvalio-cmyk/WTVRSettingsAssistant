from pathlib import Path
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
checks = 0


def need(condition, message):
    global checks
    checks += 1
    if not condition:
        raise AssertionError(message)


def read(rel):
    path = root / rel
    need(path.exists(), f"missing {rel}")
    return path.read_text(encoding="utf-8-sig", errors="strict")


# ---------- package / revision ----------
sln = read("WTAssistant_REV_2_0_5.sln")
main_sln = read("VR_Assistant_v2.0.5.sln")
proj = read("WTAssistant/WTVRSettingsAssistant.csproj")
embed_proj = read("Modules/VTrim/Integration/VTrim/VTrim.Embedded.csproj")
program = read("WTAssistant/Program.cs")
main = read("WTAssistant/MainForm_REV_2_0_5.cs")
services = read("WTAssistant/GameServices_REV_2_0_5.cs")
home = read("WTAssistant/AviationHomePage.cs")
theme = read("WTAssistant/IllustratedTheme.cs")
adv = read("WTAssistant/AdvancedSwitchBindings.cs")
hidden = read("WTAssistant/HiddenKeybindsForm.cs")
neck = read("WTAssistant/NeckAssistForm.cs")
vtrim = read("Modules/VTrim/Integration/VTrim/Form1.cs")
vtrim_embed = read("Modules/VTrim/Integration/VTrim/Form1.Embedded.cs")
vtrim_curves = read("Modules/VTrim/Integration/VTrim/Form1.Curves.cs")
vtrim_virtual = read("Modules/VTrim/Integration/VTrim/Form1.VirtualOutput.cs")

need("<Version>2.0.5</Version>" in proj, "main Version is not 2.0.5")
need("<AssemblyVersion>2.0.5.0</AssemblyVersion>" in proj, "main AssemblyVersion is not 2.0.5.0")
need("<FileVersion>2.0.5.0</FileVersion>" in proj, "main FileVersion is not 2.0.5.0")
need("<Version>2.0.5</Version>" in embed_proj, "embedded VTrim Version is not 2.0.5")
need("<PlatformTarget>x86</PlatformTarget>" in proj, "main target must remain x86")
need("<PlatformTarget>x86</PlatformTarget>" in embed_proj, "embedded VTrim target must remain x86")
need("VTrim.Embedded.csproj" in proj, "main project reference to embedded VTrim missing")
for solution in (sln, main_sln):
    need("WTVRSettingsAssistant.csproj" in solution, "solution missing main project")
    need("VTrim.Embedded.csproj" in solution, "solution missing embedded VTrim project")
for project_file in root.rglob("*.csproj"):
    ET.parse(project_file)
    checks += 1
need('CurrentVersion = "2.0.5"' in program, "runtime version is not 2.0.5")
need("WTAssistant/2.0.5" in services, "GameServices user-agent was not bumped")
need(not (root / "WTAssistant/MainForm_REV_2_0_4.cs").exists(), "old current MainForm revision remains")
need(not (root / "WTAssistant/GameServices_REV_2_0_4.cs").exists(), "old current GameServices revision remains")
need(not (root / "WTAssistant_REV_2_0_4.sln").exists(), "old current solution remains")
need(not (root / "VR_Assistant_v2.0.4.sln").exists(), "old current friendly solution remains")

# ---------- assets / branding ----------
brand = root / "WTAssistant/Assets/VRA.png"
brand_copy = root / "WTAssistant/Assets/WTAssistantBrand.png"
icon = root / "WTAssistant/Icon.ico"
need(brand.exists() and brand.stat().st_size > 100_000, "Home brand artwork missing")
need(brand_copy.exists() and brand_copy.read_bytes() == brand.read_bytes(), "WTAssistantBrand is not the supplied Home artwork")
need(icon.exists() and icon.stat().st_size > 10_000, "application icon missing")
need("<ApplicationIcon>Icon.ico</ApplicationIcon>" in proj, "main project is not configured to use Icon.ico")
need("_themeBrand = null;" in program and "_themeSubtitle = null;" in program, "obsolete sidebar brand text still exists")

# ---------- main 16:9 + responsive chrome ----------
for token in (
    "private const float LockedWindowAspectRatio = 16F / 9F;",
    "private void UpdateAspectMaximizedBounds()",
    "MaximizedBounds = new Rectangle(x, y, width, height);",
    "private Size FitClientSizeToLockedAspect",
    "WindowLayoutRevision = 205",
    "private float ThemeChromeScale",
    "ClientSize = new Size(S(1600), S(900));",
    "MinimumSize = new Size(S(1120), S(630));",
):
    need(token in program, f"main responsive/aspect feature missing: {token}")
need("UpdateAspectMaximizedBounds();" in main, "maximize 16:9 bounds hook missing")
need("LocationChanged +=" in main and "DpiChanged +=" in main, "screen/DPI maximize-bound refresh hooks missing")
need("SetOwnedFont(buttons[i], 21 * scale" in program, "navigation text does not scale with chrome")
need("int footerHeight = S(66);" in program, "footer is not scaled with chrome")

# ---------- Home reference composition ----------
for token in (
    "const int designWidth = 1240;",
    "const int designHeight = 720;",
    "const int logoAreaWidth = 600;",
    "_playMode.Visible = false;",
    "int serverY = canvasY + S(8);",
    "int modeY = canvasY + S(118);",
    "int modeHeight = S(198);",
    "int featureY = canvasY + S(330);",
    "int featureHeight = S(142);",
    "int launchY = canvasY + S(486);",
    "int launchHeight = S(116);",
    "int linksY = canvasY + S(616);",
):
    need(token in home, f"Home reference layout marker missing: {token}")
need(home.find("_server.SetBounds") < home.find("_monitor.SetBounds") < home.find("_neck.SetBounds") < home.find("_launch.SetBounds") < home.find("_links.SetBounds"),
     "Home card order is not server -> play mode -> assistants -> launch -> links")
need("float scale = Math.Min(ClientSize.Width / (float)designWidth, ClientSize.Height / (float)designHeight);" in home,
     "Home does not use one design-space scale factor")
need("PictureBoxSizeMode.Zoom" in home, "Home logo is not using aspect-preserving zoom")
need("bool playModeCard = _icon is \"monitor\" or \"visor\";" in home, "Monitor/VR dedicated scaling missing")
need("r.Height * 0.64" in home, "server icon scaling missing")
need("Height * 0.48" in home, "launch icon scaling missing")
need("Height * 0.165" in home, "assistant toggle scaling missing")
for icon_case in ('case "monitor"', 'case "visor"', 'case "live"', 'case "test"'):
    need(icon_case in theme, f"illustrated icon implementation missing: {icon_case}")

# ---------- App Options anti-crop ----------
for token in (
    "ClientSize = new Size(1080, 680)",
    "MinimumSize = new Size(860, 560)",
    "Padding = new Padding(40, 24, 40, 32)",
    "new RowStyle(SizeType.Absolute, 116)",
    "new ColumnStyle(SizeType.Percent, 53)",
    "new ColumnStyle(SizeType.Percent, 27)",
    "new ColumnStyle(SizeType.Percent, 20)",
    "void ApplyOptionScale()",
    "void FitOptionsToScreen()",
):
    need(token in program, f"App Options responsive marker missing: {token}")
need("Dock = DockStyle.Fill" in program[program.find('Text = "SAVE OPTIONS"'):program.find('Text = "CANCEL"')], "SAVE OPTIONS is not fill-docked")
need("Dock = DockStyle.Fill" in program[program.find('Text = "CANCEL"'):program.find("dialog.AcceptButton = save")], "CANCEL is not fill-docked")

# ---------- Neck Assistant ----------
need("CaptureThemeFontSizes(this);" in neck and "ApplyThemeFontScale();" in neck, "Neck Assistant responsive typography missing")
need("ClientSizeChanged" in neck and "ApplyThemeFontScale" in neck, "Neck Assistant resize typography hook missing")
need("bindingHelp.SetBounds" in neck, "Neck advanced help layout missing")

# ---------- Advanced switch editor ----------
for token in (
    'HeaderText = "CONDITIONS"',
    'HeaderText = "VIRTUAL"',
    "DropDownWidth = 330",
    'AdvancedSwitchManagerForm.B("EXAMPLES")',
    "private void ShowExamples()",
    "bool compact = w < 1250;",
    "Pulse same command (toggle)",
):
    need(token in adv, f"Custom Switch Builder feature missing: {token}")
# Even before LayoutEditor runs, initial right-side buttons must be inside 1220px client width.
need("learn.SetBounds(1036, 174, 160, 40)" in adv, "initial LEARN SWITCH bounds can crop")
need("save.SetBounds(874, 748, 144, 44)" in adv, "initial SAVE SWITCH bounds can crop")
need("cancel.SetBounds(1030, 748, 142, 44)" in adv, "initial CANCEL bounds can crop")

# ---------- VTrim global responsive system ----------
for token in (
    "private float GetResponsiveUiScale()",
    "private void RegisterResponsiveScrollSurface",
    "private void SizeResponsiveScrollSurface",
    "private void SetResponsiveFont",
    "private void FitTextControl",
    "private void FitResponsiveText",
    "Math.Clamp(Math.Min(widthScale, heightScale), 0.74F, 1.55F)",
    "RegisterResponsiveScrollSurface(parent, layout, 1080);",
    "RegisterResponsiveScrollSurface(parent, layout, 980);",
    "RegisterResponsiveScrollSurface(parent, layout, 720);",
):
    need(token in vtrim, f"VTrim responsive feature missing: {token}")
need("RegisterResponsiveScrollSurface(parent, layout, 900);" in vtrim_curves, "Axis Curves responsive surface missing")
need("AutoEllipsis = true" not in vtrim + vtrim_embed + vtrim_curves + vtrim_virtual,
     "VTrim still contains AutoEllipsis=true")
need("AutoEllipsis = true" not in adv + hidden,
     "KeyBind/Custom Switch UI still contains AutoEllipsis=true")
need("label.AutoEllipsis = false;" in vtrim_embed,
     "embedded VTrim theme can re-enable ellipsis")
need("control is Label { AutoSize: false }" in vtrim, "long VTrim labels are not included in anti-crop fitting")
need("ResponsiveTextControl_TextChanged" in vtrim and
     "control.TextChanged += ResponsiveTextControl_TextChanged;" in vtrim and
     "FitOneResponsiveTextControl(control);" in vtrim,
     "VTrim dynamic post-layout text fitting hook missing")
need("_responsiveOwnedFonts" in vtrim and "previous.Dispose()" in vtrim, "VTrim responsive font ownership guard missing")

# Dashboard / trim monitor.
for token in (
    "new RowStyle(\n                SizeType.Percent,\n                45F)",
    "new RowStyle(\n                SizeType.Percent,\n                47F)",
    "new RowStyle(SizeType.Absolute, 64F)",
    "content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36F));",
    "grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));",
):
    need(token in vtrim, f"VTrim dashboard geometry missing: {token}")
need('"Rudder Trim  0.00%"' in vtrim, "full Rudder Trim label missing")

# Rudder assist / precision / bindings.
for token in (
    "panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));",
    "panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44F));",
    "panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));",
    "MinimumSize = new Size(64, 24);",
    "MinimumSize = new Size(82, 32);",
    "layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));",
    "grid.RowStyles.Add(new RowStyle(SizeType.Percent, 23F));",
    "grid.Controls.Add(rudderSection, 0, 4);",
):
    need(token in vtrim, f"VTrim control anti-crop geometry missing: {token}")
need("grid.Controls.Add(rudderSection, 0, 5);" not in vtrim, "old out-of-range Rudder binding row remains")
need("int markerDiameter = Math.Clamp((int)Math.Round(Height * 0.34), 10, 20);" in vtrim and
     "Math.Max(markerDiameter / 2 + 2" in vtrim, "TrimBar edge-marker clipping guard missing")
need("int thumbDiameter = Math.Clamp((int)Math.Round(Height * 0.48), 12, 24);" in vtrim and
     "thumbDiameter / 2 + 2" in vtrim, "RepeatSpeedSlider clipping guard missing")

# Devices / routes / curves / virtual output.
for token in (
    "new ColumnStyle(SizeType.Percent, 54F)",
    "new ColumnStyle(SizeType.Percent, 27F)",
    "new ColumnStyle(SizeType.Percent, 19F)",
    "new ColumnStyle(SizeType.Percent, 9F)",
    "new ColumnStyle(SizeType.Percent, 33F)",
    "new ColumnStyle(SizeType.Percent, 23F)",
    "new ColumnStyle(SizeType.Percent, 14F)",
    "new ColumnStyle(SizeType.Percent, 21F)",
):
    need(token in vtrim, f"VTrim Devices/Axis routing proportional geometry missing: {token}")
need("new ColumnStyle(SizeType.Percent, 54)" in vtrim_curves and "new ColumnStyle(SizeType.Percent, 46)" in vtrim_curves,
     "Axis Curves graph/control split missing")
need("new ColumnStyle(SizeType.Percent, 56" in vtrim_virtual or "SizeType.Percent, 56" in vtrim_virtual,
     "virtual-output proportional connection layout missing")

# Profiles + aircraft categories.
for token in (
    'public string AircraftType { get; set; } = "Prop Plane";',
    "ProfileTypeComboBox",
    '"Prop Plane"',
    '"Jet Plane"',
    '"Helicopter"',
    "AircraftType = NormalizeAircraftType",
    "new RowStyle(SizeType.Absolute, 264F)",
):
    need(token in vtrim, f"VTrim profile type/layout feature missing: {token}")

# Embedded VTrim header compile-safe newline (regression that caused the screenshoted build failure).
need('Text = "Pitch  •  Roll  •  Rudder" + Environment.NewLine + "Physical buttons stay native",' in vtrim_embed,
     "embedded header must use Environment.NewLine rather than a literal newline")

# User-facing WinForms labels/buttons must never deliberately trade clipping for an ellipsis.
for rel in (
    "WTAssistant/AdvancedSwitchBindings.cs",
    "WTAssistant/HiddenKeybindsForm.cs",
    "WTAssistant/NeckAssistForm.cs",
    "Modules/VTrim/Integration/VTrim/Form1.cs",
    "Modules/VTrim/Integration/VTrim/Form1.Embedded.cs",
    "Modules/VTrim/Integration/VTrim/Form1.Instructor.cs",
    "Modules/VTrim/Integration/VTrim/Form1.VirtualOutput.cs",
    "Modules/VTrim/Integration/VTrim/Form1.Curves.cs",
):
    need("AutoEllipsis = true" not in read(rel), f"ellipsis regression in {rel}")

# Owner-drawn Home/navigation/VTrim controls also use shrink-to-fit rather than EndEllipsis.
for rel in (
    "WTAssistant/AviationHomePage.cs",
    "WTAssistant/IllustratedTheme.cs",
    "WTAssistant/Program.cs",
    "Modules/VTrim/Integration/VTrim/Form1.cs",
):
    need("EndEllipsis" not in read(rel), f"owner-drawn ellipsis regression in {rel}")

# ---------- update dialog ----------
need("ClientSize = new Size(720, 330)" in main, "Update War Thunder dialog not enlarged")
need("Dock = DockStyle.Bottom, Height = 92" in main, "Update button is not tall/bottom-docked")
need('Text = "UPDATE GAME"' in main and 'Font = new Font("Segoe UI", 23' in main, "Update button typography not enlarged")
need('Font = new Font("Segoe UI", 19' in main, "Update status typography not enlarged")

# ---------- important runtime behavior preserved ----------
need("Installed == Latest" in services and "Installed != Latest" in services, "strict Live/Test version equality missing")
need("if (!check.Current)" in main and "RunOfficialUpdaterAsync" in main, "launch/update behavior regressed")
need("SetSignals(ServerSignal live, ServerSignal test) { }" in home, "server-signal compatibility no-op missing")
need("AdvancedSwitchService" in adv and "VJoySwitchOutput" in adv, "Advanced Switch service/output path missing")
need("SharedVJoyOutputCoordinator" in read("Modules/VTrim/Integration/VTrim/VirtualOutput.cs"), "shared vJoy output coordinator missing")

# ---------- lexical / delimiter audit of every C# source ----------
def scan_csharp(path: Path):
    src = path.read_text(encoding="utf-8-sig")
    pairs = {")": "(", "]": "[", "}": "{"}
    openings = set(pairs.values())
    stack = []
    errors = []
    i = 0
    line = 1
    n = len(src)
    while i < n:
        ch = src[i]
        if ch == "\n":
            line += 1
            i += 1
            continue
        if ch == "/" and i + 1 < n and src[i + 1] == "/":
            i += 2
            while i < n and src[i] != "\n":
                i += 1
            continue
        if ch == "/" and i + 1 < n and src[i + 1] == "*":
            start = line
            i += 2
            while i + 1 < n and not (src[i] == "*" and src[i + 1] == "/"):
                if src[i] == "\n":
                    line += 1
                i += 1
            if i + 1 >= n:
                errors.append((start, "unterminated block comment"))
                break
            i += 2
            continue
        if ch == '"':
            quote_count = 1
            while i + quote_count < n and src[i + quote_count] == '"':
                quote_count += 1
            if quote_count >= 3:
                start = line
                delimiter = '"' * quote_count
                i += quote_count
                end = src.find(delimiter, i)
                if end < 0:
                    errors.append((start, f"unterminated raw string ({quote_count} quotes)"))
                    break
                line += src[i:end].count("\n")
                i = end + quote_count
                continue
            verbatim = i > 0 and src[i - 1] == "@"
            start = line
            i += 1
            closed = False
            while i < n:
                if src[i] == "\n":
                    if verbatim:
                        line += 1
                        i += 1
                        continue
                    errors.append((start, "newline in regular string literal"))
                    break
                if src[i] == '"':
                    if verbatim and i + 1 < n and src[i + 1] == '"':
                        i += 2
                        continue
                    i += 1
                    closed = True
                    break
                if not verbatim and src[i] == "\\":
                    i += 2
                else:
                    i += 1
            if not closed and i >= n:
                errors.append((start, "unterminated string literal"))
            continue
        if ch == "'":
            start = line
            i += 1
            closed = False
            while i < n:
                if src[i] == "\n":
                    errors.append((start, "newline in char literal"))
                    break
                if src[i] == "'":
                    i += 1
                    closed = True
                    break
                if src[i] == "\\":
                    i += 2
                else:
                    i += 1
            if not closed and i >= n:
                errors.append((start, "unterminated char literal"))
            continue
        if ch in openings:
            stack.append((ch, line))
        elif ch in pairs:
            if not stack or stack[-1][0] != pairs[ch]:
                errors.append((line, f"unmatched {ch}"))
            else:
                stack.pop()
        i += 1
    for opening, opening_line in stack[-12:]:
        errors.append((opening_line, f"unclosed {opening}"))
    return errors

csharp_files = list(root.rglob("*.cs"))
need(bool(csharp_files), "no C# source files found")
for cs in csharp_files:
    errors = scan_csharp(cs)
    need(not errors, f"C# lexical/delimiter audit failed for {cs.relative_to(root)}: {errors[:4]}")

print(f"WT Assistant v2.0.5 static package checks: PASS ({checks} checks, {len(csharp_files)} C# files scanned)")
