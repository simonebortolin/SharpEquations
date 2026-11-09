#nullable enable

namespace SharpEquations.Internal.MathML
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml;
    using System.Xml.Linq;
    using System.Xml.Xsl;
    using SharpEquations.Internal.Text;
    using SharpEquations.Internal.UnicodeMath;

    /// <summary>
    /// Reads MathML text, and normalizes the MathML of every parser into the form an <see cref="Equation"/> holds.
    /// </summary>
    internal static class MathMLParser
    {
        private static readonly XNamespace Mml = MathMLNamespaces.MathML;

        /// <summary>The elements of presentation MathML (MathML 3 and 4), and those of semantics.</summary>
        private static readonly HashSet<string> PresentationElements = new HashSet<string>(StringComparer.Ordinal)
        {
            "math", "mi", "mn", "mo", "mtext", "mspace", "ms", "mglyph", "mrow", "mfrac", "msqrt", "mroot", "mstyle",
            "merror", "mpadded", "mphantom", "mfenced", "menclose", "msub", "msup", "msubsup", "munder", "mover",
            "munderover", "mmultiscripts", "mprescripts", "none", "mtable", "mtr", "mlabeledtr", "mtd", "maligngroup",
            "malignmark", "mstack", "mlongdiv", "msgroup", "msrow", "mscarries", "mscarry", "msline", "maction",
            "semantics", "annotation", "annotation-xml",
        };

        /// <summary>The attributes that only format: colours, sizes, fonts and CSS.</summary>
        private static readonly HashSet<string> FormattingAttributes = new HashSet<string>(StringComparer.Ordinal)
        {
            "mathcolor", "mathbackground", "mathsize", "style", "color", "background", "fontsize", "fontfamily", "fontweight", "fontstyle",
        };

        /// <summary>Parses MathML or OMML text, with the prefixes mml, m and w declared and DTDs prohibited.</summary>
        public static XElement ParseXml(string text, EquationFormat format)
        {
            var names = new XmlNamespaceManager(new NameTable());
            names.AddNamespace("mml", MathMLNamespaces.MathML.NamespaceName);
            names.AddNamespace("m", MathMLNamespaces.Omml.NamespaceName);
            names.AddNamespace("w", MathMLNamespaces.WordprocessingML.NamespaceName);
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, ConformanceLevel = ConformanceLevel.Fragment };
            try
            {
                using var reader = XmlReader.Create(new StringReader(text), settings, new XmlParserContext(null, names, null, XmlSpace.None));
                reader.MoveToContent();
                if (reader.NodeType != XmlNodeType.Element)
                    throw new FormatException("The " + format + " equation holds no element.");
                var element = (XElement)XNode.ReadFrom(reader);
                reader.MoveToContent();
                if (!reader.EOF)
                    throw new FormatException("The " + format + " equation holds more than one element.");
                return element;
            }
            catch (XmlException e)
            {
                throw new FormatException("The " + format + " equation is not well-formed XML: " + e.Message, e);
            }
        }

        /// <summary>
        /// A copy of a math element in the MathML namespace, with the elements without a namespace moved to it and,
        /// unless the options preserve it, without formatting. Strict options reject what is not presentation MathML
        /// when <paramref name="validate"/> is set (the MathML comes from the user, not from a parser).
        /// </summary>
        public static XElement Normalize(XElement source, EquationOptions options, bool validate)
        {
            if (source.Name.LocalName != "math" || (source.Name.Namespace != Mml && source.Name.Namespace != XNamespace.None))
                throw new FormatException("Expected a MathML math element, not " + source.Name + ".");
            var math = new XElement(source);
            foreach (var element in math.DescendantsAndSelf().Where(e => e.Name.Namespace == XNamespace.None))
                element.Name = Mml + element.Name.LocalName;
            // Content MathML (apply, ci, cn...) is read as the presentation MathML it is written as
            if (validate && HasContentMarkup(math))
                math = ContentToPresentation(math);
            // the prefixes of the source (m:mi) are not part of the equation: the MathML namespace is the default one
            math.DescendantsAndSelf().Attributes().Where(a => a.IsNamespaceDeclaration).Remove();
            // a link of UnicodeMath (\href) is an mrow with href, as MathML has no a element
            foreach (var link in math.Descendants(Mml + "a"))
                link.Name = Mml + "mrow";
            if (validate && options.Strict)
                Validate(math);
            foreach (var fenced in math.Descendants(Mml + "mfenced").Reverse().ToList())
                fenced.ReplaceWith(Unfence(fenced));
            // invisible brackets (UnicodeMath 〖〗, LaTeX {} before a script) only group: an mrow
            foreach (var row in math.Descendants(Mml + "mrow").Where(IsInvisiblyFenced).Reverse().ToList())
            {
                var content = row.Elements().Skip(1).Take(row.Elements().Count() - 2).ToList();
                row.ReplaceWith(content.Count == 1 && content[0].Name == Mml + "mrow" ? content[0] : new XElement(Mml + "mrow", content));
            }
            // and with an intent or an argument name (ⓘ("name"〖𝑎+𝑏〗)), the row keeps them
            foreach (var row in math.Descendants(Mml + "mrow").Where(r => r.Elements().Count() >= 2 && IsEmptyFence(r.Elements().First()) && IsEmptyFence(r.Elements().Last())).ToList())
            {
                row.Elements().First().Remove();
                row.Elements().Last().Remove();
                if ((string?)row.Attribute("intent") == ":fenced")
                    row.Attribute("intent")!.Remove();
            }
            if (!options.PreserveFormatting)
                RemoveFormatting(math);
            // the division slash (∕) is the slash, the hyphen-minus of an operator (₋ of UnicodeMath) the minus sign, and
            // infinity an identifier
            foreach (var mo in math.Descendants(Mml + "mo").Where(e => !e.HasElements && e.Value is "∕" or "-" or "∞" && !e.HasAttributes).ToList())
            {
                if (mo.Value == "∞")
                    mo.Name = Mml + "mi";
                else
                    mo.Value = mo.Value == "∕" ? "/" : "−";
            }
            // comments, and the selection of an editor (UnicodeMath Ⓐ and Ⓕ), are not part of the equation
            math.DescendantNodes().OfType<XComment>().Remove();
            math.DescendantsAndSelf().Attributes().Where(a => a.Name == "selanchor" || a.Name == "selfocus").Remove();
            foreach (var empty in math.DescendantsAndSelf().Where(e => !e.IsEmpty && !e.Nodes().Any(n => n is not XText t || t.Value.Length > 0)).ToList())
                empty.RemoveNodes();
            // accent="false" over or under a script that is not an accent (an arrow over a letter, as OMML's groupChr),
            // which it is not by default either; mathvariant="normal" of text, which is upright
            foreach (var accent in math.Descendants().Where(e => e.Name == Mml + "mover" || e.Name == Mml + "munder" || e.Name == Mml + "munderover")
                .Where(e => !IsAccentScript(e.Elements().ElementAtOrDefault(1)) && (e.Name != Mml + "munderover" || !IsAccentScript(e.Elements().ElementAtOrDefault(2))))
                .Attributes().Where(a => (a.Name == "accent" || a.Name == "accentunder") && a.Value == "false").ToList())
                accent.Remove();
            math.Descendants(Mml + "mtext").Attributes("mathvariant").Where(a => a.Value == "normal").Remove();
            // adjacent text is one text
            foreach (var text in math.Descendants(Mml + "mtext").Where(t => !t.HasAttributes && !t.HasElements).ToList())
            {
                if (text.Parent != null && RowLike.Contains(text.Parent.Name.LocalName) && text.ElementsBeforeSelf().LastOrDefault() is { } previous && previous.Name == Mml + "mtext" && !previous.HasAttributes &&
                    !previous.HasElements && previous.NextNode == text)
                {
                    previous.Value += text.Value;
                    text.Remove();
                }
            }
            foreach (var token in math.Descendants().Where(e => (e.Name == Mml + "mi" || e.Name == Mml + "mn") && !e.HasElements))
                StyleLetters(token);
            // a bar under a base (the m:bar of OMML) is the underline of UnicodeMath and LaTeX
            foreach (var underbar in math.Descendants(Mml + "munder").Where(IsUnderbar).Reverse().ToList())
                underbar.ReplaceWith(new XElement(Mml + "menclose", new XAttribute("notation", "bottom"), underbar.Elements().First()));
            RemoveDefaultIntents(math);
            RemoveRedundantRows(math);
            // stretchy="true" on an operator that stretches anyway (arrows, bars, fences): the readers differ in writing it
            foreach (var stretchy in math.Descendants(Mml + "mo").Attributes("stretchy").Where(a => a.Value == "true" && StretchesByDefault(a.Parent!.Value)).ToList())
                stretchy.Remove();
            // a horizontal bracket (⏞, ⏟) over or under its base is never an accent; the OMML reader says so
            foreach (var accent in math.Descendants().Where(e => e.Name == Mml + "mover" || e.Name == Mml + "munder")
                .Where(e => e.Elements().Skip(1).FirstOrDefault() is { } script && script.Name == Mml + "mo" && "⏜⏝⏞⏟⎴⎵⏠⏡".Contains(script.Value) && script.Value.Length == 1)
                .Attributes().Where(a => (a.Name == "accent" || a.Name == "accentunder") && a.Value == "false").ToList())
                accent.Remove();
            // intents name what they refer to with plain letters: derivative(1,y,x), not derivative(1,𝑦,𝑥)
            foreach (var intent in math.DescendantsAndSelf().Attributes("intent"))
                intent.Value = PlainLetters(intent.Value);
            return math;
        }

        /// <summary>
        /// The intents that UnicodeMath (and LaTeX, read through it) gives by default to what its structure says already:
        /// the same equation has them or not, and in one form or another, as it is written (\binom{n}{k}, 𝑛⒞𝑘, (𝑛¦𝑘)), and
        /// the other formats do not hold them. The intents of the characters (:text, ⅆ, transpose) and of equation
        /// labels are kept.
        /// </summary>
        private static readonly HashSet<string> DefaultIntents = new HashSet<string>(StringComparer.Ordinal)
        {
            ":fenced", ":function", ":largeop", ":piecewise", ":system-of-equations", ":array", ":parenthesized-matrix", ":determinant",
            ":normed-matrix", ":bracketed-matrix", ":curly-braced-matrix", ":levi-civita", ":matrix", ":derivative", ":partial-derivative",
            "n-ary", "absolute-value", "determinant",
            "binomial-coefficient", "derivative", "partial-derivative", "cardinality", "open-interval", "closed-interval",
            "open-closed-interval", "closed-open-interval",
        };

        /// <summary>Whether an intent is one that UnicodeMath gives by default, which an equation does not hold.</summary>
        internal static bool IsDefaultIntent(string intent)
        {
            int paren = intent.IndexOf('(');
            return DefaultIntents.Contains(paren < 0 ? intent : intent.Substring(0, paren));
        }

        /// <summary>Whether an argument name is one that an intent an equation holds refers to.</summary>
        internal static bool IsReferenced(XAttribute arg)
        {
            var reference = "$" + arg.Value;
            return arg.Parent!.Ancestors().Any(a => a.Attribute("intent") is { } intent && !IsDefaultIntent(intent.Value) && intent.Value.Contains(reference));
        }

        /// <summary>The default intents removed, and the argument names that no intent refers to any more.</summary>
        private static void RemoveDefaultIntents(XElement math)
        {
            foreach (var arg in math.Descendants().Attributes("arg").Where(a => !IsReferenced(a)).ToList())
                arg.Remove();
            math.DescendantsAndSelf().Attributes("intent").Where(i => IsDefaultIntent(i.Value)).Remove();
        }

        /// <summary>
        /// The letters of an identifier in the one form of MathML Core, so that &lt;mi&gt;x&lt;/mi&gt; of OMML or MathML and 𝑥
        /// of UnicodeMath and LaTeX are the same equation: an italic letter alone is the plain one, which MathML makes
        /// italic (x), the other styles are their mathematical alphanumerics (𝐲, ℒ) rather than a mathvariant, and an
        /// upright letter is the plain one with mathvariant="normal".
        /// </summary>
        private static void StyleLetters(XElement token)
        {
            var variant = (string?)token.Attribute("mathvariant");
            var text = token.Value;
            if (variant == null)
            {
                if (token.Name == Mml + "mi" && CodePoints.IsSingle(text, out _) && LaTeX.LaTeXSymbols.TryGetPlain(text, out var italic, out var plain)
                    && italic == "mit" && CodePoints.IsSingle(plain, out var letter) && IsItalicByDefault(letter.Value))
                    token.Value = plain;
                return;
            }
            // the Arabic styles (isolated, initial...) are left as mathvariants
            if (!UnicodeMathSerializer.MathVariants.TryGetValue(variant, out var style) || !LatinAndGreekStyles.Contains(style))
                return;
            if (style == "mup" || style == "mit" && token.Name == Mml + "mi" && CodePoints.IsSingle(text, out _))
            {
                // upright, or italic alone: the plain letters, whatever characters a writer used
                var plainText = new System.Text.StringBuilder();
                foreach (var codePoint in CodePoints.Of(text))
                    plainText.Append(LaTeX.LaTeXSymbols.TryGetPlain(codePoint.ToString(), out _, out var ch) ? ch : codePoint.ToString());
                token.Value = plainText.ToString();
                if (style == "mit")
                    token.Attribute("mathvariant")!.Remove();
                return;
            }
            var styled = new System.Text.StringBuilder();
            foreach (var codePoint in CodePoints.Of(text))
            {
                var ch = codePoint.ToString();
                var mapped = UnicodeMathText.GetMathAlphanumeric(ch, style);
                if (mapped == ch && !(LaTeX.LaTeXSymbols.TryGetPlain(ch, out var existing, out _) && existing == style))
                    return;
                styled.Append(mapped);
            }
            token.Value = styled.ToString();
            token.Attribute("mathvariant")!.Remove();
        }

        /// <summary>The elements whose children are in an inferred mrow, as those of an mrow.</summary>
        private static readonly HashSet<string> RowLike = new HashSet<string>(StringComparer.Ordinal)
        {
            "math", "mrow", "msqrt", "mstyle", "merror", "mpadded", "mphantom", "menclose", "mtd",
        };

        /// <summary>
        /// The mrow elements without attributes that group nothing more than their parent: one with a single child
        /// is that child, and one that is all of a row (or of an element whose children form a row) is its children,
        /// unless fences make it a group. What the stylesheets and the parsers add differs only in these.
        /// </summary>
        private static void RemoveRedundantRows(XElement math)
        {
            foreach (var row in math.Descendants(Mml + "mrow").Reverse().ToList())
            {
                if (row.HasAttributes || row.Parent == null)
                    continue;
                var children = row.Elements().ToList();
                var parent = row.Parent;
                // the operand of an n-ary operator stays a row, which a row of its own may be
                if (IsNaryOperand(row))
                {
                    if (children.Count == 1 && children[0].Name == Mml + "mrow" && !children[0].HasAttributes)
                        row.ReplaceWith(children[0]);
                    continue;
                }
                bool fenced = children.Count > 1 && children[0].Name == Mml + "mo" && children[children.Count - 1].Name == Mml + "mo";
                if (children.Count == 1)
                    row.ReplaceWith(children[0]);
                // a row that fills its parent, as the stylesheets write them, or the content between fences: a row between
                // fences groups them
                else if (parent.Name.Namespace == Mml && (RowLike.Contains(parent.Name.LocalName) && parent.Elements().Count() == 1 || IsBetweenFences(row))
                    && !fenced)
                    row.ReplaceWith(row.Nodes());
            }
        }

        /// <summary>The only element between the fences of a row: (𝑎,𝑏) with or without a row for 𝑎,𝑏.</summary>
        private static bool IsBetweenFences(XElement row)
        {
            var parent = row.Parent!;
            if (parent.Name != Mml + "mrow")
                return false;
            var siblings = parent.Elements().ToList();
            return siblings.Count == 3 && siblings[1] == row && siblings[0].Name == Mml + "mo" && siblings[2].Name == Mml + "mo" && !parent.HasAttributes;
        }

        /// <summary>A script that is an accent by default: an operator that MathML stretches or marks as an accent.</summary>
        private static bool IsAccentScript(XElement? script)
        {
            if (script == null || script.Name != Mml + "mo" || script.HasElements || !CodePoints.IsSingle(script.Value, out var single))
                return false;
            int c = single.Value;
            return c >= 0x300 && c <= 0x36F || c >= 0x20D0 && c <= 0x20FF || c <= 0xFFFF && "^ˇ˘˙¨´`~¯‾˜ˆ←→↔⃗⏞⏟⏜⏝⎴⎵_".Contains(script.Value);
        }

        private static bool IsUnderbar(XElement underscript)
        {
            var elements = underscript.Elements().ToList();
            return elements.Count == 2 && !underscript.Attributes().Any(a => a.Name != "accentunder" || a.Value != "false")
                && elements[1].Name == Mml + "mo" && !elements[1].HasAttributes && elements[1].Value is "\u0332" or "_";
        }

        /// <summary>The row after an n-ary operator (∫, ∑ with or without limits), which is what it applies to.</summary>
        private static bool IsNaryOperand(XElement row)
        {
            if (row.Parent == null || !RowLike.Contains(row.Parent.Name.LocalName))
                return false;
            var operatorElement = row.ElementsBeforeSelf().LastOrDefault();
            while (operatorElement != null && operatorElement.Name != Mml + "mo" && operatorElement.Name.LocalName is "msub" or "msup" or "msubsup" or "munder" or "mover" or "munderover")
                operatorElement = operatorElement.Elements().FirstOrDefault();
            if (operatorElement == null || operatorElement.Name != Mml + "mo" || !CodePoints.IsSingle(operatorElement.Value, out var single))
                return false;
            int c = single.Value;
            return c >= 0x220F && c <= 0x2211 || c >= 0x222B && c <= 0x2233 || c >= 0x22C0 && c <= 0x22C3 || c >= 0x2A00 && c <= 0x2A1C;
        }

        private static bool StretchesByDefault(string text)
        {
            if (!CodePoints.IsSingle(text, out var single))
                return false;
            int c = single.Value;
            return c >= 0x2190 && c <= 0x21FF || c >= 0x27F0 && c <= 0x27FF || c >= 0x2900 && c <= 0x297F || c >= 0x23DC && c <= 0x23E1
                || c <= 0xFFFF && "¯‾_^~ˇ˘˙̲⎴⎵‖|()[]{}⟨⟩⌈⌉⌊⌋".Contains(text);
        }

        private static readonly HashSet<string> LatinAndGreekStyles = new HashSet<string>(StringComparer.Ordinal)
        {
            "mup", "mbf", "mit", "mbfit", "mscr", "mbfscr", "mfrak", "mbffrak", "Bbb", "msans", "mbfsans", "mitsans", "mbfitsans", "mtt",
        };

        private static bool IsItalicByDefault(int c) => c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= 0x3B1 && c <= 0x3C9 || c is 0x3D1 or 0x3D5 or 0x3D6 or 0x3F0 or 0x3F1 or 0x3F5;

        /// <summary>The italic letters of the mathematical alphanumerics (𝑦, 𝛼) as plain ones (y, α).</summary>
        private static string PlainLetters(string text)
        {
            var plain = new System.Text.StringBuilder();
            foreach (var codePoint in CodePoints.Of(text))
            {
                int c = codePoint.Value;
                c = c switch
                {
                    >= 0x1D434 and <= 0x1D44D => 'A' + c - 0x1D434,
                    >= 0x1D44E and <= 0x1D467 => 'a' + c - 0x1D44E,
                    0x210E => 'h',
                    >= 0x1D6E2 and <= 0x1D6FA => 0x391 + c - 0x1D6E2,
                    >= 0x1D6FC and <= 0x1D714 => 0x3B1 + c - 0x1D6FC,
                    _ => c,
                };
                plain.Append(char.ConvertFromUtf32(c));
            }
            return plain.ToString();
        }

        /// <summary>
        /// The deprecated mfenced as the mrow of MathML Core: its fences and separators as mo elements, as the UnicodeMath
        /// parser writes a bracketed expression.
        /// </summary>
        private static XElement Unfence(XElement fenced)
        {
            var open = (string?)fenced.Attribute("open") ?? "(";
            var close = (string?)fenced.Attribute("close") ?? ")";
            var separators = string.Concat(((string?)fenced.Attribute("separators") ?? ",").Where(c => !char.IsWhiteSpace(c)));
            var separatorList = CodePoints.Of(separators).Select(c => c.ToString()).ToList();
            var children = fenced.Elements().ToList();
            var content = new List<XElement>();
            for (int i = 0; i < children.Count; i++)
            {
                if (i > 0 && separatorList.Count > 0)
                    content.Add(new XElement(Mml + "mo", separatorList[Math.Min(i - 1, separatorList.Count - 1)]));
                content.Add(children[i]);
            }
            var row = new XElement(Mml + "mrow", fenced.Attributes().Where(a => a.Name != "open" && a.Name != "close" && a.Name != "separators"));
            row.SetAttributeValue("intent", (string?)row.Attribute("intent") ?? ":fenced");
            row.Add(new XElement(Mml + "mo", open));
            row.Add(content.Count == 1 && content[0].Name == Mml + "mrow" && !content[0].HasAttributes ? content[0] : new XElement(Mml + "mrow", content));
            row.Add(new XElement(Mml + "mo", close));
            return row;
        }

        private static bool IsInvisiblyFenced(XElement row)
        {
            if ((string?)row.Attribute("intent") != ":fenced" || row.Attributes().Count() != 1)
                return false;
            var children = row.Elements().ToList();
            return children.Count >= 2 && IsEmptyFence(children[0]) && IsEmptyFence(children[children.Count - 1]);
        }

        private static bool IsEmptyFence(XElement element) => element.Name == Mml + "mo" && !element.HasElements && element.Value.Length == 0;

        /// <summary>
        /// Whether an element of MathML outside the annotations is not presentation MathML: Content MathML, or an
        /// unknown element, which ctop.xsl copies as it is.
        /// </summary>
        private static bool HasContentMarkup(XElement math) =>
            math.Descendants().Any(e => e.Name.Namespace == Mml && !PresentationElements.Contains(e.Name.LocalName) &&
                !e.Ancestors().Any(a => a.Name == Mml + "annotation-xml" || a.Name == Mml + "annotation"));

        /// <summary>Content MathML rewritten as presentation MathML by ctop.xsl, with the presentation MathML around it kept.</summary>
        private static XElement ContentToPresentation(XElement math)
        {
            var source = new XElement(math);
            // the white space between elements, which ctop.xsl would copy into rows (from annotation-xml, say), and
            // around the text of the identifiers and numbers (and of the parts of a number, 2<sep/>3), which it would
            // copy into mi and mn
            source.DescendantNodes().OfType<XText>().Where(t => t.Parent!.HasElements && string.IsNullOrWhiteSpace(t.Value)).Remove();
            foreach (var text in source.Descendants().Where(e => e.Name == Mml + "ci" || e.Name == Mml + "cn").Nodes().OfType<XText>())
                text.Value = text.Value.Trim();
            XElement? presentation;
            try
            {
                presentation = EquationStylesheets.Transform("ctop.xsl", source);
            }
            catch (Exception e) when (e is ArgumentException or XsltException)
            {
                throw new FormatException(e.Message, e);
            }
            if (presentation == null)
                throw new FormatException("The Content MathML equation is empty.");
            // a limit ctop.xsl has nothing for (an integral without bounds, a sum over a condition) is an empty row:
            // no script at all
            foreach (var scripts in presentation.Descendants().Where(e => e.Name == Mml + "msubsup" || e.Name == Mml + "munderover").Reverse().ToList())
            {
                var children = scripts.Elements().ToList();
                if (children.Count != 3)
                    continue;
                var under = scripts.Name == Mml + "munderover";
                switch (IsEmptyRow(children[1]), IsEmptyRow(children[2]))
                {
                    case (true, true):
                        scripts.ReplaceWith(children[0]);
                        break;
                    case (true, false):
                        scripts.ReplaceWith(new XElement(Mml + (under ? "mover" : "msup"), scripts.Attributes(), children[0], children[2]));
                        break;
                    case (false, true):
                        scripts.ReplaceWith(new XElement(Mml + (under ? "munder" : "msub"), scripts.Attributes(), children[0], children[1]));
                        break;
                }
            }
            return presentation;
        }

        private static bool IsEmptyRow(XElement element) => element.Name == Mml + "mrow" && !element.HasElements && string.IsNullOrWhiteSpace(element.Value);

        private static void Validate(XElement math)
        {
            foreach (var element in math.DescendantsAndSelf())
            {
                if (element.Ancestors(Mml + "annotation-xml").Any())
                    continue;
                if (element.Name.Namespace != Mml || !PresentationElements.Contains(element.Name.LocalName))
                    throw new FormatException("Not an element of presentation MathML: " + element.Name + ".");
            }
        }

        private static void RemoveFormatting(XElement math)
        {
            foreach (var element in math.DescendantsAndSelf())
                element.Attributes().Where(a => a.Name.Namespace == XNamespace.None && FormattingAttributes.Contains(a.Name.LocalName)).Remove();
            // an mstyle left without attributes is an mrow, or its only child
            foreach (var style in math.Descendants(Mml + "mstyle").Where(s => !s.HasAttributes).Reverse().ToList())
            {
                var children = style.Elements().ToList();
                style.ReplaceWith(children.Count == 1 ? children[0] : new XElement(Mml + "mrow", style.Nodes()));
            }
        }
    }
}
