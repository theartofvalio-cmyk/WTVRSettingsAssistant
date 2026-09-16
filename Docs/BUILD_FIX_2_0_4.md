# WT Assistant 2.0.4 - Build Fix 1

Fixed a C# compile error in `Modules/VTrim/Integration/VTrim/Form1.Embedded.cs`.

The embedded VTrim header description accidentally contained a literal line break inside a normal C# string. It now uses `Environment.NewLine`, so `VTrim.Embedded` can compile and the downstream `CS0006` missing-metadata error is no longer caused by this source error.

The package verifier now contains a regression check for this exact source construct.
