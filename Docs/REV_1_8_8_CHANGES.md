# Revision 1.8.8 changes

## VTrim
The custom VTrim kernel driver has been removed from the project completely. There is no WDK project, INF/SYS/CAT source, driver setup helper, driver build script, prerequisite installer or driver-management UI in this revision. The embedded VTrim assistant uses vJoy Device 1 for its virtual X/Y/Rz output.

## Layout
The Home hero/logo is larger. Home width calculations no longer force controls past the available client width. Embedded VTrim font scaling is capped so labels cannot grow into fixed-height rows, the Devices & Output card was simplified, and App Options now uses responsive table layout instead of fixed driver-era coordinates. KeyBind Assistant rows now calculate their columns from the real viewport instead of forcing an 840 px table, and the Neck Assistant advanced toolbar moves Restore Defaults to a separate row when needed so its controls cannot overlap.

## Build
WT Assistant and embedded VTrim target x86 because the stable vJoy.Wrapper package is x86. Build `Release | x86`. No WDK/SDK driver-development workload is required.
