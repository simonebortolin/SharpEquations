// Build-time only: Parlot.SourceGenerator compiles this grammar into UnicodeMathGrammar.TryParse. It is a port of
// src/unicodemathml-parser.pegjs of UnicodeMathML (MIT, see NOTICE): the rules keep their names and the order of
// their alternatives, and build the same syntax tree as PEG.js (lists for sequences and repetitions).

namespace SharpEquations.Internal.UnicodeMath
{
    using System.Collections.Generic;
    using System.Linq;
    using Parlot.Fluent;
    using Parlot.SourceGenerator;
    using static Parlot.Fluent.Parsers;
    using static SharpEquations.Internal.UnicodeMath.Js;
    using static SharpEquations.Internal.UnicodeMath.UnicodeMathGrammarActions;

    internal static partial class UnicodeMathGrammar
    {
        [GenerateParser(nameof(TryParse))]
        private static Parser<object> Build()
        {
            // literals and single characters as strings, as PEG.js returns them
            Parser<object> L(string text) => Literals.Text(text).Then(x => (object)x);
            Parser<object> Any(string chars) => Literals.AnyOf(chars, 1, 1).Then(x => (object)x.ToString());
            // e? of PEG.js: the value, or null when e does not match
            Parser<object> Opt(Parser<object> parser) => OneOf(parser, Always<object>(null));

            var exp = Deferred<object>();
            var element = Deferred<object>();
            var operand = Deferred<object>();
            var factor = Deferred<object>();
            var sfactor = Deferred<object>();
            var soperand = Deferred<object>();
            var entity = Deferred<object>();
            var script = Deferred<object>();
            var scriptU = Deferred<object>();
            var abscript = Deferred<object>();
            var scriptbase = Deferred<object>();
            var basicOperator = Deferred<object>();
            var rawOperator = Deferred<object>();
            var subsupScript = Deferred<object>();
            var abovebelowScript = Deferred<object>();
            var abovebelowAbove = Deferred<object>();
            var abovebelowBelow = Deferred<object>();
            var functionName = Deferred<object>();
            var text = Deferred<object>();
            var primed = Deferred<object>();
            var unicodeSub = Deferred<object>();
            var unicodeSup = Deferred<object>();
            var arows = Deferred<object>();

            // helper rules
            var bitmask = Literals.Pattern(c => UnicodeMathChars.IsAsciiDigit(c), 1).Then(x => (object)ParseInt(x.ToString()));
            var _ = L(" ");
            var __ = OneOrMany(Literals.Char(' ')).Then(w => (object)new string(' ', w.Count));
            var anyChar = Literals.Pattern(c => true, 1, 1).Then(x => (object)x.ToString());

            // Unicode subscripts
            var nUnicodeSub = Opt(Any("₊₋")).And(Literals.Pattern(c => "₀₁₂₃₄₅₆₇₈₉".IndexOf(c) >= 0, 1))
                .Then(t =>
                {
                    var n = O("number", ReplaceMap("₀₁₂₃₄₅₆₇₈₉", "0123456789", t.Item2.ToString()));
                    return t.Item1 != null ? (object)A(O("operator", ReplaceMap("₊₋", "+-", t.Item1)), n) : n;
                });
            var atomsUnicodeSub = Literals.Pattern(c => "ₐₑₕᵢⱼₖₗₘₙₒₚᵣₛₜᵤᵥₓᵦᵧᵨᵩᵪ".IndexOf(c) >= 0, 1)
                .Then(a => (object)O("atoms", O("chars", ReplaceMap("ₐₑₕᵢⱼₖₗₘₙₒₚᵣₛₜᵤᵥₓᵦᵧᵨᵩᵪ", "aehijklmnoprstuvxβγρθφ", a.ToString()))));
            var opUnicodeSub = OneOf(
                L("₊₋").Then(o => (object)O("operator", "±")),
                L("₋₊").Then(o => (object)O("operator", "∓")),
                Any("₊₋₌").Then(o => (object)O("operator", ReplaceMap("₊₋₌", "+-=", o))));
            var factorUnicodeSub = OneOf(
                atomsUnicodeSub,
                nUnicodeSub,
                L("₍").SkipAnd(unicodeSub).AndSkip(L("₎"))
                    .Then(e => (object)O("bracketed", O(("open", "("), ("close", ")"), ("content", e)))));
            var elementUnicodeSub = OneOrMany(factorUnicodeSub).Then(x => (object)x.ToList());
            unicodeSub.Parser = OneOrMany(OneOf(elementUnicodeSub, opUnicodeSub)).Then(ex => (object)O("expr", ex.ToList()));

            // Unicode superscripts
            var nUnicodeSup = Opt(Any("⁺⁻")).And(Literals.Pattern(c => "⁰¹²³⁴⁵⁶⁷⁸⁹".IndexOf(c) >= 0, 1))
                .Then(t =>
                {
                    var n = O("number", ReplaceMap("⁰¹²³⁴⁵⁶⁷⁸⁹", "0123456789", t.Item2.ToString()));
                    return t.Item1 != null ? (object)A(O("operator", ReplaceMap("⁺⁻", "+−", t.Item1)), n) : n;
                });
            var atomsUnicodeSup = OneOf(
                Literals.Pattern(c => "ᴬᴮꟲᴰᴱꟳᴳᴴᴵᴶᴷᴸᴹᴺᴼᴾᴿꟴᵀᵁⱽᵂᵃᵇᶜᵈᵉᶠᵍʰⁱʲᵏˡᵐⁿᵒᵖʳˢᵗᵘᵛʷˣʸᶻᵅᵝᵞᵟᶿᵠᵡ".IndexOf(c) >= 0, 1)
                    .Then(a => (object)O("atoms", O("chars", ReplaceMap("ᴬᴮꟲᴰᴱꟳᴳᴴᴵᴶᴷᴸᴹᴺᴼᴾꟴᴿᵀᵁⱽᵂᵃᵇᶜᵈᵉᶠᵍʰⁱʲᵏˡᵐⁿᵒᵖʳˢᵗᵘᵛʷˣʸᶻᵅᵝᵞᵟᶿᵠᵡ",
                        "ABCDEFGHIJKLMNOPQRTUVWabcdefghijklmnoprstuvwxyzαβγδθφχ", a.ToString())))),
                L("𐞥").Then(a => (object)O("atoms", O("chars", "q"))));
            var opUnicodeSup = OneOf(
                L("⁺⁻").Then(o => (object)O("operator", "±")),
                L("⁻⁺").Then(o => (object)O("operator", "∓")),
                Any("⁺⁻⁼").Then(o => (object)O("operator", ReplaceMap("⁺⁻⁼", "+−=", o))));
            var factorUnicodeSup = OneOf(
                atomsUnicodeSup,
                nUnicodeSup,
                L("⁽").SkipAnd(unicodeSup).AndSkip(L("⁾"))
                    .Then(e => (object)O("bracketed", O(("open", "("), ("close", ")"), ("content", e)))));
            var elementUnicodeSup = OneOrMany(factorUnicodeSup).Then(x => (object)x.ToList());
            unicodeSup.Parser = OneOrMany(OneOf(elementUnicodeSup, opUnicodeSup)).Then(ex => (object)O("expr", ex.ToList()));

            // lexer-ish rules
            var nASCII = Literals.Pattern(c => UnicodeMathChars.IsAsciiDigit(c), 1, 1).Then(x => (object)x.ToString());
            var nn = Literals.Pattern(c => UnicodeMathChars.IsNn(c), 1, 1).Then(x => (object)x.ToString());
            var alphaAscii = Literals.Pattern(c => UnicodeMathChars.IsAsciiLetter(c), 1, 1).Then(x => (object)x.ToString());
            var alphaN = Literals.Pattern(c => UnicodeMathChars.IsAlphaN(c), 1, 1).Then(x => (object)x.ToString());
            var diacritic = Literals.Pattern(c => UnicodeMathChars.IsDiacritic(c), 1, 1).Then(x => (object)x.ToString());
            var alphaNOrNumber = Opt(Any("-−+")).And(OneOf(
                    OneOrMany(alphaN).Then(x => (object)x.ToList()),
                    Literals.Pattern(c => UnicodeMathChars.IsAsciiDigit(c), 1).Then(x => (object)x.ToString().Select(c => (object)c.ToString()).ToList()),
                    L("∞")))
                .Then(t => (object)A(t.Item1, t.Item2));
            var unicodeFraction = Literals.Pattern(c => UnicodeMathChars.IsUnicodeFraction(c), 1, 1).Then(x => (object)x.ToString());
            var opEnclosedMatrix = Any("⒨⒩ⓢⓈ⒱");
            var opAbs = Any("⒜ⓒ");
            var opOpen = Any("([{⟨〖⌈⌊❲⟦⟨⟪⟬⟮⦃⦅⦇⦉⦋⦍⦏⦑⦓⦕⦗⧘⧚⧼");
            var opClose = Any(")}⟩〗⌉⌋❳⟧⟩⟫⟭⟯⦄⦆⦈⦊⦌⦎⦐⦒⦔⦖⦘⧙⧛⧽]");
            var opDecimal = Any(".,");
            var opHbracket = Any("⏜⏝⏞⏟⏠⏡⎴⎵¯");
            var opNary = Literals.Pattern(c => UnicodeMathChars.IsOpNary(c), 1, 1).Then(x => (object)x.ToString());
            var opNaryand = L("▒");
            var opAtop = L("¦");
            var opChoose = L("⒞");
            var opFraction = Any("/⁄∕⊘");
            var opEnclosure = Any("▭\u0304▁▢◯⟌\u20E7⬭╱╲╳ⓓⓑ");
            var opEqNumber = L("#");
            var opPhantom = Any("⟡⬄⇳");
            var opSmash = Any("⬍⬆⬇⬌");
            var opAbstractBox = L("□");
            var opRoot = Any("√∛∜");
            var opSelection = Any("ⒶⒻ");
            var opScript = Literals.Pattern(c => UnicodeMathChars.IsOpScript(c), 1, 1).Then(x => (object)x.ToString());
            var opSubSupEtc = Any("_^)");
            var opSizeOverride = L("Ⅎ");
            var opColor = L("✎");
            var opBgColor = L("☁");
            var opCommentOpen = L("⫷");
            var opCommentClose = L("⫸");
            var opTt = L("ￗ");
            var other = Literals.Pattern(c => UnicodeMathChars.IsOther(c), 1, 1).Then(x => (object)x.ToString());

            // math spaces
            var mathspace = Literals.Pattern(c => UnicodeMathChars.IsMathSpace(c), 1, 1).Then(x => (object)x.ToString());
            var mathspaces = OneOrMany(mathspace).Then(s => (object)O("spaces", s.ToList()));

            // operators
            var mappedOperator = OneOf(
                L("*").Then(x => (object)"∗"),
                L("-+").Then(x => (object)"∓"),
                L("-").Then(x => (object)"−"),
                L("+-").Then(x => (object)"±"),
                L("<=").Then(x => (object)"≤"),
                L(">=").Then(x => (object)"≥"),
                L("~=").Then(x => (object)"≅"),
                L("~~").Then(x => (object)"≈"),
                L("::").Then(x => (object)"∷"),
                L(":=").Then(x => (object)"≔"),
                L("<<").Then(x => (object)"≪"),
                L(">>").Then(x => (object)"≫"),
                L("->").Then(x => (object)"→"),
                L("!!").Then(x => (object)"‼"),
                L("...").Then(x => (object)"…"),
                L("⋅⋅⋅").Then(x => (object)"⋯"));
            rawOperator.Parser = OneOf(mappedOperator, Not(L("\"")).SkipAnd(other));
            var negatedOperator = OneOf(
                rawOperator.AndSkip(L("\u0338")).Then(o => (object)O("negatedoperator", o)),
                L("/").SkipAnd(rawOperator).Then(o => (object)O("negatedoperator", o)));
            basicOperator.Parser = OneOf(negatedOperator, rawOperator.Then(o => (object)O("operator", o)));
            var operatorRule = OneOf(
                L("\u2007").Then(x => (object)O("operator", "\u2007")),
                mathspaces,
                L(",  ").Then(x => (object)A(O("operator", ","), O("spaces", O("space", 9)))),
                L(" :").Then(x => (object)O("operator", "∶")),
                Not(Not(basicOperator)).SkipAnd(OneOf(subsupScript, abovebelowScript)),
                basicOperator);

            exp.Parser = Opt(__).SkipAnd(OneOrMany(OneOf(element, operatorRule).AndSkip(Opt(__))))
                .Then(ex => (object)O("expr", ex.ToList()));

            // equation arrays and matrices
            var emptycell = Always<object>(null).Then(x => (object)O("atoms", O("spaces", O("space", 0))));
            var arow = Opt(__).SkipAnd(Opt(OneOf(exp, emptycell)))
                .And(ZeroOrMany(Opt(__).SkipAnd(L("&")).SkipAnd(Opt(__)).SkipAnd(OneOf(exp, emptycell))))
                .AndSkip(Opt(__))
                .Then(t => (object)O("arow", new[] { t.Item1 }.Concat(t.Item2).ToList()));
            arows.Parser = arow.And(ZeroOrMany(L("@").SkipAnd(arow)))
                .Then(t => (object)O("arows", new[] { t.Item1 }.Concat(t.Item2).ToList()));
            var array = L("█(").SkipAnd(arows).AndSkip(L(")")).Then(r => (object)O("array", r));
            var mrow = Opt(__).SkipAnd(OneOf(exp, emptycell))
                .And(ZeroOrMany(Opt(__).SkipAnd(L("&")).SkipAnd(Opt(__)).SkipAnd(OneOf(exp, emptycell))))
                .AndSkip(Opt(__))
                .Then(t => (object)O("mrow", new[] { t.Item1 }.Concat(t.Item2).ToList()));
            var mrows = mrow.And(ZeroOrMany(L("@").SkipAnd(mrow)))
                .Then(t => (object)O("mrows", new[] { t.Item1 }.Concat(t.Item2).ToList()));
            var matrix = OneOf(
                L("■(").SkipAnd(mrows).AndSkip(L(")")).Then(r => (object)O("matrix", r)),
                opEnclosedMatrix.AndSkip(L("(")).And(mrows).AndSkip(L(")"))
                    .Then(t => (object)O("bracketedMatrix", O(("type", t.Item1), ("content", O("matrix", t.Item2))))));
            var nByMmatrix = nASCII.AndSkip(L("×")).And(nASCII).And(OneOf(L("■"), opEnclosedMatrix))
                .Then(t => (object)O("specialMatrix", A(t.Item1, t.Item2, t.Item3)));
            var identityMatrix = OneOf(L("■"), opEnclosedMatrix).And(nASCII)
                .Then(t => (object)O("specialMatrix", A(t.Item2, 0, t.Item1)));

            // n-ary operations
            var nary = opNary.And(Opt(bitmask)).And(Opt(OneOf(script, abscript))).AndSkip(OneOf(opNaryand, __)).And(element)
                .Then(t =>
                {
                    var s = (JsObject)t.Item3 ?? O("type", "subsup");
                    s["base"] = O("opnary", t.Item1);
                    return (object)O("nary", O(("mask", t.Item2), ("limits", O("script", s)), ("naryand", t.Item4)));
                });

            // phantoms and smashes
            var phantom = OneOf(
                L("⟡(").SkipAnd(bitmask).AndSkip(L("&")).And(exp).AndSkip(L(")"))
                    .Then(t => (object)O("phantom", O(("mask", t.Item1), ("symbol", null), ("of", t.Item2)))),
                opPhantom.AndSkip(L("(")).And(exp).AndSkip(L(")"))
                    .Then(t => (object)O("phantom", O(("mask", null), ("symbol", t.Item1), ("of", t.Item2)))),
                opPhantom.And(operand)
                    .Then(t => (object)O("phantom", O(("mask", null), ("symbol", t.Item1), ("of", t.Item2)))),
                opPhantom.Then(s => (object)O("text", s)));
            var smash = OneOf(
                opSmash.AndSkip(L("(")).And(exp).AndSkip(L(")"))
                    .Then(t => (object)O("smash", O(("symbol", t.Item1), ("of", t.Item2)))),
                opSmash.And(operand)
                    .Then(t => (object)O("smash", O(("symbol", t.Item1), ("of", t.Item2)))),
                opSmash.Then(s => (object)O("text", s)));
            var phantomSmash = OneOf(phantom, smash);

            // fractions
            // operand !(__? (opFraction !rawOperator / opAtop / opChoose)) / fraction / atop, with the first operand
            // parsed once; atop is (operand __? ¦ __?)+ operand / operand __? ⒞ __? operand / operand (opFraction / opChoose)
            // (opFraction operand)+ with each operand parsed once, not again after the opFraction it lacks; a fraction
            // operator with no operand after it fails the tail, as (operand opFraction)* operand fails it
            var fractionStep = Opt(_).SkipAnd(opFraction);
            var fractionTail = fractionStep.And(operand).And(ZeroOrMany(fractionStep.And(operand))).AndSkip(Not(fractionStep))
                .Then(t => (object)FractionTail(t.Item1, t.Item2, t.Item3));
            var atopTail = Opt(__).SkipAnd(opAtop).SkipAnd(Opt(__))
                .SkipAnd(ZeroOrMany(operand.AndSkip(Opt(__)).AndSkip(opAtop).AndSkip(Opt(__)))).And(operand)
                .Then(t => (object)AtopTail(t.Item1, t.Item2));
            var binomTail = Opt(__).SkipAnd(opChoose).SkipAnd(Opt(__)).SkipAnd(operand).Then(b => (object)A("binom", b));
            var operatorTail = OneOf(opFraction, opChoose).Then(o => (object)A("colored", o));
            var operandElement = operand.And(OneOf(
                    Not(Opt(__).SkipAnd(OneOf(opFraction.AndSkip(Not(rawOperator)), opAtop, opChoose))).Then(x => (object)null),
                    fractionTail,
                    atopTail,
                    binomTail,
                    operatorTail))
                .Then(t => OperandElement(t.Item1, (List<object>)t.Item2));

            element.Parser = OneOf(
                array,
                identityMatrix,
                matrix,
                nary,
                nByMmatrix,
                operandElement,
                unicodeFraction.Then(f => (object)O("unicodefraction", f)),
                opNary.Then(o => (object)O("opnary", o)));

            // operands and factors
            operand.Parser = OneOrMany(factor).Then(x => (object)x.ToList());
            var trigName = Opt(L("a")).And(OneOf(L("sin"), L("cos"), L("tan"), L("sec"), L("csc"), L("cot"))).And(Opt(L("h")))
                .Then(t => (object)A(t.Item1, t.Item2, t.Item3));
            var limName = OneOf(L("lim"), L("log"), L("lg"));

            // scripts
            var subU = unicodeSub.Then(b => (object)O("low", b));
            var subL = OneOrMany(L("_").SkipAnd(soperand))
                .Then(b => (object)O("low", NestScripts(b.ToList(), "low")));
            var supU = unicodeSup.Then(p => (object)O("high", p));
            var supL = OneOrMany(L("^").SkipAnd(soperand))
                .Then(p => (object)O("high", NestScripts(p.ToList(), "high")));
            var sub = OneOf(subU, subL);
            var sup = OneOf(supU, supL);
            var subsupL = OneOf(
                subL.And(supL).Then(t => (object)O(("low", Get(t.Item1, "low")), ("high", Get(t.Item2, "high")))),
                supL.And(subL).Then(t => (object)O(("low", Get(t.Item2, "low")), ("high", Get(t.Item1, "high")))));
            var subsupU = OneOf(
                subU.And(supU).Then(t => (object)O(("low", Get(t.Item1, "low")), ("high", Get(t.Item2, "high")))),
                supU.And(subU).Then(t => (object)O(("low", Get(t.Item2, "low")), ("high", Get(t.Item1, "high")))));
            var subsup = OneOf(
                subU.And(OneOf(supU, supL)).Then(t => (object)O(("low", Get(t.Item1, "low")), ("high", Get(t.Item2, "high")))),
                supU.And(OneOf(subU, subL)).Then(t => (object)O(("low", Get(t.Item2, "low")), ("high", Get(t.Item1, "high")))),
                subsupL);
            // subsup / sub / sup with each script parsed once
            script.Parser = OneOf(
                subU.And(Opt(OneOf(supU, supL))).Then(t => SubSup(t.Item1, t.Item2)),
                supU.And(Opt(OneOf(subU, subL))).Then(t => SubSup(t.Item2, t.Item1)),
                subL.And(Opt(supL)).Then(t => SubSup(t.Item1, t.Item2)),
                supL.And(Opt(subL)).Then(t => SubSup(t.Item2, t.Item1)))
                .Then(s => { ((JsObject)s)["type"] = "subsup"; return s; });
            scriptU.Parser = OneOf(subsupU, subU, supU).Then(s => { ((JsObject)s)["type"] = "subsup"; return s; });
            var subsupSubsup = scriptbase.And(subsup).AndSkip(Opt(_))
                .Then(t => { var s = (JsObject)t.Item2; s["base"] = t.Item1; s["type"] = "subsup"; return (object)O("script", s); });
            var subsupSubscript = scriptbase.And(sub).AndSkip(Opt(_))
                .Then(t => { var s = (JsObject)t.Item2; s["base"] = t.Item1; s["type"] = "subsup"; return (object)O("script", s); });
            var subsupSuperscript = OneOf(
                scriptbase.And(supL).AndSkip(Opt(_))
                    .Then(t => { var s = (JsObject)t.Item2; s["base"] = t.Item1; s["type"] = "subsup"; return (object)O("script", s); }),
                scriptbase.And(supU)
                    .Then(t => { var s = (JsObject)t.Item2; s["base"] = t.Item1; s["type"] = "subsup"; return (object)O("script", s); }));
            subsupScript.Parser = OneOf(
                subsupSubsup,
                scriptbase.AndSkip(L("_")).And(scriptbase).AndSkip(L("^"))
                    .Then(t => (object)A(t.Item1, O("operator", "_"), t.Item2, ColoredOperator("^"))),
                subsupSubscript,
                subsupSuperscript);

            var preScript = OneOf(
                L("(").SkipAnd(Opt(__)).SkipAnd(script).AndSkip(Opt(__)).AndSkip(L(")")).And(operand)
                    .Then(t => (object)PreScript((JsObject)t.Item1, t.Item2)),
                scriptU.And(operand)
                    .Then(t => (object)PreScript((JsObject)t.Item1, t.Item2)),
                script.AndSkip(_).And(operand)
                    .Then(t => (object)PreScript((JsObject)t.Item1, t.Item2)));

            var abovebelow = OneOf(
                L("┬").SkipAnd(soperand).AndSkip(L("┴")).And(soperand).Then(t => (object)O(("low", t.Item1), ("high", t.Item2))),
                L("┴").SkipAnd(soperand).AndSkip(L("┬")).And(soperand).Then(t => (object)O(("low", t.Item2), ("high", t.Item1))));
            var above = L("┴").SkipAnd(OneOf(abovebelowAbove, soperand)).Then(p => (object)O("high", p));
            var below = L("┬").SkipAnd(OneOf(abovebelowBelow, soperand)).Then(b => (object)O("low", b));
            abscript.Parser = OneOf(abovebelow, above, below).Then(s => { ((JsObject)s)["type"] = "abovebelow"; return s; });
            var abovebelowAbovebelow = scriptbase.And(abovebelow).AndSkip(Opt(__))
                .Then(t => { var s = (JsObject)t.Item2; s["base"] = t.Item1; s["type"] = "abovebelow"; return (object)O("script", s); });
            abovebelowAbove.Parser = scriptbase.And(above).AndSkip(Opt(_))
                .Then(t => { var s = (JsObject)t.Item2; s["base"] = t.Item1; s["type"] = "abovebelow"; return (object)O("script", s); });
            abovebelowBelow.Parser = scriptbase.And(below).AndSkip(Opt(_))
                .Then(t => { var s = (JsObject)t.Item2; s["base"] = t.Item1; s["type"] = "abovebelow"; return (object)O("script", s); });
            abovebelowScript.Parser = OneOf(abovebelowAbovebelow, abovebelowAbove, abovebelowBelow);

            // subsupScript / abovebelowScript / scriptbase [_^] of factor, with the base parsed once: alternatives
            // that each parse the base again take exponential time on nested brackets
            var scriptSuffix = OneOf(
                subU.And(Opt(OneOf(supU, supL))).AndSkip(Opt(_)).Then(t => (object)A("subsup", SubSup(t.Item1, t.Item2))),
                supU.And(OneOf(subU, subL)).AndSkip(Opt(_)).Then(t => (object)A("subsup", SubSup(t.Item2, t.Item1))),
                subL.And(OneOf(supL, Not(L("^")).SkipAnd(Always<object>(null)))).AndSkip(Opt(_))
                    .Then(t => (object)A("subsup", SubSup(t.Item1, t.Item2))),
                L("_").SkipAnd(scriptbase).AndSkip(L("^")).Then(b => (object)A("subcaret", b)),
                subL.AndSkip(Opt(_)).Then(s => (object)A("subsup", s)),
                supL.And(Opt(subL)).AndSkip(Opt(_)).Then(t => (object)A("subsup", SubSup(t.Item2, t.Item1))),
                supU.Then(s => (object)A("subsup", s)),
                abovebelow.AndSkip(Opt(__)).Then(s => (object)A("abovebelow", s)),
                above.AndSkip(Opt(_)).Then(s => (object)A("abovebelow", s)),
                below.AndSkip(Opt(_)).Then(s => (object)A("abovebelow", s)),
                Any("_^").Then(o => (object)A("trailing", o)));
            // primes
            var prime = OneOf(
                OneOf(L("'"), L("′")).Then(x => (object)1),
                L("″").Then(x => (object)2),
                L("‴").Then(x => (object)3),
                L("⁗").Then(x => (object)4));
            var primedbase = OneOf(entity, basicOperator, opNary.Then(o => (object)O("opnary", o)));
            primed.Parser = primedbase.And(OneOrMany(prime))
                .Then(t => (object)O("primed", O(("base", t.Item1), ("primes", t.Item2.Sum(p => (int)p)))));
            scriptbase.Parser = OneOf(text, primedbase.AndSkip(Not(prime)), primed);

            // scriptbase without entity: an entity is parsed by factorEntity, or by entityFactor in sfactor
            var opBase = OneOf(basicOperator, opNary.Then(o => (object)O("opnary", o)));
            var opPrimed = opBase.And(OneOrMany(prime))
                .Then(t => (object)O("primed", O(("base", t.Item1), ("primes", t.Item2.Sum(p => (int)p)))));
            var nonEntityScriptbase = OneOf(text, opBase.AndSkip(Not(prime)), opPrimed);
            var isFunctionName = Not(Not(functionName));
            var scriptedBase = OneOf(isFunctionName.SkipAnd(scriptbase), nonEntityScriptbase).And(scriptSuffix)
                .Then(t => ScriptedBase(t.Item1, (List<object>)t.Item2));

            soperand.Parser = OneOf(
                OneOrMany(sfactor).Then(x => (object)x.ToList()),
                basicOperator.And(OneOrMany(sfactor)).Then(t => (object)A(t.Item1, t.Item2.ToList())),
                OneOrMany(basicOperator).Then(x => (object)x.ToList()));


            // enclosures and other special constructs
            var exceptAmpersand = Not(L("&")).SkipAnd(anyChar);
            var exceptParen = Not(L(")")).SkipAnd(anyChar);
            var enclosed = OneOf(
                L("▭(").SkipAnd(bitmask).AndSkip(L("&")).And(exp).AndSkip(L(")"))
                    .Then(t => (object)O("enclosed", O(("mask", t.Item1), ("symbol", null), ("of", t.Item2)))),
                L("ⓗ(").SkipAnd(OneOrMany(exceptAmpersand)).AndSkip(L("&")).And(Opt(exp)).AndSkip(L(")"))
                    .Then(t => (object)O("href", O(("url", Join(t.Item1.ToList())), ("name", t.Item2)))),
                L("ⓡ(").SkipAnd(OneOrMany(exceptAmpersand)).AndSkip(L("&")).And(OneOrMany(exceptParen)).AndSkip(L(")"))
                    .Then(t => (object)O("remark", O(("base", Join(t.Item1.ToList())), ("comment", Join(t.Item2.ToList()))))),
                opEnclosure.AndSkip(L("(")).And(exp).AndSkip(L(")"))
                    .Then(t => (object)O("enclosed", O(("mask", null), ("symbol", t.Item1), ("of", t.Item2)))),
                opEnclosure.And(operand)
                    .Then(t => (object)O("enclosed", O(("mask", null), ("symbol", t.Item1), ("of", t.Item2)))),
                opEnclosure.Then(e => (object)O("text", e)),
                L("ⓣ(").SkipAnd(OneOrMany(exceptParen)).AndSkip(L(")"))
                    .Then(o => (object)O("text", Join(o.ToList()))),
                L("⍁").SkipAnd(exp).AndSkip(L("&")).And(exp).AndSkip(L("〗"))
                    .Then(t => (object)O("fraction", O(("symbol", "/"), ("of", A(t.Item1, t.Item2))))),
                L("⒝").SkipAnd(exp).AndSkip(L("&")).And(exp).AndSkip(L("〗"))
                    .Then(t => (object)O("binom", O(("top", t.Item1), ("bottom", t.Item2)))),
                L("ⓗ").SkipAnd(exp).AndSkip(L("&")).And(exp).AndSkip(L("〗"))
                    .Then(t => (object)O("href", O(("url", t.Item1), ("name", t.Item2)))),
                opSelection.AndSkip(L("(")).And(Opt(exp)).AndSkip(L(")")).And(Opt(opSubSupEtc))
                    .Then(t => (object)O("enclosed", O(("mask", t.Item3), ("symbol", t.Item1), ("of", t.Item2)))));
            var abstractbox = opAbstractBox.SkipAnd(L("(")).SkipAnd(bitmask).AndSkip(L("&")).And(exp).AndSkip(L(")"))
                .Then(t => (object)O("abstractbox", O(("mask", t.Item1), ("of", t.Item2))));
            var hbrack = opHbracket.And(operand)
                .Then(t => (object)O("hbrack", O(("bracket", t.Item1), ("of", t.Item2))));
            var intent = L("ⓘ(").SkipAnd(text).AndSkip(Opt(__)).And(exp).AndSkip(L(")"))
                .Then(t => (object)O("intend", O(("op", "ⓘ"), ("intent", t.Item1), ("content", t.Item2))));
            // the first two roots need a & or a ▒ further on the line: without it they would parse the radicand for nothing
            var ampersandAhead = Not(Not(Literals.Pattern(c => c != '&' && c != '\n').Optional().SkipAnd(L("&"))));
            var naryandAhead = Not(Not(Literals.Pattern(c => c != '▒' && c != '\n').Optional().SkipAnd(opNaryand)));
            var root = OneOf(
                ampersandAhead.SkipAnd(L("√(")).SkipAnd(exp).AndSkip(L("&")).And(exp).AndSkip(L(")"))
                    .Then(t => (object)O("root", O(("degree", t.Item1), ("of", t.Item2)))),
                naryandAhead.SkipAnd(OneOf(L("√"), L("⒭"))).SkipAnd(exp).AndSkip(opNaryand).And(operand)
                    .Then(t => (object)O("root", O(("degree", t.Item1), ("of", t.Item2)))),
                L("√").SkipAnd(operand).Then(o => (object)O("sqrt", o)),
                L("∛").SkipAnd(operand).Then(o => (object)O("root", O(("degree", O("number", "3")), ("of", o)))),
                L("∜").SkipAnd(operand).Then(o => (object)O("root", O(("degree", O("number", "4")), ("of", o)))),
                opRoot.Then(o => (object)ColoredOperator(o)));

            var arctrigName = OneOf(L("arcsin"), L("arcsec"), L("arctan"), L("arccot"), L("arccos"), L("arccsc")).And(Opt(L("h")))
                .Then(t => (object)A(t.Item1, t.Item2));
            functionName.Parser = OneOf(
                OneOf(trigName, arctrigName, limName,
                    L("arg"), L("def"), L("deg"), L("det"), L("dim"), L("erf"), L("exp"), L("gcd"), L("ker"), L("hom"),
                    L("Im"), L("inf"), L("ln"), L("max"), L("min"), L("mod"), L("Pr"), L("Re"), L("sup"), L("tg"),
                    L("lcm"), L("lub"), L("glb"), L("lim\u00A0inf"), L("lim\u00A0sup"))
                    .AndSkip(Not(alphaAscii)),
                OneOrMany(alphaN).AndSkip(L("\u2061")).Then(t => (object)t.ToList()));
            var function = OneOf(
                functionName.AndSkip(Opt(__)).AndSkip(Opt(OneOf(L("\u2061"), opNaryand))).And(Opt(script)).AndSkip(Opt(__)).And(operand).AndSkip(Opt(_))
                    .Then(t =>
                    {
                        var c = O("chars", FunctionName(t.Item1, true));
                        c["funct"] = true;
                        if (t.Item2 is JsObject s)
                        {
                            s["base"] = O("atoms", c);
                            return (object)O("function", O(("f", O("script", s)), ("of", t.Item3)));
                        }
                        return (object)O("function", O(("f", O("atoms", c)), ("of", t.Item3)));
                    }),
                functionName.AndSkip(Opt(__)).And(operand).AndSkip(Opt(_))
                    .Then(t => (object)O("function", O(("f", O("atoms", O("chars", FunctionName(t.Item1, false)))), ("of", t.Item2)))),
                // a name marked as a function without an operand (NaN⁡,) is one name, as a function name is (sin⁡,), and not
                // its letters; the function apply sign is read after it (but not one with scripts after the sign, argmax⁡┬(𝑥∈𝑆))
                OneOrMany(alphaN).AndSkip(Not(Not(L("⁡").And(Not(opScript)))))
                    .Then(t => (object)O("atoms", O(("chars", Join(t.ToList())), ("funct", true)))));

            // text, overrides, colors, comments, typewriter
            text.Parser = L("\"").SkipAnd(OneOrMany(OneOf(L("\\\"").Then(x => (object)"\""), Not(L("\"")).SkipAnd(anyChar)))).AndSkip(L("\""))
                .Then(c => (object)O("text", Join(c.ToList())));
            var sizeOverride = opSizeOverride.SkipAnd(Any("ABCD")).And(OneOf(operand, basicOperator))
                .Then(t => (object)O("sizeoverride", O(("size", t.Item1), ("of", t.Item2))));
            var fontOverride = OneOf(
                opSizeOverride.SkipAnd(Literals.Pattern(c => c >= 'a' && c <= 'z', 1, 1)).And(OneOrMany(alphaN))
                    .Then(t => (object)O("fontoverride", O(("font", t.Item1.ToString()), ("of", Join(t.Item2.ToList()))))),
                opSizeOverride.SkipAnd(Literals.Pattern(c => c >= 'a' && c <= 'z', 1, 1)).AndSkip(L("(")).And(exp).AndSkip(L(")"))
                    .Then(t => (object)O("fontoverride", O(("font", t.Item1.ToString()), ("of", t.Item2)))));
            var color = OneOrMany(exceptAmpersand).Then(c => (object)Join(c.ToList()));
            var colored = OneOf(
                opColor.SkipAnd(L("(")).SkipAnd(color).AndSkip(L("&")).And(exp).AndSkip(L(")"))
                    .Then(t => (object)O("colored", O(("color", t.Item1), ("of", t.Item2)))),
                opBgColor.SkipAnd(L("(")).SkipAnd(color).AndSkip(L("&")).And(exp).AndSkip(L(")"))
                    .Then(t => (object)O("bgcolored", O(("color", t.Item1), ("of", t.Item2)))));
            var comment = opCommentOpen.SkipAnd(ZeroOrMany(OneOf(L("\\").SkipAnd(opCommentClose), Not(opCommentClose).SkipAnd(anyChar)))).AndSkip(opCommentClose)
                .Then(c => (object)O("comment", Join(c.ToList())));
            var tt = opTt.SkipAnd(L("(")).SkipAnd(ZeroOrMany(OneOf(L("\\)").Then(x => (object)")"), exceptParen))).AndSkip(L(")"))
                .Then(t => (object)O("tt", Join(t.ToList())));

            var factorial = OneOf(
                entity.AndSkip(OneOf(L("‼"), L("!!"))).Then(e => (object)O("factorial", O("factorial", e))),
                entity.AndSkip(L("!")).Then(e => (object)O("factorial", e)));

            // scriptbase scriptU / entity / primed / factorial of sfactor when the base is an entity, parsed once
            var entityTail = OneOf(
                OneOrMany(prime).And(Opt(scriptU)).Then(t => (object)A("primed", t.Item1.Sum(p => (int)p), t.Item2)),
                scriptU.Then(s => (object)A("script", s)),
                OneOf(L("‼"), L("!!")).Then(x => (object)A("factorial2")),
                L("!").Then(x => (object)A("factorial")),
                Always<object>(null));
            var entityFactor = entity.And(entityTail).Then(t => EntityFactor(t.Item1, (List<object>)t.Item2));

            // entity [!opScript] / scriptbase with scripts / sfactor of factor when the base is an entity (not a
            // function name), parsed once
            var factorEntity = Not(functionName).SkipAnd(entity).And(OneOf(
                    Not(opScript).Then(x => (object)A("plain")),
                    OneOrMany(prime).And(Opt(scriptSuffix)).Then(t => (object)A("primedscript", t.Item1.Sum(p => (int)p), t.Item2)),
                    scriptSuffix,
                    entityTail))
                .Then(t => FactorEntity(t.Item1, (List<object>)t.Item2));

            var sfactorStart = OneOf(
                enclosed,
                phantomSmash,
                abstractbox,
                hbrack,
                intent,
                root,
                function,
                text,
                fontOverride,
                sizeOverride,
                colored,
                comment,
                tt);
            var sfactorEnd = OneOf(
                nonEntityScriptbase.And(scriptU).Then(t => { var s = (JsObject)t.Item2; s["base"] = t.Item1; return (object)O("script", s); }),
                opPrimed,
                L("∞").Then(x => (object)O("atoms", O("chars", "∞"))));
            sfactor.Parser = OneOf(sfactorStart, entityFactor, sfactorEnd);
            var sfactorAfterEntity = OneOf(sfactorStart, isFunctionName.SkipAnd(entityFactor), sfactorEnd);

            factor.Parser = OneOf(
                OneOf(trigName, limName).AndSkip(Not(Not(opScript))).And(script).AndSkip(Opt(__)).AndSkip(Opt(L("\u2061"))).And(operand).AndSkip(Opt(_))
                    .Then(t =>
                    {
                        var c = O(("chars", FunctionName(t.Item1, false)), ("funct", true));
                        var s = (JsObject)t.Item2;
                        s["base"] = O("atoms", c);
                        return (object)O("function", O(("f", O("script", s)), ("of", t.Item3)));
                    }),
                limName.AndSkip(L("┬")).And(operand).AndSkip(Opt(__)).And(operand)
                    .Then(t =>
                    {
                        var c = O(("chars", t.Item1), ("funct", true));
                        var s = O(("base", O("atoms", c)), ("low", t.Item2), ("type", "abovebelow"));
                        return (object)O("function", O(("f", O("script", s)), ("of", t.Item3)));
                    }),
                preScript,
                factorEntity,
                scriptedBase,
                sfactorAfterEntity);

            // atoms, diacritics, numbers
            var diacriticbase = OneOf(
                alphaN.AndSkip(Opt(L("\u00A0"))).Then(c => (object)O("chars", c)),
                nn.Then(n => (object)O("number", n)),
                L("(").SkipAnd(exp).AndSkip(L(")")).AndSkip(Opt(L("\u00A0"))));
            var diacriticized = diacriticbase.And(OneOrMany(diacritic))
                .Then(t => (object)O("diacriticized", O(("base", t.Item1), ("diacritics", t.Item2.ToList()))));
            var atom = OneOf(diacriticized, alphaN.Then(c => (object)O("char", c)), mathspaces);
            var atoms = OneOrMany(atom).Then(a => (object)Atoms(a.ToList()));
            var digits = OneOrMany(nn).Then(n => (object)Join(n.ToList()));
            var number = OneOf(
                opDecimal.And(digits).Then(t => (object)O("number", (string)t.Item1 + (string)t.Item2)),
                digits.And(opDecimal).And(digits).Then(t => (object)O("number", (string)t.Item1 + (string)t.Item2 + (string)t.Item3)),
                digits.Then(a => (object)O("number", a)));

            // brackets
            var interval = alphaNOrNumber.And(L(",")).And(alphaNOrNumber).Then(t => (object)A(t.Item1, t.Item2, t.Item3));
            var opSquareBrackets = OneOf(L("]"), L("["));
            var expBracketOpen = OneOf(
                L("〖").Then(x => (object)""),
                opOpen,
                L("├").SkipAnd(bitmask).And(OneOf(opOpen, opClose)).Then(t => (object)O(("bracket", t.Item2), ("size", t.Item1))),
                L("├").SkipAnd(OneOf(opOpen, opClose, L("|"), L("‖"))),
                L("├").Then(x => (object)""));
            var expBracketClose = OneOf(
                L("〗").Then(x => (object)""),
                opClose,
                L("| ").Then(x => (object)"|"),
                OneOf(L("|| "), L("‖ ")).Then(x => (object)"‖"),
                L("┤").SkipAnd(bitmask).And(OneOf(opOpen, opClose)).Then(t => (object)O(("bracket", t.Item2), ("size", t.Item1))),
                L("┤").SkipAnd(OneOf(opOpen, opClose, L("|"), L("‖"))),
                L("┤").Then(x => (object)""));
            // exp !(│|∣) / (exp │)+ exp / (exp ∣)+ exp, with the first exp parsed once
            var expBracketContentsTail = OneOf(
                Not(OneOf(L("│"), L("∣"))).Then(x => (object)null),
                OneOrMany(L("│").SkipAnd(exp)).Then(e => (object)A("│", e.ToList())),
                OneOrMany(L("∣").SkipAnd(exp)).Then(e => (object)A("∣", e.ToList())));
            var expBracketContents = exp.And(expBracketContentsTail).Then(t => BracketContents(t.Item1, (List<object>)t.Item2));
            // (exp|exp) / ( contents close, with the first exp parsed once
            var parenPipeTail = L("|").SkipAnd(exp).AndSkip(L(")")).Then(e => (object)A("|", e));
            var parenContentsTail = expBracketContentsTail.And(expBracketClose).Then(t => (object)A("contents", t.Item1, t.Item2));
            // (exp) with diacritics, which the entity refuses as a bracket, is the diacriticbase of atoms: read here, not
            // again by diacriticbase
            var parenDiacriticTail = L(")").SkipAnd(Opt(L(" "))).SkipAnd(OneOrMany(diacritic)).And(ZeroOrMany(atom))
                .Then(t => (object)A("diacriticized", t.Item1.ToList(), t.Item2.ToList()));
            var parenBracket = L("(").SkipAnd(exp).And(OneOf(parenDiacriticTail, parenPipeTail, parenContentsTail))
                .Then(t => ParenBracket(t.Item1, (List<object>)t.Item2));
            // the brackets with contents that fail at an offset are remembered, as PEG gives a rule at an offset the same
            // result: an unclosed bracket would otherwise parse its contents for nothing, then again as the operand that
            // follows its opening bracket read as an operator, twice or three times per level of nesting
            var bracketUntried = Always<object>(null).When((context, x) => BracketFailures.Untried(context.Scanner.Buffer, context.Scanner.Cursor.Offset));
            var bracketFailed = Always<object>(null).When((context, x) => BracketFailures.Fail(context.Scanner.Buffer, context.Scanner.Cursor.Offset));
            var expBracketWithContents = OneOf(
                OneOf(L("||"), L("‖")).SkipAnd(exp).AndSkip(OneOf(L("||"), L("‖")))
                    .Then(e => (object)O("bracketed", O(("open", "‖"), ("close", "‖"), ("content", e)))),
                L("|").SkipAnd(exp).And(OneOf(L("|"), Not(L(")")).SkipAnd(expBracketClose)))
                    .Then(t => (object)O("bracketed", O(("open", "|"), ("close", t.Item2), ("content", t.Item1)))),
                L("(").SkipAnd(interval).AndSkip(L("]")).Then(e => (object)GetBracketedInterval("(", e, "]")),
                L("[").SkipAnd(interval).AndSkip(L(")")).Then(e => (object)GetBracketedInterval("[", e, ")")),
                opSquareBrackets.And(interval).And(opSquareBrackets)
                    .Then(t => (object)GetBracketedInterval((string)t.Item1, t.Item2, (string)t.Item3)),
                parenBracket,
                expBracketOpen.AndSkip(Opt(__)).And(expBracketClose)
                    .Then(t => (object)O("bracketed", O(("open", t.Item1), ("close", t.Item2), ("content", O("atoms", O("spaces", A())))))),
                expBracketOpen.And(expBracketContents).And(expBracketClose)
                    .Then(t => (object)O("bracketed", O(("open", t.Item1), ("close", t.Item3), ("content", t.Item2)))),
                L("Ⓒ(").SkipAnd(arows).AndSkip(L(")"))
                    .Then(r => (object)O("bracketed", O(("open", "{"), ("close", ""), ("intent", ":piecewise"), ("content", O("array", r))))),
                L("Ⓒ〖").SkipAnd(arows).AndSkip(L("〗"))
                    .Then(r => (object)O("bracketed", O(("open", "{"), ("close", ""), ("intent", ":piecewise"), ("content", O("array", r))))));
            var expBracket = OneOf(
                bracketUntried.SkipAnd(expBracketWithContents),
                bracketFailed,
                expBracketOpen.Then(op => (object)ColoredOperator(op)));
            var absoluteValue = OneOf(
                opAbs.AndSkip(L("(")).And(exp).AndSkip(L(")"))
                    .Then(t => (object)O("bracketed", O(("open", "|"), ("close", "|"), ("intent", t.Item1), ("content", t.Item2)))),
                opAbs.And(operand)
                    .Then(t => (object)O("bracketed", O(("open", "|"), ("close", "|"), ("intent", t.Item1), ("content", t.Item2)))));
            var arg = OneOf(
                L("ⓐ").SkipAnd(OneOrMany(alphaAscii)).AndSkip(Opt(__)).And(exp)
                    .Then(t => (object)O("intend", O(("op", "ⓐ"), ("intent", O("text", Join(t.Item1.ToList()))), ("content", t.Item2)))),
                L("ⓐ(").SkipAnd(OneOrMany(alphaAscii)).AndSkip(Opt(__)).And(exp).AndSkip(L(")"))
                    .Then(t => (object)O("intend", O(("op", "ⓐ"), ("intent", O("text", Join(t.Item1.ToList()))), ("content", t.Item2)))));
            entity.Parser = Always<object>(null).When((context, x) => UnicodeMathGrammarWork.Step()).SkipAnd(OneOf(
                expBracket.And(OneOf(Not(OneOf(L("\u00A0"), diacritic)).Then(x => (object)true), Always<object>(false)))
                    .When((context, t) => (bool)t.Item2 || t.Item1 is DiacriticizedParen)
                    .Then(t => t.Item1 is DiacriticizedParen d ? d.Atoms : t.Item1),
                absoluteValue,
                atoms,
                arg,
                number,
                L("\\").SkipAnd(anyChar).Then(c => (object)O("operator", "\\" + (string)c))));

            // entry rule
            var newlines = ZeroOrMany(L("\n")).Then(n => (object)n.Select(x => (object)O("newline", null)).ToList());
            var eqnumber = opEqNumber.SkipAnd(OneOrMany(Not(L("\n")).SkipAnd(anyChar))).Then(n => (object)Join(n.ToList()));
            var unicodemath = ZeroOrMany(L("\n")).SkipAnd(ZeroOrMany(exp.And(newlines))).And(Opt(eqnumber)).AndSkip(ZeroOrMany(L("\n")))
                .Then(t => (object)O("unicodemath", O(
                    ("content", Flatten(t.Item1.Select(e => (object)new[] { e.Item1 }.Concat((List<object>)e.Item2).ToList()).ToList())),
                    ("eqnumber", t.Item2))));
            return unicodemath.Eof();
        }
    }
}
