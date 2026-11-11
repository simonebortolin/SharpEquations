#nullable enable

namespace SharpEquations.Tests.MathML
{
    using System.Threading.Tasks;
    using SharpEquations;

    /// <summary>
    /// MathML written as standard LaTeX, Word's LaTeX and UnicodeMath: the elements of presentation MathML and the
    /// characters with a LaTeX command. A null expectation is a flavor not checked for that MathML.
    /// </summary>
    public class MathMLWriterTests
    {
        private static readonly EquationOptions Word = new EquationOptions { LaTeXFlavor = LaTeXFlavor.Word };

        [Test]
        [Arguments(@"<math><munder><mi>x</mi><mo>_</mo></munder></math>", @"\underline{x}", @"\underline{𝑥}", @"▁𝑥")]
        [Arguments(@"<math><munder><mrow><mi>x</mi><mo>+</mo><mi>y</mi></mrow><mo>&#x332;</mo></munder></math>", @"\underline{x+y}", @"\underline{𝑥+𝑦}", @"▁(𝑥+𝑦)")]
        [Arguments(@"<math><mover><mi>x</mi><mo>¯</mo></mover></math>", @"\overline{x}", @"\overbar{𝑥}", @"¯𝑥")]
        [Arguments(@"<math><mover><mrow><mi>a</mi><mi>b</mi></mrow><mo>&#x23DE;</mo></mover></math>", @"\overbrace{ab}", @"\overbrace{𝑎𝑏}", @"⏞𝑎𝑏")]
        [Arguments(@"<math><munder><mrow><mi>a</mi><mi>b</mi></mrow><mo>&#x23DF;</mo></munder></math>", @"\underbrace{ab}", @"\underbrace{𝑎𝑏}", @"⏟𝑎𝑏")]
        [Arguments(@"<math><menclose notation=""box""><mi>x</mi></menclose></math>", @"\boxed{x}", @"\boxed{𝑥}", @"▭𝑥")]
        [Arguments(@"<math><menclose notation=""top""><mi>x</mi></menclose></math>", @"\bar{x}", @"\bar{𝑥}", @"̄𝑥")]
        [Arguments(@"<math><menclose notation=""bottom""><mi>x</mi></menclose></math>", @"\underline{x}", @"\underline{𝑥}", @"▁𝑥")]
        [Arguments(@"<math><menclose notation=""updiagonalstrike""><mi>x</mi></menclose></math>", @"\cancel{x}", @"\cancel{𝑥}", @"▭(143&𝑥)")]
        [Arguments(@"<math><menclose notation=""downdiagonalstrike""><mi>x</mi></menclose></math>", @"\bcancel{x}", @"\bcancel{𝑥}", @"▭(79&𝑥)")]
        [Arguments(@"<math><menclose notation=""updiagonalstrike downdiagonalstrike""><mi>x</mi></menclose></math>", @"\xcancel{x}", @"\xcancel{𝑥}", @"▭(207&𝑥)")]
        [Arguments(@"<math><menclose notation=""circle""><mi>x</mi></menclose></math>", @"\enclose{circle}{x}", @"\circle{𝑥}", @"◯𝑥")]
        [Arguments(@"<math><menclose notation=""horizontalstrike""><mi>x</mi></menclose></math>", @"\sout{x}", @"\boxed{𝑥}", @"▭(31&𝑥)")]
        [Arguments(@"<math><menclose notation=""verticalstrike""><mi>x</mi></menclose></math>", @"\enclose{verticalstrike}{x}", @"\boxed{𝑥}", @"▭(47&𝑥)")]
        [Arguments(@"<math><menclose notation=""longdiv""><mi>x</mi></menclose></math>", @"\enclose{longdiv}{x}", @"\boxed{𝑥}", @"⟌𝑥")]
        [Arguments(@"<math><menclose notation=""roundedbox""><mi>x</mi></menclose></math>", @"\enclose{roundedbox}{x}", @"\boxed{𝑥}", @"▢𝑥")]
        [Arguments(@"<math><menclose notation=""actuarial""><mi>x</mi></menclose></math>", @"\enclose{actuarial}{x}", @"\boxed{𝑥}", "⃧𝑥")]
        [Arguments(@"<math><menclose notation=""left""><mi>x</mi></menclose></math>", @"\enclose{left}{x}", @"\boxed{𝑥}", @"▭(11&𝑥)")]
        [Arguments(@"<math><menclose notation=""right""><mi>x</mi></menclose></math>", @"\enclose{right}{x}", @"\boxed{𝑥}", @"▭(7&𝑥)")]
        [Arguments(@"<math><menclose notation=""top bottom""><mi>x</mi></menclose></math>", @"\enclose{top,bottom}{x}", @"\boxed{𝑥}", @"▭(12&𝑥)")]
        [Arguments(@"<math><menclose notation=""madruwb""><mi>x</mi></menclose></math>", @"\enclose{madruwb}{x}", @"\boxed{𝑥}", @"▭𝑥")]
        [Arguments(@"<math><menclose notation=""radical""><mi>x</mi></menclose></math>", @"\sqrt{x}", @"\boxed{𝑥}", @"▭𝑥")]
        [Arguments(@"<math><menclose notation=""left right top bottom""><mi>x</mi></menclose><menclose><mi>y</mi></menclose></math>", @"\boxed{x}\boxed{y}", @"\boxed{𝑥}\boxed{𝑦}", @"▭(0&𝑥)▭𝑦")]
        [Arguments(@"<math><mi>ℓ</mi><mi>ℑ</mi><mi>ℜ</mi><mi>℘</mi><mi>ℏ</mi><mi>ℵ</mi></math>", @"\ell\Im\Re\wp\hbar\aleph", @"ℓℑℜ℘ℏℵ", @"ℓℑℜ℘ℏℵ")]
        [Arguments(@"<math><mi>Re</mi><mo>&#x2061;</mo><mi>z</mi><mo>+</mo><mi>Im</mi><mo>&#x2061;</mo><mi>z</mi></math>", @"\operatorname{Re}z+\operatorname{Im}z", @"\operatorname{Re}𝑧+\operatorname{Im}𝑧", @"Re⁡𝑧+Im⁡𝑧")]
        [Arguments("<math><mi>m</mi><mo>〈</mo><mi>n</mi><mo>〉</mo></math>", @"m\langle n\rangle", "𝑚〈𝑛〉", "𝑚〈𝑛〉")]
        [Arguments("<math><mrow><mo>〈</mo><mfrac><mi>a</mi><mi>b</mi></mfrac><mo>〉</mo></mrow><mrow><mo>〈</mo><mfrac><mi>a</mi><mi>b</mi></mfrac><mo>〉</mo></mrow></math>", @"\left\langle\frac{a}{b}\right\rangle\left\langle\frac{a}{b}\right\rangle", null, "〈𝑎/𝑏 〉〈𝑎/𝑏 〉")]
        [Arguments("<math><mrow><mo>|</mo><mi>a</mi><mo>〉</mo></mrow><mrow><mo>〈</mo><mi>b</mi><mo>|</mo></mrow></math>", @"|a\rangle\langle b|", null, "|𝑎〉〈𝑏|")]
        [Arguments("<math><mrow><mo>〈</mo><mi>a</mi><mo>|</mo><mi>b</mi><mo>〉</mo></mrow></math>", @"\langle a|b\rangle", null, "〈𝑎|𝑏〉")]
        [Arguments(@"<math><mmultiscripts><mi>C</mi><mi>b</mi><none/><mprescripts/><mi>a</mi><mi>c</mi></mmultiscripts></math>", @"{}_a^cC_b", @"{}_𝑎^𝑐𝐶_𝑏", @"_𝑎^𝑐 𝐶_𝑏")]
        [Arguments(@"<math><mi>a</mi><mspace width=""thinmathspace""/><mi>b</mi><mspace width=""1em""/><mi>c</mi></math>", @"a\,b\quad c", @"𝑎 𝑏 𝑐", @"𝑎 𝑏 𝑐")]
        [Arguments(@"<math><mphantom><mi>x</mi></mphantom></math>", @"\phantom{x}", @"\phantom{𝑥}", @"⟡𝑥")]
        [Arguments(@"<math><mi mathvariant=""bold"">x</mi><mi mathvariant=""script"">L</mi><mi mathvariant=""fraktur"">g</mi><mi mathvariant=""double-struck"">R</mi><mi mathvariant=""sans-serif"">s</mi><mi mathvariant=""monospace"">t</mi><mi mathvariant=""bold-italic"">v</mi></math>", @"\mathbf{x}\mathcal{L}\mathfrak{g}\mathbb{R}\mathsf{s}\mathtt{t}\boldsymbol{v}", @"𝐱ℒ𝔤ℝ𝗌𝚝𝒗", @"𝐱ℒ𝔤ℝ𝗌𝚝𝒗")]
        [Arguments(@"<math><mi mathvariant=""normal"">d</mi><mi>x</mi></math>", @"\mathrm{d}x", @"\mathrm{d}𝑥", @"""d""𝑥")]
        [Arguments(@"<math><mi>ⅆ</mi><mi>x</mi><mo>+</mo><mi>ⅇ</mi><mo>+</mo><mi>ⅈ</mi><mo>+</mo><mi>ⅉ</mi><mo>+</mo><mi>ⅅ</mi></math>", @"\mathrm{d}x+\mathrm{e}+\mathrm{i}+\mathrm{j}+\mathrm{D}", @"ⅆ𝑥+ⅇ+ⅈ+ⅉ+ⅅ", @"ⅆ𝑥+ⅇ+ⅈ+ⅉ+ⅅ")]
        [Arguments(@"<math><mfrac bevelled=""true""><mi>a</mi><mi>b</mi></mfrac></math>", @"\frac{a}{b}", @"\frac{𝑎}{𝑏}", @"𝑎⁄𝑏")]
        [Arguments(@"<math><mfrac linethickness=""0""><mi>a</mi><mi>b</mi></mfrac></math>", @"\genfrac{}{}{0pt}{}{a}{b}", @"\genfrac{}{}{0pt}{}{𝑎}{𝑏}", @"𝑎¦𝑏")]
        [Arguments(@"<math><mrow><mo>|</mo><mi>x</mi><mo>|</mo></mrow></math>", @"|x|", @"\abs{𝑥}", @"|𝑥|")]
        [Arguments(@"<math><mrow><mo>‖</mo><mi>x</mi><mo>‖</mo></mrow></math>", @"\|x\|", @"‖𝑥‖", @"‖𝑥‖")]
        [Arguments(@"<math><mrow><mo>⌊</mo><mi>x</mi><mo>⌋</mo></mrow><mrow><mo>⌈</mo><mi>x</mi><mo>⌉</mo></mrow></math>", @"\lfloor x\rfloor\lceil x\rceil", @"⌊𝑥⌋⌈𝑥⌉", @"⌊𝑥⌋⌈𝑥⌉")]
        [Arguments(@"<math><mrow><mo>⟨</mo><mi>x</mi><mo>|</mo><mi>y</mi><mo>⟩</mo></mrow></math>", @"\langle x|y\rangle", @"⟨𝑥|𝑦⟩", @"⟨𝑥│𝑦⟩")]
        [Arguments(@"<math><mrow><mo>(</mo><mfrac><mi>a</mi><mi>b</mi></mfrac><mo>)</mo></mrow></math>", @"\left(\frac{a}{b}\right)", @"(\frac{𝑎}{𝑏})", @"(𝑎/𝑏)")]
        [Arguments(@"<math><mrow><mo>[</mo><mtable><mtr><mtd><mn>1</mn></mtd><mtd><mn>2</mn></mtd></mtr></mtable><mo>]</mo></mrow></math>", @"\begin{bmatrix}1&2\end{bmatrix}", @"\begin{bmatrix}1&2\end{bmatrix}", @"ⓢ(1&2)")]
        [Arguments(@"<math><mrow><mo>|</mo><mtable><mtr><mtd><mn>1</mn></mtd><mtd><mn>2</mn></mtd></mtr><mtr><mtd><mn>3</mn></mtd><mtd><mn>4</mn></mtd></mtr></mtable><mo>|</mo></mrow></math>", @"\begin{vmatrix}1&2\\3&4\end{vmatrix}", @"\begin{vmatrix}1&2\\3&4\end{vmatrix}", @"⒱(1&2@3&4)")]
        [Arguments(@"<math><mrow><mo>{</mo><mtable><mtr><mtd><mn>1</mn></mtd></mtr></mtable><mo>}</mo></mrow></math>", @"\begin{Bmatrix}1\end{Bmatrix}", @"\begin{Bmatrix}1\end{Bmatrix}", @"Ⓢ(1)")]
        [Arguments(@"<math><mrow><mo>{</mo><mtable><mtr><mtd><mn>1</mn></mtd><mtd><mi>x</mi><mo>&gt;</mo><mn>0</mn></mtd></mtr></mtable></mrow></math>", @"\begin{cases}1&x>0\end{cases}", @"\begin{cases}1&𝑥>0\end{cases}", @"{■(1&𝑥>0)┤")]
        [Arguments(@"<math><mtable><mtr><mtd><mn>1</mn></mtd><mtd><mn>0</mn></mtd></mtr><mtr><mtd><mn>0</mn></mtd><mtd><mn>1</mn></mtd></mtr></mtable></math>", @"\begin{matrix}1&0\\0&1\end{matrix}", @"\begin{matrix}1&0\\0&1\end{matrix}", @"■(1&0@0&1)")]
        [Arguments(@"<math><munderover><mo>∑</mo><mrow><mi>i</mi><mo>=</mo><mn>1</mn></mrow><mi>n</mi></munderover><msub><mi>a</mi><mi>i</mi></msub></math>", @"\sum\limits_{i=1}^na_i", @"∑┬{𝑖=1}┴𝑛𝑎_𝑖", @"∑┬(𝑖=1)┴𝑛 𝑎_𝑖")]
        [Arguments(@"<math><msubsup><mo>∫</mo><mn>0</mn><mn>1</mn></msubsup><mi>f</mi></math>", @"\int_0^1f", @"∫_0^1𝑓", @"∫₀¹𝑓")]
        [Arguments(@"<math><munder><mo>lim</mo><mrow><mi>x</mi><mo>→</mo><mn>0</mn></mrow></munder><mi>f</mi></math>", @"\lim_{x\to0}f", null, @"lim┬(𝑥→0) 𝑓")]
        [Arguments(@"<math><mi>sin</mi><mo>&#x2061;</mo><mi>x</mi><mo>+</mo><mi>log</mi><mo>&#x2061;</mo><mi>y</mi><mo>+</mo><mi>sgn</mi><mo>&#x2061;</mo><mi>z</mi></math>", @"\sin x+\log y+\operatorname{sgn}z", null, @"sin⁡𝑥+log⁡𝑦+sgn⁡𝑧")]
        [Arguments(@"<math><msup><mi>f</mi><mo>′</mo></msup><mo>+</mo><msup><mi>g</mi><mo>″</mo></msup><mo>+</mo><msup><mi>h</mi><mo>‴</mo></msup></math>", @"f'+g''+h'''", @"𝑓′+𝑔″+ℎ‴", @"𝑓′+𝑔″+ℎ‴")]
        [Arguments(@"<math><mover><mi>x</mi><mo>^</mo></mover><mover><mi>x</mi><mo>~</mo></mover><mover><mi>x</mi><mo>˙</mo></mover><mover><mi>x</mi><mo>¨</mo></mover><mover><mi>x</mi><mo>ˇ</mo></mover><mover><mi>x</mi><mo>˘</mo></mover><mover><mi>x</mi><mo>→</mo></mover></math>", @"\hat{x}\tilde{x}\dot{x}\ddot{x}\check{x}\breve{x}\overset{\to}{x}", null, @"𝑥┴^ 𝑥┴~ 𝑥┴˙ 𝑥┴¨ 𝑥┴ˇ 𝑥┴˘ 𝑥┴→")]
        [Arguments(@"<math><mover accent=""true""><mrow><mi>a</mi><mi>b</mi></mrow><mo>^</mo></mover><mover accent=""true""><mrow><mi>a</mi><mi>b</mi></mrow><mo>~</mo></mover></math>", @"\hat{ab}\tilde{ab}", null, @"(𝑎𝑏)^(𝑎𝑏)~")]
        [Arguments(@"<math><mtext>if # $ % &amp; _ ^ ~ \ { }</mtext></math>", @"\text{if \# \$ \% \& \_ \^{} \~{} \textbackslash{} \{ \}}", @"\textrm{if # $ % & _ ^ ~ \ { }}", @"""if # $ % & _ ^ ~ \ {}""")]
        [Arguments(@"<math><ms>str</ms></math>", @"\text{""str""}", null, @"""\""str\""""")]
        [Arguments(@"<math><mn>3.14</mn><mo>×</mo><msup><mn>10</mn><mn>5</mn></msup></math>", @"3.14\times{10}^5", @"3.14×{10}^5", @"3.14×10⁵")]
        [Arguments(@"<math><mi>α</mi><mi>β</mi><mi>Γ</mi><mi>Δ</mi><mi>ϵ</mi><mi>ϑ</mi><mi>ϕ</mi><mi>ω</mi><mi>Ω</mi></math>", @"\alpha\beta\Gamma\Delta\epsilon\vartheta\phi\omega\Omega", @"𝛼𝛽ΓΔ𝜖𝜗𝜙𝜔Ω", @"𝛼𝛽ΓΔ𝜖𝜗𝜙𝜔Ω")]
        [Arguments(@"<math><mo>≤</mo><mo>≥</mo><mo>≠</mo><mo>≈</mo><mo>≡</mo><mo>∼</mo><mo>⊂</mo><mo>⊆</mo><mo>∈</mo><mo>∉</mo><mo>∀</mo><mo>∃</mo><mo>∅</mo><mo>∞</mo><mo>∂</mo><mo>∇</mo><mo>⇒</mo><mo>⇔</mo><mo>↦</mo><mo>±</mo><mo>∓</mo><mo>·</mo><mo>…</mo><mo>⋯</mo></math>", @"\le\ge\ne\approx\equiv\sim\subset\subseteq\in\notin\forall\exists\emptyset\infty\partial\nabla\Rightarrow\Leftrightarrow\mapsto\pm\mp\cdot\ldots\cdots", @"≤≥≠≈≡∼⊂⊆∈∉∀∃∅∞∂∇⇒⇔↦±∓·…⋅⋅⋅", @"≤≥≠≈≡∼⊂⊆∈∉∀∃∅∞∂∇⇒⇔↦±∓·…⋯")]
        [Arguments(@"<math><mstyle displaystyle=""true""><mfrac><mn>1</mn><mn>2</mn></mfrac></mstyle></math>", @"\frac{1}{2}", @"\frac{1}{2}", @"1/2")]
        [Arguments(@"<math><mpadded width=""2em""><mi>x</mi></mpadded><merror><mi>e</mi></merror></math>", @"xe", null, @"⟡(1&𝑥)𝑒")]
        [Arguments(@"<math><semantics><mi>x</mi><annotation encoding=""TeX"">x</annotation></semantics></math>", @"x", @"𝑥", @"𝑥")]
        [Arguments(@"<math><maction actiontype=""toggle""><mi>a</mi><mi>b</mi></maction></math>", @"ab", @"𝑎𝑏", @"𝑎𝑏")]
        [Arguments(@"<math><mroot><mrow><mi>x</mi><mo>+</mo><mn>1</mn></mrow><mn>3</mn></mroot><msqrt><mn>2</mn></msqrt></math>", @"\sqrt[3]{x+1}\sqrt{2}", @"\sqrt[3]{𝑥+1}\sqrt{2}", @"√(3&𝑥+1)√2")]
        [Arguments(@"<math><msub><mi>x</mi><mrow><mi>i</mi><mi>j</mi></mrow></msub><msup><mi>e</mi><mrow><mo>-</mo><mi>x</mi></mrow></msup></math>", @"x_{ij}e^{-x}", @"𝑥_{𝑖𝑗}𝑒^{−𝑥}", @"𝑥_𝑖𝑗 𝑒^−𝑥")]
        [Arguments(@"<math><mo>(</mo><mi>a</mi><mo>,</mo><mi>b</mi><mo>]</mo></math>", @"(a,b]", @"(𝑎,𝑏]", @"(𝑎,𝑏]")]
        [Arguments(@"<math><mi>x</mi><mo>∣</mo><mi>y</mi></math>", @"x\mid y", @"𝑥∣𝑦", @"𝑥∣𝑦")]
        [Arguments(@"<math><mtext>&#xA0;</mtext><mi>x</mi></math>", @"\text{ }x", @"\textrm{ }𝑥", @""" ""𝑥")]
        public async Task WritesMathML(string mathML, string? standard, string? word, string unicodeMath)
        {
            var equation = Equation.Parse(mathML, EquationFormat.MathML);
            if (standard != null)
                await Assert.That(equation.ToString(EquationFormat.LaTeX)).IsEqualTo(standard).Because("standard LaTeX");
            if (word != null)
                await Assert.That(equation.ToString(EquationFormat.LaTeX, Word)).IsEqualTo(word).Because("Word's LaTeX");
            await Assert.That(equation.ToString(EquationFormat.UnicodeMath)).IsEqualTo(unicodeMath).Because("UnicodeMath");
        }

        [Test]
        [Arguments("<math><menclose notation=\"updiagonalstrike\"><mi>x</mi></menclose></math>", @"\cancel{x}")]
        [Arguments("<math><menclose notation=\"downdiagonalstrike\"><mi>x</mi></menclose></math>", @"\bcancel{x}")]
        [Arguments("<math><menclose notation=\"updiagonalstrike downdiagonalstrike\"><mi>x</mi></menclose></math>", @"\xcancel{x}")]
        [Arguments("<math><menclose notation=\"box\"><mi>x</mi></menclose></math>", @"\boxed{x}")]
        [Arguments("<math><menclose notation=\"horizontalstrike\"><mrow><mi>a</mi><mo>+</mo><mi>b</mi></mrow></menclose></math>", @"\sout{a+b}")]
        [Arguments("<math><menclose notation=\"verticalstrike\"><mi>x</mi></menclose></math>", @"\enclose{verticalstrike}{x}")]
        [Arguments("<math><menclose notation=\"longdiv\"><mfrac><mi>a</mi><mi>b</mi></mfrac></menclose></math>", @"\enclose{longdiv}{\frac{a}{b}}")]
        [Arguments("<math><menclose notation=\"roundedbox\"><mi>x</mi></menclose></math>", @"\enclose{roundedbox}{x}")]
        [Arguments("<math><menclose notation=\"circle\"><mi>x</mi></menclose></math>", @"\enclose{circle}{x}")]
        [Arguments("<math><menclose notation=\"actuarial\"><mi>n</mi></menclose></math>", @"\enclose{actuarial}{n}")]
        [Arguments("<math><menclose notation=\"left\"><mi>x</mi></menclose></math>", @"\enclose{left}{x}")]
        public async Task StrikesAndBoxesReadBack(string mathML, string latex)
        {
            var equation = Equation.Parse(mathML, EquationFormat.MathML);
            await Assert.That(equation.ToString(EquationFormat.LaTeX)).IsEqualTo(latex);
            await Assert.That(Equation.Parse(latex, EquationFormat.LaTeX)).IsEqualTo(equation);
            await Assert.That(Equation.Parse(latex, EquationFormat.LaTeX).ToString(EquationFormat.LaTeX)).IsEqualTo(latex);
            await Assert.That(Equation.Parse(equation.ToString(EquationFormat.UnicodeMath), EquationFormat.UnicodeMath).ToString(EquationFormat.LaTeX)).IsEqualTo(latex);
        }

        [Test]
        public async Task WritesLabelledRows()
        {
            var equation = Equation.Parse("<math><mtable><mlabeledtr><mtd><mtext>(1)</mtext></mtd><mtd><mi>a</mi><mo>=</mo><mi>b</mi></mtd></mlabeledtr></mtable></math>", EquationFormat.MathML);
            await Assert.That(equation.ToString(EquationFormat.LaTeX)).IsEqualTo(@"a=b\tag{1}");
            await Assert.That(equation.ToString(EquationFormat.UnicodeMath)).IsEqualTo("𝑎=𝑏#(1)");
        }
    }
}
