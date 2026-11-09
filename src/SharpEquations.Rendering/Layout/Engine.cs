namespace SharpEquations.Rendering.Layout
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Xml.Linq;
    using SharpEquations.Editing;
    using SharpEquations.Rendering.OpenType;

    /// <summary>A request to stretch an operator: vertically to cover a height and a depth, or horizontally to a width.</summary>
    internal readonly struct Stretch
    {
        public Stretch(double height, double depth, double width, bool accent, bool under = false)
        {
            Height = height;
            Depth = depth;
            Width = width;
            Accent = accent;
            Under = under;
        }

        /// <summary>The operator is under its base (a bar drawn as a rule goes at the top of its box).</summary>
        public bool Under { get; }

        public double Height { get; }

        public double Depth { get; }

        public double Width { get; }

        /// <summary>The operator is an accent: its box is its ink, so that a combining mark centres on its base.</summary>
        public bool Accent { get; }

        public bool Vertical => Height > 0 || Depth > 0;

        public static Stretch None => default;
    }

    /// <summary>
    /// The layout of presentation MathML after MathML Core, with the constants of the OpenType MATH table: rows with
    /// the spaces of the operator dictionary, stretchy operators, fractions, radicals, scripts and limits, tables.
    /// </summary>
    internal sealed class Engine
    {
        private static readonly char[] Spaces = { ' ' };

        private readonly IMathFont font;
        private readonly MathTable? math;
        private readonly MathLayoutOptions options;
        private readonly double upem;

        public Engine(IMathFont font, MathLayoutOptions options)
        {
            this.font = font;
            math = font.Math;
            this.options = options;
            upem = font.UnitsPerEm;
        }

        private double ScriptPercent => (math?[MathConstant.ScriptPercentScaleDown] is int p and > 0 ? p : 71) / 100.0;

        private double ScriptScriptPercent => (math?[MathConstant.ScriptScriptPercentScaleDown] is int p and > 0 ? p : 50) / 100.0;

        public Box LayoutRoot(MathNode root, int[]? path)
        {
            var display = options.Display || root.GetAttribute("display") == "block";
            var style = new Style(options.FontSize, 0, display, false, null);
            return Layout(root, path, style, Stretch.None);
        }

        private double C(MathConstant constant, Style s)
        {
            if (math != null)
                return math[constant] * s.Size / upem;
            return Fallback(constant) * s.Size / 1000;
        }

        /// <summary>Constants of a font without MATH table, in thousandths of an em, close to those of Latin Modern Math.</summary>
        private static double Fallback(MathConstant constant) => constant switch
        {
            MathConstant.AxisHeight => 250,
            MathConstant.AccentBaseHeight => 450,
            MathConstant.FractionRuleThickness or MathConstant.OverbarRuleThickness or MathConstant.UnderbarRuleThickness or MathConstant.RadicalRuleThickness => 40,
            MathConstant.SubscriptShiftDown => 250,
            MathConstant.SuperscriptShiftUp => 360,
            MathConstant.SuperscriptShiftUpCramped => 290,
            MathConstant.FractionNumeratorShiftUp => 390,
            MathConstant.FractionDenominatorShiftDown => 340,
            MathConstant.FractionNumeratorDisplayStyleShiftUp => 680,
            MathConstant.FractionDenominatorDisplayStyleShiftDown => 690,
            MathConstant.SpaceAfterScript => 40,
            _ => 100,
        };

        private double Axis(Style s) => C(MathConstant.AxisHeight, s);

        private static double Em(Style s, double eighteenths) => s.Size * eighteenths / 18;

        private Style Script(Style s, bool cramped, int increment = 1) =>
            s.WithLevel(s.Level + increment, ScriptPercent, ScriptScriptPercent).WithDisplay(false).WithCramped(cramped || s.Cramped);

        private Box Layout(MathNode node, int[]? path, Style style, Stretch stretch)
        {
            if (node.GetAttribute("mathcolor") is { } color)
                style = style.WithColor(color);

            var box = LayoutElement(node, path, style, stretch);
            if (style.Color != null)
                box.Color = style.Color;

            if (node.GetAttribute("mathbackground") is { } background)
            {
                var wrapped = new Box { Width = box.Width, Height = box.Height, Depth = box.Depth, Italic = box.Italic, TopAccent = box.TopAccent, IsToken = box.IsToken, IsLargeOp = box.IsLargeOp };
                wrapped.Shapes.Add(new ShapeItem(MathShapeKind.Fill, MathShapeRole.Background, 0, -box.Depth, box.Width, box.Height + box.Depth, color: background));
                wrapped.Add(box, 0, 0);
                return wrapped;
            }
            return box;
        }

        private Box LayoutElement(MathNode node, int[]? path, Style style, Stretch stretch)
        {
            if (node.IsRun)
                return LayoutRun(node, path, style);
            if (node.IsOpaque)
                return LayoutOpaque(node, style);

            switch (node.Name)
            {
                case "math":
                    return node.Children.Count == 1 ? Layout(node.Children[0], Sub(path, 0), style, stretch) : LayoutRow(node, null, style, stretch);
                case "mrow":
                    return LayoutRow(node, path, style, stretch);
                case "mi" or "mn" or "mo" or "mtext" or "ms":
                    return LayoutToken(node, style, stretch);
                case "mspace":
                    return LayoutSpace(node, style);
                case "none" or "mprescripts" or "malignmark" or "maligngroup" or "mglyph":
                    return new Box();
                case "mstyle":
                    return LayoutInferred(node, path, ApplyStyle(node, style), stretch);
                case "mphantom":
                    var phantom = LayoutInferred(node, path, style, stretch);
                    phantom.Invisible = true;
                    return phantom;
                case "mpadded":
                    return LayoutPadded(node, path, style);
                case "merror":
                    return LayoutError(node, path, style);
                case "msqrt":
                    return LayoutRadical(Layout(node.Children[0], Sub(path, 0), style.WithCramped(true), Stretch.None), null, style);
                case "mroot":
                    return LayoutRadical(
                        Layout(node.Children[0], Sub(path, 0), style.WithCramped(true), Stretch.None),
                        node.Children.Count > 1 ? Layout(node.Children[1], Sub(path, 1), Script(style, true, 2), Stretch.None) : null,
                        style);
                case "mfrac":
                    return LayoutFraction(node, path, style);
                case "msub" or "msup" or "msubsup":
                    return LayoutScripts(node, path, style);
                case "munder" or "mover" or "munderover":
                    return LayoutUnderOver(node, path, style, stretch);
                case "mmultiscripts":
                    return LayoutMultiscripts(node, path, style);
                case "mtable":
                    return LayoutTable(node, path, style);
                case "menclose":
                    return LayoutEnclose(node, path, style);
                case "mtd" or "mtr" or "mlabeledtr":
                    return LayoutRow(MathNode.Row(node.Children), null, style, Stretch.None);
                default:
                    return LayoutRow(MathNode.Row(node.Children), null, style, Stretch.None);
            }
        }

        private static int[]? Sub(int[]? path, params int[] more) => path == null ? null : MathTreePaths.Append(path, more);

        private Box LayoutInferred(MathNode node, int[]? path, Style style, Stretch stretch) =>
            node.Children.Count == 1 ? Layout(node.Children[0], Sub(path, 0), style, stretch) : LayoutRow(MathNode.Row(node.Children), null, style, stretch);

        private Style ApplyStyle(MathNode node, Style style)
        {
            if (node.GetAttribute("displaystyle") is { } display)
                style = style.WithDisplay(display == "true");
            if (node.GetAttribute("scriptlevel") is { } level)
            {
                var value = level.Trim();
                if (int.TryParse(value.TrimStart('+'), out var n))
                    style = style.WithLevel(value.Length > 0 && value[0] is '+' or '-' ? style.Level + n : n, ScriptPercent, ScriptScriptPercent);
            }
            if (node.GetAttribute("mathsize") is { } size && Lengths.TryParse(size, style.Size, Ex(style), style.Size, out var s) && s > 0)
                style = style.WithSize(s);
            return style;
        }

        private double Ex(Style s) => math != null && math[MathConstant.AccentBaseHeight] > 0 ? C(MathConstant.AccentBaseHeight, s) : s.Size * 0.45;

        // ---- rows -----------------------------------------------------------------------------------------------

        private Box LayoutRow(MathNode row, int[]? path, Style style, Stretch stretch)
        {
            var n = row.Children.Count;
            var box = new Box { Path = path };
            if (n == 0)
            {
                if (options.ShowPlaceholders && path != null)
                {
                    var w = style.Size * 0.55;
                    var h = style.Size * 0.68;
                    box.Width = w + style.Size * 0.1;
                    box.Height = h;
                    box.Shapes.Add(new ShapeItem(MathShapeKind.Outline, MathShapeRole.Placeholder, style.Size * 0.05, 0, w, h, thickness: Math.Max(0.75, style.Size / 24)));
                }
                box.CaretX = new[] { 0.0 };
                SetCaretExtent(box, style);
                return box;
            }

            // a single child gets the stretch of the row (a row in the slot of an accent, a fence of a table)
            var boxes = new Box[n];
            var stretchy = new bool[n];
            for (var i = 0; i < n; i++)
            {
                var child = row.Children[i];
                stretchy[i] = n > 1 && IsVerticalStretchy(child);
                if (!stretchy[i])
                    boxes[i] = Layout(child, Sub(path, i), style, n == 1 ? stretch : Stretch.None);
            }

            if (stretchy.Any(s => s))
            {
                double height = 0, depth = 0;
                var any = false;
                for (var i = 0; i < n; i++)
                {
                    if (stretchy[i])
                        continue;
                    any = true;
                    height = Math.Max(height, boxes[i].Height);
                    depth = Math.Max(depth, boxes[i].Depth);
                }
                if (!any)
                {
                    height = style.Size * 0.7;
                    depth = style.Size * 0.2;
                }
                for (var i = 0; i < n; i++)
                {
                    if (stretchy[i])
                        boxes[i] = Layout(row.Children[i], Sub(path, i), style, new Stretch(height, depth, 0, false));
                }
            }

            var caret = new double[n + 1];
            var x = 0.0;
            for (var i = 0; i < n; i++)
            {
                caret[i] = x;
                var (lspace, rspace) = Spacing(row, i, style);
                x += lspace;
                box.Add(boxes[i], x, 0);
                x += boxes[i].Width;
                if (boxes[i].IsItalicLetter && i + 1 < n)
                    x += boxes[i].Italic;
                x += rspace;
                box.Height = Math.Max(box.Height, boxes[i].Height);
                box.Depth = Math.Max(box.Depth, boxes[i].Depth);
            }
            caret[n] = x;
            box.Width = x;
            box.CaretX = caret;
            SetCaretExtent(box, style);
            if (n == 1)
            {
                box.Italic = boxes[0].Italic;
                box.TopAccent = boxes[0].TopAccent + (box.Children[0].X);
                box.IsToken = boxes[0].IsToken;
                box.IsLargeOp = boxes[0].IsLargeOp;
                box.IsItalicLetter = boxes[0].IsItalicLetter;
            }
            else if (n > 0)
            {
                box.Italic = boxes[n - 1].Italic;
            }
            return box;
        }

        private static void SetCaretExtent(Box box, Style style)
        {
            box.CaretHeight = Math.Max(box.Height, style.Size * 0.75);
            box.CaretDepth = Math.Max(box.Depth, style.Size * 0.22);
        }

        /// <summary>The spaces before and after a child of a row: those of its operator, if it is an embellished operator.</summary>
        private (double L, double R) Spacing(MathNode row, int i, Style style)
        {
            var child = row.Children[i];
            var op = CoreOperator(child);
            if (op == null)
                return (0, 0);
            var entry = OperatorDictionary.Lookup(op.Text, FormOf(row, i, op));
            var l = Em(style, entry.LSpace);
            var r = Em(style, entry.RSpace);
            if (style.Level > 0)
                l = r = 0;
            if (op.GetAttribute("lspace") is { } ls && Lengths.TryParse(ls, style.Size, Ex(style), style.Size, out var lv))
                l = lv;
            if (op.GetAttribute("rspace") is { } rs && Lengths.TryParse(rs, style.Size, Ex(style), style.Size, out var rv))
                r = rv;
            return (l, r);
        }

        private static OperatorForm FormOf(MathNode row, int i, MathNode op)
        {
            switch (op.GetAttribute("form"))
            {
                case "prefix":
                    return OperatorForm.Prefix;
                case "postfix":
                    return OperatorForm.Postfix;
                case "infix":
                    return OperatorForm.Infix;
            }
            var n = row.Children.Count;
            if (n <= 1)
                return OperatorForm.Infix;
            if (i == 0)
                return OperatorForm.Prefix;
            return i == n - 1 ? OperatorForm.Postfix : OperatorForm.Infix;
        }

        /// <summary>The operator an element is an embellishment of (an <c>mo</c> with scripts, in a row of its own...), or null.</summary>
        private static MathNode? CoreOperator(MathNode node)
        {
            switch (node.Name)
            {
                case "mo":
                    return node;
                case "mrow":
                    return node.Children.Count == 1 ? CoreOperator(node.Children[0]) : null;
                case "msub" or "msup" or "msubsup" or "munder" or "mover" or "munderover" or "mmultiscripts" or "mfrac":
                    return node.Children.Count > 0 ? CoreOperator(node.Children[0]) : null;
                case "mstyle" or "mphantom" or "mpadded":
                    return node.Children.Count == 1 ? CoreOperator(node.Children[0]) : null;
                default:
                    return null;
            }
        }

        private static bool IsVerticalStretchy(MathNode node)
        {
            var op = node.Name == "mo" ? node : null;
            if (op == null || op.GetAttribute("stretchy") == "false")
                return false;
            if (!OperatorDictionary.TryLookup(op.Text, out var entry))
                return op.GetAttribute("stretchy") == "true";
            return entry.Has(OperatorFlags.Stretchy) && !entry.Has(OperatorFlags.Horizontal) || op.GetAttribute("stretchy") == "true" && !entry.Has(OperatorFlags.Horizontal);
        }

        private static bool IsHorizontalStretchy(MathNode node)
        {
            var op = CoreOperatorOfSlot(node);
            if (op == null || op.GetAttribute("stretchy") == "false")
                return false;
            return OperatorDictionary.TryLookup(op.Text, out var entry) && entry.Has(OperatorFlags.Stretchy | OperatorFlags.Horizontal);
        }

        private static MathNode? CoreOperatorOfSlot(MathNode node) => node.Name switch
        {
            "mo" => node,
            "mrow" when node.Children.Count == 1 => CoreOperatorOfSlot(node.Children[0]),
            _ => null,
        };

        // ---- tokens ---------------------------------------------------------------------------------------------

        private Box LayoutToken(MathNode node, Style style, Stretch stretch)
        {
            var text = node.Text;
            if (node.Name == "ms")
                text = (node.GetAttribute("lquote") ?? "\"") + text + (node.GetAttribute("rquote") ?? "\"");

            var italic = node.Name == "mi" && node.GetAttribute("mathvariant") != "normal" && IsSingle(text, out var single) && MathItalic(single) != single;
            if (node.Name == "mo")
                return LayoutOperator(node, text, style, stretch);

            var box = LayoutText(text, style, italic);
            box.IsToken = true;
            box.IsItalicLetter = italic;
            return box;
        }

        private Box LayoutOperator(MathNode node, string text, Style style, Stretch stretch)
        {
            OperatorDictionary.TryLookup(text, out var entry);
            var isLarge = node.GetAttribute("largeop") is { } lo ? lo == "true" : entry.Has(OperatorFlags.LargeOp);
            if (IsSingle(text, out var cp) && Glyph(cp, out var f, out var glyph))
            {
                var fmath = f.Math;
                if (stretch.Vertical && fmath?.Vertical(glyph) is { } vertical)
                {
                    var symmetric = node.GetAttribute("symmetric") is { } sym ? sym == "true" : entry.Has(OperatorFlags.Symmetric);
                    var axis = Axis(style);
                    double target, center;
                    if (symmetric)
                    {
                        var half = Math.Max(stretch.Height - axis, stretch.Depth + axis);
                        target = 2 * half;
                        center = axis;
                    }
                    else
                    {
                        target = stretch.Height + stretch.Depth;
                        center = (stretch.Height - stretch.Depth) / 2;
                    }
                    target = ApplyMinMax(node, target, style);
                    var stretched = StretchGlyph(f, glyph, vertical, target, style, true);
                    return Box.Wrap(stretched, 0, center - (stretched.Height - stretched.Depth) / 2);
                }

                if (stretch.Width > 0 && IsBar(cp) && (fmath?.Horizontal(glyph) is not { } bars || bars.Variants.Count == 0 && bars.Assembly == null))
                    return BarBox(stretch, style);

                if (stretch.Width > 0 && fmath?.Horizontal(glyph) is { } horizontal)
                {
                    var stretched = StretchGlyph(f, glyph, horizontal, stretch.Width, style, false);
                    return stretch.Accent ? InkBox(stretched) : stretched;
                }

                if (isLarge && style.Display && fmath?.Vertical(glyph) is { } large)
                {
                    var min = fmath[MathConstant.DisplayOperatorMinHeight];
                    var variant = large.Variants.Count > 0 ? large.Variants[large.Variants.Count - 1].Glyph : glyph;
                    foreach (var (g, advance) in large.Variants)
                    {
                        if (advance >= min && g != glyph)
                        {
                            variant = g;
                            break;
                        }
                    }
                    var big = GlyphBox(f, variant, style);
                    big.IsLargeOp = true;
                    big.IsToken = true;
                    return Box.Wrap(big, 0, Axis(style) - (big.Height - big.Depth) / 2);
                }
            }

            var box = LayoutText(text, style, false);
            box.IsToken = true;
            box.IsLargeOp = isLarge;
            if (isLarge)
                return Box.Wrap(box, 0, Axis(style) - (box.Height - box.Depth) / 2);
            return stretch.Accent ? InkBox(box) : box;
        }

        private static bool IsBar(int cp) => cp is 0xAF or 0x203E or '_' or 0x0332 or 0x0305 or 0x2015 or 0x2500 or 0x23AF;

        /// <summary>A bar over or under its base, as a rule as wide as the base: the overbar and underbar of the MATH table.</summary>
        private Box BarBox(Stretch stretch, Style style)
        {
            var box = new Box { Width = stretch.Width };
            if (stretch.Under)
            {
                var gap = C(MathConstant.UnderbarVerticalGap, style);
                var t = C(MathConstant.UnderbarRuleThickness, style);
                box.Height = gap + t;
                box.Depth = C(MathConstant.UnderbarExtraDescender, style);
                box.Shapes.Add(ShapeItem.Rule(0, 0, stretch.Width, t));
            }
            else
            {
                var gap = C(MathConstant.OverbarVerticalGap, style);
                var t = C(MathConstant.OverbarRuleThickness, style);
                var bottom = stretch.Accent ? C(MathConstant.AccentBaseHeight, style) + gap : 0;
                box.Height = bottom + t + C(MathConstant.OverbarExtraAscender, style);
                box.Depth = stretch.Accent ? 0 : gap;
                box.Shapes.Add(ShapeItem.Rule(0, bottom, stretch.Width, t));
            }
            return box;
        }

        private double ApplyMinMax(MathNode node, double target, Style style)
        {
            if (node.GetAttribute("minsize") is { } min && Lengths.TryParse(min, style.Size, Ex(style), style.Size, out var minValue))
                target = Math.Max(target, minValue);
            if (node.GetAttribute("maxsize") is { } max && Lengths.TryParse(max, style.Size, Ex(style), style.Size, out var maxValue) && maxValue > 0)
                target = Math.Min(target, maxValue);
            return target;
        }

        /// <summary>A copy of the box whose extent is its ink, starting at its left.</summary>
        private static Box InkBox(Box box)
        {
            double xMin = double.MaxValue, xMax = double.MinValue;
            foreach (var g in box.Glyphs)
            {
                var b = g.Font.GetBounds(g.Glyph);
                var scale = g.Size / g.Font.UnitsPerEm;
                xMin = Math.Min(xMin, g.X + b.XMin * scale);
                xMax = Math.Max(xMax, g.X + b.XMax * scale);
            }
            foreach (var (child, x, _) in box.Children)
            {
                xMin = Math.Min(xMin, x);
                xMax = Math.Max(xMax, x + child.Width);
            }
            if (xMin > xMax)
                return box;
            var wrapped = Box.Wrap(box, -xMin, 0);
            wrapped.Width = xMax - xMin;
            wrapped.TopAccent = box.TopAccent - xMin;
            return wrapped;
        }

        private Box LayoutText(string text, Style style, bool italic)
        {
            var box = new Box();
            var x = 0.0;
            ushort last = 0;
            IMathFont? lastFont = null;
            var count = 0;
            foreach (var rune in text.EnumerateRunes())
            {
                var cp = italic ? MathItalic(rune.Value) : rune.Value;
                if (IsInvisible(cp))
                    continue;
                if (!Glyph(cp, out var f, out var glyph) && !(italic && Glyph(rune.Value, out f, out glyph)))
                {
                    if (cp is ' ' or 0xA0)
                    {
                        x += style.Size * 0.25;
                        continue;
                    }
                    f = font;
                    glyph = 0;
                }
                if (style.Level > 0)
                    glyph = f.GetScriptVariant(glyph, style.Level);
                var scale = style.Size / f.UnitsPerEm;
                var bounds = f.GetBounds(glyph);
                box.Glyphs.Add(new GlyphItem(f, glyph, x, 0, style.Size));
                box.Height = Math.Max(box.Height, bounds.YMax * scale);
                box.Depth = Math.Max(box.Depth, -bounds.YMin * scale);
                x += f.GetAdvance(glyph) * scale;
                last = glyph;
                lastFont = f;
                count++;
            }
            box.Width = x;
            if (lastFont?.Math is { } m)
            {
                box.Italic = m.ItalicCorrection(last) * style.Size / lastFont.UnitsPerEm;
                if (count == 1 && m.TopAccentAttachment(last) is { } attachment)
                    box.TopAccent = attachment * style.Size / lastFont.UnitsPerEm;
            }
            return box;
        }

        private static Box GlyphBox(IMathFont f, ushort glyph, Style style, double x = 0)
        {
            var scale = style.Size / f.UnitsPerEm;
            var bounds = f.GetBounds(glyph);
            var box = new Box
            {
                Width = f.GetAdvance(glyph) * scale,
                Height = bounds.YMax * scale,
                Depth = -bounds.YMin * scale,
                Italic = (f.Math?.ItalicCorrection(glyph) ?? 0) * scale,
            };
            if (f.Math?.TopAccentAttachment(glyph) is { } attachment)
                box.TopAccent = attachment * scale;
            box.Glyphs.Add(new GlyphItem(f, glyph, x, 0, style.Size));
            return box;
        }

        /// <summary>A glyph stretched to a size at least: the first variant big enough, or an assembly of parts.</summary>
        private static Box StretchGlyph(IMathFont f, ushort glyph, GlyphConstruction construction, double target, Style style, bool vertical)
        {
            var scale = style.Size / f.UnitsPerEm;
            var targetUnits = target / scale;
            var natural = vertical ? GlyphBox(f, glyph, style) : null;
            if (vertical && natural!.Height + natural.Depth >= target)
                return natural;
            if (!vertical && f.GetAdvance(glyph) >= targetUnits)
                return GlyphBox(f, glyph, style);

            foreach (var (variant, advance) in construction.Variants)
            {
                if (advance >= targetUnits)
                    return GlyphBox(f, variant, style);
            }

            if (construction.Assembly is { Count: > 0 } parts)
                return Assembly(f, parts, f.Math!.MinConnectorOverlap, targetUnits, style, vertical, construction.AssemblyItalicCorrection);

            return construction.Variants.Count > 0 ? GlyphBox(f, construction.Variants[construction.Variants.Count - 1].Glyph, style) : GlyphBox(f, glyph, style);
        }

        private static Box Assembly(IMathFont f, IReadOnlyList<GlyphPart> parts, int minOverlap, double target, Style style, bool vertical, int italic)
        {
            var scale = style.Size / f.UnitsPerEm;
            List<GlyphPart> list = new List<GlyphPart>();
            for (var repeats = 0; repeats <= 64; repeats++)
            {
                list = new List<GlyphPart>();
                foreach (var part in parts)
                {
                    if (part.IsExtender)
                    {
                        for (var k = 0; k < repeats; k++)
                            list.Add(part);
                    }
                    else
                    {
                        list.Add(part);
                    }
                }
                if (list.Count == 0)
                    continue;
                var max = list.Sum(p => (double)p.FullAdvance) - minOverlap * (list.Count - 1);
                if (max >= target)
                    break;
            }

            var sum = list.Sum(p => (double)p.FullAdvance);
            var overlap = 0.0;
            if (list.Count > 1)
            {
                var maxOverlap = double.MaxValue;
                for (var i = 1; i < list.Count; i++)
                    maxOverlap = Math.Min(maxOverlap, Math.Min(list[i - 1].EndConnector, list[i].StartConnector));
                overlap = Math.Max(minOverlap, Math.Min((sum - target) / (list.Count - 1), Math.Max(minOverlap, maxOverlap)));
            }

            var box = new Box();
            var at = 0.0;
            foreach (var part in list)
            {
                var bounds = f.GetBounds(part.Glyph);
                if (vertical)
                {
                    box.Glyphs.Add(new GlyphItem(f, part.Glyph, 0, (at - bounds.YMin) * scale, style.Size));
                    box.Width = Math.Max(box.Width, f.GetAdvance(part.Glyph) * scale);
                }
                else
                {
                    box.Glyphs.Add(new GlyphItem(f, part.Glyph, (at - bounds.XMin) * scale, 0, style.Size));
                    box.Height = Math.Max(box.Height, bounds.YMax * scale);
                    box.Depth = Math.Max(box.Depth, -bounds.YMin * scale);
                }
                at += part.FullAdvance - overlap;
            }
            var size = (at + overlap) * scale;
            if (vertical)
            {
                // the parts of a vertical assembly start at the baseline: the assembly goes from 0 up
                box.Height = size;
                box.Depth = 0;
                box.Italic = italic * scale;
            }
            else
            {
                box.Width = size;
            }
            return box;
        }

        private bool Glyph(int codePoint, out IMathFont f, out ushort glyph)
        {
            if (font.TryGetGlyph(codePoint, out glyph) && glyph != 0)
            {
                f = font;
                return true;
            }
            if (font.GetFallback(codePoint) is { } fallback && fallback.TryGetGlyph(codePoint, out glyph) && glyph != 0)
            {
                f = fallback;
                return true;
            }
            f = font;
            glyph = 0;
            return false;
        }

        private static bool IsInvisible(int cp) => cp is >= 0x2061 and <= 0x2064 or 0x200B or 0x200C or 0x200D or 0x2060 or 0xFEFF;

        private static bool IsSingle(string text, out int codePoint)
        {
            codePoint = 0;
            var count = 0;
            foreach (var rune in text.EnumerateRunes())
            {
                codePoint = rune.Value;
                count++;
            }
            return count == 1;
        }

        /// <summary>The mathematical italic of a letter, as MathML Core shows a single letter of <c>mi</c>.</summary>
        internal static int MathItalic(int c) => c switch
        {
            'h' => 0x210E,
            >= 'A' and <= 'Z' => 0x1D434 + c - 'A',
            >= 'a' and <= 'z' => 0x1D44E + c - 'a',
            0x131 => 0x1D6A4,
            0x237 => 0x1D6A5,
            >= 0x391 and <= 0x3A9 => 0x1D6E2 + c - 0x391,
            0x3F4 => 0x1D6F3,
            0x2207 => 0x1D6FB,
            >= 0x3B1 and <= 0x3C9 => 0x1D6FC + c - 0x3B1,
            0x2202 => 0x1D715,
            0x3F5 => 0x1D716,
            0x3D1 => 0x1D717,
            0x3F0 => 0x1D718,
            0x3D5 => 0x1D719,
            0x3F1 => 0x1D71A,
            0x3D6 => 0x1D71B,
            _ => c,
        };

        // ---- runs and other leaves ------------------------------------------------------------------------------

        private Box LayoutRun(MathNode run, int[]? path, Style style)
        {
            var box = new Box { Path = path, IsRun = true };
            var caret = new List<double> { 0 };
            var x = 0.0;
            foreach (var rune in run.Text.EnumerateRunes())
            {
                var c = rune.Value;
                var cp = c < 0x80 && char.IsLetter((char)c) || c is >= 0x3B1 and <= 0x3C9 ? MathItalic(c) : c;
                if (cp is ' ' or 0xA0 || IsInvisible(cp) || !Glyph(cp, out var f, out var glyph) && !Glyph(c, out f, out glyph))
                {
                    x += cp is ' ' or 0xA0 ? style.Size * 0.28 : 0;
                    caret.Add(x);
                    continue;
                }
                var scale = style.Size / f.UnitsPerEm;
                var bounds = f.GetBounds(glyph);
                box.Glyphs.Add(new GlyphItem(f, glyph, x, 0, style.Size));
                box.Height = Math.Max(box.Height, bounds.YMax * scale);
                box.Depth = Math.Max(box.Depth, -bounds.YMin * scale);
                x += f.GetAdvance(glyph) * scale;
                caret.Add(x);
            }
            box.Width = x;
            box.CaretX = caret.ToArray();
            SetCaretExtent(box, style);
            return box;
        }

        private Box LayoutSpace(MathNode node, Style style)
        {
            var box = new Box();
            if (Lengths.TryParse(node.GetAttribute("width"), style.Size, Ex(style), style.Size, out var w))
                box.Width = w;
            if (Lengths.TryParse(node.GetAttribute("height"), style.Size, Ex(style), style.Size, out var h))
                box.Height = h;
            if (Lengths.TryParse(node.GetAttribute("depth"), style.Size, Ex(style), style.Size, out var d))
                box.Depth = d;
            return box;
        }

        private Box LayoutOpaque(MathNode node, Style style)
        {
            var source = node.Source!;
            switch (node.Name)
            {
                case "semantics":
                    var first = source.Elements().FirstOrDefault(e => !e.Name.LocalName.StartsWith("annotation", StringComparison.Ordinal));
                    return first == null ? new Box() : Layout(MathNode.FromMathML(first), null, style, Stretch.None);
                case "mstack" or "mlongdiv":
                    return LayoutStack(source, style);
                default:
                    var text = LayoutText(source.Value, style, false);
                    return text;
            }
        }

        /// <summary>The rows of an <c>mstack</c> or <c>mlongdiv</c>, one under the other, aligned on the right.</summary>
        private Box LayoutStack(XElement stack, Style style)
        {
            var rows = new List<Box>();
            foreach (var child in stack.Elements())
            {
                switch (child.Name.LocalName)
                {
                    case "msline":
                        var line = new Box { Height = style.Size * 0.1 };
                        line.Shapes.Add(ShapeItem.Rule(0, style.Size * 0.03, 0, C(MathConstant.FractionRuleThickness, style)));
                        rows.Add(line);
                        break;
                    case "mscarries":
                        rows.Add(LayoutRow(MathNode.Row(child.Elements().Select(MathNode.FromMathML)), null, Script(style, false), Stretch.None));
                        break;
                    case "msrow" or "msgroup":
                        rows.Add(LayoutRow(MathNode.Row(child.Elements().Select(MathNode.FromMathML)), null, style, Stretch.None));
                        break;
                    default:
                        rows.Add(Layout(MathNode.FromMathML(child), null, style, Stretch.None));
                        break;
                }
            }

            var width = rows.Count == 0 ? 0 : rows.Max(r => r.Width);
            var box = new Box { Width = width };
            var y = 0.0;
            var gap = style.Size * 0.15;
            var placed = new List<(Box Box, double Y)>();
            foreach (var row in rows)
            {
                y -= row.Height;
                placed.Add((row, y));
                y -= row.Depth + gap;
            }
            var total = -y - gap;
            var shift = total / 2 + Axis(style);
            foreach (var (row, top) in placed)
            {
                if (row.Shapes.Count == 1 && row.Glyphs.Count == 0 && row.Children.Count == 0)
                {
                    var rule = row.Shapes[0];
                    var full = new Box { Width = width, Height = row.Height };
                    full.Shapes.Add(ShapeItem.Rule(0, rule.Y, width, rule.Height));
                    box.Add(full, 0, top + shift);
                }
                else
                {
                    box.Add(row, width - row.Width, top + shift);
                }
            }
            box.Height = shift;
            box.Depth = total - shift;
            return box;
        }

        private Box LayoutPadded(MathNode node, int[]? path, Style style)
        {
            var content = LayoutInferred(node, path, style, Stretch.None);
            double Dimension(string attribute, double current)
            {
                var value = node.GetAttribute(attribute);
                if (value == null)
                    return current;
                var v = value.Trim();
                var sign = v.Length == 0 ? 0 : v[0] == '+' ? 1 : v[0] == '-' ? -1 : 0;
                var body = sign == 0 ? v : v.Substring(1);
                if (!Lengths.TryParse(body, style.Size, Ex(style), current, out var length))
                    return current;
                return sign == 0 ? length : current + sign * length;
            }

            var lspace = Dimension("lspace", 0);
            var voffset = Dimension("voffset", 0);
            var box = Box.Wrap(content, lspace, voffset);
            box.Width = Dimension("width", content.Width);
            box.Height = Dimension("height", content.Height);
            box.Depth = Dimension("depth", content.Depth);
            return box;
        }

        private Box LayoutError(MathNode node, int[]? path, Style style)
        {
            var content = LayoutInferred(node, path, style, Stretch.None);
            var pad = style.Size * 0.05;
            var box = Box.Wrap(content, pad, 0);
            box.Width = content.Width + 2 * pad;
            box.Height = content.Height + pad;
            box.Depth = content.Depth + pad;
            box.Shapes.Add(new ShapeItem(MathShapeKind.Fill, MathShapeRole.Error, 0, -box.Depth, box.Width, box.Height + box.Depth));
            return box;
        }

        // ---- fractions and radicals -----------------------------------------------------------------------------

        private Box LayoutFraction(MathNode node, int[]? path, Style style)
        {
            var inner = style.Display ? style.WithDisplay(false) : Script(style, false);
            var num = Layout(node.Children[0], Sub(path, 0), inner, Stretch.None);
            var den = Layout(node.Children.Count > 1 ? node.Children[1] : MathNode.Row(Array.Empty<MathNode>()), Sub(path, 1), inner.WithCramped(true), Stretch.None);

            var defaultThickness = C(MathConstant.FractionRuleThickness, style);
            var thickness = defaultThickness;
            if (node.GetAttribute("linethickness") is { } lt)
            {
                thickness = lt switch
                {
                    "thin" => defaultThickness / 2,
                    "medium" => defaultThickness,
                    "thick" => defaultThickness * 2,
                    _ => Lengths.TryParse(lt, style.Size, Ex(style), defaultThickness, out var t) ? t : defaultThickness,
                };
            }

            var axis = Axis(style);
            double numShift, denShift;
            if (thickness > 0)
            {
                numShift = C(style.Display ? MathConstant.FractionNumeratorDisplayStyleShiftUp : MathConstant.FractionNumeratorShiftUp, style);
                denShift = C(style.Display ? MathConstant.FractionDenominatorDisplayStyleShiftDown : MathConstant.FractionDenominatorShiftDown, style);
                var numGap = C(style.Display ? MathConstant.FractionNumDisplayStyleGapMin : MathConstant.FractionNumeratorGapMin, style);
                var denGap = C(style.Display ? MathConstant.FractionDenomDisplayStyleGapMin : MathConstant.FractionDenominatorGapMin, style);
                numShift = Math.Max(numShift, axis + thickness / 2 + numGap + num.Depth);
                denShift = Math.Max(denShift, den.Height + denGap - axis + thickness / 2);
            }
            else
            {
                numShift = C(style.Display ? MathConstant.StackTopDisplayStyleShiftUp : MathConstant.StackTopShiftUp, style);
                denShift = C(style.Display ? MathConstant.StackBottomDisplayStyleShiftDown : MathConstant.StackBottomShiftDown, style);
                var gapMin = C(style.Display ? MathConstant.StackDisplayStyleGapMin : MathConstant.StackGapMin, style);
                var gap = numShift - num.Depth - (den.Height - denShift);
                if (gap < gapMin)
                {
                    numShift += (gapMin - gap) / 2;
                    denShift += (gapMin - gap) / 2;
                }
            }

            var pad = style.Size * 0.12;
            var width = Math.Max(num.Width, den.Width) + 2 * pad;
            var box = new Box { Width = width };
            box.Add(num, (width - num.Width) / 2, numShift);
            box.Add(den, (width - den.Width) / 2, -denShift);
            if (thickness > 0)
                box.Shapes.Add(ShapeItem.Rule(pad / 2, axis - thickness / 2, width - pad, thickness));
            box.Height = Math.Max(numShift + num.Height, axis + thickness / 2);
            box.Depth = Math.Max(denShift + den.Depth, thickness / 2 - axis);
            return box;
        }

        private Box LayoutRadical(Box radicand, Box? index, Style style)
        {
            var gap = C(style.Display ? MathConstant.RadicalDisplayStyleVerticalGap : MathConstant.RadicalVerticalGap, style);
            var thickness = C(MathConstant.RadicalRuleThickness, style);
            var extra = C(MathConstant.RadicalExtraAscender, style);
            var target = radicand.Height + radicand.Depth + gap + thickness;

            Box sign;
            if (Glyph(0x221A, out var f, out var glyph))
                sign = f.Math?.Vertical(glyph) is { } construction ? StretchGlyph(f, glyph, construction, target, style, true) : GlyphBox(f, glyph, style);
            else
                sign = LayoutText("√", style, false);

            var total = sign.Height + sign.Depth;
            if (total > target)
                gap += (total - target) / 2;
            var ruleBottom = radicand.Height + gap;
            var top = ruleBottom + thickness;
            var signShift = top - sign.Height;

            var box = new Box();
            var x = 0.0;
            if (index != null)
            {
                var before = C(MathConstant.RadicalKernBeforeDegree, style);
                var after = C(MathConstant.RadicalKernAfterDegree, style);
                var raise = (math?[MathConstant.RadicalDegreeBottomRaisePercent] ?? 60) / 100.0;
                var indexShift = (signShift - sign.Depth) + raise * total + index.Depth;
                box.Add(index, before, indexShift);
                x = Math.Max(0, before + index.Width + after);
                box.Height = Math.Max(box.Height, indexShift + index.Height);
            }

            box.Add(sign, x, signShift);
            x += sign.Width;
            box.Add(radicand, x, 0);
            box.Shapes.Add(ShapeItem.Rule(x, ruleBottom, radicand.Width, thickness));
            box.Width = x + radicand.Width;
            box.Height = Math.Max(box.Height, top + extra);
            box.Depth = Math.Max(radicand.Depth, sign.Depth - signShift);
            return box;
        }

        // ---- scripts ----------------------------------------------------------------------------------------------

        private Box LayoutScripts(MathNode node, int[]? path, Style style)
        {
            var baseBox = Layout(node.Children[0], Sub(path, 0), style, Stretch.None);
            Box? sub = null, sup = null;
            switch (node.Name)
            {
                case "msub":
                    sub = Layout(node.Children[1], Sub(path, 1), Script(style, true), Stretch.None);
                    break;
                case "msup":
                    sup = Layout(node.Children[1], Sub(path, 1), Script(style, false), Stretch.None);
                    break;
                default:
                    sub = Layout(node.Children[1], Sub(path, 1), Script(style, true), Stretch.None);
                    sup = Layout(node.Children[2], Sub(path, 2), Script(style, false), Stretch.None);
                    break;
            }
            return AttachScripts(baseBox, new List<(Box?, Box?)> { (sub, sup) }, new List<(Box?, Box?)>(), style);
        }

        private Box LayoutMultiscripts(MathNode node, int[]? path, Style style)
        {
            var baseBox = Layout(node.Children[0], Sub(path, 0), style, Stretch.None);
            var post = new List<(Box?, Box?)>();
            var pre = new List<(Box?, Box?)>();
            var target = post;
            var i = 1;
            while (i < node.Children.Count)
            {
                if (node.Children[i].Name == "mprescripts")
                {
                    target = pre;
                    i++;
                    continue;
                }
                var sub = Layout(node.Children[i], Sub(path, i), Script(style, true), Stretch.None);
                Box? sup = null;
                if (i + 1 < node.Children.Count && node.Children[i + 1].Name != "mprescripts")
                    sup = Layout(node.Children[i + 1], Sub(path, i + 1), Script(style, false), Stretch.None);
                target.Add((sub, sup));
                i += sup == null ? 1 : 2;
            }
            return AttachScripts(baseBox, post, pre, style);
        }

        private Box AttachScripts(Box baseBox, List<(Box? Sub, Box? Sup)> post, List<(Box? Sub, Box? Sup)> pre, Style style)
        {
            var hasSub = post.Concat(pre).Any(p => p.Sub != null && p.Sub.Width + p.Sub.Height > 0);
            var hasSup = post.Concat(pre).Any(p => p.Sup != null && p.Sup.Width + p.Sup.Height > 0);
            var subShift = 0.0;
            var supShift = 0.0;
            if (hasSub)
            {
                subShift = C(MathConstant.SubscriptShiftDown, style);
                foreach (var (sub, _) in post.Concat(pre))
                {
                    if (sub != null)
                        subShift = Math.Max(subShift, sub.Height - C(MathConstant.SubscriptTopMax, style));
                }
                if (!baseBox.IsToken)
                    subShift = Math.Max(subShift, baseBox.Depth + C(MathConstant.SubscriptBaselineDropMin, style));
            }
            if (hasSup)
            {
                supShift = C(style.Cramped ? MathConstant.SuperscriptShiftUpCramped : MathConstant.SuperscriptShiftUp, style);
                foreach (var (_, sup) in post.Concat(pre))
                {
                    if (sup != null)
                        supShift = Math.Max(supShift, sup.Depth + C(MathConstant.SuperscriptBottomMin, style));
                }
                if (!baseBox.IsToken)
                    supShift = Math.Max(supShift, baseBox.Height - C(MathConstant.SuperscriptBaselineDropMax, style));
            }
            if (hasSub && hasSup)
            {
                var supDepth = post.Concat(pre).Max(p => p.Sup?.Depth ?? 0);
                var subHeight = post.Concat(pre).Max(p => p.Sub?.Height ?? 0);
                var gap = supShift - supDepth - (subHeight - subShift);
                var gapMin = C(MathConstant.SubSuperscriptGapMin, style);
                if (gap < gapMin)
                {
                    subShift += gapMin - gap;
                    var maxWithSub = C(MathConstant.SuperscriptBottomMaxWithSubscript, style);
                    var delta = maxWithSub - (supShift - supDepth);
                    if (delta > 0)
                    {
                        supShift += delta;
                        subShift -= delta;
                    }
                }
            }

            var box = new Box();
            var space = C(MathConstant.SpaceAfterScript, style);
            var x = 0.0;
            foreach (var (sub, sup) in pre)
            {
                var w = Math.Max(sub?.Width ?? 0, sup?.Width ?? 0);
                if (sub != null)
                    box.Add(sub, x + w - sub.Width, -subShift);
                if (sup != null)
                    box.Add(sup, x + w - sup.Width, supShift);
                x += w + space;
            }

            box.Add(baseBox, x, 0);
            var italic = baseBox.Italic;
            x += baseBox.Width;
            var end = x;
            var first = true;
            foreach (var (sub, sup) in post)
            {
                var subX = x - (first && baseBox.IsLargeOp ? italic : 0);
                var supX = x + (first && !baseBox.IsLargeOp ? italic : 0);
                if (sub != null)
                {
                    box.Add(sub, subX, -subShift);
                    end = Math.Max(end, subX + sub.Width);
                }
                if (sup != null)
                {
                    box.Add(sup, supX, supShift);
                    end = Math.Max(end, supX + sup.Width);
                }
                x = end;
                first = false;
            }
            box.Width = end + (post.Count > 0 ? space : 0);

            box.Height = baseBox.Height;
            box.Depth = baseBox.Depth;
            foreach (var (sub, sup) in post.Concat(pre))
            {
                if (sub != null)
                {
                    box.Height = Math.Max(box.Height, sub.Height - subShift);
                    box.Depth = Math.Max(box.Depth, sub.Depth + subShift);
                }
                if (sup != null)
                {
                    box.Height = Math.Max(box.Height, sup.Height + supShift);
                    box.Depth = Math.Max(box.Depth, sup.Depth - supShift);
                }
            }
            return box;
        }

        private Box LayoutUnderOver(MathNode node, int[]? path, Style style, Stretch stretch)
        {
            var op = CoreOperator(node.Children[0]);
            var movable = op != null && (op.GetAttribute("movablelimits") is { } ml
                ? ml == "true"
                : OperatorDictionary.TryLookup(op.Text, out var e) && e.Has(OperatorFlags.MovableLimits));
            if (movable && !style.Display)
            {
                var scripts = node.Name switch { "munder" => "msub", "mover" => "msup", _ => "msubsup" };
                return LayoutScripts(MathNode.Element(scripts, node.Children), path, style);
            }

            var hasUnder = node.Name is "munder" or "munderover";
            var hasOver = node.Name is "mover" or "munderover";
            var underIndex = hasUnder ? 1 : -1;
            var overIndex = node.Name == "mover" ? 1 : node.Name == "munderover" ? 2 : -1;
            var accentUnder = hasUnder && IsAccent(node, node.Children[underIndex], "accentunder");
            var accentOver = hasOver && IsAccent(node, node.Children[overIndex], "accent");

            var baseStyle = style;
            var underStyle = accentUnder ? style.WithDisplay(false) : Script(style, false);
            var overStyle = accentOver ? style.WithDisplay(false).WithCramped(true) : Script(style, false);

            // stretchy operators take the width of the widest of the others
            var stretchyBase = IsHorizontalStretchy(node.Children[0]);
            var stretchyUnder = hasUnder && IsHorizontalStretchy(node.Children[underIndex]);
            var stretchyOver = hasOver && IsHorizontalStretchy(node.Children[overIndex]);

            Box? baseBox = stretchyBase ? null : Layout(node.Children[0], Sub(path, 0), baseStyle, Stretch.None);
            Box? under = hasUnder && !stretchyUnder ? Layout(node.Children[underIndex], Sub(path, underIndex), underStyle, new Stretch(0, 0, 0, accentUnder)) : null;
            Box? over = hasOver && !stretchyOver ? Layout(node.Children[overIndex], Sub(path, overIndex), overStyle, new Stretch(0, 0, 0, accentOver)) : null;
            var width = new[] { baseBox, under, over }.Where(b => b != null).Select(b => b!.Width).DefaultIfEmpty(0).Max();
            if (stretch.Width > width)
                width = stretch.Width;
            if (width <= 0)
                width = style.Size;
            baseBox ??= Layout(node.Children[0], Sub(path, 0), baseStyle, new Stretch(0, 0, width, false));
            if (hasUnder && under == null)
                under = Layout(node.Children[underIndex], Sub(path, underIndex), underStyle, new Stretch(0, 0, width, accentUnder, true));
            if (hasOver && over == null)
                over = Layout(node.Children[overIndex], Sub(path, overIndex), overStyle, new Stretch(0, 0, width, accentOver));

            var total = Math.Max(baseBox.Width, Math.Max(under?.Width ?? 0, over?.Width ?? 0));
            var baseX = (total - baseBox.Width) / 2;
            var placed = new List<(Box Box, double X, double Shift)> { (baseBox, baseX, 0) };
            var height = baseBox.Height;
            var depth = baseBox.Depth;

            if (over != null)
            {
                double shift;
                double x;
                if (accentOver)
                {
                    shift = Math.Max(0, baseBox.Height - C(MathConstant.AccentBaseHeight, style));
                    var attach = baseX + (baseBox.TopAccent ?? baseBox.Width / 2);
                    x = attach - (over.TopAccent ?? over.Width / 2);
                }
                else
                {
                    double gap, rise, extra = 0;
                    if (baseBox.IsLargeOp)
                    {
                        gap = C(MathConstant.UpperLimitGapMin, style);
                        rise = C(MathConstant.UpperLimitBaselineRiseMin, style);
                    }
                    else if (stretchyOver)
                    {
                        gap = C(MathConstant.StretchStackGapBelowMin, style);
                        rise = C(MathConstant.StretchStackTopShiftUp, style);
                    }
                    else
                    {
                        gap = C(MathConstant.OverbarVerticalGap, style);
                        rise = 0;
                        extra = C(MathConstant.OverbarExtraAscender, style);
                    }
                    shift = baseBox.Height + Math.Max(gap + over.Depth, rise);
                    height = Math.Max(height, shift + over.Height + extra);
                    x = (total - over.Width) / 2;
                    if (baseBox.IsLargeOp)
                        x += baseBox.Italic / 2;
                }
                placed.Add((over, x, shift));
                height = Math.Max(height, shift + over.Height);
                depth = Math.Max(depth, over.Depth - shift);
            }

            if (under != null)
            {
                double shift;
                if (accentUnder)
                {
                    shift = baseBox.Depth + under.Height;
                }
                else
                {
                    double gap, drop, extra = 0;
                    if (baseBox.IsLargeOp)
                    {
                        gap = C(MathConstant.LowerLimitGapMin, style);
                        drop = C(MathConstant.LowerLimitBaselineDropMin, style);
                    }
                    else if (stretchyUnder)
                    {
                        gap = C(MathConstant.StretchStackGapAboveMin, style);
                        drop = C(MathConstant.StretchStackBottomShiftDown, style);
                    }
                    else
                    {
                        gap = C(MathConstant.UnderbarVerticalGap, style);
                        drop = 0;
                        extra = C(MathConstant.UnderbarExtraDescender, style);
                    }
                    shift = baseBox.Depth + Math.Max(gap + under.Height, drop);
                    depth = Math.Max(depth, shift + under.Depth + extra);
                }
                var x = (total - under.Width) / 2 - (baseBox.IsLargeOp ? baseBox.Italic / 2 : 0);
                placed.Add((under, x, -shift));
                depth = Math.Max(depth, shift + under.Depth);
                height = Math.Max(height, under.Height - shift);
            }

            var minX = placed.Min(p => p.X);
            var maxX = placed.Max(p => p.X + p.Box.Width);
            var box = new Box { Width = maxX - minX, Height = height, Depth = depth, IsLargeOp = baseBox.IsLargeOp };
            foreach (var (b, x, shift) in placed)
                box.Add(b, x - minX, shift);
            return box;
        }

        private static bool IsAccent(MathNode node, MathNode script, string attribute)
        {
            if (node.GetAttribute(attribute) is { } value)
                return value == "true";
            var op = CoreOperatorOfSlot(script);
            if (op == null)
                return false;
            if (op.GetAttribute("accent") is { } accent)
                return accent == "true";
            return OperatorDictionary.TryLookup(op.Text, out var entry) && entry.Has(OperatorFlags.Accent);
        }

        // ---- tables and enclosures --------------------------------------------------------------------------------

        private Box LayoutTable(MathNode table, int[]? path, Style style)
        {
            // an equation array (the lines of █(...@...) or of aligned, which start with maligngroup) keeps the style of
            // the equation; any other table is in text style, as MathML Core says
            var isArray = table.Children.Count > 0 && table.Children.All(r => r.Children.Where(c => c.Name == "mtd").All(StartsWithAlignGroup));
            var cellStyle = style.WithDisplay(table.GetAttribute("displaystyle") is { } displaystyle ? displaystyle == "true" : isArray && style.Display);
            var marks = new List<List<double?>>();
            var rows = new List<(List<Box> Cells, Box? Label, MathNode Row)>();
            var columns = 0;
            for (var r = 0; r < table.Children.Count; r++)
            {
                var row = table.Children[r];
                var cells = new List<Box>();
                var rowMarks = new List<double?>();
                Box? label = null;
                for (var c = 0; c < row.Children.Count; c++)
                {
                    var cell = row.Children[c];
                    var content = cell.Name == "mtd" && cell.Children.Count == 1 ? cell.Children[0] : cell;
                    var cellBox = Layout(content, cell.Name == "mtd" && cell.Children.Count == 1 ? Sub(path, r, c, 0) : null, cellStyle, Stretch.None);
                    if (row.Name == "mlabeledtr" && c == 0)
                    {
                        label = cellBox;
                    }
                    else
                    {
                        cells.Add(cellBox);
                        rowMarks.Add(AlignMark(content, cellBox));
                    }
                }
                columns = Math.Max(columns, cells.Count);
                rows.Add((cells, label, row));
                marks.Add(rowMarks);
            }

            // the cells of a column with alignment marks (malignmark, the & of UnicodeMath and of aligned) line up at
            // the first mark; the others by columnalign
            var widths = new double[columns];
            var markLefts = new double?[columns];
            for (var c = 0; c < columns; c++)
            {
                double markLeft = 0, markRight = 0, other = 0;
                var marked = false;
                for (var r = 0; r < rows.Count; r++)
                {
                    if (c >= rows[r].Cells.Count)
                        continue;
                    var cell = rows[r].Cells[c];
                    if (marks[r][c] is { } mark)
                    {
                        marked = true;
                        markLeft = Math.Max(markLeft, mark);
                        markRight = Math.Max(markRight, cell.Width - mark);
                    }
                    else
                    {
                        other = Math.Max(other, cell.Width);
                    }
                }
                widths[c] = marked ? Math.Max(markLeft + markRight, other) : other;
                if (marked)
                    markLefts[c] = markLeft;
            }

            var columnSpacing = Lists(table.GetAttribute("columnspacing"), "0.8em", style, columns - 1);
            var rowSpacing = Lists(table.GetAttribute("rowspacing"), "1.0ex", style, rows.Count - 1);
            var aligns = (table.GetAttribute("columnalign") ?? "center").Split(Spaces, StringSplitOptions.RemoveEmptyEntries);

            var lefts = new double[columns];
            var x = 0.0;
            for (var c = 0; c < columns; c++)
            {
                lefts[c] = x;
                x += widths[c] + (c < columns - 1 ? columnSpacing[c] : 0);
            }
            var tableWidth = x;

            var box = new Box();
            var y = 0.0;
            var placedRows = new List<(double Baseline, (List<Box> Cells, Box? Label, MathNode Row) Row)>();
            for (var r = 0; r < rows.Count; r++)
            {
                var (cells, label, _) = rows[r];
                var h = cells.Select(c => c.Height).DefaultIfEmpty(0).Max();
                var d = cells.Select(c => c.Depth).DefaultIfEmpty(0).Max();
                if (label != null)
                {
                    h = Math.Max(h, label.Height);
                    d = Math.Max(d, label.Depth);
                }
                h = Math.Max(h, style.Size * 0.6);
                d = Math.Max(d, style.Size * 0.2);
                y -= h;
                placedRows.Add((y, rows[r]));
                y -= d + (r < rows.Count - 1 ? rowSpacing[r] : 0);
            }
            var total = -y;
            var shift = total / 2 + Axis(style);
            // the labels of the rows (the numbers of the equations) on the right, or on the left with side="left": at the
            // edge of the line when its width is known, with the table in the middle of what is left
            var labelWidth = placedRows.Select(p => p.Row.Label?.Width ?? 0).DefaultIfEmpty(0).Max();
            var hasLabels = placedRows.Any(p => p.Row.Label != null);
            var left = table.GetAttribute("side") is "left" or "leftoverlap";
            var gap = style.Size;
            var width = hasLabels ? tableWidth + gap + labelWidth : tableWidth;
            var tableX = hasLabels && left ? labelWidth + gap : 0;
            if (hasLabels && options.LineWidth is { } line && line > width)
            {
                width = line;
                var centered = (line - tableWidth) / 2;
                tableX = left ? Math.Max(centered, labelWidth + gap) : Math.Min(centered, line - labelWidth - gap - tableWidth);
            }

            foreach (var (baseline, (cells, label, row)) in placedRows)
            {
                var rowAligns = row.GetAttribute("columnalign")?.Split(Spaces, StringSplitOptions.RemoveEmptyEntries);
                var cellOffset = row.Name == "mlabeledtr" ? 1 : 0;
                for (var c = 0; c < cells.Count; c++)
                {
                    var cellNode = row.Children[c + cellOffset];
                    var align = cellNode.GetAttribute("columnalign") ?? Pick(rowAligns, c) ?? Pick(aligns, c) ?? "center";
                    var rowIndex = placedRows.FindIndex(p => ReferenceEquals(p.Row.Row, row));
                    var cx = markLefts[c] is { } columnMark && marks[rowIndex][c] is { } mark
                        ? lefts[c] + columnMark - mark
                        : align switch
                        {
                            "left" => lefts[c],
                            "right" => lefts[c] + widths[c] - cells[c].Width,
                            _ => lefts[c] + (widths[c] - cells[c].Width) / 2,
                        };
                    box.Add(cells[c], tableX + cx, baseline + shift);
                }
                if (label != null)
                    box.Add(label, left ? 0 : width - label.Width, baseline + shift);
            }
            box.Width = width;
            box.Height = shift;
            box.Depth = total - shift;
            return box;
        }

        private static bool StartsWithAlignGroup(MathNode cell) =>
            cell.Children.Count == 1 && cell.Children[0] is { IsRow: true } row && row.Children.Count > 0 && row.Children[0].Name == "maligngroup";

        /// <summary>The x of the first alignment mark of the content of a cell, from the caret positions of its row; null without one.</summary>
        private static double? AlignMark(MathNode content, Box box)
        {
            if (!content.IsRow || box.CaretX == null)
                return null;
            for (var i = 0; i < content.Children.Count && i < box.CaretX.Length; i++)
            {
                if (content.Children[i].Name == "malignmark")
                    return box.CaretX[i];
            }
            return null;
        }

        private static string? Pick(string[]? values, int index) =>
            values == null || values.Length == 0 ? null : values[Math.Min(index, values.Length - 1)];

        private double[] Lists(string? value, string fallback, Style style, int count)
        {
            var parts = (value ?? fallback).Split(Spaces, StringSplitOptions.RemoveEmptyEntries);
            var result = new double[Math.Max(0, count)];
            for (var i = 0; i < result.Length; i++)
            {
                var part = parts.Length == 0 ? fallback : parts[Math.Min(i, parts.Length - 1)];
                result[i] = Lengths.TryParse(part, style.Size, Ex(style), style.Size, out var length) ? length : style.Size * 0.8;
            }
            return result;
        }

        private Box LayoutEnclose(MathNode node, int[]? path, Style style)
        {
            var notations = new HashSet<string>((node.GetAttribute("notation") ?? "longdiv").Split(Spaces, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
            if (notations.Contains("radical"))
                return LayoutRadical(LayoutInferred(node, path, style.WithCramped(true), Stretch.None), null, style);

            var content = LayoutInferred(node, path, style, Stretch.None);
            var t = C(MathConstant.OverbarRuleThickness, style);
            var pad = style.Size * 0.15;
            bool Has(params string[] names) => names.Any(notations.Contains);

            var left = Has("box", "roundedbox", "left", "longdiv", "circle") ? pad + t : 0;
            var right = Has("box", "roundedbox", "right", "actuarial", "madruwb", "circle") ? pad + t : 0;
            var top = Has("box", "roundedbox", "top", "actuarial", "longdiv", "circle") ? pad + t : 0;
            var bottom = Has("box", "roundedbox", "madruwb", "circle", "phasorangle") ? pad + t : 0;
            if (notations.Contains("bottom"))
                bottom = Math.Max(bottom, C(MathConstant.UnderbarVerticalGap, style) + C(MathConstant.UnderbarRuleThickness, style));
            if (notations.Contains("top") && !Has("box", "roundedbox"))
                top = Math.Max(top, C(MathConstant.OverbarVerticalGap, style) + t);
            if (notations.Contains("circle"))
            {
                left += pad;
                right += pad;
            }

            var box = Box.Wrap(content, left, 0);
            box.Width = left + content.Width + right;
            box.Height = content.Height + top;
            box.Depth = content.Depth + bottom;
            var x0 = t / 2;
            var x1 = box.Width - t / 2;
            var y0 = -box.Depth + t / 2;
            var y1 = box.Height - t / 2;
            void Line(double ax, double ay, double bx, double by) => box.Shapes.Add(ShapeItem.Line(ax, ay, bx, by, t));

            if (Has("box", "roundedbox"))
                box.Shapes.Add(new ShapeItem(MathShapeKind.Outline, MathShapeRole.Ink, x0, y0, x1 - x0, y1 - y0, thickness: t));
            if (notations.Contains("circle"))
                box.Shapes.Add(new ShapeItem(MathShapeKind.Ellipse, MathShapeRole.Ink, x0, y0, x1 - x0, y1 - y0, thickness: t));
            if (Has("left", "longdiv"))
                Line(x0, y0, x0, y1);
            if (Has("right", "actuarial", "madruwb"))
                Line(x1, y0, x1, y1);
            if (Has("top", "actuarial", "longdiv"))
                Line(x0, y1, x1, y1);
            if (Has("bottom", "madruwb", "phasorangle"))
                Line(x0, y0, x1, y0);
            if (Has("updiagonalstrike", "updiagonalarrow"))
                Line(left, -content.Depth, left + content.Width, content.Height);
            if (notations.Contains("downdiagonalstrike"))
                Line(left, content.Height, left + content.Width, -content.Depth);
            if (notations.Contains("verticalstrike"))
                Line(left + content.Width / 2, -content.Depth, left + content.Width / 2, content.Height);
            if (notations.Contains("horizontalstrike"))
                Line(left, Axis(style), left + content.Width, Axis(style));
            if (notations.Contains("phasorangle"))
                Line(x0, y0, x0 + (y1 - y0) / 2, y1);
            return box;
        }
    }

    internal static class MathTreePaths
    {
        public static int[] Append(int[] path, int[] more)
        {
            var result = new int[path.Length + more.Length];
            path.CopyTo(result, 0);
            more.CopyTo(result, path.Length);
            return result;
        }
    }
}
