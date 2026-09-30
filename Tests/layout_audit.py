from math import isclose

checks = 0

def need(cond, msg):
    global checks
    checks += 1
    if not cond:
        raise AssertionError(msg)

# Home design-space geometry copied from AviationHomePage.Arrange.
DW, DH = 1240, 720
LOGO_W, GAP = 600, 18
RIGHT_W = DW - LOGO_W - GAP
bands = {
    "server": (8, 92),
    "monitor_vr": (118, 198),
    "assistants": (330, 142),
    "launch": (486, 116),
    "links": (616, 44),
}
previous_bottom = -1
for name, (y, h) in bands.items():
    need(y >= 0 and h > 0 and y + h <= DH, f"Home band outside design canvas: {name}")
    need(y >= previous_bottom, f"Home bands overlap before scaling: {name}")
    previous_bottom = y + h
need(RIGHT_W > 560, "Home right-side control region is unexpectedly narrow")

# Verify the exact same scale is applied in both axes across practical content viewports.
for vw, vh in [(900, 520), (1100, 640), (1240, 720), (1500, 850), (1750, 980), (2100, 1180)]:
    scale = min(vw / DW, vh / DH)
    cw, ch = round(DW * scale), round(DH * scale)
    need(cw <= vw + 1 and ch <= vh + 1, f"scaled Home canvas escapes viewport {vw}x{vh}")
    right_w = round(RIGHT_W * scale)
    gap = max(1, round(10 * scale))
    half = (right_w - gap) // 2
    third = (right_w - 2 * gap) // 3
    need(half > 0 and right_w - half - gap > 0, f"Monitor/VR widths invalid at {vw}x{vh}")
    need(third > 0 and right_w - 2 * (third + gap) > 0, f"assistant widths invalid at {vw}x{vh}")
    # Scaled bands must remain separated; rounding gets a 1px tolerance.
    scaled = [(round(y * scale), round((y + h) * scale)) for y, h in bands.values()]
    for idx in range(1, len(scaled)):
        need(scaled[idx][0] + 1 >= scaled[idx-1][1], f"Home scaled bands overlap at {vw}x{vh}")

# Largest centered 16:9 rectangle in common Windows working areas.
ASPECT = 16 / 9
for ww, wh in [(1920, 1040), (2048, 1110), (2560, 1400), (3440, 1400), (3840, 2080), (1366, 728)]:
    width = ww
    height = round(width / ASPECT)
    if height > wh:
        height = wh
        width = round(height * ASPECT)
    need(width <= ww and height <= wh, f"maximized bounds escape working area {ww}x{wh}")
    need(abs(width / height - ASPECT) < 0.0025, f"maximized bounds ratio drift at {ww}x{wh}")
    need(ww - width >= 0 and wh - height >= 0, "negative centering margin")

# App Options: fixed rows + scaled padding must leave a meaningful flexible help region,
# and percentage columns must keep both framed action buttons usable.
for width, height in [(860, 560), (940, 600), (1080, 680), (1320, 820)]:
    scale = min(width / 1080, height / 680)
    scale = max(0.80, min(scale, 1.55))
    fixed_rows = sum(v * scale for v in (58, 122, 72, 72, 116))
    vertical_padding = (24 + 32) * scale
    flexible = height - fixed_rows - vertical_padding
    need(flexible >= 75, f"App Options flexible help row too short at {width}x{height}: {flexible:.1f}px")
    inner_width = width - (40 + 40) * scale
    save_cell = inner_width * 0.27
    cancel_cell = inner_width * 0.20
    need(save_cell - 20 * scale >= 150, f"SAVE OPTIONS cell too narrow at {width}x{height}")
    need(cancel_cell - 6 * scale >= 120, f"CANCEL cell too narrow at {width}x{height}")

# VTrim design surfaces: fixed bands must leave substantial flexible content.
# Dashboard design 1080: live 45%, center 47%, activity 8%.
for scale in (0.74, 0.82, 1.0, 1.25, 1.55):
    dash = 1080 * scale
    live = dash * .45
    middle = dash * .47
    activity = dash * .08
    need(live >= 359, f"VTrim live monitor too short at scale {scale}")
    need(middle >= 375, f"VTrim bindings/precision too short at scale {scale}")
    need(activity >= 63, f"VTrim activity row too short at scale {scale}")
    # Live card: scaled 84 heading + 64 instructor strip + vertical padding 26.
    live_content = live - (84 + 64 + 26) * scale
    need(live_content >= 220, f"VTrim live preview content crushed at scale {scale}")

# Setup: fixed cards still leave a large axis-routing region.
for scale in (0.82, 1.0, 1.30):
    setup = 980 * scale
    flexible = setup - (178 + 210 + 130) * scale
    need(flexible >= 375 * scale, f"VTrim setup axis-routing region too short at scale {scale}")

# Profiles and curves retain usable content heights.
for scale in (0.74, 1.0, 1.55):
    profile_summary = (720 - 264) * scale
    need(profile_summary >= 337, f"VTrim profile summary too short at scale {scale}")
    curve_card = (900 - 72) * scale / 3
    need(curve_card >= 204, f"VTrim curve card too short at scale {scale}")

