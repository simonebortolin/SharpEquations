# Equation test data

`omml/*.omml`: equations written by Microsoft Word, taken from the test suite of
[scienceai/omml2mathml](https://github.com/scienceai/omml2mathml) (commit
57ce40c7a114dcdabc4858fdf281a5fb806aac89, `test/fixtures`), licensed under the Apache License 2.0.
Unused namespace declarations were removed.

`mtef/records/mathtype3`, `mtef/records/mathtype5`: the "Equation Native" streams (`*.eqn`) of Equation Editor
3.0 and MathType OLE objects, taken out of the OLE objects, with the XML that https://github.com/jure/mathtype (commit
80413caa2b57d957a3c4ec20c2a681606a937c92, `spec/fixtures`) reads from them (`*.xml`, whitespace removed).
Copyright (c) 2015 Jure Triglav, MIT License.

`mtef/mathml`: the "Equation Native" streams (`*.eqn`) of MathType OLE objects and reference MathML (`*.xml`, whitespace removed) from
https://github.com/jure/mathtype_to_mathml (commit db2030b16be7e429340b1b53eb615471441ed3d4,
`spec/fixtures`). Some of the reference files were written by MathType's own MathML translator and differ
in structure from what the stylesheets produce. Copyright (c) 2015 Jure Triglav, MIT License.

`mtef/poi`: three "Equation Native" streams (`*.eqn`) of Equation Editor 3.0 objects of the Word documents
`Bug61268.doc` (`ObjectPool/_1393240891`, `_1393240981`) and `Bug50936_1.doc` (`ObjectPool/_1018193555`) of the test
data of [Apache POI](https://github.com/apache/poi) (commit e6a04b49211e23c704fcdbe524d99d2f4486b083,
`test-data/document`), licensed under the Apache License 2.0, as the fixtures of
[Mengfei-Li/document-equation-migration](https://github.com/Mengfei-Li/document-equation-migration) (commit
4024fe5a419b0f35971b57228e00dba163cfc898, `tests/fixtures/equation_editor_3_ole/apache_poi`) took them out of the
documents. `bug50936_1-0013.eqn` holds big summation operators with both limits (template selector 43, variation 2),
which that project leaves unsupported.

`mtef/invalid`: payloads that only look like MTEF, from the same fixtures of document-equation-migration
(`equation_editor_3_ole/equation3_native_payload.hex`, `equation_editor_3_ole/mathtype_like_payload.hex`, and the
"Equation Native" stream of `mathtype_ole/main_story/word/embeddings/oleObject1.bin.b64`), decoded: a zeroed header
and the first bytes of an MTEF 3 or MTEF 5 header followed by text, and a stream that names MTEF in text. Copyright (c)
2026 Document Equation Migration contributors, MIT License.

