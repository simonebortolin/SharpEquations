#nullable enable

namespace SharpEquations.Tests.UnicodeMath
{
    using System.Threading.Tasks;
    using System;
    using SharpEquations;

    /// <summary>
    /// UnicodeMath (Word's linear format) read and written: the UnicodeMath written back and the LaTeX show what was
    /// read. The corpus tests of UnicodeMathML cover the grammar; these cover the constructs of the public API.
    /// </summary>
    public class UnicodeMathTests
    {
        [Test]
        [Arguments(@"a=b#(1)", @"𝑎=𝑏#(1)", @"a=b\tag{1}")]
        [Arguments(@"⒱(a&b@c&d)", @"⒱(𝑎&𝑏@𝑐&𝑑)", @"\begin{vmatrix}a&b\\c&d\end{vmatrix}")]
        [Arguments(@"⬍x+⬆y+⬇z+⬌w", @"⬍𝑥+⬆𝑦+⬇𝑧+⬌𝑤", @"\smash{x}+\smash[t]{y}+\smash[b]{z}+\mathrlap{w}")]
        [Arguments(@"▭(1&x)+▭(2&x)+▭(4&x)+▭(8&x)+▭(16&x)+▭(32&x)", @"▭(1&𝑥)+▭(2&𝑥)+▭(4&𝑥)+▭(8&𝑥)+▭(16&𝑥)+▭(32&𝑥)", @"\enclose{right,left,bottom}{x}+\enclose{right,left,top}{x}+\enclose{right,bottom,top}{x}+\enclose{left,bottom,top}{x}+\enclose{horizontalstrike,right,left,bottom,top}{x}+\enclose{verticalstrike,right,left,bottom,top}{x}")]
        [Arguments(@"_a^b x", @"_𝑎^𝑏 𝑥", @"{}_a^bx")]
        [Arguments(@"_a x", @"_𝑎 𝑥", @"{}_ax")]
        [Arguments(@"^b x", @"^𝑏 𝑥", @"{}^bx")]
        [Arguments(@"⏞(a+b)^n", @"⏞(𝑎+𝑏)┴𝑛", @"\overset{n}{\overbrace{a+b}}")]
        [Arguments(@"a\thinsp b\medsp c\thicksp d", @"𝑎 𝑏 𝑐 𝑑", @"a\,b\:c\;d")]
        [Arguments(@"├)a┤(", @"├)𝑎┤(", @"\left)a\right(")]
        [Arguments(@"⟡(1&x)+⟡(2&x)+⟡(4&x)", @"⟡(1&𝑥)+⇳𝑥+⟡(4&𝑥)", @"x+\vphantom{x}+\phantom{x}")]
        [Arguments(@"\sqrt(x)+∛x+∜x", @"√𝑥+√(3&𝑥)+√(4&𝑥)", @"\sqrt{x}+\sqrt[3]{x}+\sqrt[4]{x}")]
        [Arguments(@"a⊘b+a⒞b+a¦b", @"𝑎⊘𝑏+𝑎⒞𝑏+𝑎¦𝑏", @"\frac{a}{b}+\binom{a}{b}+\genfrac{}{}{0pt}{}{a}{b}")]
        [Arguments(@"x⃗^2+x̂_1", @"𝑥⃗²+𝑥̂₁", @"\vec{x}^2+\hat{x}_1")]
        [Arguments(@"""text""^2", @"""text""²", @"\text{text}^2")]
        [Arguments(@"\overbrace(a+b)", @"⏞(𝑎+𝑏)", @"\overbrace{a+b}")]
        [Arguments(@"\rect(a)", @"▭𝑎", @"\boxed{a}")]
        [Arguments(@"\matrix(a&b@c&d)", @"■(𝑎&𝑏@𝑐&𝑑)", @"\begin{matrix}a&b\\c&d\end{matrix}")]
        [Arguments(@"\pmatrix(a&b@c&d)", @"⒨(𝑎&𝑏@𝑐&𝑑)", @"\begin{pmatrix}a&b\\c&d\end{pmatrix}")]
        [Arguments(@"\eqarray(a&=b@c&=d)", @"█(𝑎&=𝑏@𝑐&=𝑑)", @"\begin{aligned}a&=b\\c&=d\end{aligned}")]
        [Arguments(@"\cases(1&x>0@0&x<0)", @"Ⓒ(1&𝑥>0@0&𝑥<0)", @"\begin{cases}1&x>0\\0&x<0\end{cases}")]
        [Arguments(@"\int_0^1\of x dx", @"∫₀¹ 𝑥 𝑑𝑥", @"\int_0^1xdx")]
        [Arguments(@"\sum_(i=1)^n\of i", @"∑_(𝑖=1)^𝑛 𝑖", @"\sum_{i=1}^ni")]
        [Arguments(@"1/2/3", @"1/2/3", @"\frac{\frac{1}{2}}{3}")]
        [Arguments(@"x^-1+x_-1", @"𝑥^−1+𝑥_−1", @"x^{-1}+x_{-1}")]
        [Arguments(@"a\below b+a\above c", @"𝑎┬𝑏+𝑎┴𝑐", @"\underset{b}{a}+\overset{c}{a}")]
        [Arguments(@"(a\atop b)", @"𝑎⒞𝑏", @"\binom{a}{b}")]
        [Arguments(@"sin^-1 x+log_2 x", @"sin^−1 ⁡𝑥+log₂⁡𝑥", @"\sin^{-1}x+\log_2x")]
        [Arguments(@"ⅈ+ⅇ^x+ⅆx+ⅅy", @"ⅈ+ⅇ^𝑥+ ⅆ𝑥+ⅅ𝑦", @"\mathrm{i}+\mathrm{e}^x+\mathrm{d}x+\mathrm{D}y")]
        [Arguments(@"𝐀+𝔸+𝒜+𝔄+𝖠+𝙰", @"𝐀+𝔸+𝒜+𝔄+𝖠+𝙰", @"\mathbf{A}+\mathbb{A}+\mathcal{A}+\mathfrak{A}+\mathsf{A}+\mathtt{A}")]        [Arguments(@"ⅆ/ⅆx f(x)", @"ⅆ/ⅆ𝑥 𝑓(𝑥)", @"\frac{\mathrm{d}}{\mathrm{d}x}f(x)")]
        [Arguments(@"ⅆy/ⅆx", @"ⅆ𝑦/ⅆ𝑥", @"\frac{\mathrm{d}y}{\mathrm{d}x}")]
        [Arguments(@"ⅆ^2 y/ⅆx^2", @"ⅆ²𝑦/ⅆ𝑥²", @"\frac{\mathrm{d}^2y}{\mathrm{d}x^2}")]
        [Arguments(@"∂f/∂x", @"𝜕𝑓/𝜕𝑥", @"\frac{\partial f}{\partial x}")]
        [Arguments(@"∂^2 f/∂x∂y", @"𝜕²𝑓/𝜕𝑥𝜕𝑦", @"\frac{\partial^2f}{\partial x\partial y}")]
        [Arguments(@"ⅆ/ⅆx sin x", @"ⅆ/ⅆ𝑥 sin⁡𝑥", @"\frac{\mathrm{d}}{\mathrm{d}x}\sin x")]
        [Arguments(@"f'(x)+f''(x)+f'''(x)", @"𝑓′(𝑥)+𝑓″(𝑥)+𝑓‴(𝑥)", @"f'(x)+f''(x)+f'''(x)")]
        [Arguments(@"y′+y″", @"𝑦′+𝑦″", @"y'+y''")]
        [Arguments(@"ⅆ/ⅆt (x^2)", @"ⅆ/ⅆ𝑡 (𝑥²)", @"\frac{\mathrm{d}}{\mathrm{d}t}(x^2)")]
        [Arguments(@"ⅆ^n/ⅆx^n f", @"ⅆ^𝑛/ⅆ𝑥^𝑛 𝑓", @"\frac{\mathrm{d}^n}{\mathrm{d}x^nf}")]
        [Arguments(@"∮_C F·ⅆr", @"∮_𝐶 𝐹· ⅆ𝑟", @"\oint_CF\cdot\mathrm{d}r")]
        [Arguments(@"x_i^2'", @"𝑥_𝑖^2′", @"x_i^{2'}")]
        [Arguments(@"a^b_c", @"𝑎_𝑐^𝑏", @"a_c^b")]
        [Arguments(@"sin^2 x", @"sin²⁡𝑥", @"\sin^2x")]
        [Arguments(@"sin x^2", @"sin⁡𝑥²", @"\sin x^2")]
        [Arguments(@"max_(i∈I) x_i", @"max_(𝑖∈𝐼) 𝑥_𝑖", @"\max_{i\in I}x_i")]
        [Arguments(@"min_i x_i", @"min_𝑖 𝑥_𝑖", @"\min_ix_i")]
        [Arguments(@"√(n&x)", @"√(𝑛&𝑥)", @"\sqrt[n]{x}")]
        [Arguments(@"∛(x+1)", @"√(3&𝑥+1)", @"\sqrt[3]{x+1}")]
        [Arguments(@"⒨(1&0@0&1)", @"⒨(1&0@0&1)", @"\begin{pmatrix}1&0\\0&1\end{pmatrix}")]
        [Arguments(@"⒱(1&0@0&1)", @"⒱(1&0@0&1)", @"\begin{vmatrix}1&0\\0&1\end{vmatrix}")]
        [Arguments(@"⟨x│y⟩", @"⟨𝑥│𝑦⟩", @"\langle x|y\rangle")]
        [Arguments(@"⌊x/2⌋", @"⌊𝑥/2 ⌋", @"\left\lfloor\frac{x}{2}\right\rfloor")]
        [Arguments(@"‖x‖_2", @"‖𝑥‖₂", @"{\|x\|}_2")]
        [Arguments(@"|x|^2", @"〖|𝑥|〗²", @"{|x|}^2")]
        [Arguments(@"(a,b]", @"(𝑎,𝑏]", @"(a,b]")]
        [Arguments(@"[a,b)", @"[𝑎,𝑏)", @"[a,b)")]
        [Arguments(@"]a,b[", @"]𝑎,𝑏[", @"]a,b[")]
        [Arguments(@"{x│x>0}", @"{𝑥│𝑥>0}", @"\{x|x>0\}")]
        [Arguments(@"{x|x>0}", @"{𝑥│𝑥>0}", @"\{x|x>0\}")]
        [Arguments(@"{|x|}", @"{|𝑥|}", @"\{|x|\}")]
        [Arguments(@"x ∈ ℝ^n", @"𝑥∈ℝ^𝑛", @"x\in\mathbb{R}^n")]
        [Arguments(@"∀ε>0 ∃δ>0", @"∀𝜀>0∃𝛿>0", @"\forall\varepsilon>0\exists\delta>0")]
        [Arguments(@"a≝b", @"𝑎≝𝑏", @"a≝b")]
        [Arguments(@"x≔1", @"𝑥≔1", @"x\coloneqq1")]
        [Arguments(@"""if ""x>0", @"""if ""𝑥>0", @"\text{if }x>0")]
        [Arguments(@"1/2 x", @"1/2 𝑥", @"\frac{1}{2}x")]
        [Arguments(@"12/34", @"12/34", @"\frac{12}{34}")]
        [Arguments(@"½+¾", @"½+¾", @"\frac{1}{2}+\frac{3}{4}")]
        [Arguments(@"x⁽²⁾", @"𝑥²", @"x^2")]
        [Arguments(@"x₍ᵢ₎", @"𝑥_𝑖", @"x_i")]
        [Arguments(@"𝑥²⁺ⁿ", @"𝑥^(2+𝑛)", @"x^{2+n}")]
        [Arguments(@"a⁻¹", @"𝑎^−1", @"a^{-1}")]
        [Arguments(@"E=mc²", @"𝐸=𝑚𝑐²", @"E=mc^2")]
        [Arguments(@"ⓒ(A)", @"|𝐴|", @"|A|")]
        [Arguments(@"\card(A)", @"|𝐴|", @"|A|")]
        public async Task ReadsAndWritesUnicodeMath(string unicodeMath, string expectedUnicodeMath, string expectedLaTeX)
        {
            var equation = Equation.Parse(unicodeMath, EquationFormat.UnicodeMath);
            await Assert.That(equation.ToString(EquationFormat.UnicodeMath)).IsEqualTo(expectedUnicodeMath);
            await Assert.That(equation.ToString(EquationFormat.LaTeX)).IsEqualTo(expectedLaTeX);
            await Assert.That(Equation.Parse(expectedUnicodeMath, EquationFormat.UnicodeMath)).IsEqualTo(equation).Because("written UnicodeMath reads back");
        }

        [Test]
        [Arguments("├)a)", "├)𝑎)")]
        [Arguments("├a)", "├𝑎)")]
        [Arguments("├]a┤[", "├]𝑎┤[")]
        [Arguments("[a)", "[𝑎)")]
        [Arguments("(a┤", "(𝑎┤")]
        [Arguments("├)a┤", "├)𝑎┤")]
        public async Task WritesUnpairedFences(string unicodeMath, string expected)
        {
            var equation = Equation.Parse(unicodeMath, EquationFormat.UnicodeMath);
            await Assert.That(equation.ToString(EquationFormat.UnicodeMath)).IsEqualTo(expected);
            await Assert.That(Equation.Parse(expected, EquationFormat.UnicodeMath)).IsEqualTo(equation);
        }

        [Test]
        [Arguments("∑₀¹ x", "∑₀¹ 𝑥")]
        [Arguments("∫₀¹ f(x) dx", "∫₀¹ 𝑓(𝑥) 𝑑𝑥")]
        [Arguments("∫_0^1▒f(x) dx", "∫₀¹ 𝑓(𝑥) 𝑑𝑥")]
        [Arguments("∑_(i=1)^n i", "∑_(𝑖=1)^𝑛 𝑖")]
        [Arguments("𝛼 ⅆ𝛽", "𝛼 ⅆ𝛽")]
        [Arguments("A^⊺=0", "𝐴^⊺=0")]
        [Arguments("a_(b_b)^c", "𝑎_(𝑏_𝑏)^𝑐")]
        [Arguments("(\"a\")̂", "(\"a\")̂")]
        [Arguments("(\"potter\")͛", "(\"potter\")͛")]
        [Arguments("(ℕ_+)⃗", "(ℕ_+)⃗")]
        [Arguments("〖a+b〗_n", "〖𝑎+𝑏〗_𝑛")]
        public async Task WritesWhatReadsBack(string unicodeMath, string expected)
        {
            var equation = Equation.Parse(unicodeMath, EquationFormat.UnicodeMath);
            await Assert.That(equation.ToString(EquationFormat.UnicodeMath)).IsEqualTo(expected);
            await Assert.That(Equation.Parse(expected, EquationFormat.UnicodeMath)).IsEqualTo(equation);
        }

        // a fraction after a function or a script, which UnicodeMath would make its numerator
        [Test]
        [Arguments(@"\lim_{h\to0}\frac{f(x)}{h}", "lim_(ℎ→0) ⁡〖𝑓(𝑥)/ℎ〗")]
        [Arguments(@"\sin\frac{x}{2}", "sin⁡〖𝑥/2〗")]
        [Arguments(@"a\stackrel{n\to\infty}{\longrightarrow}\frac{1}{2}", "𝑎⟶┴(𝑛→∞) 〖1/2〗")]
        [Arguments(@"x_i\frac{1}{2}+x^n\frac{1}{2}", "𝑥_𝑖 〖1/2〗+𝑥^𝑛 〖1/2〗")]
        [Arguments(@"x^2\frac{1}{2}+\hat{x}\frac{1}{2}", "𝑥² 1/2+𝑥̂ 1/2")]        public async Task WritesFractionsApartFromWhatPrecedes(string latex, string expected)
        {
            var equation = Equation.Parse(latex, EquationFormat.LaTeX);
            await Assert.That(equation.ToString(EquationFormat.UnicodeMath)).IsEqualTo(expected);
            await Assert.That(Equation.Parse(expected, EquationFormat.UnicodeMath)).IsEqualTo(equation);
        }

        [Test]
        [Arguments("\"\\foo\"", "\\foo")]
        [Arguments("\"\\p\"", "\\p")]
        [Arguments("\"a\\\"b\"", "a\"b")]
        [Arguments("\\foo", "\\foo")]
        public async Task KeepsBackslashesInText(string unicodeMath, string text)
        {
            var math = Equation.Parse(unicodeMath, EquationFormat.UnicodeMath).ToMathML();
            await Assert.That(math.Value).IsEqualTo(text);
        }

        [Test]
        public async Task StrictRejectsUnknownControlWordsOutsideText()
        {
            var strict = new EquationOptions { Strict = true };
            await Assert.That(() => Equation.Parse("\\foo", EquationFormat.UnicodeMath, strict)).Throws<FormatException>().WithMessage("Unknown control word \\foo.");
            await Assert.That(() => Equation.Parse("\"\\foo\"+\\alpha", EquationFormat.UnicodeMath, strict)).ThrowsNothing();
        }

        [Test]
        [Arguments("x_a^b_c")]
        [Arguments("\"abc")]
        public async Task RejectsTextThatIsNotUnicodeMath(string unicodeMath)
        {
            await Assert.That(() => Equation.Parse(unicodeMath, EquationFormat.UnicodeMath)).Throws<FormatException>().WithMessageMatching("Not valid UnicodeMath" + "*");
        }
    }
}
