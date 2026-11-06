namespace SharpEquations.Internal.Mtef
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Xml.Linq;

    /// <summary>
    /// Prepares the MTEF XML for mtef2mml.xsl, as the Mover and CharReplacer classes of
    /// https://github.com/jure/mathtype_to_mathml (MIT) do:
    /// <list type="bullet">
    /// <item>MTEF scripts follow (or precede) the item they apply to, while MathML scripts contain it:
    /// the scripted item (or the fenced expression ending, or starting, with it) is moved into a new first slot of the script;</item>
    /// <item>an embellished char becomes an embell holding the char;</item>
    /// <item>chars in Unicode ranges XSLT 1.0 cannot test are replaced by MathML tokens.</item>
    /// </list>
    /// MTEF 3 equations also get the equation_options and char variations that only MTEF 5 records carry.
    /// </summary>
    internal static class MtefPreprocessor
    {
        private static readonly string[] Scripts = { "tmSUP", "tmSUB", "tmSUBSUP" };
        private static readonly (string Open, string Close)[] Parentheses = { ("0x0028", "0x0029"), ("0x005B", "0x005D"), ("0x007B", "0x007D") };

        public static void Prepare(XElement root)
        {
            var mtef = root.Element("mtef");
            if (mtef != null && mtef.Element("equation_options") == null)
            {
                mtef.Element("product_subversion")?.AddAfterSelf(new XElement("equation_options", "inline"));
                foreach (var c in mtef.Descendants("char").Where(c => c.Element("variation") == null))
                {
                    var typeface = (string)c.Element("typeface");
                    c.Add(new XElement("variation", typeface == "1" || typeface == "9" || typeface == "10" ? "textmode" : "mathmode"));
                }
            }
            MoveFollowingScripts(root);
            MovePrecedingScripts(root);
            InvertEmbellishments(root);
            GroupFunctionNames(root);
            GroupNumbers(root);
            ReplaceCharacters(root);
            MergeTexts(root);
        }

        /// <summary>
        /// Turns a run of letters in the function typeface (sin, log, max) into a single mi, as the
        /// MathType translators do; MathML shows a multi-letter mi upright.
        /// </summary>
        private static void GroupFunctionNames(XElement root)
        {
            foreach (var first in root.Descendants("char").ToList())
            {
                if (first.Parent == null || !IsFunctionLetter(first) || IsFunctionLetter(first.ElementsBeforeSelf().LastOrDefault()))
                    continue;
                var run = new List<XElement> { first };
                for (var next = first.ElementsAfterSelf().FirstOrDefault(); IsFunctionLetter(next); next = next.ElementsAfterSelf().FirstOrDefault())
                    run.Add(next);
                if (run.Count < 2)
                    continue;
                var name = string.Concat(run.Select(c => (char)MathCode(c)));
                first.AddBeforeSelf(new XElement("mi", name));
                foreach (var c in run)
                    c.Remove();
            }
        }

        private static bool IsFunctionLetter(XElement element)
        {
            var value = MathCode(element);
            return (string)element?.Element("typeface") == "2" && ((value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z'));
        }

        /// <summary>
        /// Turns a run of digits, with decimal points between them, into a single mn.
        /// </summary>
        private static void GroupNumbers(XElement root)
        {
            foreach (var first in root.Descendants("char").ToList())
            {
                if (first.Parent == null || !IsDigit(first) || IsDigit(first.ElementsBeforeSelf().LastOrDefault()))
                    continue;
                var run = new List<XElement> { first };
                var next = first.ElementsAfterSelf().FirstOrDefault();
                while (next != null)
                {
                    var after = next.ElementsAfterSelf().FirstOrDefault();
                    if (IsDigit(next))
                        run.Add(next);
                    else if (MathCode(next) == '.' && IsDigit(after))
                        run.Add(next);
                    else
                        break;
                    next = after;
                }
                if (run.Count < 2)
                    continue;
                first.AddBeforeSelf(new XElement("mn", string.Concat(run.Select(c => (char)MathCode(c)))));
                foreach (var c in run)
                    c.Remove();
            }
        }

        private static bool IsDigit(XElement element)
        {
            var value = MathCode(element);
            return value >= '0' && value <= '9';
        }

        /// <summary>
        /// The character code of a math-mode char element, or -1.
        /// </summary>
        private static int MathCode(XElement element)
        {
            if (element == null || element.Name != "char" || (string)element.Element("variation") == "textmode")
                return -1;
            var code = (string)element.Element("mt_code_value");
            if (code == null || code.Length != 6 || !int.TryParse(code.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
                return -1;
            return value;
        }

        /// <summary>
        /// Joins the adjacent texts left by text-mode chars, so that a word becomes one mtext.
        /// </summary>
        private static void MergeTexts(XElement root)
        {
            foreach (var text in root.DescendantNodes().OfType<XText>().ToList())
            {
                if (text.Parent != null && text.PreviousNode is XText previous)
                {
                    previous.Value += text.Value;
                    text.Remove();
                }
            }
        }

        private static bool IsScript(XElement template, bool precedes)
        {
            return Scripts.Contains((string)template.Element("selector"))
                && template.Elements("variation").Any(v => v.Value == "tvSU_PRECEDES") == precedes;
        }

        private static bool HasCode(XElement element, IEnumerable<string> codes)
        {
            var code = (string)element?.Element("mt_code_value");
            return code != null && codes.Contains(code);
        }

        private static void MoveFollowingScripts(XElement root)
        {
            var previous = new List<XElement>();
            foreach (var template in root.Descendants("tmpl").Where(t => IsScript(t, false)).ToList())
            {
                var all = template.ElementsBeforeSelf().Where(e => e.Name == "tmpl" || e.Name == "char").ToList();
                var siblings = all.Where(s => !previous.Contains(s)).ToList();
                previous = all;

                var slot = new XElement("slot");
                if (siblings.Count > 0)
                {
                    var last = siblings[siblings.Count - 1];
                    if (HasCode(last, Parentheses.Select(p => p.Close)))
                    {
                        var start = siblings.Count - 1;
                        while (start > 0 && !HasCode(siblings[start - 1].ElementsAfterSelf().FirstOrDefault(), Parentheses.Select(p => p.Open)))
                            start--;
                        MoveFenced(siblings.Skip(start).ToList(), slot);
                    }
                    else
                    {
                        Move(last, slot);
                    }
                }
                template.Descendants("slot").FirstOrDefault()?.AddBeforeSelf(slot);
            }
        }

        private static void MovePrecedingScripts(XElement root)
        {
            foreach (var template in root.Descendants("tmpl").Where(t => IsScript(t, true)).ToList())
            {
                var siblings = template.ElementsAfterSelf().Where(e => e.Name == "tmpl" || e.Name == "char").ToList();
                var slot = new XElement("slot");
                if (siblings.Count > 0)
                {
                    if (HasCode(siblings[0], Parentheses.Select(p => p.Open)))
                        MoveFenced(siblings, slot);
                    else
                        Move(siblings[0], slot);
                }
                template.Descendants("slot").FirstOrDefault()?.AddBeforeSelf(slot);
            }
        }

        /// <summary>
        /// Moves the elements from the opening parenthesis they start with up to its closing parenthesis.
        /// </summary>
        private static void MoveFenced(List<XElement> elements, XElement slot)
        {
            if (elements.Count == 0)
                return;
            foreach (var (open, close) in Parentheses)
            {
                if ((string)elements[0].Element("mt_code_value") != open)
                    continue;
                foreach (var element in elements)
                {
                    Move(element, slot);
                    if ((string)element.Element("mt_code_value") == close)
                        break;
                }
            }
        }

        private static void Move(XElement element, XElement slot)
        {
            element.Remove();
            slot.Add(element);
        }

        private static void InvertEmbellishments(XElement root)
        {
            foreach (var c in root.Descendants("char").Where(c => c.Element("embell") != null).ToList())
            {
                var embell = c.Element("embell");
                embell.Remove();
                embell.Add(new XElement(c));
                c.ReplaceWith(embell);
            }
        }

        private enum Token
        {
            Default,
            Text,
            Unsupported,
            Mi,
            Mn,
            Mo,
            MoNotStretchy,
            MText,
        }

        private static (int From, int To, Token Math, Token Text) R(int from, int to, Token math, Token text = Token.Default) => (from, to, math, text);

        private static (int From, int To, Token Math, Token Text) C(int code, Token math) => (code, code, math, Token.Default);

        private const Token U = Token.Unsupported;

        // The REPLACEMENTS table of CharReplacer, in its order: the first range holding the code applies.
        private static readonly (int From, int To, Token Math, Token Text)[] Replacements =
        {
            C(0x0021, Token.Mo), C(0x0028, Token.MoNotStretchy), C(0x0029, Token.MoNotStretchy), C(0x002A, Token.Mo),
            C(0x002B, Token.Mo), C(0x002C, Token.Mo), C(0x002D, Token.Mo), C(0x002E, Token.Mo), C(0x002F, Token.Mo),
            C(0x003D, Token.Mo), C(0x003F, Token.Mo), C(0x005B, Token.MoNotStretchy), C(0x005D, Token.MoNotStretchy), C(0x007E, Token.Mo),
            R(0x0000, 0x0008, U, U), R(0x000B, 0x001F, U, U),
            R(0x0030, 0x0039, Token.Mn, Token.Text), R(0x003A, 0x003B, Token.Mo, Token.Text),
            R(0x0041, 0x005A, Token.Mi, Token.Text), R(0x0061, 0x007A, Token.Mi, Token.Text),
            R(0x0080, 0x009F, U, U), R(0x00A0, 0x00B0, Token.Mo), R(0x00B2, 0x00BB, Token.Mo), R(0x00BC, 0x00BE, Token.Mn),
            R(0x02C6, 0x02FF, Token.Mo), R(0x0300, 0x036F, Token.Mo), R(0x2000, 0x200B, Token.MText), R(0x200C, 0x200F, U, U),
            R(0x2010, 0x2027, Token.Mo), R(0x2028, 0x202F, U, U), R(0x2030, 0x2069, Token.Mo), R(0x206A, 0x206F, U, U),
            R(0x2070, 0x209F, Token.Mo), R(0x20A0, 0x20CF, Token.Mi), R(0x20D0, 0x20FF, Token.Mo), R(0x2100, 0x2101, Token.Mo),
            R(0x2103, 0x210A, Token.Mo), R(0x2116, 0x2117, Token.Mo), R(0x213C, 0x2146, Token.Mo), R(0x2150, 0x218F, Token.Mn),
            R(0x2190, 0x21FF, Token.Mo), R(0x2200, 0x2211, Token.Mo), R(0x2213, 0x221D, Token.Mo), R(0x221F, 0x22FF, Token.Mo),
            R(0x2300, 0x23FF, Token.Mo), R(0x2400, 0x243F, Token.Mo), R(0x2500, 0x257F, Token.Mo), R(0x2580, 0x259F, Token.Mo),
            R(0x25A0, 0x25FF, Token.Mo), R(0x2600, 0x267F, Token.Mo), R(0x2700, 0x27BF, Token.Mo), R(0x27F0, 0x27FF, Token.Mo),
            R(0x2900, 0x297F, Token.Mo), R(0x2980, 0x29AF, Token.Mo), R(0x29B1, 0x29DB, Token.Mo), R(0x29DD, 0x29FF, Token.Mo),
            R(0x2A00, 0x2AFF, Token.Mo), R(0x3000, 0x303F, Token.Mo),
            R(0xE000, 0xE900, U, U), R(0xE905, 0xE90A, U, U), R(0xE90D, 0xE921, U, U), R(0xE926, 0xE92C, U, U),
            R(0xE92E, 0xE931, U, U), R(0xE934, 0xE939, U, U), R(0xE93C, 0xE98E, U, U), R(0xE990, 0xEA05, U, U),
            R(0xEA08, 0xEA0A, U, U), R(0xEA0D, 0xEA31, U, U), R(0xEA36, 0xEA39, U, U), R(0xEA3C, 0xEA3F, U, U),
            R(0xEA46, 0xEB00, U, U), R(0xEB03, 0xEB04, U, U), R(0xEB07, 0xED09, U, U), R(0xED14, 0xED15, U, U),
            R(0xED17, 0xEE03, U, U), R(0xEE04, 0xEE0C, Token.Default, U), R(0xEE0D, 0xEE18, U, U), R(0xEE1A, 0xEEFF, U, U),
            R(0xEF09, 0xEFFF, U, U), R(0xF000, 0xF033, Token.Default, U), R(0xF034, 0xF07F, U, U), R(0xF080, 0xF0B3, Token.Default, U),
            R(0xF0B4, 0xF0BF, U, U), R(0xF0C0, 0xF0C9, Token.Default, U), R(0xF0CA, 0xF0FF, U, U), R(0xF100, 0xF133, Token.Default, U),
            R(0xF134, 0xF8FF, U, U), R(0xFB00, 0xFB4F, Token.MText), R(0xFE35, 0xFE4F, Token.Mo),
        };

        /// <summary>
        /// Replaces chars with the token CharReplacer gives them. Unsupported characters (control and
        /// private use characters MathType uses for spacing and font-specific glyphs) are dropped.
        /// </summary>
        private static void ReplaceCharacters(XElement root)
        {
            foreach (var c in root.Descendants("char").ToList())
            {
                var hex = ((string)c.Element("mt_code_value") ?? string.Empty).Replace("0x", string.Empty);
                var code = int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) ? value : 0;
                var textMode = (string)c.Element("variation") == "textmode";
                if (textMode && code == 0x0020)
                {
                    c.ReplaceWith(new XText(" "));
                    continue;
                }
                var index = System.Array.FindIndex(Replacements, r => code >= r.From && code <= r.To);
                if (index < 0)
                    continue;
                var token = textMode ? Replacements[index].Text : Replacements[index].Math;
                if (token == Token.Default)
                    token = textMode ? Token.Text : Token.Mi;
                if (token == Token.Unsupported || (code >= 0xD800 && code <= 0xDFFF))
                {
                    c.Remove();
                    continue;
                }
                var text = char.ConvertFromUtf32(code);
                switch (token)
                {
                    case Token.Text:
                        c.ReplaceWith(new XText(text));
                        break;
                    case Token.MoNotStretchy:
                        c.ReplaceWith(new XElement("mo", new XAttribute("stretchy", "false"), text));
                        break;
                    default:
                        c.ReplaceWith(new XElement(token.ToString().ToLowerInvariant(), text));
                        break;
                }
            }
        }
    }
}
