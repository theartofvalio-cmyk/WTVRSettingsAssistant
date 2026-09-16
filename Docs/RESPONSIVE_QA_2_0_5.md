# Responsive / crop QA - WT Assistant v2.0.5

This pass specifically targets the resize, overlap and cropping problems visible in the supplied screenshots.

## Automated checks performed

`python3 Tests/layout_audit.py`

- validates Home design bands do not overlap before or after scaling;
- validates Monitor/VR and assistant card widths over multiple practical viewports;
- validates icon/toggle vertical separation in Home assistant cards;
- validates the largest centered 16:9 maximize rectangle for common 1366, 1920, 2048, 2560, 3440 and 3840 working areas;
- validates App Options fixed-row budget, flexible help area, bottom padding and Save/Cancel widths;
- validates VTrim Dashboard, Devices & Output, Profiles and Axis Curves design-height budgets;
- validates VTrim responsive surfaces are never shorter than either the current viewport or the scaled design height;
- validates Rudder Assist and Trim Precision minimum horizontal control widths;
- validates custom RepeatSpeedSlider and TrimBar marker/thumb edge containment;
- validates Custom Switch Builder LEARN/SAVE/CANCEL initial bounds.

`python3 Tests/verify_package.py`

- validates project/solution references and v2.0.5 version consistency;
- validates application artwork/icon wiring;
- validates 16:9 resize/maximize hooks and responsive Home composition;
- validates App Options, Neck Assistant, Custom Switch Builder and VTrim anti-crop source markers;
- rejects `AutoEllipsis=true` in the critical embedded UI sources;
- rejects `EndEllipsis` in owner-drawn Home/navigation/footer/VTrim controls that should shrink-to-fit instead;
- scans every C# source for unbalanced delimiters and malformed regular string literals, protecting against the prior `CS1010 Newline in constant` regression.

## Environment limitation

The packaging environment is Linux and does not contain Visual Studio, MSBuild, `dotnet`, `csc` or the Windows Desktop runtime/toolchain. Therefore these checks cannot replace a real Windows compilation or pixel-level runtime inspection. The final archive should still be built and visually checked on Windows with `Release | x86`, including 100%, 125% and 150% display scaling. See `Docs/WINDOWS_CHECKLIST.md`.
