# Revision 2.0.2 changes

This revision fixes the Visual Studio compiler report from the v2.0.1 package:

- CS1061: added the missing `AviationHomePage.SetServerVersions` forwarding method.
- CS8620: changed DataGridView row-copy values to a non-null `object[]`.
- CS8602 (2 occurrences): captured `CurrentRow` in a nullable local and reused the validated row.

No feature removals were made.
