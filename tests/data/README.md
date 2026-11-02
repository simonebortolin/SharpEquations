# Equation test data

`omml/*.omml`: equations written by Microsoft Word, taken from the test suite of
[scienceai/omml2mathml](https://github.com/scienceai/omml2mathml) (commit
57ce40c7a114dcdabc4858fdf281a5fb806aac89, `test/fixtures`), licensed under the Apache License 2.0.
Unused namespace declarations were removed.

`mtef/records/mathtype3`, `mtef/records/mathtype5`: Equation Editor 3.0 and MathType OLE objects
(`*.bin`) with the XML that https://github.com/jure/mathtype (commit
80413caa2b57d957a3c4ec20c2a681606a937c92, `spec/fixtures`) reads from them (`*.xml`, whitespace removed).
Copyright (c) 2015 Jure Triglav, MIT License.

`mtef/mathml`: MathType OLE objects (`*.bin`) and reference MathML (`*.xml`, whitespace removed) from
https://github.com/jure/mathtype_to_mathml (commit db2030b16be7e429340b1b53eb615471441ed3d4,
`spec/fixtures`). Some of the reference files were written by MathType's own MathML translator and differ
in structure from what the stylesheets produce. Copyright (c) 2015 Jure Triglav, MIT License.

`omml/` files with a descriptive name (`style-bold.omml`, `nary-no-limits.omml`, ...): written for SharpEquations,
reduced by hand to the OMML constructs found in equations saved by Word and missing from the files above.
