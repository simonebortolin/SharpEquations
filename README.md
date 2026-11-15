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

The converter targets .NET Standard 2.0 and .NET 10, and depends on nothing but the .NET base library (and
System.Memory on .NET Standard 2.0). An equation editor built on it comes for Avalonia, Uno Platform and WinUI 3, WPF
and Windows Forms: see [Equation editor](#equation-editor).

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

## Packages

| Package | What it is | Targets |
|---|---|---|
| `SharpEquations` | The converter | .NET Standard 2.0, .NET 10 |
| `SharpEquations.Avalonia` | The equation editor and the view for Avalonia | .NET 8, .NET 10 |
| `SharpEquations.Uno` | The same for Uno Platform and for WinUI 3 | .NET 10, .NET 10 for Windows (Windows App SDK) |
| `SharpEquations.Wpf` | The same for WPF | .NET 10 for Windows, .NET Framework 4.8.1 |
| `SharpEquations.WinForms` | The same for Windows Forms | .NET 10 for Windows, .NET Framework 4.8.1 |
| `SharpEquations.Editing` | The editing model, no user interface | .NET Standard 2.0, .NET 10 |
| `SharpEquations.Rendering` | The layout and the view of an editor, no user interface | .NET Standard 2.0, .NET 10 |

```bash
dotnet add package SharpEquations            # the converter alone
dotnet add package SharpEquations.Avalonia   # or .Uno, .Wpf, .WinForms: the editor, with what it needs
```

All have one version. The package of a toolkit brings `SharpEquations.Rendering`, `SharpEquations.Editing` and
`SharpEquations` with it; the last two are there for an editor on another toolkit. The converter, `Editing`,
`Rendering`, `Avalonia` and `Uno` are trimmable and compatible with Native AOT (no reflection, no code generated at run
time), and their analyzers keep them so: the WebAssembly demos are compiled ahead of time.

## Equation editor

`MathEditBox` is a real-time equation editor in the manner of the `RichEditBox` of WinUI in math mode and of the
equations of Word: UnicodeMath or LaTeX typed at the caret builds up into the equation when a space or an operator
ends an argument (`a/b␣` becomes a fraction, `\alpha␣` α), the caret moves through the slots of the structures, and the
equation is read and written as MathML or as an `Equation`. `MathView` shows an equation without editing it. Both are
the same controls in Avalonia, Uno Platform and WinUI 3, WPF and Windows Forms, with the same members (those of Uno
have no `EquationNumber` and no `GetSelectedText` yet).

```csharp
var editor = new MathEditBox { FontSize = 28, InputFormat = EquationFormat.LaTeX };
editor.EquationChanged += (_, e) => Console.WriteLine(e.Equation?.ToString(EquationFormat.MathML));
string mathML = editor.GetMath();        // as RichEditTextDocument.GetMath
editor.SetMath(mathML);                  // as RichEditTextDocument.SetMath
editor.ViewMode = MathViewMode.Linear;   // the equation as one line of UnicodeMath (Ctrl+Shift+L)
editor.EquationNumber = "(1)";           // at the right of the line, as #(1) or \tag{1}
string latex = editor.GetSelectedText(EquationFormat.LaTeX)!;
editor.CopyTextFormat = EquationFormat.MathML;   // Ctrl+C pastes as an equation into Word
editor.Editor.InsertLineBreak();         // Shift+Enter: lines aligned at &
editor.Editor.InsertTableRow();          // and columns, in the matrix of the caret
```

Typing `\` and the start of a command opens a floating bar at the caret with its completions
(`EquationCommands.Complete("al", EquationFormat.UnicodeMath)` gives them from the core). The bar also offers
structures for a selection made with the pointer, as the mini toolbar of Word does, the commands of a table and quick
symbols.

### Avalonia

`SharpEquations.Avalonia`, in AXAML:

```xml
<StackPanel xmlns:math="using:SharpEquations.Avalonia">
    <math:MathEditBox x:Name="Editor" FontSize="28" InputFormat="UnicodeMath" Equation="{Binding Equation}" />
    <math:MathView FontSize="22" IsDisplay="True" Equation="{Binding #Editor.Equation}" />
</StackPanel>
```

The math font is Cambria Math on Windows, where it is a font of the system, and Libertinus Math (SIL Open Font License),
embedded, elsewhere; `MathFonts.Get` gives another one.

### Uno Platform and WinUI 3

`SharpEquations.Uno` is built only of the elements every renderer of Uno has: the glyphs of Libertinus Math (embedded)
are drawn as paths from their outlines, the structures as shapes, and what is typed goes through a transparent
`TextBox`, so that input methods and the keyboards of touch screens work. The same package targets WinUI 3 of the
Windows App SDK (`net10.0-windows10.0.19041`), so a WinUI app on Windows uses it as it is. In XAML:

```xml
<StackPanel xmlns:math="using:SharpEquations.Uno">
    <math:MathEditBox x:Name="Editor" FontSize="28" EquationChanged="OnEquationChanged" />
    <math:MathView FontSize="22" IsDisplay="True" Equation="{Binding Equation, ElementName=Editor}" />
</StackPanel>
```

### WPF

`SharpEquations.Wpf` draws in `OnRender`, the glyphs of Libertinus Math (embedded) as geometries from their outlines, or
of another math font read from its file (`MathFonts.FromFile(@"C:\Windows\Fonts\cambria.ttc")` for Cambria Math). The
clipboard has the text and the MathML that Word reads. In XAML:

```xml
<StackPanel xmlns:math="clr-namespace:SharpEquations.Wpf;assembly=SharpEquations.Wpf">
    <math:MathEditBox x:Name="Editor" FontSize="28" EquationChanged="OnEquationChanged" />
    <math:MathView FontSize="22" IsDisplay="True" Equation="{Binding Equation, ElementName=Editor}" />
</StackPanel>
```

### Windows Forms

`SharpEquations.WinForms` draws with SkiaSharp rather than GDI+: the glyphs of Libertinus Math (embedded) as paths from
their outlines, or of another math font read from its file (`MathFonts.FromFile`). The height of the editor follows the
equation, as that of a `TextBox` follows its font.

```csharp
var editor = new SharpEquations.WinForms.MathEditBox { FontSize = 28, Width = 600 };
var view = new SharpEquations.WinForms.MathView { FontSize = 22, IsDisplay = true };
editor.EquationChanged += (_, e) => view.Equation = e.Equation;
```

### How it is made

The controls lay out MathML after MathML Core with the OpenType MATH table of a math font. The editor is three layers,
so that the core keeps no dependency: `SharpEquations.Editing` (the editing model), `SharpEquations.Rendering` (the
layout, and the view of an editor without a toolkit) and the controls, which are adapters of that view to their
toolkit. The design is in [docs/EDITOR.md](docs/EDITOR.md).

## Demos

| Sample | What it shows | Run |
|---|---|---|
| `samples/SharpEquations.Avalonia.Demo` | The editor with its MathML, UnicodeMath and LaTeX | `dotnet run --project samples/SharpEquations.Avalonia.Demo` |
| `samples/SharpEquations.Avalonia.Browser.Demo` | The same in the browser, on WebAssembly (published to GitHub Pages from `main`) | `dotnet workload install wasm-tools`, then `dotnet run --project samples/SharpEquations.Avalonia.Browser.Demo` |
| `samples/SharpEquations.Uno.Demo` | A page in XAML with the editor and a view, on the desktop | `dotnet run --project samples/SharpEquations.Uno.Demo` |
| the same, on WinUI 3 (Windows) | | `dotnet run --project samples/SharpEquations.Uno.Demo -f net10.0-windows10.0.19041` |
| `samples/SharpEquations.Wpf.Demo` | A window in XAML with the editor and a view (Windows) | `dotnet run --project samples/SharpEquations.Wpf.Demo` |
| `samples/SharpEquations.WinForms.Demo` | A form with the editor and a view (Windows) | `dotnet run --project samples/SharpEquations.WinForms.Demo` |
| `samples/SharpEquations.Avalonia.MathTools.Demo` | Math tools around the editor | `dotnet run --project samples/SharpEquations.Avalonia.MathTools.Demo` |
| `samples/SharpEquations.Avalonia.Browser.MathTools.Demo` | The math tools in the browser (published under `math/` of the same site) | as the web demo |

The math tools are a second app, in MVVM with views in AXAML: a scientific keypad that types into the editor and
computes what is typed with [AngouriMath](https://github.com/asc-community/AngouriMath), a library of formulas, a unit
converter, a solver of triangles, graphs in the plane and in space, and a worksheet that keeps what was computed. What
they compute is in `samples/SharpEquations.MathTools.Core` and `samples/SharpEquations.MathTools.Graphing`, which have
no user interface. They are samples, not packages.

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
.NET Framework 4.8.1) and macOS (.NET 10), and packs the NuGet packages (the converter and the six of the editor, with one version). The release version is
`VersionPrefix` in `BuildVersion.props`: bump it, and merging to `main` tags `vX.Y.Z`, creates the GitHub release with
the packages attached, and publishes them to GitHub Packages and to
nuget.org (with [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing): the repository
variable `NUGET_USER` names the nuget.org account; without it nuget.org is skipped). A push that keeps the version on
purpose says `[no-new-version]` in its commit message. Every pull request gets a `pr-<N>-…` pre-release, with the
package versioned `X.Y.Z-pr.<N>.<run>`, replaced on each push and deleted when the pull request closes. Dependabot keeps
the GitHub Actions current and Renovate the NuGet dependencies.

## License

MIT (see `LICENSE`). Third-party files (the TEI stylesheets, the ports of UnicodeMathML and of the mathtype gem, the
code Parlot generates, test data) keep their licences: see `NOTICE`.
