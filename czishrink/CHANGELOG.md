# CziShrink version history

## 1.4.0

- Upgrade to .NET 10 (LTS); .NET 8 reaches end of support in November 2026.
- Update the native czicompress build to libCZI 0.72.0; updating CziShrink's bundled native package is a separate follow-up.
- Update dependencies (Avalonia 11.3, Microsoft.DotNet.ILCompiler 10, System.IO.Abstractions 22 and others), which also resolves reported vulnerabilities in transitive dependencies.
- Tests: replace FluentAssertions with its Apache-2.0 licensed fork AwesomeAssertions.

## 1.3.0

- Copy accompanying files and folders (files other than CZI) to the output folder.
