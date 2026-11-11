namespace SharpEquations.Rendering
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml.Linq;
    using SharpEquations.Editing;
    using SharpEquations.Rendering.Layout;

    /// <summary>The options of a layout.</summary>
    public sealed class MathLayoutOptions
    {
        /// <summary>The size of the em, in the units of the layout (device independent pixels for a toolkit); 16 by default.</summary>
        public double FontSize { get; set; } = 16;

        /// <summary>Lays out in display style even if the <c>math</c> element is inline.</summary>
        public bool Display { get; set; }

        /// <summary>
        /// The width of the line the equation is in, if it is known: the numbers of equations (the labels of
        /// <c>mlabeledtr</c>) go to its right edge, as in Word, with the equation in the middle of what is left.
        /// </summary>
        public double? LineWidth { get; set; }

        /// <summary>
        /// Breaks a display equation wider than <see cref="LineWidth"/> into lines, before its binary operators and
        /// relations, as Word does; true by default.
        /// </summary>
        public bool BreakLines { get; set; } = true;

        /// <summary>Draws a box in the empty slots, for an editor.</summary>
        public bool ShowPlaceholders { get; set; }
    }

    /// <summary>
    /// Lays out presentation MathML after MathML Core, with the OpenType MATH table of a math font.
    /// </summary>
    public static class MathLayoutEngine
    {
        /// <summary>Lays out an equation.</summary>
        public static MathLayout Layout(Equation equation, IMathFont font, MathLayoutOptions? options)
        {
            if (equation == null)
                throw new ArgumentNullException(nameof(equation));
            return Layout(MathNode.FromMathML(equation.ToMathML()), font, options);
        }

        /// <summary>Lays out a <c>math</c> element of MathML.</summary>
        public static MathLayout Layout(XElement math, IMathFont font, MathLayoutOptions? options)
        {
            if (math == null)
                throw new ArgumentNullException(nameof(math));
            return Layout(MathNode.FromMathML(math), font, options);
        }

        /// <summary>
        /// Lays out the <c>math</c> node of an editor (<see cref="MathEditor.Root"/>), with the geometry of the caret
        /// in every row and run.
        /// </summary>
        public static MathLayout Layout(MathNode root, IMathFont font, MathLayoutOptions? options) => Layout(root, font, options, null);

        /// <summary>
        /// Lays out the <c>math</c> node of an editor (<see cref="MathEditor.Root"/>), with the geometry of the caret
        /// in every row and run, with a cache of the layouts before: the nodes this layout shares with them (those an
        /// edit did not replace) are not laid out again.
        /// </summary>
        public static MathLayout Layout(MathNode root, IMathFont font, MathLayoutOptions? options, MathLayoutCache? cache)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));
            if (font == null)
                throw new ArgumentNullException(nameof(font));

            var engine = new Engine(font, options ?? new MathLayoutOptions(), cache);
            var box = engine.LayoutRoot(root, Array.Empty<int>());

            var glyphs = new List<MathGlyph>();
            var shapes = new List<MathShape>();
            var rows = new List<RowGeometry>();
            Flatten(box, 0, box.Height, null, false, Array.Empty<int>(), glyphs, shapes, rows);
            return new MathLayout(box.Width, box.Height + box.Depth, box.Height, glyphs, shapes, rows);
        }

        private static void Flatten(Box box, double x, double baseline, string? color, bool invisible, int[] anchor, List<MathGlyph> glyphs, List<MathShape> shapes, List<RowGeometry> rows)
        {
            if (box.Anchor != null)
                anchor = Concat(anchor, box.Anchor);
            color = box.Color ?? color;
            invisible |= box.Invisible;
            if (!invisible)
            {
                foreach (var shape in box.Shapes)
                {
                    var top = baseline - shape.Y - shape.Height;
                    shapes.Add(new MathShape(shape.Kind, shape.Role, x + shape.X, shape.Kind == MathShapeKind.Line ? baseline - shape.Y : top,
                        shape.Width, shape.Height, x + shape.X2, baseline - shape.Y2, shape.Thickness, shape.Color ?? color));
                }
                foreach (var glyph in box.Glyphs)
                    glyphs.Add(new MathGlyph(glyph.Font, glyph.Glyph, x + glyph.X, baseline - glyph.Shift, glyph.Size, color));
            }

            if (box.Path != null && box.CaretX != null)
            {
                var count = box.CaretX.Length;
                var caret = new double[count];
                var top = new double[count];
                var bottom = new double[count];
                var line = new int[count];
                for (var i = 0; i < count; i++)
                {
                    caret[i] = x + box.CaretX[i];
                    if (box.Lines is { } lines)
                    {
                        var k = line[i] = lines.CaretLine[i];
                        top[i] = baseline - lines.Shift[k] - lines.Height[k];
                        bottom[i] = baseline - lines.Shift[k] + lines.Depth[k];
                    }
                    else
                    {
                        top[i] = baseline - box.CaretHeight;
                        bottom[i] = baseline + box.CaretDepth;
                    }
                }
                var right = box.Lines?.Right.Select(r => x + r).ToArray() ?? new[] { x + Math.Max(box.Width, 1) };
                rows.Add(new RowGeometry(Concat(anchor, box.Path), box.IsRun, caret, top, bottom, line, right, x, baseline - box.Height, baseline + box.Depth));
            }

            foreach (var (child, cx, shift) in box.Children)
                Flatten(child, x + cx, baseline - shift, color, invisible, anchor, glyphs, shapes, rows);
        }

        private static int[] Concat(int[] path, int[] more) => path.Length == 0 ? more : more.Length == 0 ? path : MathTreePaths.Append(path, more);
    }
}
