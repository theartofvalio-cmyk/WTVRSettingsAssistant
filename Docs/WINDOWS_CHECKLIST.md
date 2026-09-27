# Windows validation checklist - WT Assistant v2.0.5

1. Extract the complete archive, open `WTVRSettingsAssistant.sln`, select `Release | x86`, then Clean Solution and Rebuild Solution.
2. Confirm there are zero compile errors and the executable/window icon uses the supplied WT VR Assistant artwork.
3. Home at default size: confirm Live/Test at top; large Monitor/VR below; three assistants below them; large Launch Game below assistants; Discord/YouTube/Buy Me a Beer at bottom.
4. Resize the main window from its minimum upward. Confirm the window remains 16:9 and logo/cards/icons/text/nav/footer grow and shrink together without overlaps or `...` text.
5. Maximize on the primary monitor. Confirm the app occupies the largest centered 16:9 region that fits the working area and resembles the supplied fullscreen reference rather than leaving a tiny UI in a large canvas.
6. Confirm duplicate `VR ASSISTANT / FOR WAR THUNDER` sidebar branding is absent.
7. Neck Assistant: test Simple and Advanced while resizing; all help text, Recenter/Advanced controls and Camera Transition text must remain visible.
8. KeyBind Assistant / Custom Switch Builder: resize the host and dialog; verify column headings, conditions, virtual button, trigger and output behavior are readable; LEARN/SAVE/CANCEL remain inside; `Pulse same command (toggle)` is fully visible; open EXAMPLES.
9. VTrim Dashboard: resize repeatedly and maximize. Verify Pitch/Roll/Rudder trim labels and bars, stick preview, Rudder/Yaw preview, Horizontal Assist, Roll/Pitch Gain sliders, calibration/status text and all action buttons stay inside their cards. Scroll if the design surface is taller than the viewport.
10. VTrim Trim Precision: verify Pitch/Roll/Rudder labels, editors, `%` units, Trim Hold Speed slider/thumb, Universal Trigger, Input Context Guard and Repeat While Held stay inside their frame.
11. VTrim Trim Bindings: verify command groups have clear vertical spacing and full names/buttons are readable, including Rudder Left/Right.
12. VTrim Devices & Output: verify device selector, Auto-Detect, Refresh, status line, every Physical Axis Routing row and lower output/game cards are fully reachable and no slider/button extends through a frame.
13. VTrim Axis Curves: verify each graph and its controls resize together and all three curve sections are reachable.
14. VTrim Profiles: create Prop Plane, Jet Plane and Helicopter profiles; verify category icons/text fit, save/reload persists the category, and management buttons are not cropped.
15. App Options: test minimum/default/larger dialog sizes. SAVE OPTIONS and CANCEL must remain fully inside the bottom action row and all explanatory text must remain readable.
16. Update War Thunder: verify version text is larger and UPDATE GAME is a tall bottom-aligned action with comfortable margins.
17. Repeat the visual checks at Windows display scaling 100%, 125% and 150% where possible.
