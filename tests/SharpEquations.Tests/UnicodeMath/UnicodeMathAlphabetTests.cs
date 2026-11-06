namespace SharpEquations.Tests.UnicodeMath
{
    using System.Threading.Tasks;
    using SharpEquations;

    /// <summary>
    /// The control words of the mathematical alphanumerics (\mbfA, \Bbb1, \misolب): the styled Latin, Greek and
    /// Arabic letters and digits, the letters a style lacks (left as they are) and their LaTeX fonts.
    /// </summary>
    public class UnicodeMathAlphabetTests
    {
        [Test]
        [Arguments(@"\mbfA+\mitA+\mbfitA+\mscrA+\mbfscrA+\mfrakA+\BbbA+\mbffrakA+\msansA+\mbfsansA+\mitsansA+\mbfitsansA+\mttA", @"𝐀+𝐴+𝑨+𝒜+𝓐+𝔄+𝔸+𝕬+𝖠+𝗔+𝘈+𝘼+𝙰", @"\mathbf{A}+A+\boldsymbol{A}+\mathcal{A}+\mathcal{A}+\mathfrak{A}+\mathbb{A}+\mathfrak{A}+\mathsf{A}+\mathsf{A}+\mathsf{A}+\mathsf{A}+\mathtt{A}")]
        [Arguments(@"\mbfalpha+\mitalpha+\mbfitalpha+\mbfsansalpha+\mbfitsansalpha", @"𝛂+𝛼+𝜶+𝝰+𝞪", @"\mathbf{\alpha}+\alpha+\boldsymbol{\alpha}+\mathsf{\alpha}+\mathsf{\alpha}")]
        [Arguments(@"\Bbb1+\msans1+\mbfsans1+\mtt1", @"𝟙+𝟣+𝟭+𝟷", @"\mathbb{1}+\mathsf{1}+\mathsf{1}+\mathtt{1}")]
        [Arguments(@"\mscrh+\mfrakH+\BbbC+\mith", @"𝒽+ℌ+ℂ+ℎ", @"\mathcal{h}+\mathfrak{H}+\mathbb{C}+h")]
        [Arguments(@"\mitBbbD+\mitBbbe+\mitBbbi+\mitBbbj", @"ⅅ+ⅇ+ⅈ+ⅉ", @"\mathrm{D}+\mathrm{e}+\mathrm{i}+\mathrm{j}")]
        [Arguments(@"\doubleR+\scriptL+\boldx", @"ℝ+ℒ+𝐱", @"\mathbb{R}+\mathcal{L}+\mathbf{x}")]
        [Arguments(@"\misol ب", @"𞸁", @"𞸁")]
        [Arguments(@"\minitب", @"𞸡", @"𞸡")]
        [Arguments(@"\misolب+\mtailج+\mstrcد+\mloopه+\minitه", @"𞸁+𞹂+د+𞺄+𞸤", @"𞸁+𞹂+د+𞺄+𞸤")]
        [Arguments(@"\Bbbب+\minitٮ", @"𞺡+ٮ", @"𞺡+ٮ")]
        [Arguments(@"\mbf2+\mbfsans9+\mitBbbd", @"𝟐+𝟵+ ⅆ", @"\mathbf{2}+\mathsf{9}+\mathrm{d}")]
        [Arguments(@"\mbfDigamma", @"𝟊", @"𝟊")]        public async Task StylesCharacters(string unicodeMath, string expectedUnicodeMath, string expectedLaTeX)
        {
            var equation = Equation.Parse(unicodeMath, EquationFormat.UnicodeMath);
            await Assert.That(equation.ToString(EquationFormat.UnicodeMath)).IsEqualTo(expectedUnicodeMath);
            await Assert.That(equation.ToString(EquationFormat.LaTeX)).IsEqualTo(expectedLaTeX);
        }
    }
}
