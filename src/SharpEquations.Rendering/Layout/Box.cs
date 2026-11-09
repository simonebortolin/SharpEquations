namespace SharpEquations.Rendering.Layout
{
    using System.Collections.Generic;

    /// <summary>
    /// A laid out node: its width and its height and depth from the baseline (y up), with its children, glyphs and
    /// shapes placed relative to its origin on the baseline.
    /// </summary>
    internal sealed class Box
    {
        public double Width { get; set; }

        public double Height { get; set; }

        public double Depth { get; set; }

        /// <summary>The italic correction of the last glyph, when the box is a token.</summary>
        public double Italic { get; set; }

        /// <summary>Where an accent attaches above the box; null for its middle.</summary>
        public double? TopAccent { get; set; }

        /// <summary>A token element, which the baseline drops of scripts do not apply to.</summary>
        public bool IsToken { get; set; }

        /// <summary>A large operator, whose subscript moves left by its italic correction.</summary>
        public bool IsLargeOp { get; set; }

        /// <summary>A single letter of <c>mi</c>, after which the italic correction is added.</summary>
        public bool IsItalicLetter { get; set; }

        /// <summary>Nothing of the box is drawn (<c>mphantom</c>).</summary>
        public bool Invisible { get; set; }

        public string? Color { get; set; }

        public List<(Box Box, double X, double Shift)> Children { get; } = new List<(Box, double, double)>();

        public List<GlyphItem> Glyphs { get; } = new List<GlyphItem>();

        public List<ShapeItem> Shapes { get; } = new List<ShapeItem>();

        /// <summary>The path of the row or run of an editor the box is, and its caret positions (x from the origin).</summary>
        public int[]? Path { get; set; }

        public bool IsRun { get; set; }

        public double[]? CaretX { get; set; }

        /// <summary>The height and depth the caret has at least in the row.</summary>
        public double CaretHeight { get; set; }

        public double CaretDepth { get; set; }

        public void Add(Box box, double x, double shift) => Children.Add((box, x, shift));

        /// <summary>A box of one child, shifted.</summary>
        public static Box Wrap(Box child, double x = 0, double shift = 0)
        {
            var box = new Box
            {
                Width = child.Width + x,
                Height = child.Height + shift,
                Depth = child.Depth - shift,
                Italic = child.Italic,
                TopAccent = child.TopAccent + x,
                IsToken = child.IsToken,
                IsLargeOp = child.IsLargeOp,
                IsItalicLetter = child.IsItalicLetter,
            };
            box.Add(child, x, shift);
            return box;
        }
    }

    internal readonly struct GlyphItem
    {
        public GlyphItem(IMathFont font, ushort glyph, double x, double shift, double size)
        {
            Font = font;
            Glyph = glyph;
            X = x;
            Shift = shift;
            Size = size;
        }

        public IMathFont Font { get; }

        public ushort Glyph { get; }

        public double X { get; }

        public double Shift { get; }

        public double Size { get; }
    }

    /// <summary>A shape in the coordinates of a box: x right, y up from the baseline; a rectangle from (X, Y) to (X + Width, Y + Height).</summary>
    internal readonly struct ShapeItem
    {
        public ShapeItem(MathShapeKind kind, MathShapeRole role, double x, double y, double width, double height, double x2 = 0, double y2 = 0, double thickness = 0, string? color = null)
        {
            Kind = kind;
            Role = role;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            X2 = x2;
            Y2 = y2;
            Thickness = thickness;
            Color = color;
        }

        public MathShapeKind Kind { get; }

        public MathShapeRole Role { get; }

        public double X { get; }

        public double Y { get; }

        public double Width { get; }

        public double Height { get; }

        public double X2 { get; }

        public double Y2 { get; }

        public double Thickness { get; }

        public string? Color { get; }

        public static ShapeItem Rule(double x, double y, double width, double height) => new ShapeItem(MathShapeKind.Fill, MathShapeRole.Ink, x, y, width, height);

        public static ShapeItem Line(double x, double y, double x2, double y2, double thickness) => new ShapeItem(MathShapeKind.Line, MathShapeRole.Ink, x, y, 0, 0, x2, y2, thickness);
    }
}
