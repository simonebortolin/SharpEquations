#nullable enable

namespace SharpEquations.Internal.Xslt
{
    using System.Xml.Linq;

    // The stylesheets of Resources, whose Transform SharpEquations.XsltGenerator implements from Resources/<name>.xsl.
    // They are declared here so that the code that uses them compiles without the generator (as Parlot compiles it).

    /// <summary>omml2mml.xsl: OMML to MathML.</summary>
    internal sealed partial class Omml2mmlStylesheet
    {
        public static partial XElement? Transform(XElement input);
    }

    /// <summary>mml2omml.xsl: MathML to OMML.</summary>
    internal sealed partial class Mml2ommlStylesheet
    {
        public static partial XElement? Transform(XElement input);
    }

    /// <summary>ctop.xsl: content MathML to presentation MathML.</summary>
    internal sealed partial class CtopStylesheet
    {
        public static partial XElement? Transform(XElement input);
    }

    /// <summary>mtef2mml.xsl: the XML of MTEF records to MathML.</summary>
    internal sealed partial class Mtef2mmlStylesheet
    {
        public static partial XElement? Transform(XElement input);
    }
}
