# War Thunder VR Assistant v2.0.10

- Controls Assist now reads up to **128 physical controller buttons** through DirectInput, including the PTO2 landing gear buttons **35, 36 and 37**. This removes the need to switch a compatible controller into 4×32 mode for the assistant.
- Added **Repeat while held** in the Action sequence editor. Enable it on the button's ON position to repeat the configured sequence until release. The existing press count still controls each sequence cycle; leave the new option off for a fixed count.
- Corrected sequence timing: intervals now run between the starts of presses, rather than adding the selected delay after the previous key release. A 100 ms interval targets approximately 10 presses per second. Values below 40 ms use a 40 ms minimum, with separate press and release time. Windows scheduling, other active sequences and the game can reduce the achieved rate.
- Repeated sequences stop on physical release, disconnect, disable, or loss of game focus. Reconnecting or returning to the game while the button remains held requires a fresh release and press.
- Existing settings and aircraft profiles are preserved. Older 32-button bindings and recorded gestures continue to load; new controller bindings retain the device identity across USB ordering changes.

The updater preserves local settings and profiles. For a manual install, extract the entire ZIP and keep the executable, DLLs, Drivers and Licenses together. The ZIP contains no personal settings or profiles.

Automated checks cover all 128 button positions, legacy settings, repeat cancellation and press scheduling. Physical PTO2 operation and the game's countermeasure deployment rate still require a real-device test; this release does not promise the same rate as the game's built-in periodic countermeasures or MAW slaving.
