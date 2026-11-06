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