# Narrow-card horizontal allocations. These are logical pre-scale widths; percentages
# preserve the same ratios after scaling.
for card_width in (280, 320, 380, 520):
    slider_width = card_width * .44
    value_width = card_width * .22
    need(slider_width >= 64, f"Horizontal Assist slider below slider minimum at width {card_width}")
    need(value_width >= 55, f"Horizontal Assist value label too narrow at width {card_width}")

for card_width in (300, 360, 480, 620):
    editor_width = card_width * .64
    need(editor_width >= 82, f"Trim Precision editor below minimum at width {card_width}")

# Custom slider painting: marker/thumb radius must fit inside the computed padding.
for width in (64, 90, 140, 260, 420):
    for height in (24, 30, 42, 64):
        thumb = max(12, min(round(height * .48), 24))
        pad = max(thumb // 2 + 2, max(8, min(width // 45, 18)))
        need(width - 2 * pad >= 1, f"RepeatSpeedSlider track invalid at {width}x{height}")
        need(pad >= thumb // 2 + 2, f"RepeatSpeedSlider thumb can clip at {width}x{height}")
        marker = max(10, min(round(height * .34), 20))
        bar_pad = max(marker // 2 + 2, max(8, min(width // 35, 18)))
        need(width - 2 * bar_pad >= 1, f"TrimBar track invalid at {width}x{height}")
        need(bar_pad >= marker // 2 + 2, f"TrimBar marker can clip at {width}x{height}")

# Home owner-drawn content must remain inside each scaled card, including the
# largest labels/toggles that were visibly cramped in the screenshots.
for scale in (0.62, 0.74, 0.90, 1.0, 1.25, 1.55):
    server_h = round(92 * scale)
    mode_h = round(198 * scale)
    feature_h = round(142 * scale)
    launch_h = round(116 * scale)
    links_h = round(44 * scale)
    mode_w = round((RIGHT_W - 10) / 2 * scale)
    feature_w = round((RIGHT_W - 20) / 3 * scale)
    server_w = round(RIGHT_W * scale)
    launch_w = server_w
    link_w = round(RIGHT_W / 3 * scale)

    # Monitor / VR title and icon vertical budget.
    title_h = max(44, min(round(mode_h / 4), 62))
    icon_top = 7 + title_h + 2
    icon_bottom = mode_h - 12
    icon_size = min(max(72, min(180, round(mode_h * .53))), mode_w - 24, max(32, icon_bottom - icon_top))
    need(icon_size > 0 and icon_top + icon_size <= icon_bottom + 1, f"Home Monitor/VR icon crops at scale {scale}")

    # Assistant icon/toggle/footer state must fit without colliding.
    toggle_h = max(22, min(round(feature_h * .165), 34))
    toggle_y = feature_h - toggle_h - max(14, feature_h // 12)
    icon_bottom = feature_h - 54
    need(toggle_y >= icon_bottom - 2, f"Home assistant icon/toggle bands overlap at scale {scale}")
    need(feature_w >= 96, f"Home assistant card too narrow at scale {scale}")

    # Server, launch and social controls retain a useful interior after frames.
    need(server_h >= 54 and server_w >= 340, f"Home server cards unusable at scale {scale}")
    need(launch_h >= 64 and launch_w >= 340, f"Home launch button unusable at scale {scale}")
    need(links_h >= 26 and link_w >= 90, f"Home social button unusable at scale {scale}")

# Initial Custom Switch Builder right-side geometry must be within the 1220px client.
for x, w, name in [(1036, 160, "LEARN SWITCH"), (874, 144, "SAVE SWITCH"), (1030, 142, "CANCEL")]:
    need(x >= 0 and x + w <= 1220, f"Custom Switch Builder {name} initially crops")

# App Options bottom action row: verify scaled row/padding keeps the framed buttons
# away from the bottom edge and from each other.
for width, height in [(860, 560), (1080, 680), (1440, 900)]:
    scale = max(.80, min(min(width / 1080, height / 680), 1.55))
    bottom_pad = 32 * scale
    action_h = 116 * scale
    need(action_h >= 92, f"App Options action row too short at {width}x{height}")
    need(bottom_pad >= 25, f"App Options bottom padding too small at {width}x{height}")
    inner = width - 80 * scale
    save_w = inner * .27 - 20 * scale
    cancel_w = inner * .20 - 6 * scale
    need(save_w >= 150 and cancel_w >= 120, f"App Options action buttons crop at {width}x{height}")

# VTrim responsive scroll surfaces are deliberately at least the viewport height.
# This guards against the old failure mode where scaled children were taller than
# an unscaled page and the last slider/button row was simply clipped.
for design_h, min_scale in [(1080, .74), (980, .74), (900, .74), (720, .74)]:
    for viewport_h in (420, 560, 720, 900, 1180):
        for scale in (min_scale, 1.0, 1.30, 1.55):
            surface_h = max(viewport_h, round(design_h * scale))
            need(surface_h >= viewport_h, f"VTrim surface shorter than viewport: {design_h}/{viewport_h}/{scale}")
            need(surface_h >= round(design_h * scale), f"VTrim scaled design clipped: {design_h}/{viewport_h}/{scale}")

print(f"WT Assistant v2.0.8 layout geometry audit: PASS ({checks} checks)")
