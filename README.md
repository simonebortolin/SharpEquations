# SharpEquations

[![CI](https://github.com/simonebortolin/SharpEquations/actions/workflows/ci.yml/badge.svg)](https://github.com/simonebortolin/SharpEquations/actions/workflows/ci.yml)
[![CodeQL](https://github.com/simonebortolin/SharpEquations/actions/workflows/codeql.yml/badge.svg)](https://github.com/simonebortolin/SharpEquations/actions/workflows/codeql.yml)
[![NuGet](https://img.shields.io/nuget/v/SharpEquations?logo=nuget)](https://www.nuget.org/packages/SharpEquations)
[![NuGet downloads](https://img.shields.io/nuget/dt/SharpEquations?label=downloads&logo=nuget)](https://www.nuget.org/packages/SharpEquations)
[![GitHub release](https://img.shields.io/github/v/release/simonebortolin/SharpEquations?logo=github)](https://github.com/simonebortolin/SharpEquations/releases/latest)
[![Renovate](https://img.shields.io/badge/renovate-enabled-brightgreen?logo=renovatebot)](https://developer.mend.io/github/simonebortolin/SharpEquations)
[![Dependabot](https://img.shields.io/badge/dependabot-enabled-025E8C?logo=dependabot)](https://github.com/simonebortolin/SharpEquations/blob/main/.github/dependabot.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/simonebortolin/SharpEquations/blob/main/LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-512BD4?logo=dotnet&logoColor=white)](https://learn.microsoft.com/dotnet/standard/net-standard)
[![Platforms](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey)](https://github.com/simonebortolin/SharpEquations#releases)
[![Dependencies](https://img.shields.io/badge/dependencies-.NET%20BCL%20only-brightgreen)](https://github.com/simonebortolin/SharpEquations)
[![Formats](https://img.shields.io/badge/formats-MathML%20%7C%20OMML%20%7C%20MTEF%20%7C%20UnicodeMath%20%7C%20LaTeX-orange)](https://github.com/simonebortolin/SharpEquations)

Equations converted between MathML, OMML (Word), MTEF (MathType and Equation Editor 3.0), UnicodeMath (Word's linear
format) and LaTeX, in .NET, with MathML as the common representation.

| Format | Read | Write |
|---|---|---|
| MathML (presentation; Content MathML is read as the presentation it is written as) | ✓ | ✓ |
| OMML (Office Math Markup Language, the equations of `.docx`) | ✓ | ✓ |
| MTEF (MathType, Equation Editor 3.0) | ✓ | |
| UnicodeMath | ✓ | ✓ |
| LaTeX (standard, or as Word writes it) | ✓ | ✓ |

SharpEquations targets .NET Standard 2.0 and .NET 10, and depends on nothing but the .NET base library (and
System.Memory on .NET Standard 2.0).

## Usage

```csharp
using SharpEquations;

var equation = Equation.Parse(@"\frac{a^2+b^2}{c}", EquationFormat.LaTeX);
string unicodeMath = equation.ToString(EquationFormat.UnicodeMath);   // (a^2+b^2)/c
XElement omml = equation.ToOmml();
string mathML = equation.ToString();
string latex = $"{equation:L}";

string converted = EquationConverter.Convert("x=(-b±√(b^2-4ac))/2a", EquationFormat.UnicodeMath, EquationFormat.LaTeX);
```

`EquationOptions` sets the display mode, strict reading, whether formatting is kept, the LaTeX flavor (standard, or
the LaTeX of Word's equation editor) and LaTeX macros. An MTEF equation is read from the bytes of the "Equation Native"
stream of its OLE object (`Equation.Parse(bytes, EquationFormat.Mtef)`); reading the OLE object is left to the caller.

Equations compare by their normalized MathML: an equation written in its own format (UnicodeMath, OMML) reads back to
the same equation, and one written in another format loses only what that format cannot hold.

## Architecture

How the conversions work, format by format, and how round trips are kept exact: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Building and testing

```bash
dotnet build SharpEquations.slnx
dotnet test --solution SharpEquations.slnx
```

The tests use [TUnit](https://tunit.dev/); they run on .NET 10 against the .NET 10 build, and against the .NET
Standard 2.0 build on .NET 8 (Linux, macOS) or on .NET Framework 4.8.1 (Windows). Their data is in `tests/data` (see
its README).

## Releases

CI (`.github/workflows/ci.yml`) tests every push and pull request on Linux (.NET 10 and .NET 8), Windows (.NET 10 and
.NET Framework 4.8.1) and macOS (.NET 10), and packs the NuGet package. The release version is `VersionPrefix` in `BuildVersion.props`: bump it, and merging to
`main` tags `vX.Y.Z`, creates the GitHub release with the package attached, and publishes it to GitHub Packages and to
nuget.org (with [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing): the repository
variable `NUGET_USER` names the nuget.org account; without it nuget.org is skipped). A push that keeps the version on
purpose says `[no-new-version]` in its commit message. Every pull request gets a `pr-<N>-…` pre-release, with the
package versioned `X.Y.Z-pr.<N>.<run>`, replaced on each push and deleted when the pull request closes. Dependabot keeps
the GitHub Actions current and Renovate the NuGet dependencies.

## License

MIT (see `LICENSE`). Third-party files (the TEI stylesheets, the ports of UnicodeMathML and of the mathtype gem, the
code Parlot generates, test data) keep their licences: see `NOTICE`.
