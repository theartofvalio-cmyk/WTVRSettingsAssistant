# WT Assistant 1.8.8

- Removed the custom VTrim KMDF/VHF driver, installer, build scripts and driver-management UI completely.
- VTrim now uses the proven vJoy Device 1 output path; the integrated VTrim UI remains part of WT Assistant.
- Removed VTrim driver install/repair/uninstall controls from App Options and the VTrim page.
- Increased the main Home hero/logo size.
- Hardened responsive sizing so Home/VTrim rows stay inside their containers and embedded VTrim fonts do not scale beyond fixed row heights.
- Reworked KeyBind Assistant row widths and the narrow Neck Assistant toolbar so buttons/text stay in their own cells instead of colliding.
- Rebuilt App Options with a responsive TableLayoutPanel to prevent label/button overlap.
- Preserved Live/Test server, DevServer, updater, Neck Assistant and KeyBind Assistant functionality from 1.8.x.
