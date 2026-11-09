# AGENTS.md

Guidance for AI agents and new contributors working in this repository.

## Project Overview

SharpEquations converts equations between MathML, OMML (the equations of Word), MTEF (MathType and Equation Editor
3.0, read only), UnicodeMath (Word's linear format) and LaTeX, with presentation MathML as the common representation.
It targets netstandard2.0 and net10.0 and depends on nothing but the .NET base library (and System.Memory, for the
generated parser, on netstandard2.0): keep it so, and keep containers and other packages out of it.

| Directory | Content |
|---|---|
| `src/SharpEquations/` | The library: `Public/` (the public API), `Internal/` (one folder per format), `Resources/` (XSLT stylesheets) |
| `src/SharpEquations.RegexPolyfill/` | Build time only: the `[GeneratedRegex]` source generator of the netstandard2.0 build |
| `tests/SharpEquations.Tests/` | TUnit tests, in folders by subject (PublicApi, MathML, Omml, Mtef, UnicodeMath, LaTeX, Property, Regression) |
| `tests/data/` | Test data and its provenance (`tests/data/README.md`) |

## Code Style

- 4 spaces for indentation; `var` when the type is clear from the right-hand side
- No comments unless they explain non-obvious logic; XML documentation (`/// <summary>`) for the public API
- Nullable reference types as the surrounding code uses them
- Text is handled as Unicode code points, never as UTF-16 `char`s: use `System.Text.Rune` (on .NET Standard 2.0, which
  lacks it, the internal polyfill of `Internal/Text/Rune.cs`, with the members of `Rune` the library uses: add there
  the ones it starts to use) and the helpers of `CodePoints` (`Of`, `IsSingle`, `Contains`)
- Regular expressions are `[GeneratedRegex]` partial methods, never `new Regex` or the static `Regex` methods: .NET
  generates them on net10.0, `SharpEquations.RegexPolyfill` (an analyzer of the netstandard2.0 build, which .NET
  Standard lacks the generator for) as a `Regex` built once from the pattern, the options and the timeout. A `Regex`
  with a constant pattern fails the build (SYSLIB1045, an error in `src/`, reported by .NET on net10.0 and by the
  polyfill on netstandard2.0); a search for one character is `CodePoints.Contains`, not a regular expression
- `switch` (statements and expressions) and pattern matching (`case "(" or "[":`, `>= 0x1D434 and <= 0x1D44D =>`,
  `case var _ when Set.Contains(ch):`) rather than chains of `else if` on one value
- An `if` is never followed directly by an unrelated `if`/`else if` chain, which reads as if the chain continued it:
  either it belongs to the chain (`else if`, or a case of the `switch`), or a blank line separates them
- The code ported line by line (the translator of UnicodeMath, `LaTeXReader` from TeX2UnicodeMath) keeps the structure
  of the original, so that it can be compared with it: only blank lines are added there
- The public API (`Public/`) does not change without a reason that is the user's to give

## Architecture

The public API is small: `Equation` (immutable, `IEquatable`, `IFormattable`: `Equation.Parse(source, EquationFormat,
EquationOptions?)` from a string, an `XElement` or MTEF bytes, `TryParse`, `ToMathML()`, `ToOmml()`,
`ToString(EquationFormat)`, `$"{equation:L}"`), `EquationConverter` (`Convert`/`TryConvert` between any two formats,
`CanRead`/`CanWrite`), `EquationFormat` (`MathML`, `Omml`, `Mtef`, `UnicodeMath`, `LaTeX`), `EquationOptions` (an
init-only record: `Display`, `Strict`, `PreserveFormatting`, `LaTeXFlavor` standard or Word, `LaTeXMacros`) and
`MathMLNamespaces`. Everything else is internal.

- `EquationPipeline` picks the parser of a format by pattern matching on the format and the source;
  `EquationSerializer` picks the serializer.
- `MathMLParser.Normalize` gives every equation the one form an `Equation` holds and compares: MathML namespace without
  prefixes, no `mfenced`, `a` or invisible fences, no formatting unless kept, intents with plain letters, letters as
  MathML Core writes them (`<mi>x</mi>` for an italic letter, `𝐲` rather than `mathvariant="bold"`, plain letters with
  `mathvariant="normal"` for upright ones), no `mrow` that groups nothing more than its parent, underbars as
  `menclose notation="bottom"`. The default intents (`:fenced`, `:function`, `absolute-value`,
  `binomial-coefficient`, the matrices...) are not kept, since they would make an equation differ by how it was
  written (`\binom`, `⒞`, `(¦)`); `DefaultIntents.Restore` infers them again from the structure for the LaTeX and
  UnicodeMath writers. Content MathML of the source (`apply`, `ci`, `cn`..., outside the annotations) is first rewritten
  as presentation MathML by `ctop.xsl` (David Carlisle's, see `NOTICE`), keeping the presentation MathML around it.
- `Internal/Omml/`: OMML ↔ MathML with the XSLT 1.0 stylesheets of `Resources/` (from the TEI Stylesheets, see
  `NOTICE`), run with `XslCompiledTransform`. OMML is written as Word writes it (plain letters with `m:sty`/`m:scr`,
  underbars as `m:bar`), and an upright word of OMML (`max`) is read as one identifier.
- `Internal/Mtef/`: MTEF of Equation Editor 3.0 and MathType, read only; a C# port of the jure/mathtype gem, converted
  with `mtef2mml.xsl`. It reads the "Equation Native" stream of the OLE object; opening the OLE object is the caller's.
- `Internal/UnicodeMath/`: a port of UnicodeMathML. Its PEG grammar is `UnicodeMathGrammar.parlot.cs`, compiled at
  build time by Parlot.SourceGenerator (the generated parser goes to `obj/`, never to the repository; Parlot has no
  memoization, so alternatives that share a prefix parse it once; the generator compiles the whole project, so any C#
  error shows as PARLOT001). Its JavaScript translator is ported line by line over `JsObject`, with strings indexed by
  code points (`Js.Ch`, `Js.Len`, `Js.Sub`) where the JavaScript indexes UTF-16 units; Parlot parses `char`s, so the
  input maps the characters outside the basic plane to the private use area first (`MapToPrivate`). The writer checks
  what it writes by reading it back.
- `Internal/LaTeX/`: LaTeX is read as Word reads it, through UnicodeMath (`LaTeXReader`, a port of TeX2UnicodeMath, with
  `RewriteTeXOnly` for the commands UnicodeMath has no control word for, and for what LaTeX means otherwise than
  UnicodeMath: a slash is a slash, not a fraction, and a bar without its pair is a bar, not an absolute value), and
  written as standard LaTeX or as the LaTeX Word writes (`LaTeXSerializer`).

## Building and Testing

```bash
dotnet build SharpEquations.slnx
dotnet test --solution SharpEquations.slnx
```

The tests run on net10.0 against the net10.0 build, and against the netstandard2.0 build on net8.0 (Linux, macOS) or
net481 (Windows, where the test project targets net10.0 and net481). The 738 examples of
`tests/data/unicodemath/` test the UnicodeMath port, and `Property/RoundTripPropertyTests` checks, for all of them and
for the equations of Word documents (`tests/data/omml/`), that an equation written in its own format (UnicodeMath,
OMML) reads back to the same equation, and that one written in another format (standard LaTeX, Word's LaTeX, OMML)
loses only the first time what that format cannot hold. Keep these at 100%.

## Releases

`.github/workflows/ci.yml`, the same pipeline as SharpMediaInfo and Letra200bSharp: it tests on Linux (net10.0, net8.0),
Windows (net10.0, net481) and macOS (net10.0), packs, publishes a `pr-<N>-…` pre-release for each pull request (deleted on close by
`prerelease-cleanup.yml`) and, on `main`, tags `v<VersionPrefix>` of `BuildVersion.props`, creates the GitHub release
and pushes the package to GitHub Packages and nuget.org (Trusted Publishing, repository variable `NUGET_USER`). A change
that should release bumps `VersionPrefix`; one that should not says `[no-new-version]` in its commit message.
