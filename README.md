# SharpEquations

Equations converted between MathML, OMML (Word), MTEF (MathType and Equation Editor 3.0), UnicodeMath (Word's linear
format) and LaTeX, in .NET, with MathML as the common representation.

| Format | Read | Write |
|---|---|---|
| MathML | ✓ | ✓ |
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

The tests use [TUnit](https://tunit.dev/); they run on .NET 10 against the .NET 10 build, and on .NET 8 against the
.NET Standard 2.0 build. Their data is in `tests/data` (see its README).

## License

MIT (see `LICENSE`). Third-party files (the TEI stylesheets, the ports of UnicodeMathML and of the mathtype gem, the
code Parlot generates, test data) keep their licences: see `NOTICE`.
