# Validation - VR Assistant v2.0.5

Static and geometry validation is performed by:

- `Tests/verify_package.py`
- `Tests/layout_audit.py`

The package audit checks project XML, project/solution references, revision/version consistency, required source/assets, 16:9 main-window hooks, responsive Home composition, App Options anti-crop layout, Neck Assistant scaling, Custom Switch Builder bounds/examples, VTrim responsive surfaces and aircraft profile types. It also scans every C# source file for malformed ordinary string literals and gross delimiter errors.

The geometry audit exercises Home card bands and owner-drawn content budgets, common 16:9 maximize working areas, App Options row/button budgets, VTrim page height budgets, VTrim responsive scroll surfaces, narrow slider/editor allocations, custom slider/trim marker containment and Custom Switch Builder initial bounds.

Critical user-facing VTrim/KeyBind labels and buttons are checked to ensure `AutoEllipsis=true` is not reintroduced, and owner-drawn Home/navigation/footer/VTrim controls are checked to ensure `EndEllipsis` is not used where shrink-to-fit is required.

This Linux environment does not provide Visual Studio/MSBuild/.NET Windows Desktop tooling, so final WinForms compilation and runtime rendering must be validated in Visual Studio on Windows using `Docs/WINDOWS_CHECKLIST.md`.
