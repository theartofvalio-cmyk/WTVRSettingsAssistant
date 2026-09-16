# VR Assistant 2.0.2

Build-fix revision for the v2.0.1 source package.

- Restored `AviationHomePage.SetServerVersions(...)`, forwarding to the Live/Test selector.
- Fixed the DataGridView nullable `Rows.Insert` warning in Advanced Switch Bindings.
- Fixed nullable `CurrentRow` dereference warnings in Learn Position by capturing the selected row once.
- Preserves the v2.0.1 feature/UI behavior; this revision is focused on making the delivered solution compile cleanly.
