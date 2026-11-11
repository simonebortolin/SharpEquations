namespace SharpEquations.MathTools.Graphing
{
    using System;
    using System.Collections.Generic;

    /// <summary>The marks of an axis: round values (1, 2 or 5 times a power of ten) about <c>count</c> of them in a range.</summary>
    public static class Ticks
    {
        /// <summary>The step between the marks: 1, 2 or 5 times a power of ten, closest to a range divided in <paramref name="count"/>.</summary>
        public static double Step(double min, double max, int count = 8)
        {
            if (!(max > min) || count < 1)
                throw new ArgumentOutOfRangeException(nameof(max), "The range is empty, or there is no mark to put.");
            var rough = (max - min) / count;
            var power = Math.Pow(10, Math.Floor(Math.Log10(rough)));
            var fraction = rough / power;
            return power * fraction switch
            {
                < 1.5 => 1,
                < 3.5 => 2,
                < 7.5 => 5,
                _ => 10,
            };
        }

        /// <summary>The multiples of <see cref="Step"/> between <paramref name="min"/> and <paramref name="max"/>, with 0 exactly 0.</summary>
        public static IReadOnlyList<double> Values(double min, double max, int count = 8)
        {
            var step = Step(min, max, count);
            var values = new List<double>();
            for (var i = Math.Ceiling(min / step); i * step <= max; i++)
                values.Add(i == 0 ? 0 : i * step);
            return values;
        }
    }
}