`mtef/metaphor`: MathType OLE equation streams (`quadratic.eqn`, `fraction.eqn`, `matrices.eqn`) from the test resources
of [danielrendall/Metaphor](https://github.com/danielrendall/Metaphor/tree/master/code/src/test/resources/ole)

`mtef/metaforml`: the 23 Equation Native streams (`*.eqn`) extracted from the OLE files in `word2000/` (MathType 5.2
and 6.7) and `word2010/` (MathType 6.7) of the test resources of [Sukii/Metaforml](https://github.com/Sukii/Metaforml).
`expected-MathML/` contains one MathML reference for each stream. The Word 2010 references for objects 1–7 come from
upstream; objects 8–9 were authored here. The 14 Word 2000 references were captured from SharpEquations' normalized
output to complete the set. The upstream checkout contains no license file; its `CREDITS` names Daniel Rendall,
Thilo Planz, and Javier Fernandez.

`mtef/go`: two "Equation Native" streams extracted from MathType OLE objects supplied by the user and attributed to the
Go project in the pasted source. `expected-MathML/` contains the corresponding MathML produced by SharpEquations.

`mtef/npoi`: seven "Equation Native" streams (`*.eqn`) of the MathType objects of `equations-ole-all.doc` of the test
data of NPOI (`testcases/test-data/document`), one paragraph of MathType's palettes each: `overlines` (horizontal
strikes), `operators`, `symbols` (ℓ, ℑ, ℜ, ℘, ℏ, ℵ...), `fraction-and-rad` (with two long divisions), `bra-ket`,
`parentheses` and `nested-fences` (angle brackets as MTEF writes them, U+2329 and U+232A), extracted from the
`ObjectPool` of the document.

`mtef/mtef-rs`: the "Equation Native" stream extracted from the MathType OLE object with the duplicate filename,
supplied by the user and attributed to [hiro93/mtef-rs](https://github.com/hiro93/mtef-rs).
`expected-MathML/` contains the MathML produced by SharpEquations.

`omml/` files with a descriptive name (`style-bold.omml`, `nary-no-limits.omml`, ...): written for SharpEquations,
reduced by hand to the OMML constructs found in equations saved by Word and missing from the files above.

`unicodemath/unicodemathml-examples.json`: the UnicodeMath expressions of `dist/example.html` of
[MurrayIII/UnicodeMathML](https://github.com/MurrayIII/UnicodeMathML) (commit 636dc8f), with the MathML its
author expects for each (the `results` table of `dist/unicodemathml-integration.js`), as
`{input, display, mathml}`. Copyright (c) 2019 Noah Doersing, MIT License; maintained by Murray Sargent.

`unicodemath/unicodemathml-testmml.json`: the `mathML`, `unicodeMath` and `mathTeXs` tables of `test/testmml.js` of
[MurrayIII/UnicodeMathML](https://github.com/MurrayIII/UnicodeMathML) (commit 636dc8f), as `{mathml, unicodemath, tex}`:
MathML with the UnicodeMath and the TeX its author expects for it. Copyright (c) 2019 Noah Doersing, MIT License;
maintained by Murray Sargent.

`contentmathml/ctop-*.mml`: the 40 equations of `ctop/tests/ctopxslt.html` of
[davidcarlisle/web-xslt](https://github.com/davidcarlisle/web-xslt) (commit 64a32a4a4f78529010f548524f3acc7241be54d5),
one file each, as they are there: Content MathML, some of it mixed with presentation MathML. Copyright David Carlisle,
MIT License.

`latex/latex2mathml-converter.json`: the LaTeX of the `test_converter` and `test_converter_inline` cases of
`tests/test_converter.py` of [roniemartinez/latex2mathml](https://github.com/roniemartinez/latex2mathml) (commit
b9ee564589d79c3b7bfaf4bb920962745abbb694), with the MathML of their snapshots (`tests/__snapshots__/test_converter`,
character references resolved), as `{id, latex, display, mathml}`; `latex/latex2mathml-macros.json`: the `test_macro`
cases, as `{id, latex, equivalent, display}`. Copyright (c) 2016 Ronie Martinez, MIT License. Nine snapshots were
corrected where latex2mathml writes MathML that is not what the LaTeX means: the `~` of `\text{ Hello~World }` and of
`\textbf`, `\textit`, `\textrm`, `\textsf`, `\texttt` is a no-break space, not a tilde (`tilde-and-space-in-text`,
`textbf`...); the superscript of `( - 25 ) ^ { 2 }` is that of the parenthesized group, not of `)` (`issue-42`);
`\omicron` is a letter, not an operator (`omicron`); the limit of `\lim` goes under it in a display equation
(`limit-at-plus-infinity`). Ten more were corrected for the
fonts of mathematics, which do not reach the text of `\text` (in the text font): `var` is plain text in `mathbf`,
`mathit`, `mathcal`, `calligraphic-mode`, `mathscr`, `mathfrak`, `mathtt`, `mit` and `blackboard-bold`, and the `1`
of `\mathsf{1}` is sans-serif (`mathsf`). Seven more were corrected for the operator names of amsmath: `\liminf`,
`\limsup`, `\injlim` and `\projlim` are one name with the thin space of TeX between its words (`lim\,inf`), where
latex2mathml writes an operator with a six-per-em space (`liminf`, `limsup`, `injlim`, `projlim`); `\varinjlim` is lim
with an arrow under it, not inj lim (`varinjlim`); the limit of `\operatorname*` and of `\operatornamewithlimits` goes
under the name in a display equation (`operatorname-star`, `operatornamewithlimits`).

`mathml/mml2tex.json`: the scenarios of `xspec/*.xspec` of [transpect/mml2tex](https://github.com/transpect/mml2tex)
(commit 9458a7a2f5125609c92eb178fcd0f8b8419e2c40), MathML with the LaTeX mml2tex writes for it, and the MathML of
`example/*.xml`, which has none, as `{source, label, mathml, tex}` (namespace prefixes, processing instructions and
whitespace removed). Copyright (c) 2015, transpect.io, BSD 2-Clause License.

`latex/treeblood-mhchem.json`: the LaTeX of the `chemistry` cases of `test/TESTCASES.yaml` of
[Wyatt915/treeblood](https://github.com/Wyatt915/treeblood) (commit 83ddf845ee4aafc040ea4639dae0f8536aeda071), the
`\ce` formulas of mhchem, as a list of strings; their MathML, which treeblood writes in its own form, is not kept.
Copyright (c) 2024 Wyatt Sheffield, MIT License.

`mathtools/cas.json`: the reference cases of the computer algebra of the math tools, written for this repository: an
action (`Evaluate`, `Solve`, `Simplify`, `Expand`, `Factor`, `Differentiate`, `Integrate`), its input in UnicodeMath,
the letter it is done by (`variable`, optional) and the expected values in UnicodeMath (the solutions, for `Solve`),
which `SharpEquations.MathTools.Tests` compares by their value, not by how they are written. A case with `pending` is a
goal the engine does not reach yet, and says why; it is skipped until it passes.

`mathtools/units.json`: conversions of the unit converter, with the exact factors of NIST SP 811 (2008), Appendix B,
and of the SI Brochure (9th edition) for the defined constants.
