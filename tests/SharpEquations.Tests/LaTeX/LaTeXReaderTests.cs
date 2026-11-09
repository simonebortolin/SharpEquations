#nullable enable

namespace SharpEquations.Tests.LaTeX
{
    using System.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using SharpEquations;

    /// <summary>LaTeX read into equations: the LaTeX written back (standard) and the UnicodeMath show what was read.</summary>
    public class LaTeXReaderTests
    {
        private static readonly EquationOptions Strict = new EquationOptions { Strict = true };

        [Test]
        [Arguments(@"\dfrac{a}{b}+\tfrac{c}{d}", @"\frac{a}{b}+\frac{c}{d}", @"𝑎/𝑏+𝑐/𝑑")]
        [Arguments(@"{a \over b}", @"\frac{a}{b}", @"𝑎/𝑏")]
        [Arguments(@"{n \choose k}", @"\binom{n}{k}", @"𝑛⒞𝑘")]
        [Arguments(@"\sqrt[n]{x}", @"\sqrt[n]{x}", @"√(𝑛&𝑥)")]
        [Arguments(@"\left[\frac{a}{b}\right]", @"\left[\frac{a}{b}\right]", @"[𝑎/𝑏]")]
        [Arguments(@"\left\langle x \right\rangle", @"\langle x\rangle", @"⟨𝑥⟩")]
        [Arguments(@"\left| x \right|", @"|x|", @"|𝑥|")]
        [Arguments(@"\left\| x \right\|", @"\|x\|", @"‖𝑥‖")]
        [Arguments(@"\left\lfloor x \right\rfloor", @"\lfloor x\rfloor", @"⌊𝑥⌋")]
        [Arguments(@"\bigl( x \bigr)", @"(x)", @"(𝑥)")]
        [Arguments(@"\Big( x \Big)", @"(x)", @"(𝑥)")]
        [Arguments(@"\hat{x}\widehat{xy}\bar{x}\vec{v}\dot{x}\ddot{x}\tilde{x}\check{x}\breve{x}\acute{x}\grave{x}", @"\hat{x}\hat{xy}\bar{x}\vec{v}\dot{x}\ddot{x}\tilde{x}\check{x}\breve{x}\acute{x}\grave{x}", @"𝑥̂(𝑥𝑦)̂̄𝑥 𝑣⃗𝑥̇𝑥̈𝑥̃𝑥̌𝑥̆𝑥́𝑥̀")]
        [Arguments(@"\overline{x+y}\underline{z}", @"\overline{x+y}\underline{z}", @"¯(𝑥+𝑦)▁𝑧")]
        [Arguments(@"\overbrace{a+b}^{n}", @"\overset{n}{\overbrace{a+b}}", @"⏞(𝑎+𝑏)┴𝑛")]
        [Arguments(@"\underbrace{a+b}_{n}", @"\underset{n}{\underbrace{a+b}}", @"⏟(𝑎+𝑏)┬𝑛")]
        [Arguments(@"\operatorname{rank} A", @"\operatorname{rank}A", @"rank⁡𝐴")]
        [Arguments(@"\sum\limits_{i=1}^n i", @"\sum\limits_{i=1}^ni", @"∑1_(𝑖=1)^𝑛 𝑖")]
        [Arguments(@"\int\limits_0^1 f", @"\int\limits_0^1f", @"∫1₀¹ 𝑓")]
        [Arguments(@"\sum\nolimits_{i=1}^n i", @"\sum_{i=1}^ni", @"∑_(𝑖=1)^𝑛 𝑖")]
        [Arguments(@"\iint_D f\,dA + \oint_C F", @"\iint_Df\,dA+\oint_CF", @"∬_𝐷 〖𝑓 〗𝑑𝐴+∮_𝐶 𝐹")]
        [Arguments(@"\prod_{i} a_i \coprod \bigcup_i A_i \bigcap_j B_j", @"\prod_ia_i\coprod\bigcup_iA_i\bigcap_jB_j", @"∏_𝑖 𝑎_𝑖 ∐⋃_𝑖 𝐴_𝑖 ⋂_𝑗 𝐵_𝑗")]
        [Arguments(@"a \pmod{n}", @"a\;(\mod\,n)", @"𝑎 (mod⁡ 𝑛)")]
        [Arguments(@"a \bmod n", @"a \mod n", @"𝑎 mod⁡𝑛")]
        [Arguments(@"\mathrm{d}x\,\mathit{ab}\,\boldsymbol{\alpha}", @"\mathrm{d}x\,ab\,\boldsymbol{\alpha}", @"""d""𝑥 𝑎𝑏 𝜶")]
        [Arguments(@"x \pm y \mp z", @"x\pm y\mp z", @"𝑥±𝑦∓𝑧")]
        [Arguments(@"1,\ldots,n\quad 1+\cdots+n\quad\vdots\quad\ddots", @"1,\ldots,n\quad1+\cdots+n\quad\vdots\quad\ddots", @"1,…,𝑛 1+⋯+𝑛 ⋮ ⋱")]
        [Arguments(@"\boxed{E=mc^2}", @"\boxed{E=mc^2}", @"▭(𝐸=𝑚𝑐²)")]
        [Arguments(@"\phantom{x}y", @"\phantom{x}y", @"⟡𝑥 𝑦")]
        [Arguments(@"\cancel{x}", @"\cancel{x}", @"▭(143&𝑥)")]
        [Arguments(@"a \not= b \neq c \leq d \geq e", @"a\ne b\ne c\le d\ge e", @"𝑎≠𝑏≠𝑐≤𝑑≥𝑒")]
        [Arguments(@"f'(x)+f''(x)", @"f'(x)+f''(x)", @"𝑓′(𝑥)+𝑓″(𝑥)")]
        [Arguments(@"a^{b^c}", @"a^{b^c}", @"𝑎^𝑏^𝑐 ")]
        [Arguments(@"a_b^c", @"a_b^c", @"𝑎_𝑏^𝑐")]
        [Arguments(@"{}_a^b X", @"{}_a^bX", @"⬚_𝑎^𝑏 𝑋")]
        [Arguments(@"\tbinom{n}{k}+\dbinom{n}{k}", @"\binom{n}{k}+\binom{n}{k}", @"𝑛⒞𝑘+𝑛⒞𝑘")]
        [Arguments(@"x % comment", @"x", @"𝑥")]
        [Arguments(@"\displaystyle\sum_i i", @"\sum_ii", @"∑┬𝑖 𝑖")]
        [Arguments(@"a=b \tag{1}", @"a=b\tag{1}", @"𝑎=𝑏#(1)")]
        [Arguments(@"\nabla\cdot\vec{E}=\frac{\rho}{\varepsilon_0}", @"\nabla\cdot\vec{E}=\frac{\rho}{\varepsilon_0}", @"∇⋅𝐸⃗=𝜌/𝜀₀")]
        [Arguments(@"\lim_{x\to\infty}\left(1+\frac{1}{x}\right)^x=e", @"\lim_{x\to\infty}{\left(1+\frac{1}{x}\right)}^x=e", @"lim_(𝑥→∞) ⁡(1+1/𝑥)^𝑥=𝑒")]
        [Arguments(@"\det(A)\gcd(a,b)\max_i x_i", @"\det(A)\gcd(a,b)\max_ix_i", @"det⁡(𝐴)gcd⁡(𝑎,𝑏)max_𝑖 𝑥_𝑖")]
        [Arguments(@"\mathbb{N}\subset\mathbb{Z}\subset\mathbb{Q}", @"\mathbb{N}\subset\mathbb{Z}\subset\mathbb{Q}", @"ℕ⊂ℤ⊂ℚ")]
        [Arguments(@"\sin^2\theta+\cos^2\theta=1", @"\sin^2\theta+\cos^2\theta=1", @"sin²⁡𝜃+cos²⁡𝜃=1")]
        [Arguments(@"e^{i\pi}+1=0", @"e^{i\pi}+1=0", @"𝑒^𝑖𝜋+1=0")]
        [Arguments(@"\frac{\partial^2 u}{\partial t^2}=c^2\nabla^2u", @"\frac{\partial^2u}{\partial t^2}=c^2\nabla^2u", @"𝜕²𝑢/𝜕𝑡²=𝑐²∇²𝑢")]
        [Arguments(@"\begin{bmatrix}1&2\\3&4\end{bmatrix}", @"\begin{bmatrix}1&2\\3&4\end{bmatrix}", @"ⓢ(1&2@3&4)")]
        [Arguments(@"\begin{vmatrix}a&b\\c&d\end{vmatrix}", @"\begin{vmatrix}a&b\\c&d\end{vmatrix}", @"⒱(𝑎&𝑏@𝑐&𝑑)")]
        [Arguments(@"\begin{Bmatrix}1\end{Bmatrix}", @"\begin{Bmatrix}1\end{Bmatrix}", @"Ⓢ(1)")]
        [Arguments(@"\begin{matrix}a&b\end{matrix}", @"\begin{matrix}a&b\end{matrix}", @"■(𝑎&𝑏)")]
        [Arguments(@"\begin{smallmatrix}a&b\end{smallmatrix}", @"\begin{matrix}a&b\end{matrix}", @"■(𝑎&𝑏)")]
        [Arguments(@"\begin{dcases}1&x\end{dcases}", @"\begin{cases}1&x\end{cases}", @"Ⓒ(1&𝑥)")]
        [Arguments(@"\begin{alignat}{2}a&=b\end{alignat}", @"\begin{aligned}a&=b\end{aligned}", @"█(𝑎&=𝑏)")]
        [Arguments(@"\xrightarrow{f}", @"\overset{f}{\to}", @"→┴𝑓")]
        [Arguments(@"\mathop{\mathrm{argmin}}_x f", @"\operatorname{argmin}_xf", @"argmin⁡_𝑥 𝑓")]
        [Arguments(@"x\in\left\{1,2\right\}", @"x\in\{1,2\}", @"𝑥∈{1, 2}")]
        [Arguments(@"\{x\mid x>0\}", @"\{x\mid x>0\}", @"{𝑥∣𝑥>0}")]
        [Arguments(@"a\mid b", @"a\mid b", @"𝑎\∣𝑏")]
        [Arguments(@"\langle\psi\mid", @"\langle\psi\mid", @"⟨𝜓\∣")]
        [Arguments(@"\mid\psi\rangle", @"\mid\psi\rangle", @"\∣𝜓\⟩")]
        [Arguments(@"\overset{\to}{a}\overset{\to}{b}", @"\overset{\to}{a}\overset{\to}{b}", @"𝑎┴→ 𝑏┴→")]
        [Arguments(@"\mathring{a}\overleftarrow{b}", @"\mathring{a}\overleftarrow{b}", @"𝑎┴˚ 𝑏┴←")]
        [Arguments(@"\vphantom{\sum}_1^2\sum_3^4", @"\vphantom{\sum}_1^2\sum_3^4", @"⇳∑₁²∑₃⁴")]
        [Arguments(@"\_\{\}", @"\_\{\}", @"\_{}")]
        [Arguments(@"\Gamma=\mathit{\Gamma}", @"\Gamma=\mathit{\Gamma}", @"Γ=𝛤")]
        [Arguments(@"\xrightarrow[y]{x} \xleftarrow x", @"\underset{y}{\overset{x}{\to}}\overset{x}{\leftarrow}", @"→┬𝑦┴𝑥 ←┴𝑥")]
        [Arguments(@"\underset ab+\underleftarrow{c}", @"\underset{a}{b}+\underset{\leftarrow}{c}", @"𝑏┬𝑎+𝑐┬←")]
        [Arguments(@"\sideset{_1^2}{_3^4}\sum", @"\vphantom{\sum}_1^2\sum_3^4", @"⇳∑₁²∑₃⁴")]
        [Arguments(@"\prescript{14}{6}{C}+\prescript{}{2}{x}", @"{}_6^{14}C+{}_2x", @"_6^14 𝐶+(_2)𝑥")]
        [Arguments(@"\genfrac\{]{0pt}{}{a}{b}+\dbinom a b", @"\{\genfrac{}{}{0pt}{}{a}{b}]+\binom{a}{b}", @"{𝑎¦𝑏]+𝑎⒞𝑏")]
        [Arguments(@"a \pod{n}+\not a", @"a\quad(n)+\overset{̸}{a}", @"𝑎 (𝑛)+𝑎̸")]
        [Arguments(@"^3+{'x}", @"{}^3+{}'x", @"⬚³+⬚′𝑥")]
        [Arguments(@"a_{m,1}+\color{}b", @"a_{m,1}+b", @"𝑎_(𝑚, 1)+𝑏")]
        [Arguments(@"\begin{pmatrix*}[r]1&2\end{pmatrix*}", @"\begin{pmatrix}1&2\end{pmatrix}", @"⒨(1&2)")]
        [Arguments(@"x ^ { 2 } _ i", @"x_i^2", @"𝑥_𝑖^2")]
        [Arguments(@"\sum_{\substack{i<n\\i\ne j}} a_i", @"\sum_{\begin{matrix}i<n\\i\ne j\end{matrix}}a_i", @"∑_■(𝑖<𝑛@𝑖≠𝑗) 𝑎_𝑖")]
        [Arguments(@"a\cdot b\times c\div d", @"a\cdot b\times c\div d", @"𝑎⋅𝑏×𝑐÷𝑑")]
        [Arguments(@"\hbar\ell\Re\Im\aleph", @"\hbar\ell  \Re\Im\aleph", @"ℏℓ Re⁡Im⁡ℵ")]
        [Arguments(@"\langle a, b\rangle", @"\langle a,b\rangle", @"⟨𝑎,𝑏⟩")]
        [Arguments(@"|x|", @"|x|", @"|𝑥|")]
        [Arguments(@"\big| x \big|", @"|x|", @"|𝑥|")]
        [Arguments(@"\sqrt{\sqrt{x}}", @"\sqrt{\sqrt{x}}", @"√√𝑥")]
        [Arguments(@"\frac{1}{\frac{1}{x}}", @"\frac{1}{\frac{1}{x}}", @"1/(1/𝑥)")]
        [Arguments(@"\hspace{1em}x", @"x", @"𝑥")]
        [Arguments(@"\mathrm{ABC}", @"\text{ABC}", @"""ABC""")]
        [Arguments(@"\mathbf{AB}\mathbf{1}", @"\mathbf{A}\mathbf{B}\mathbf{1}", @"𝐀𝐁𝟏")]
        [Arguments(@"\textbf{bold}", @"\text{bold}", @"""bold""")]
        [Arguments(@"\textit{it}", @"\text{it}", @"""it""")]
        [Arguments(@"x^\prime", @"x'", @"𝑥′")]
        [Arguments(@"\vec{AB}", @"\vec{AB}", @"(𝐴𝐵)⃗")]
        [Arguments(@"\overrightarrow{AB}", @"\vec{AB}", @"(𝐴𝐵)⃗")]
        [Arguments(@"\Vert x\Vert", @"\|x\|", @"‖𝑥‖")]
        [Arguments(@"\lVert x\rVert", @"\|x\|", @"‖𝑥‖")]
        [Arguments(@"\lvert x\rvert", @"|x|", @"|𝑥|")]
        [Arguments(@"\varphi\varepsilon\vartheta", @"\varphi\varepsilon\vartheta", @"𝜑𝜀𝜗")]
        [Arguments(@"\infty-\infty", @"\infty-\infty", @"∞−∞")]
        [Arguments(@"\$5", @"\$5", @"$5")]
        [Arguments(@"\#\&\%", @"\#\&\%", @"\#\&%")]
        [Arguments(@"\left( \begin{matrix} a \\ b \end{matrix} \right)", @"\begin{pmatrix}a\\b\end{pmatrix}", @"⒨(𝑎@𝑏)")]        public async Task ReadsLaTeX(string latex, string expectedLaTeX, string expectedUnicodeMath)
        {
            var equation = Equation.Parse(latex, EquationFormat.LaTeX);
            await Assert.That(equation.ToString(EquationFormat.LaTeX)).IsEqualTo(expectedLaTeX);
            await Assert.That(equation.ToString(EquationFormat.UnicodeMath)).IsEqualTo(expectedUnicodeMath);
        }

        [Test]
        [Arguments(@"\left\langle a \middle| b \right\rangle", @"\langle a|b\rangle", @"⟨𝑎│𝑏⟩")]
        [Arguments(@"\bigl( x \bigr)", @"(x)", @"(𝑥)")]
        [Arguments(@"\bm x", @"\boldsymbol{x}", @"𝒙")]
        [Arguments(@"\bm{x}", @"\boldsymbol{x}", @"𝒙")]
        [Arguments(@"\Vert x \Vert", @"\|x\|", @"‖𝑥‖")]
        [Arguments(@"\color{red}{\{x\}} + y", @"\{x\}+y", @"{𝑥}+𝑦")]
        [Arguments(@"x_ \text{ab}", @"x_{\text{ab}}", @"𝑥_""ab""")]
        [Arguments(@"x_\alpha", @"x_\alpha", @"𝑥_𝛼")]
        [Arguments(@"x^\sqrt{2}", @"x^{\sqrt{2}}", @"𝑥^√2")]
        [Arguments(@"x^\hat{a}", @"x^{\hat{a}}", @"𝑥^𝑎̂")]
        [Arguments(@"\def\f{x}\f+\f", @"x+x", @"𝑥+𝑥")]
        [Arguments(@"\def\g#1{#1^2}\g{a}", @"a^2", @"𝑎²")]
        [Arguments(@"\newcommand{\h}[2]{#1+#2}\h{a}{b}", @"a+b", @"𝑎+𝑏")]
        [Arguments(@"\newcommand\k{k}\k", @"k", @"𝑘")]
        [Arguments(@"\begin{equation}\label{x}a\end{equation}", @"a\tag{x}", @"𝑎#(x)")]
        [Arguments(@"\begin{equation}\label{eq:1}a", @"a\tag{1}", @"𝑎#(1)")]
        [Arguments(@"\langle a \vert b \rangle", @"\langle a|b\rangle", @"⟨𝑎│𝑏⟩")]
        [Arguments(@"\left. x \right. ", @"x", @"𝑥")]
        [Arguments(@"\mathop{\sum}_i", @"\sum_i", @"∑_𝑖")]
        [Arguments(@"\mathop{x}\limits_0^1", @"\underset{0}{\overset{1}{x}}", @"𝑥┬0┴1")]
        [Arguments(@"\underset{0}{\overset{1}{x}}", @"\underset{0}{\overset{1}{x}}", @"𝑥┬0┴1")]
        [Arguments(@"\overbrace3^a_x", @"\underset{x}{\overset{a}{\overbrace{3}}}", @"⏞3┬𝑥┴𝑎")]
        [Arguments(@"\underset{x}{\overset{a}{\overbrace{3}}}", @"\underset{x}{\overset{a}{\overbrace{3}}}", @"⏞3┬𝑥┴𝑎")]
        [Arguments(@"\underbrace{a+b}_n^x+c", @"\underset{n}{\overset{x}{\underbrace{a+b}}}+c", @"⏟(𝑎+𝑏)┬𝑛┴𝑥+𝑐")]
        [Arguments(@"\underset{n}{\overset{x}{\underbrace{a+b}}}+c", @"\underset{n}{\overset{x}{\underbrace{a+b}}}+c", @"⏟(𝑎+𝑏)┬𝑛┴𝑥+𝑐")]
        [Arguments(@"\xleftarrow{g}", @"\overset{g}{\leftarrow}", @"←┴𝑔")]
        [Arguments(@"\pmod n", @"(\mod\,n)", @"(mod⁡ 𝑛)")]
        [Arguments(@"\hspace*{2cm}x", @"x", @"𝑥")]
        [Arguments(@"a\;b\:c\,d\!e\ f", @"a\;b\:c\,de~f", @"𝑎 𝑏 𝑐 𝑑𝑒 𝑓")]
        [Arguments(@"\mathrm{d}\mathbf{d}\mathit{d}\mathsf{d}\mathtt{d}\mathcal{D}\mathfrak{d}\mathbb{D}", @"\mathrm{d}\mathbf{d}d\mathsf{d}\mathtt{d}\mathcal{D}\mathfrak{d}\mathbb{D}", @"""d""𝐝𝑑𝖽𝚍𝒟𝔡𝔻")]
        [Arguments(@"\mathrm{x_1}", @"\mathrm{x}_1", @"""x""₁")]
        [Arguments(@"\mathrm{A \{b\}}", @"\mathrm{A}\{\mathrm{b}\}", @"""A""{""b""}")]
        [Arguments(@"\mathsf{\text{var} = 1+\{b\}}", @"\text{var}=\mathsf{1}+\{\mathsf{b}\}", @"""var""=𝟣+{𝖻}")]
        [Arguments(@"\mathbf{\text{var}+x}", @"\text{var}+\mathbf{x}", @"""var""+𝐱")]
        [Arguments(@"\boldsymbol{1}\boldsymbol{x1\alpha}", @"\mathbf{1}\boldsymbol{x}\mathbf{1}\boldsymbol{\alpha}", @"𝟏𝒙𝟏𝜶")]
        [Arguments(@"\mathbf{\mathrm{x}y}", @"\mathrm{x}\mathbf{y}", @"""x""𝐲")]
        [Arguments(@"\mathbf{\Gamma+\sin x}", @"\mathbf{\Gamma}+\sin\mathbf{x}", @"𝚪+sin⁡𝐱")]
        [Arguments(@"\mathbf{\alpha}", @"\mathbf{\alpha}", @"𝛂")]
        [Arguments(@"\tag*{A}", @"\tag{A}", @"#A")]
        [Arguments(@"{\color{blue} a \{ b} + c", @"a\{b+c", @"𝑎\{𝑏+𝑐")]
        public async Task ReadsLessCommonCommands(string latex, string expectedLaTeX, string expectedUnicodeMath)
        {
            await ReadsLaTeX(latex, expectedLaTeX, expectedUnicodeMath);
        }

        // a slash and a bar are ordinary characters of LaTeX, which UnicodeMath reads as a fraction and an absolute value
        [Test]
        [Arguments(@"a/b", @"a/b", @"𝑎\/𝑏")]
        [Arguments(@"(a+b)/2", @"(a+b)/2", @"(𝑎+𝑏)\/2")]
        [Arguments(@"\left( a \middle/ b \right)", @"(a/b)", @"(𝑎\/𝑏)")]
        [Arguments(@"\left( a \middle\| b \right)", @"(a\|b)", @"(𝑎\‖𝑏)")]
        [Arguments(@"\langle a | b \rangle | c", @"\langle a|b\rangle|c", @"⟨𝑎│𝑏⟩\|𝑐")]
        [Arguments(@"a|b", @"a|b", @"𝑎\|𝑏")]
        [Arguments(@"(a\|b)", @"(a\|b)", @"(𝑎\‖𝑏)")]
        [Arguments(@"P(c|d)", @"P(c|d)", @"𝑃(𝑐│𝑑)")]
        [Arguments(@"\{x | x>0\}", @"\{x|x>0\}", @"{𝑥│𝑥>0}")]
        [Arguments(@"|r\rangle\langle r|\psi\rangle", @"|r\rangle\langle r|\psi\rangle", @"|𝑟⟩⟨𝑟│𝜓⟩")]
        [Arguments(@"|x|+|y|", @"|x|+|y|", @"|𝑥|+|𝑦|")]
        [Arguments(@"\left.\frac{d}{dx}\right|_{x=0}", @"{\left.\frac{d}{dx}\right|}_{x=0}", @"├𝑑/𝑑𝑥 ┤|_(𝑥=0)")]
        [Arguments(@"{a \over b}", @"\frac{a}{b}", @"𝑎/𝑏")]
        [Arguments(@"x_\frac 1 2", @"x_{\frac{1}{2}}", @"𝑥_ 1/2 ")]
        [Arguments(@"\frac 1 2+\binom n k+\frac{a} b", @"\frac{1}{2}+\binom{n}{k}+\frac{a}{b}", @"1/2+𝑛⒞𝑘+𝑎/𝑏")]
        public async Task ReadsSlashesAndBarsAsLaTeX(string latex, string expectedLaTeX, string expectedUnicodeMath)
        {
            await ReadsLaTeX(latex, expectedLaTeX, expectedUnicodeMath);
            await Assert.That(Equation.Parse(expectedLaTeX, EquationFormat.LaTeX)).IsEqualTo(Equation.Parse(latex, EquationFormat.LaTeX)).Because("the LaTeX written reads back");
        }

        // the demos of math.webc.site, whose LaTeX and UnicodeMath read back to the same equation
        [Test]
        [Arguments(@"\left. \frac{df}{dx} \right| _0", @"{\left.\frac{df}{dx}\right|}_0", @"├𝑑𝑓/𝑑𝑥 ┤|₀")]
        [Arguments(@"\sum _{i=1} ^n i", @"\sum_{i=1}^ni", @"∑_(𝑖=1)^𝑛 𝑖")]
        [Arguments(@"a \lt 1 \gt b", @"a<1>b", @"𝑎<1>𝑏")]
        [Arguments(@"y \lt 1 \tag{1}", @"y<1\tag{1}", @"𝑦<1#(1)")]
        [Arguments(@"{\rm rxn}", @"\text{rxn}", @"""rxn""")]
        [Arguments(@"{\rm d} x", @"\mathrm{d}x", @"""d""𝑥")]
        [Arguments(@"{\bf x} + {\it ab} + {\cal F} + {\sf s} + {\tt t}", @"\mathbf{x}+ab+\mathcal{F}+\mathsf{s}+\mathtt{t}", @"𝐱+𝑎𝑏+ℱ+𝗌+𝚝")]
        [Arguments(@"\Delta H_{\rm rxn}^\circ = -R T \ln K", @"\Delta H_{\text{rxn}}^\circ=-RT \ln K", @"Δ𝐻_""rxn""^∘ =−𝑅𝑇 ln⁡𝐾")]
        [Arguments(@"\Gamma=\Omega", @"\Gamma=\Omega", @"Γ=Ω")]
        [Arguments(@"\theta = 45\degree", @"\theta=45\degree", @"𝜃=45°")]
        [Arguments(@"f \circ g", @"f\circ g", @"𝑓∘𝑔")]
        [Arguments(@"\bar{x} = \frac{1}{n} \cdot \sum_{i=1}^n x_i", @"\bar{x}=\frac{1}{n}\cdot\sum_{i=1}^nx_i", @"̄𝑥=1/𝑛 ⋅∑_(𝑖=1)^𝑛 𝑥_𝑖")]
        [Arguments(@"\bar{A\cup B}+\overline{A\cup B}", @"\bar{A\cup B}+\overline{A\cup B}", @"̄(𝐴∪𝐵)+¯(𝐴∪𝐵)")]
        [Arguments(@"a\stackrel{n\to\infty}{\longrightarrow}b", @"a\overset{n\to\infty}{\longrightarrow}b", @"𝑎⟶┴(𝑛→∞) 𝑏")]
        [Arguments(@"x\xrightarrow{f}y", @"x\overset{f}{\to}y", @"𝑥→┴𝑓 𝑦")]
        [Arguments(@"{\overset{a}{b}}c", @"\overset{a}{b}c", @"𝑏┴𝑎 𝑐")]
        public async Task ReadsLaTeXThatWritesBack(string latex, string expectedLaTeX, string expectedUnicodeMath)
        {
            await ReadsLaTeX(latex, expectedLaTeX, expectedUnicodeMath);
            var equation = Equation.Parse(latex, EquationFormat.LaTeX);
            await Assert.That(Equation.Parse(expectedLaTeX, EquationFormat.LaTeX)).IsEqualTo(equation).Because("the LaTeX written reads back");
            await Assert.That(Equation.Parse(expectedUnicodeMath, EquationFormat.UnicodeMath)).IsEqualTo(equation).Because("the UnicodeMath written reads back");
        }

        [Test]
        [Arguments(@"{x}^2", @"x^2")]
        [Arguments(@"\overset{a}{b}^2", @"\overset{a}{b}^2")]
        [Arguments(@"{a+b}^2", @"{a+b}^2")]
        [Arguments(@"{a+b}_n", @"{a+b}_n")]
        [Arguments(@"x_\text{ab}", @"x_{\text{ab}}")]
        [Arguments(@"y^\frac12", @"y^{\frac{1}{2}}")]
        [Arguments(@"e^\frac{x}{2}", @"e^{\frac{x}{2}}")]
        [Arguments(@"\mathcal{G}_n", @"\mathcal{G}_n")]
        [Arguments(@"\mathbb{R}^n", @"\mathbb{R}^n")]
        [Arguments(@"\left(a\right.", @"\left(a\right.")]
        [Arguments(@"\left.a\right)", @"\left.a\right)")]
        [Arguments(@"\left.\frac{d}{dx}\right|_{x=0}", @"{\left.\frac{d}{dx}\right|}_{x=0}")]
        [Arguments(@"\left]a\right[", @"\left]a\right[")]
        [Arguments(@"\lim _{n\to\infty} x", @"\lim_{n\to\infty}x")]
        [Arguments(@"\sin ^2\theta", @"\sin^2\theta")]
        [Arguments(@"\underset{x\to0}{\lim}", @"\lim_{x\to0}")]
        [Arguments(@"\overset{def}{=}", @"\overset{def}{=}")]
        [Arguments(@"\stackrel{?}{=}", @"\overset{?}{=}")]
        [Arguments(@"\overset{n}{x+y}", @"\overset{n}{x+y}")]
        [Arguments(@"\color{red}{x}+y", @"x+y")]
        [Arguments(@"\color{red} x+y", @"x+y")]
        [Arguments(@"\colorbox{yellow}{x}", @"x")]
        public async Task ReadsGroupsScriptsAndDelimiters(string latex, string expected)
        {
            await Assert.That(Equation.Parse(latex, EquationFormat.LaTeX).ToString(EquationFormat.LaTeX)).IsEqualTo(expected);
        }

        [Test]
        [Arguments(@"\text{a\{b\}\_c 50\% \& \$5 \#1}", "a{b}_c 50% & $5 #1")]
        [Arguments(@"\text{a\textbackslash{}b \textbackslash c}", @"a\b \ c")]
        [Arguments(@"\text{f(x) ""q""}", @"f(x) ""q""")]
        [Arguments(@"\text{ if }", " if ")]
        [Arguments(@"\mbox{word}", "word")]
        [Arguments(@"\textrm{word}", "word")]
        [Arguments(@"\textbf{word}", "word")]
        public async Task ReadsText(string latex, string expected)
        {
            var math = Equation.Parse(latex, EquationFormat.LaTeX).ToMathML();
            // the spaces of text are no-break spaces, which MathML does not collapse
            await Assert.That(string.Concat(math.Descendants(MathMLNamespaces.MathML + "mtext").Select(t => t.Value)).Replace('\u00A0', ' ')).IsEqualTo(expected);
        }

        [Test]
        public async Task ReadsTextWithMathInside()
        {
            var equation = Equation.Parse(@"\text{if $x>0$ and $y$}", EquationFormat.LaTeX);
            await Assert.That(equation.ToString(EquationFormat.LaTeX)).IsEqualTo(@"\text{if }x>0\text{ and }y");
        }

        [Test]
        [Arguments(@"a\,b\:c\;d\quad e\qquad f~g\ h", @"a\,b\:c\;d\quad e\quad\quad f~g~h")]
        [Arguments(@"a\!b", "ab")]
        [Arguments(@"a\>b", @"a\:b")]
        [Arguments(@"\hspace{1em}x", "x")]
        public async Task ReadsSpaces(string latex, string expected)
        {
            await Assert.That(Equation.Parse(latex, EquationFormat.LaTeX).ToString(EquationFormat.LaTeX)).IsEqualTo(expected);
        }

        [Test]
        [Arguments(@"\begin{array}{ll}1&x>0\\0&x\le0\end{array}", @"\begin{matrix}1&x>0\\0&x\le0\end{matrix}")]
        [Arguments(@"f(x)=\left\{\begin{array}{ll}1 & x>0\\0 & x\le 0\end{array}\right.", @"f(x)=\begin{cases}1&x>0\\0&x\le0\end{cases}")]
        [Arguments(@"\begin{array}{c|c}a&b\end{array}", @"\begin{matrix}a&b\end{matrix}")]
        [Arguments(@"\begin{align*}a&=b\\c&=d\end{align*}", @"\begin{aligned}a&=b\\c&=d\end{aligned}")]
        [Arguments(@"\begin{align}a&=b\end{align}", @"\begin{aligned}a&=b\end{aligned}")]
        [Arguments(@"\begin{gather}a=b\\c=d\end{gather}", @"\begin{aligned}a=b\\c=d\end{aligned}")]
        [Arguments(@"\begin{split}a&=b\end{split}", @"\begin{aligned}a&=b\end{aligned}")]
        [Arguments(@"\begin{eqnarray}a&=&b\end{eqnarray}", @"\begin{aligned}a&=&b\end{aligned}")]
        [Arguments(@"\begin{cases}1&x>0\\0&\text{otherwise}\end{cases}", @"\begin{cases}1&x>0\\0&\text{otherwise}\end{cases}")]
        [Arguments(@"\begin{pmatrix}a&b\\c&d\end{pmatrix}", @"\begin{pmatrix}a&b\\c&d\end{pmatrix}")]
        [Arguments(@"\begin{Vmatrix}1\end{Vmatrix}", @"\begin{Vmatrix}1\end{Vmatrix}")]
        [Arguments(@"\left( \begin{matrix} a \\ b \end{matrix} \right)", @"\begin{pmatrix}a\\b\end{pmatrix}")]
        [Arguments(@"\begin{equation}\label{eq42}a=b\end{equation}", @"a=b\tag{42}")]
        [Arguments(@"\begin{equation*}a=b\end{equation*}", @"a=b")]
        [Arguments(@"\begin{equation}a=b\end{equation}", @"a=b")]
        public async Task ReadsEnvironments(string latex, string expected)
        {
            await Assert.That(Equation.Parse(latex, EquationFormat.LaTeX).ToString(EquationFormat.LaTeX)).IsEqualTo(expected);
        }

        [Test]
        [Arguments(@"\begin{foo}a\end{foo}")]
        [Arguments(@"\begin{tabular}{ll}a&b\end{tabular}")]
        public async Task RejectsUnknownEnvironments(string latex)
        {
            var exception = await Assert.That(() => Equation.Parse(latex, EquationFormat.LaTeX)).ThrowsExactly<FormatException>();
            await Assert.That(exception!.Message).Contains("environment");
        }

        [Test]
        [Arguments("$x$", false)]
        [Arguments("$$x$$", true)]
        [Arguments(@"\(x\)", false)]
        [Arguments(@"\[x\]", true)]
        [Arguments(@"\displaystyle x", true)]
        [Arguments("x", false)]
        public async Task ReadsDelimitersAsDisplay(string latex, bool display)
        {
            var equation = Equation.Parse(latex, EquationFormat.LaTeX);
            await Assert.That(equation.IsDisplay).IsEqualTo(display);
            await Assert.That(equation.ToString(EquationFormat.LaTeX)).IsEqualTo("x");
        }

        [Test]
        [Arguments(@"\def\R{\mathbb{R}} x\in\R", @"x\in\mathbb{R}")]
        [Arguments(@"\newcommand{\pd}[2]{\frac{\partial #1}{\partial #2}}\pd{f}{x}", @"\frac{\partial f}{\partial x}")]
        [Arguments(@"\newcommand{\N}{\mathbb{N}}\N", @"\mathbb{N}")]
        public async Task ExpandsMacros(string latex, string expected)
        {
            await Assert.That(Equation.Parse(latex, EquationFormat.LaTeX).ToString(EquationFormat.LaTeX)).IsEqualTo(expected);
        }

        [Test]
        public async Task ExpandsMacrosOfTheOptions()
        {
            var options = new EquationOptions { LaTeXMacros = new Dictionary<string, string> { ["R"] = @"\mathbb{R}", ["half"] = @"\frac{1}{2}" } };
            await Assert.That(Equation.Parse(@"\half x\in\R", EquationFormat.LaTeX, options).ToString(EquationFormat.LaTeX)).IsEqualTo(@"\frac{1}{2}x\in\mathbb{R}");
        }

        [Test]
        [Arguments(@"\newenvironment{wrapper}{a +}{+ c} \begin{wrapper} b \end{wrapper}", @"a+b+c")]
        [Arguments(@"\newenvironment{paren}[1]{\left( #1 +}{\right)} \begin{paren}{x} y \end{paren}", @"\left(x+y\right)")]
        [Arguments(@"\newenvironment{e}[2][1]{#1+#2}{} \begin{e}{a} b\end{e}", @"1+ab")]
        [Arguments(@"\newenvironment{e}[2][1]{#1+#2}{} \begin{e}[2]{a}\end{e}", @"2+a")]
        [Arguments(@"\newenvironment{g}{(}{)} \begin{g} a \begin{g} b \end{g} \end{g}", @"(a(b))")]
        [Arguments(@"\newenvironment{al}{\alpha}{\beta}\begin{al}x\end{al}y", @"\alpha x\beta y")]
        [Arguments(@"\newenvironment{e}{a}{} \renewenvironment{e}{b}{} \begin{e}\end{e}", @"b")]
        [Arguments(@"\newenvironment{m}{\begin{pmatrix}}{\end{pmatrix}} \begin{m} 1 & 2 \end{m}", @"\begin{pmatrix}1&2\end{pmatrix}")]
        [Arguments(@"\newenvironment{h}{\newcommand{\f}[1]{##1^2}}{} \begin{h}\f{x}\end{h}", @"x^2")]
        public async Task ExpandsEnvironments(string latex, string equivalent)
        {
            await Assert.That(Equation.Parse(latex, EquationFormat.LaTeX)).IsEqualTo(Equation.Parse(equivalent, EquationFormat.LaTeX));
        }

        [Test]
        public async Task RejectsEnvironmentsThatExpandForever()
        {
            await Assert.That(Equation.TryParse(@"\newenvironment{r}{\begin{r}}{\end{r}} \begin{r} x \end{r}", EquationFormat.LaTeX, out _)).IsFalse();
        }

        [Test]
        [Arguments(@"\foo x", @"Unknown control word \foo in the LaTeX.")]
        [Arguments(@"a+\undefinedcommand", @"Unknown control word \undefinedcommand in the LaTeX.")]
        [Arguments(@"\sgn r", @"Unknown control word \sgn in the LaTeX.")]
        [Arguments(@"\lg d", @"Unknown control word \lg in the LaTeX.")]
        [Arguments(@"\liminf v", @"Unknown control word \liminf in the LaTeX.")]
        [Arguments(@"a \backslash b", @"Unknown control word \backslash in the LaTeX.")]
        [Arguments(@"\sgn2", @"Unknown control word \sgn in the LaTeX.")]
        public async Task RejectsUnknownControlWordsWhenStrict(string latex, string message)
        {
            await Assert.That(() => Equation.Parse(latex, EquationFormat.LaTeX, Strict)).Throws<FormatException>().WithMessage(message);
            await Assert.That(Equation.Parse(latex, EquationFormat.LaTeX).ToMathML().Descendants(MathMLNamespaces.MathML + "mtext")).IsNotEmpty();
        }

        [Test]
        [Arguments(@"\sgn r", @"\sgn", "r")]
        [Arguments(@"\lg d", @"\lg", "d")]
        [Arguments(@"\liminf v", @"\liminf", "v")]
        [Arguments(@"a \backslash b", @"\backslash", "b")]
        [Arguments(@"\sgn  r", @"\sgn", "r")]
        [Arguments(@"\sgn{r}", @"\sgn", "r")]
        [Arguments(@"\text{\sgn} r", @"\sgn", "r")]
        [Arguments(@"\sgn2", @"\sgn", "2")]
        public async Task EndsUnknownControlWordsAtTheirLetters(string latex, string text, string after)
        {
            var math = Equation.Parse(latex, EquationFormat.LaTeX).ToMathML();
            await Assert.That(math.Descendants(MathMLNamespaces.MathML + "mtext").Select(e => e.Value).ToList()).IsEquivalentTo(new[] { text });
            await Assert.That(math.Descendants().Where(e => e.Name.LocalName is "mi" or "mn").Select(e => e.Value).Last()).IsEqualTo(after);
        }

        [Test]
        public async Task KeepsKnownControlWordsWhenStrict()
        {
            await Assert.That(Equation.Parse(@"\alpha+\frac{1}{2}\text{\foo}", EquationFormat.LaTeX, Strict).ToString(EquationFormat.LaTeX)).IsEqualTo(@"\alpha+\frac{1}{2}\text{\textbackslash{}foo}");
        }

        [Test]
        [Arguments(@"a\\b")]
        [Arguments(@"\frac{a}")]
        [Arguments(@"x_\frac{1}")]
        [Arguments(@"\text{unclosed")]
        public async Task RejectsLaTeXThatIsNotAnEquation(string latex)
        {
            await Assert.That(() => Equation.Parse(latex, EquationFormat.LaTeX)).ThrowsExactly<FormatException>();
        }

        [Test]
        [Arguments(@"a\square b", "□")]
        [Arguments(@"a\blacksquare b", "■")]
        [Arguments(@"a\surd b", "√")]
        [Arguments(@"a\triangleq b", "≜")]
        [Arguments(@"a\eqsim b", "≂")]
        [Arguments(@"a\doteqdot b", "≑")]
        [Arguments(@"a\rightsquigarrow b", "↝")]
        [Arguments(@"a\leftrightsquigarrow b", "↭")]
        [Arguments(@"a\measuredangle b", "∡")]
        [Arguments(@"a\sphericalangle b", "∢")]
        [Arguments(@"a\mho b", "℧")]
        [Arguments(@"a\yen b", "¥")]
        [Arguments(@"a\circledR b", "®")]
        [Arguments(@"a\S b", "§")]
        [Arguments(@"a\P b", "¶")]
        [Arguments(@"a\geqslant b", "⩾")]
        [Arguments(@"a\leqslant b", "⩽")]
        [Arguments(@"a\therefore b", "∴")]
        [Arguments(@"a\coloneqq b", "≔")]
        [Arguments(@"a\dblcolon b", "∷")]
        [Arguments(@"a\ngtr b", "≯")]
        [Arguments(@"a\lozenge b", "◊")]
        [Arguments(@"a\oslash b", "⊘")]
        [Arguments(@"a\succeq b", "⪰")]
        [Arguments(@"a\mid b", "∣")]
        public async Task ReadsSymbolsThatUnicodeMathHasNoControlWordFor(string latex, string symbol)
        {
            var math = Equation.Parse(latex, EquationFormat.LaTeX, Strict).ToMathML();
            await Assert.That(math.Elements().Select(e => e.Value).ToList()).IsEquivalentTo(new[] { "a", symbol, "b" });
        }

        [Test]
        [Arguments("□", @"a\square b")]
        [Arguments("√", @"a{\surd}b")]
        [Arguments("≦", @"a\leqq b")]
        [Arguments("↠", @"a\twoheadrightarrow b")]
        [Arguments("⇄", @"a\rightleftarrows b")]
        [Arguments("≼", @"a\preccurlyeq b")]
        [Arguments("∌", @"a\not\ni b")]
        [Arguments("〈", @"a\langle b")]
        public async Task WritesTheSymbolsOfAmssymbAsCommands(string symbol, string latex)
        {
            var equation = Equation.Parse("<math><mi>a</mi><mo>" + symbol + "</mo><mi>b</mi></math>", EquationFormat.MathML);
            var written = equation.ToString(EquationFormat.LaTeX);
            await Assert.That(written).IsEqualTo(latex);
            var back = Equation.Parse(written, EquationFormat.LaTeX, Strict);
            await Assert.That(Equation.Parse(back.ToString(EquationFormat.LaTeX), EquationFormat.LaTeX)).IsEqualTo(back);
        }
    }
}
