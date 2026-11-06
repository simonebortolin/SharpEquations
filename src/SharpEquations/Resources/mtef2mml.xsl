<?xml version="1.0" encoding="UTF-8"?>
<!--
  mtef2mml.xsl: MathType Equation Format (MTEF), as XML, to MathML.
  The stylesheets of https://github.com/jure/mathtype_to_mathml (lib/transform.xsl and lib/xsl/*.xsl,
  commit db2030b16be7e429340b1b53eb615471441ed3d4), merged into one file for SharpEquations. The XML input is the
  one written by https://github.com/jure/mathtype and SharpEquations (MtefReader).
  Also included from the pull requests of that repository: square and nth roots (xsl/root.xsl, #11)
  and text in mtext (#4).
  Changed for SharpEquations: the sum, product, coproduct, union and intersection templates, and the upper-limit
  templates of tmSUMOP and tmINTOP, matched cases meant for other templates; their conditions now
  exclude each other instead of depending on the processor picking the last matching template.

  Copyright (c) 2015 Jure Triglav. MIT License, see NOTICE.
-->
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:xs="http://www.w3.org/2001/XMLSchema"
    exclude-result-prefixes="xs"
    version="1.0">
    <xsl:template match="/">
        <xsl:apply-templates select=".//mtef" />
    </xsl:template>

    <xsl:template match="mtef[equation_options = 'inline']">
        <math>
            <xsl:apply-templates select="pile | slot"/>
        </math>
    </xsl:template>

    <xsl:template match="mtef[equation_options = 'block']">
        <math display="block">
            <xsl:apply-templates select="pile | slot"/>
        </math>
    </xsl:template>

    <xsl:template match="slot">
        <mrow>
            <xsl:apply-templates/>
        </mrow>
    </xsl:template>

    <!-- Non-empty text nodes -->
    <xsl:template match="text()[normalize-space()]">
      <mtext>
        <xsl:value-of select="."/>
      </mtext>
    </xsl:template>

    <xsl:template match="*" />

    <!-- xsl/int.xsl -->

    <!-- Integral operators -->
    <!-- The order is important because it would be very messy to address all
         conditions for each template, and so we rely on the fact that the
         last matching rule has precedence -->

    <xsl:template match="tmpl[selector='tmINTOP']">
        <msubsup>
            <mstyle mathsize="140%" displaystyle="true"><xsl:apply-templates select="slot[4] | pile[4]"/></mstyle>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
            <xsl:apply-templates select="slot[3] | pile[3]"/>
        </msubsup>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTOP' and variation='tvBO_UPPER' and not(variation='tvBO_LOWER')]">
        <msup>
            <mstyle mathsize="140%" displaystyle="true"><xsl:apply-templates select="slot[4] | pile[4] | pile[4]"/></mstyle>
            <xsl:apply-templates select="slot[3] | pile[3]"/>
        </msup>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTOP' and variation='tvBO_LOWER' and not(variation='tvBO_UPPER')]">
        <msub>
            <mstyle mathsize="140%" displaystyle="true"><xsl:apply-templates select="slot[4] | pile[4]"/></mstyle>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </msub>
    </xsl:template>

    <!-- Integrals -->

    <xsl:template match="tmpl[selector='tmINTEG']">
        <mstyle displaystyle="true">
            <mrow>
                <msubsup>
                    <mo>&#x222B;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                    <xsl:apply-templates select="slot[3] | pile[3]"/>
                </msubsup>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and (not(variation='tvBO_UPPER') and not(variation='tvBO_LOWER'))]">
        <mstyle displaystyle="true">
            <mrow><mo>&#x222B;</mo>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvBO_SUM']">
        <mstyle displaystyle="true">
            <mrow>
                <munderover>
                    <mo>&#x222B;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                    <xsl:apply-templates select="slot[3] | pile[3]"/>
                </munderover>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_EXPAND']">
        <mstyle displaystyle="true">
            <mrow>
                <msubsup>
                    <mo stretchy="true">&#x222B;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                    <xsl:apply-templates select="slot[3] | pile[3]"/>
                </msubsup>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_1' and variation='tvBO_LOWER' and not(variation='tvBO_UPPER')]">
        <mstyle displaystyle="true">
            <mrow>
                <msub>
                    <mo>&#x222B;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </msub>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_2']">
        <mstyle displaystyle="true">
            <mrow>
                <msub>
                    <mo>&#x222C;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </msub>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_2' and (not(variation='tvBO_UPPER') and not(variation='tvBO_LOWER'))]">
        <mstyle displaystyle="true">
            <mrow>
                <mo>&#x222C;</mo>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_3']">
        <mstyle displaystyle="true">
            <mrow>
                <msub>
                    <mo>&#x222D;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </msub>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_LOOP']">
        <mstyle displaystyle="true">
            <mrow>
                <msub>
                    <mo>&#x222E;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </msub>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_CW_LOOP']">
        <mstyle displaystyle="true">
            <mrow>
                <msub>
                    <mo>&#x2232;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </msub>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_CCW_LOOP']">
        <mstyle displaystyle="true">
            <mrow>
                <msub>
                    <mo>&#x2233;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </msub>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_LOOP' and (not(variation='tvBO_UPPER') and not(variation='tvBO_LOWER'))]">
        <mstyle displaystyle="true">
            <mrow>
                <mo>&#x222E;</mo>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvBO_SUM' and variation='tvBO_LOWER' and not(variation='tvBO_UPPER')]">
        <mstyle displaystyle="true">
            <mrow>
                <munder>
                    <mo>&#x222B;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </munder>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_EXPAND' and variation='tvBO_SUM']">
        <mstyle displaystyle="true">
            <mrow>
                <munderover>
                    <mo stretchy="true">&#x222B;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                    <xsl:apply-templates select="slot[3] | pile[3]"/>
                </munderover>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_2' and variation='tvBO_SUM']">
        <mstyle displaystyle="true">
            <mrow>
                <munder>
                    <mo>&#x222C;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </munder>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_2' and variation='tvINT_LOOP']">
        <mstyle displaystyle="true">
            <mrow>
                <msub>
                    <mo>&#x222F;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </msub>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_2' and variation='tvINT_EXPAND']">
        <mstyle displaystyle="true">
            <mrow>
                <msub>
                    <mo stretchy="true">&#x222C;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </msub>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>

        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_2' and variation='tvINT_EXPAND' and variation='tvBO_SUM']">
        <mstyle displaystyle="true">
            <mrow>
                <munder>
                    <mo stretchy="true">&#x222C;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </munder>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>


    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_LOOP' and variation='tvBO_SUM']">
        <mstyle displaystyle="true">
            <mrow>
                <munder>
                    <mo>&#x222E;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </munder>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_CW_LOOP' and variation='tvBO_SUM']">
        <mstyle displaystyle="true">
            <mrow>
                <munder>
                    <mo>&#x2232;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </munder>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_CCW_LOOP' and variation='tvBO_SUM']">
        <mstyle displaystyle="true">
            <mrow>
                <munder>
                    <mo>&#x2233;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </munder>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_3' and variation='tvBO_SUM']">
        <mstyle displaystyle="true">
            <mrow>
                <munder>
                    <mo>&#x222D;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </munder>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_3' and variation='tvINT_LOOP']">
        <mstyle displaystyle="true">
            <mrow>
                <msub>
                    <mo>&#x2230;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </msub>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_3' and variation='tvINT_EXPAND']">
        <mstyle displaystyle="true">
            <mrow>
                <msub>
                    <mo stretchy="true">&#x222D;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </msub>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_EXPAND' and variation='tvINT_CW_LOOP']">
        <mstyle displaystyle="true">
            <mrow>
                <msub>
                    <mo stretchy="true">&#x2232;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </msub>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_EXPAND' and variation='tvINT_CCW_LOOP']">
        <mstyle displaystyle="true">
            <mrow>
                <msub>
                    <mo stretchy="true">&#x2233;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </msub>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>


    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_EXPAND' and variation='tvINT_LOOP']">
        <mstyle displaystyle="true">
            <mrow>
                <msub>
                    <mo stretchy="true">&#x222E;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </msub>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_2' and variation='tvINT_LOOP' and variation='tvBO_SUM']">
        <mstyle displaystyle="true">
            <mrow>
                <munder>
                    <mo>&#x222F;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </munder>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_2' and variation='tvINT_EXPAND' and variation='tvINT_LOOP']">
        <mstyle displaystyle="true">
            <mrow>
                <msub>
                    <mo stretchy="true">&#x222F;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </msub>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_2' and variation='tvINT_LOOP' and (not(variation='tvBO_UPPER') and not(variation='tvBO_LOWER'))]">
        <mstyle displaystyle="true">
            <mrow>
                <mo>&#x222F;</mo>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_3' and (not(variation='tvBO_UPPER') and not(variation='tvBO_LOWER'))]">
        <mstyle displaystyle="true">
            <mrow>
                <mo>&#x222D;</mo>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_3' and variation='tvINT_EXPAND' and variation='tvBO_SUM']">
        <mstyle displaystyle="true">
            <mrow>
                <munder>
                    <mo stretchy="true">&#x222D;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </munder>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_3' and variation='tvINT_LOOP' and variation='tvBO_SUM']">
        <mstyle displaystyle="true">
            <mrow>
                <munder>
                    <mo>&#x2230;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </munder>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_3' and variation='tvINT_EXPAND' and variation='tvINT_LOOP']">
        <mstyle displaystyle="true">
            <mrow>
                <msub>
                    <mo stretchy="true">&#x2230;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </msub>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>


    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_CW_LOOP' and (not(variation='tvBO_UPPER') and not(variation='tvBO_LOWER'))]">
        <mstyle displaystyle="true">
            <mrow>
                <mo>&#x2232;</mo>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_CCW_LOOP' and (not(variation='tvBO_UPPER') and not(variation='tvBO_LOWER'))]">
        <mstyle displaystyle="true">
            <mrow>
                <mo>&#x2233;</mo>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_EXPAND' and variation='tvBO_LOWER' and not(variation='tvBO_UPPER')]">
        <mstyle displaystyle="true">
            <mrow>
                <msub>
                    <mo stretchy="true">&#x222B;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </msub>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_EXPAND' and variation='tvINT_LOOP' and variation='tvBO_SUM']">
        <mstyle displaystyle="true">
            <mrow>
                <munder>
                    <mo stretchy="true">&#x222E;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </munder>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_EXPAND' and variation='tvINT_CCW_LOOP' and variation='tvBO_SUM']">
        <mstyle displaystyle="true">
            <mrow>
                <munder>
                    <mo stretchy="true">&#x2233;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </munder>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_EXPAND' and variation='tvINT_CW_LOOP' and variation='tvBO_SUM']">
        <mstyle displaystyle="true">
            <mrow>
                <munder>
                    <mo stretchy="true">&#x2232;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </munder>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_EXPAND' and (not(variation='tvBO_UPPER') and not(variation='tvBO_LOWER'))]">
        <mstyle displaystyle="true">
            <mrow>
                <mo stretchy="true">&#x222B;</mo>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_2' and variation='tvINT_EXPAND' and (not(variation='tvBO_UPPER') and not(variation='tvBO_LOWER'))]">
        <mstyle displaystyle="true">
            <mrow>
                <mo stretchy="true">&#x222C;</mo>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_3' and variation='tvINT_EXPAND' and (not(variation='tvBO_UPPER') and not(variation='tvBO_LOWER'))]">
        <mstyle displaystyle="true">
            <mrow>
                <mo stretchy="true">&#x222D;</mo>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_EXPAND' and variation='tvBO_SUM' and variation='tvBO_LOWER' and not(variation='tvBO_UPPER')]">
        <mstyle displaystyle="true">
            <mrow>
                <munder>
                    <mo stretchy="true">&#x222B;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </munder>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_3' and variation='tvINT_LOOP' and (not(variation='tvBO_UPPER') and not(variation='tvBO_LOWER'))]">
        <mstyle displaystyle="true">
            <mrow>
                <mo>&#x2230;</mo>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_3' and variation='tvINT_EXPAND' and variation='tvINT_LOOP' and variation='tvBO_SUM']">
        <mstyle displaystyle="true">
            <mrow>
                <munder>
                    <mo stretchy="true">&#x2230;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </munder>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_EXPAND' and variation='tvINT_CCW_LOOP' and (not(variation='tvBO_UPPER') and not(variation='tvBO_LOWER'))]">
        <mstyle displaystyle="true">
            <mrow>
                <mo stretchy="true">&#x2233;</mo>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>


    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_EXPAND' and variation='tvINT_LOOP' and (not(variation='tvBO_UPPER') and not(variation='tvBO_LOWER'))]">
        <mstyle displaystyle="true">
            <mrow>
                <mo stretchy="true">&#x222E;</mo>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>


    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_2' and variation='tvINT_EXPAND' and variation='tvINT_LOOP' and variation='tvBO_SUM']">
        <mstyle displaystyle="true">
            <mrow>
                <munder>
                    <mo stretchy="true">&#x222F;</mo>
                    <xsl:apply-templates select="slot[2] | pile[2]"/>
                </munder>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_EXPAND' and variation='tvINT_CW_LOOP' and (not(variation='tvBO_UPPER') and not(variation='tvBO_LOWER'))]">
        <mstyle displaystyle="true">
            <mrow>
                <mo stretchy="true">&#x2232;</mo>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>


    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_2' and variation='tvINT_EXPAND' and variation='tvINT_LOOP' and (not(variation='tvBO_UPPER') and not(variation='tvBO_LOWER'))]">
        <mstyle displaystyle="true">
            <mrow>
                <mo stretchy="true">&#x222F;</mo>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTEG' and variation='tvINT_3' and variation='tvINT_EXPAND' and variation='tvINT_LOOP' and (not(variation='tvBO_UPPER') and not(variation='tvBO_LOWER'))]">
        <mstyle displaystyle="true">
            <mrow>
                <mo stretchy="true">&#x2230;</mo>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            </mrow>
        </mstyle>
    </xsl:template>

    <!-- xsl/lim.xsl -->

     <!-- Limits -->

    <xsl:template match="tmpl[selector='tmLIM']">
        <munder>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </munder>
    </xsl:template>


    <xsl:template match="tmpl[selector='tmLIM' and variation='tvBO_UPPER']">
        <mover>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
            <xsl:apply-templates select="slot[3] | pile[3]"/>
        </mover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmLIM' and (variation='tvBO_UPPER' and variation='tvBO_LOWER')]">
        <munderover>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
            <xsl:apply-templates select="slot[3] | pile[3]"/>
        </munderover>
    </xsl:template>


    <!-- xsl/frac.xsl -->

     <!-- Fractions -->

     <xsl:template match="tmpl[selector='tmFRACT']">
        <mfrac>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </mfrac>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmFRACT' and variation='tvFR_SLASH']">
        <mfrac bevelled="true">
            <xsl:apply-templates select="slot[1] | pile[1]"/>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </mfrac>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmFRACT' and variation='tvFR_SMALL']">
        <mstyle scriptlevel="+1">
            <mfrac>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
                <xsl:apply-templates select="slot[2] | pile[2]"/>
            </mfrac>
        </mstyle>
    </xsl:template>


    <xsl:template match="tmpl[selector='tmFRACT' and variation='tvFR_SLASH' and variation='tvFR_BASE']">
        <mrow><xsl:apply-templates select="slot[1] | pile[1]"/><mo>/</mo><xsl:apply-templates select="slot[2] | pile[2]"/></mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmFRACT' and variation='tvFR_SLASH' and variation='tvFR_SMALL']">
        <mstyle scriptlevel="+1">
            <mfrac bevelled="true">
                <xsl:apply-templates select="slot[1] | pile[1]"/>
                <xsl:apply-templates select="slot[2] | pile[2]"/>
            </mfrac>
        </mstyle>
    </xsl:template>


    <!-- xsl/pile.xsl -->

    <!-- The translation of a pile using this form of translation string is performed with the following steps:

    The <start> part of the translation string is output;
    The first line is translated;
    The <repeat> part of the translation string is output;
    The next line is translated;
    If there are any more lines, go back to Step 3. Otherwise, continue with the next step;
    The <end> part of the translation string is output.
    -->
    <xsl:template match="pile">
        <mtable>
            <xsl:apply-templates select="slot" mode="wrap"/>
        </mtable>
    </xsl:template>

    <xsl:template match="pile[halign='left']">
        <mtable columnalign="left">
            <xsl:apply-templates select="slot" mode="wrap"/>
        </mtable>
    </xsl:template>

    <xsl:template match="pile/slot" mode="wrap">
        <mtr>
            <mtd>
                <xsl:apply-templates select="."/>
            </mtd>
        </mtr>
    </xsl:template>

    <xsl:template match="pile[halign='right']">
        <mtable columnalign="right">
            <xsl:apply-templates select="slot" mode="wrap"/>
        </mtable>
    </xsl:template>

    <xsl:template match="pile[halign='dec']">
        <mtable groupalign="decimalpoint">
            <xsl:apply-templates select="slot" mode="wrap"/>
        </mtable>
    </xsl:template>

    <xsl:template match="pile[halign='al']">
        <mtable>
            <xsl:apply-templates select="slot" mode="wrap"/>
        </mtable>
    </xsl:template>


    <!-- xsl/char.xsl -->

    <xsl:template match="mi | mo | mn | mtext">
        <xsl:copy-of select="."/>
    </xsl:template>

    <xsl:template name="charhex">
        <xsl:param name="mt_code_value"/>
        <xsl:text disable-output-escaping="yes">&amp;#</xsl:text>
        <xsl:value-of select="substring($mt_code_value, 2)"/>
        <xsl:text>;</xsl:text>
    </xsl:template>

    <!-- Default char translation for mathmode -->
    <xsl:template match="char[not(variation) or variation != 'textmode']">
        <mi>
            <xsl:call-template name="charhex">
                <xsl:with-param name="mt_code_value" select="mt_code_value/text()" />
            </xsl:call-template>
        </mi>
    </xsl:template>

    <xsl:template match="char[variation = 'textmode']">
        <xsl:call-template name="charhex">
            <xsl:with-param name="mt_code_value" select="mt_code_value/text()" />
        </xsl:call-template>
    </xsl:template>

    <xsl:template match="char[typeface = '2']">
        <mn>
            <xsl:call-template name="charhex">
                <xsl:with-param name="mt_code_value" select="mt_code_value/text()" />
            </xsl:call-template>
        </mn>
    </xsl:template>

    <xsl:template match="char[typeface = '8']">
        <mn>
            <xsl:call-template name="charhex">
                <xsl:with-param name="mt_code_value" select="mt_code_value/text()" />
            </xsl:call-template>
        </mn>
    </xsl:template>

    <xsl:template match="char[typeface = '3']">
        <mi>
            <xsl:call-template name="charhex">
                <xsl:with-param name="mt_code_value" select="mt_code_value/text()" />
            </xsl:call-template>
        </mi>
    </xsl:template>

    <xsl:template match="char[typeface = '6']">
        <mo>
            <xsl:call-template name="charhex">
                <xsl:with-param name="mt_code_value" select="mt_code_value/text()" />
            </xsl:call-template>
        </mo>
    </xsl:template>

<!--     <xsl:template match="char[mt_code_value='0x2026']">
        <mo>&#x2026;</mo>
    </xsl:template>

    <xsl:template match="char[mt_code_value='0x22EE' ]">
      <mo>&#x22EE;</mo>
    </xsl:template>

    <xsl:template match="char[mt_code_value='0x22F1' ]">
      <mo>&#x22F1;</mo>
    </xsl:template>

    <xsl:template match="char[mt_code_value='0x22EF' ]">
      <mo>&#x22EF;</mo>
    </xsl:template>

    <xsl:template match="char[mt_code_value='0x03C0' ]">
        <mi>π</mi>
    </xsl:template>

    <xsl:template match="char[mt_code_value='0x03B8' ]">
        <mi>θ</mi>
    </xsl:template> -->

    <!-- All Char rules -->

    <!-- HORIZONTAL TABULATION -->
    <xsl:template match="char[mt_code_value='0x0009' and variation='mathmode']">
        <mtext>
            <xsl:call-template name="charhex">
                <xsl:with-param name="mt_code_value" select="mt_code_value/text()"/>
            </xsl:call-template>
        </mtext>
    </xsl:template>

    <!-- LINE FEED -->
    <xsl:template match="char[mt_code_value='0x000A' and variation='mathmode']">
        <mtext>
            <xsl:call-template name="charhex">
                <xsl:with-param name="mt_code_value" select="mt_code_value/text()"/>
            </xsl:call-template>
        </mtext>
    </xsl:template>

    <!-- SPACE -->
    <xsl:template match="char[mt_code_value='0x0020' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x00A0;</xsl:text>
    </xsl:template>

    <!-- QUOTATION MARK -->
    <xsl:template match="char[mt_code_value='0x0022' and variation='mathmode']">
        <mo>
            <xsl:call-template name="charhex">
                <xsl:with-param name="mt_code_value" select="mt_code_value/text()"/>
            </xsl:call-template>
        </mo>
    </xsl:template>

    <!-- NUMBER SIGN -->
    <xsl:template match="char[mt_code_value='0x0023' and variation='mathmode']">
        <mo>
            <xsl:call-template name="charhex">
                <xsl:with-param name="mt_code_value" select="mt_code_value/text()"/>
            </xsl:call-template>
        </mo>
    </xsl:template>

    <!-- AMPERSAND -->
    <xsl:template match="char[mt_code_value='0x0026' and variation='mathmode']">
        <mo>
            <xsl:call-template name="charhex">
                <xsl:with-param name="mt_code_value" select="mt_code_value/text()"/>
            </xsl:call-template>
        </mo>
    </xsl:template>

    <!-- APOSTROPHE -->
    <xsl:template match="char[mt_code_value='0x0027' and variation='mathmode']">
        <mo>
            <xsl:call-template name="charhex">
                <xsl:with-param name="mt_code_value" select="mt_code_value/text()"/>
            </xsl:call-template>
        </mo>
    </xsl:template>

    <!-- LESS-THAN SIGN -->
    <xsl:template match="char[mt_code_value='0x003C' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x003C;</xsl:text></mo>
    </xsl:template>

    <!-- GREATER-THAN SIGN -->
    <xsl:template match="char[mt_code_value='0x003E' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x003E;</xsl:text></mo>
    </xsl:template>

    <!-- COMMERCIAL AT -->
    <xsl:template match="char[mt_code_value='0x0040' and variation='mathmode']">
        <mo>
            <xsl:call-template name="charhex">
                <xsl:with-param name="mt_code_value" select="mt_code_value/text()"/>
            </xsl:call-template>
        </mo>
    </xsl:template>

    <!-- REVERSE SOLIDUS -->
    <xsl:template match="char[mt_code_value='0x005C' and variation='mathmode']">
        <mo>
            <xsl:call-template name="charhex">
                <xsl:with-param name="mt_code_value" select="mt_code_value/text()"/>
            </xsl:call-template>
        </mo>
    </xsl:template>

    <!-- CIRCUMFLEX ACCENT -->
    <xsl:template match="char[mt_code_value='0x005E' and variation='mathmode']">
        <mo>
            <xsl:call-template name="charhex">
                <xsl:with-param name="mt_code_value" select="mt_code_value/text()"/>
            </xsl:call-template>
        </mo>
    </xsl:template>

    <!-- LOW LINE -->
    <xsl:template match="char[mt_code_value='0x005F' and variation='mathmode']">
        <mo>
            <xsl:call-template name="charhex">
                <xsl:with-param name="mt_code_value" select="mt_code_value/text()"/>
            </xsl:call-template>
        </mo>
    </xsl:template>

    <!-- GRAVE ACCENT -->
    <xsl:template match="char[mt_code_value='0x0060' and variation='mathmode']">
        <mo>
            <xsl:call-template name="charhex">
                <xsl:with-param name="mt_code_value" select="mt_code_value/text()"/>
            </xsl:call-template>
        </mo>
    </xsl:template>

    <!-- LEFT CURLY BRACKET -->
    <xsl:template match="char[mt_code_value='0x007B' and variation='mathmode']">
        <mo>
            <xsl:call-template name="charhex">
                <xsl:with-param name="mt_code_value" select="mt_code_value/text()"/>
            </xsl:call-template>
        </mo>
    </xsl:template>

    <!-- VERTICAL LINE -->
    <xsl:template match="char[mt_code_value='0x007C' and variation='mathmode']">
        <mo>
            <xsl:call-template name="charhex">
                <xsl:with-param name="mt_code_value" select="mt_code_value/text()"/>
            </xsl:call-template>
        </mo>
    </xsl:template>

    <!-- RIGHT CURLY BRACKET -->
    <xsl:template match="char[mt_code_value='0x007D' and variation='mathmode']">
        <mo>
            <xsl:call-template name="charhex">
                <xsl:with-param name="mt_code_value" select="mt_code_value/text()"/>
            </xsl:call-template>
        </mo>
    </xsl:template>

    <!-- UNSUPPORTED: DELETE -->
    <xsl:template match="char[mt_code_value='0x007F']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>

    <!-- range: Latin-1 Supplement -->
    <xsl:template match="char[mt_code_value='0x00B1' and variation='mathmode']">
        <mo>
            <xsl:call-template name="charhex">
                <xsl:with-param name="mt_code_value" select="mt_code_value/text()"/>
            </xsl:call-template>
        </mo>
    </xsl:template>


    <!-- INVERTED QUESTION MARK -->
    <xsl:template match="char[mt_code_value='0x00BF' and variation='mathmode']">
        <mo>
            <xsl:call-template name="charhex">
                <xsl:with-param name="mt_code_value" select="mt_code_value/text()"/>
            </xsl:call-template>
        </mo>
    </xsl:template>

    <!-- MULTIPLICATION SIGN -->
    <xsl:template match="char[mt_code_value='0x00D7' and variation='mathmode']">
        <mo>
            <xsl:call-template name="charhex">
                <xsl:with-param name="mt_code_value" select="mt_code_value/text()"/>
            </xsl:call-template>
        </mo>
    </xsl:template>

    <!-- DIVISION SIGN -->
    <xsl:template match="char[mt_code_value='0x00F7' and variation='mathmode']">
        <mo>
            <xsl:call-template name="charhex">
                <xsl:with-param name="mt_code_value" select="mt_code_value/text()"/>
            </xsl:call-template>
        </mo>
    </xsl:template>

    <!-- DOUBLE-STRUCK CAPITAL C -->
    <xsl:template match="char[mt_code_value='0x2102' and variation='mathmode']">
        <mi>
            <xsl:call-template name="charhex">
                <xsl:with-param name="mt_code_value" select="mt_code_value/text()"/>
            </xsl:call-template>
        </mi>
    </xsl:template>


    <!-- PLUS ABOVE EQUALS -->
    <xsl:template match="char[mt_code_value='0xE901' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2A72;</xsl:text></mo>
    </xsl:template>

    <!-- PLUS ABOVE EQUALS -->
    <xsl:template match="char[mt_code_value='0xE901' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2A72;</xsl:text>
    </xsl:template>

    <!-- PLUS BELOW EQUALS -->
    <xsl:template match="char[mt_code_value='0xE902' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2A71;</xsl:text></mo>
    </xsl:template>

    <!-- PLUS BELOW EQUALS -->
    <xsl:template match="char[mt_code_value='0xE902' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2A71;</xsl:text>
    </xsl:template>

    <!-- PLUS ABOVE TILDE -->
    <xsl:template match="char[mt_code_value='0xE903' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2A26;</xsl:text></mo>
    </xsl:template>

    <!-- PLUS ABOVE TILDE -->
    <xsl:template match="char[mt_code_value='0xE903' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2A26;</xsl:text>
    </xsl:template>

    <!-- PLUS BELOW TILDE -->
    <xsl:template match="char[mt_code_value='0xE904' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2A24;</xsl:text></mo>
    </xsl:template>

    <!-- PLUS BELOW TILDE -->
    <xsl:template match="char[mt_code_value='0xE904' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2A24;</xsl:text>
    </xsl:template>

    <!-- SUPERSET OF OR EQUAL TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xE90B' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2287;</xsl:text></mo>
    </xsl:template>

    <!-- SUPERSET OF OR EQUAL TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xE90B' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2287;</xsl:text>
    </xsl:template>

    <!-- SUBSET OF OR EQUAL TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xE90C' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2286;</xsl:text></mo>
    </xsl:template>

    <!-- SUBSET OF OR EQUAL TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xE90C' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2286;</xsl:text>
    </xsl:template>

    <!-- LESS-THAN EQUAL TO (DOUBLE) OR GREATER-THAN -->
    <xsl:template match="char[mt_code_value='0xE922' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x22DA;</xsl:text></mo>
    </xsl:template>

    <!-- LESS-THAN EQUAL TO (DOUBLE) OR GREATER-THAN -->
    <xsl:template match="char[mt_code_value='0xE922' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x22DA;</xsl:text>
    </xsl:template>

    <!-- UNSUPPORTED: EQUIVALENT TO OR GREATER-THAN -->
    <xsl:template match="char[mt_code_value='0xE923' and variation='mathmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>

    <!-- UNSUPPORTED: EQUIVALENT TO OR GREATER-THAN -->
    <xsl:template match="char[mt_code_value='0xE923' and variation='textmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>

    <!-- TILDE WITH DOT -->
    <xsl:template match="char[mt_code_value='0xE924' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2A6A;</xsl:text></mo>
    </xsl:template>

    <!-- TILDE WITH DOT -->
    <xsl:template match="char[mt_code_value='0xE924' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2A6A;</xsl:text>
    </xsl:template>

    <!-- TILDE WITH TWO DOTS -->
    <xsl:template match="char[mt_code_value='0xE925' and variation='mathmode']">
        <mover accent='true'><mo><xsl:text disable-output-escaping="yes">&amp;#x223B;</xsl:text></mo><mo><xsl:text disable-output-escaping="yes">&amp;#x00A8;</xsl:text></mo></mover>
    </xsl:template>

    <!-- UNSUPPORTED: TILDE WITH TWO DOTS -->
    <xsl:template match="char[mt_code_value='0xE925' and variation='textmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>

    <!-- GREATER-THAN EQUAL TO (DOUBLE) OR LESS-THAN -->
    <xsl:template match="char[mt_code_value='0xE92D' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x22DB;</xsl:text></mo>
    </xsl:template>

    <!-- GREATER-THAN EQUAL TO (DOUBLE) OR LESS-THAN -->
    <xsl:template match="char[mt_code_value='0xE92D' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x22DB;</xsl:text>
    </xsl:template>

    <!-- LESS-THAN OR APPROXIMATELY EQUAL TO -->
    <xsl:template match="char[mt_code_value='0xE932' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2272;</xsl:text></mo>
    </xsl:template>

    <!-- LESS-THAN OR APPROXIMATELY EQUAL TO -->
    <xsl:template match="char[mt_code_value='0xE932' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2272;</xsl:text>
    </xsl:template>

    <!-- GREATER-THAN OR APPROXIMATELY EQUAL TO -->
    <xsl:template match="char[mt_code_value='0xE933' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2273;</xsl:text></mo>
    </xsl:template>

    <!-- GREATER-THAN OR APPROXIMATELY EQUAL TO -->
    <xsl:template match="char[mt_code_value='0xE933' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2273;</xsl:text>
    </xsl:template>

    <!-- PRECEDES OR EQUIVALENT TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xE93A' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x227E;</xsl:text></mo>
    </xsl:template>

    <!-- PRECEDES OR EQUIVALENT TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xE93A' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x227E;</xsl:text>
    </xsl:template>

    <!-- SUCCEEDS OR EQUIVALENT TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xE93B' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x227F;</xsl:text></mo>
    </xsl:template>

    <!-- SUCCEEDS OR EQUIVALENT TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xE93B' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x227F;</xsl:text>
    </xsl:template>

    <!-- MEDIUM DOT OPERATOR (FREE RADICAL) -->
    <xsl:template match="char[mt_code_value='0xE98F' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x00B7;</xsl:text></mo>
    </xsl:template>

    <!-- MEDIUM DOT OPERATOR (FREE RADICAL) -->
    <xsl:template match="char[mt_code_value='0xE98F' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x00B7;</xsl:text>
    </xsl:template>

    <!-- NOT LESS-THAN OR EQUAL TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xEA06' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2270;</xsl:text></mo>
    </xsl:template>

    <!-- NOT LESS-THAN OR EQUAL TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xEA06' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2270;</xsl:text>
    </xsl:template>

    <!-- NOT GREATER-THAN OR EQUAL TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xEA07' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2271;</xsl:text></mo>
    </xsl:template>

    <!-- NOT GREATER-THAN OR EQUAL TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xEA07' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2271;</xsl:text>
    </xsl:template>

    <!-- NEITHER SUPERSET OF NOR EQUAL TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xEA0B' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2289;</xsl:text></mo>
    </xsl:template>

    <!-- NEITHER SUPERSET OF NOR EQUAL TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xEA0B' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2289;</xsl:text>
    </xsl:template>

    <!-- NEITHER SUBSET OF NOR EQUAL TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xEA0C' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2288;</xsl:text></mo>
    </xsl:template>

    <!-- NEITHER SUBSET OF NOR EQUAL TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xEA0C' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2288;</xsl:text>
    </xsl:template>

    <!-- LESS-THAN BUT NOT APPROXIMATELY EQUAL TO -->
    <xsl:template match="char[mt_code_value='0xEA32' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2A89;</xsl:text></mo>
    </xsl:template>

    <!-- LESS-THAN BUT NOT APPROXIMATELY EQUAL TO -->
    <xsl:template match="char[mt_code_value='0xEA32' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2A89;</xsl:text>
    </xsl:template>

    <!-- GREATER-THAN BUT NOT APPROXIMATELY EQUAL TO -->
    <xsl:template match="char[mt_code_value='0xEA33' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2A8A;</xsl:text></mo>
    </xsl:template>

    <!-- GREATER-THAN BUT NOT APPROXIMATELY EQUAL TO -->
    <xsl:template match="char[mt_code_value='0xEA33' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2A8A;</xsl:text>
    </xsl:template>

    <!-- LESS-THAN OR NOT EQUAL TO (SINGLE) -->
    <xsl:template match="char[mt_code_value='0xEA34' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2268;</xsl:text></mo>
    </xsl:template>

    <!-- LESS-THAN OR NOT EQUAL TO (SINGLE) -->
    <xsl:template match="char[mt_code_value='0xEA34' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2268;</xsl:text>
    </xsl:template>

    <!-- GREATER-THAN OR NOT EQUAL TO (SINGLE) -->
    <xsl:template match="char[mt_code_value='0xEA35' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2269;</xsl:text></mo>
    </xsl:template>

    <!-- GREATER-THAN OR NOT EQUAL TO (SINGLE) -->
    <xsl:template match="char[mt_code_value='0xEA35' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2269;</xsl:text>
    </xsl:template>

    <!-- PRECEDES BUT NOT EQUIVALENT TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xEA3A' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x22E8;</xsl:text></mo>
    </xsl:template>

    <!-- PRECEDES BUT NOT EQUIVALENT TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xEA3A' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x22E8;</xsl:text>
    </xsl:template>

    <!-- SUCCEEDS BUT NOT EQUIVALENT TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xEA3B' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x22E9;</xsl:text></mo>
    </xsl:template>

    <!-- SUCCEEDS BUT NOT EQUIVALENT TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xEA3B' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x22E9;</xsl:text>
    </xsl:template>

    <!-- PRECEDES BUT NOT EQUAL TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xEA40' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2AB5;</xsl:text></mo>
    </xsl:template>

    <!-- PRECEDES BUT NOT EQUAL TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xEA40' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2AB5;</xsl:text>
    </xsl:template>

    <!-- SUCCEEDS BUT NOT EQUAL TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xEA41' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2AB6;</xsl:text></mo>
    </xsl:template>

    <!-- SUCCEEDS BUT NOT EQUAL TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xEA41' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2AB6;</xsl:text>
    </xsl:template>

    <!-- NOT SUBSET OF NOR EQUAL TO -->
    <xsl:template match="char[mt_code_value='0xEA42' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2288;</xsl:text></mo>
    </xsl:template>

    <!-- NOT SUBSET OF NOR EQUAL TO -->
    <xsl:template match="char[mt_code_value='0xEA42' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2288;</xsl:text>
    </xsl:template>

    <!-- NOT SUPERSET OF NOR EQUAL TO -->
    <xsl:template match="char[mt_code_value='0xEA43' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2289;</xsl:text></mo>
    </xsl:template>

    <!-- NOT SUPERSET OF NOR EQUAL TO -->
    <xsl:template match="char[mt_code_value='0xEA43' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2289;</xsl:text>
    </xsl:template>

    <!-- SUBSET OF OR NOT EQUAL TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xEA44' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x228A;</xsl:text></mo>
    </xsl:template>

    <!-- SUBSET OF OR NOT EQUAL TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xEA44' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x228A;</xsl:text>
    </xsl:template>

    <!-- SUPERSET OF OR NOT EQUAL TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xEA45' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x228B;</xsl:text></mo>
    </xsl:template>

    <!-- SUPERSET OF OR NOT EQUAL TO (DOUBLE) -->
    <xsl:template match="char[mt_code_value='0xEA45' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x228B;</xsl:text>
    </xsl:template>

    <!-- RIGHTWARDS ARROW ABOVE SHORT LEFTWARDS ARROW -->
    <xsl:template match="char[mt_code_value='0xEB01' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2942;</xsl:text></mo>
    </xsl:template>

    <!-- SHORT RIGHTWARDS ARROW ABOVE LEFTWARDS ARROW -->
    <xsl:template match="char[mt_code_value='0xEB02' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2944;</xsl:text></mo>
    </xsl:template>

    <!-- DOUBLE ARROW NORTHEAST SOUTHWEST -->
    <xsl:template match="char[mt_code_value='0xEB05' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2922;</xsl:text></mo>
    </xsl:template>

    <!-- DOUBLE ARROW NORTHWEST SOUTHEAST -->
    <xsl:template match="char[mt_code_value='0xEB06' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2921;</xsl:text></mo>
    </xsl:template>

    <!-- COMBINING TILDE TWO SPACE -->
    <xsl:template match="char[mt_code_value='0xEE04' and variation='mathmode']">
        <mo stretchy='true'>&amp;#x02DC;</mo>
    </xsl:template>

    <!-- COMBINING TILDE THREE SPACE -->
    <xsl:template match="char[mt_code_value='0xEE05' and variation='mathmode']">
        <mo stretchy='true'>&amp;#x02DC;</mo>
    </xsl:template>

    <!-- COMBINING TILDE FOUR SPACE -->
    <xsl:template match="char[mt_code_value='0xEE06' and variation='mathmode']">
        <mo stretchy='true'>&amp;#x02DC;</mo>
    </xsl:template>

    <!-- COMBINING CIRCUMFLEX TWO SPACE -->
    <xsl:template match="char[mt_code_value='0xEE07' and variation='mathmode']">
        <mo stretchy='true'>&amp;#x005E;</mo>
    </xsl:template>

    <!-- COMBINING CIRCUMFLEX THREE SPACE -->
    <xsl:template match="char[mt_code_value='0xEE08' and variation='mathmode']">
        <mo stretchy='true'>&amp;#x005E;</mo>
    </xsl:template>

    <!-- COMBINING CIRCUMFLEX FOUR SPACE -->
    <xsl:template match="char[mt_code_value='0xEE09' and variation='mathmode']">
        <mo stretchy='true'>&amp;#x005E;</mo>
    </xsl:template>

    <!-- COMBINING ARC TWO SPACE -->
    <xsl:template match="char[mt_code_value='0xEE0A' and variation='mathmode']">
        <mo stretchy='true'>&amp;#x2322;</mo>
    </xsl:template>

    <!-- COMBINING ARC THREE SPACE -->
    <xsl:template match="char[mt_code_value='0xEE0B' and variation='mathmode']">
        <mo stretchy='true'>&amp;#x2322;</mo>
    </xsl:template>

    <!-- COMBINING ARC FOUR SPACE -->
    <xsl:template match="char[mt_code_value='0xEE0C' and variation='mathmode']">
        <mo stretchy='true'>&amp;#x2322;</mo>
    </xsl:template>

    <!-- QUADRUPLE PRIME -->
    <xsl:template match="char[mt_code_value='0xEE19' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2057;</xsl:text></mo>
    </xsl:template>

    <!-- QUADRUPLE PRIME -->
    <xsl:template match="char[mt_code_value='0xEE19' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2057;</xsl:text>
    </xsl:template>

    <!-- ALIGNMENT MARK -->
    <xsl:template match="char[mt_code_value='0xEF00' and variation='mathmode']">
        <malignmark/>
    </xsl:template>

    <!-- ALIGNMENT MARK -->
    <xsl:template match="char[mt_code_value='0xEF00' and variation='textmode']">
        <malignmark/>
    </xsl:template>

    <!-- MT ZERO SPACE -->
    <xsl:template match="char[mt_code_value='0xEF01' and variation='mathmode']">
        <mtext><xsl:text disable-output-escaping="yes">&amp;#x200B;</xsl:text></mtext>
    </xsl:template>

    <!-- MT ZERO SPACE -->
    <xsl:template match="char[mt_code_value='0xEF01' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x200B;</xsl:text>
    </xsl:template>

    <!-- MT THIN SPACE -->
    <xsl:template match="char[mt_code_value='0xEF02' and variation='mathmode']">
        <mtext><xsl:text disable-output-escaping="yes">&amp;#x2009;</xsl:text></mtext>
    </xsl:template>

    <!-- MT THIN SPACE -->
    <xsl:template match="char[mt_code_value='0xEF02' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2009;</xsl:text>
    </xsl:template>

    <!-- MT MEDIUM SPACE -->
    <xsl:template match="char[mt_code_value='0xEF03' and variation='mathmode']">
        <mtext><xsl:text disable-output-escaping="yes">&amp;#x205F;</xsl:text></mtext>
    </xsl:template>

    <!-- MT MEDIUM SPACE -->
    <xsl:template match="char[mt_code_value='0xEF03' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x205F;</xsl:text>
    </xsl:template>

    <!-- MT THICK SPACE -->
    <xsl:template match="char[mt_code_value='0xEF04' and variation='mathmode']">
        <mtext><xsl:text disable-output-escaping="yes">&amp;#x2009;</xsl:text></mtext>
    </xsl:template>

    <!-- MT THICK SPACE -->
    <xsl:template match="char[mt_code_value='0xEF04' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2009;</xsl:text>
    </xsl:template>

    <!-- MT EM SPACE -->
    <xsl:template match="char[mt_code_value='0xEF05' and variation='mathmode']">
        <mtext><xsl:text disable-output-escaping="yes">&amp;#x2003;</xsl:text></mtext>
    </xsl:template>

    <!-- MT EM SPACE -->
    <xsl:template match="char[mt_code_value='0xEF05' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2003;</xsl:text>
    </xsl:template>

    <!-- MT 2 EM SPACE -->
    <xsl:template match="char[mt_code_value='0xEF06' and variation='mathmode']">
        <mtext><xsl:text disable-output-escaping="yes">&amp;#x2003;</xsl:text>&amp;#x2003;</mtext>
    </xsl:template>

    <!-- MT 2 EM SPACE -->
    <xsl:template match="char[mt_code_value='0xEF06' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2003;&amp;#x2003;</xsl:text>
    </xsl:template>

    <!-- UNSUPPORTED: range: Private Use Area -->
    <xsl:template match="char[mt_code_value='0xEF07' and variation='mathmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>

    <!-- UNSUPPORTED: range: Private Use Area -->
    <xsl:template match="char[mt_code_value='0xEF07' and variation='textmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>

    <!-- MT 1 POINT SPACE -->
    <xsl:template match="char[mt_code_value='0xEF08' and variation='mathmode']">
        <mtext><xsl:text disable-output-escaping="yes">&amp;#x200A;</xsl:text></mtext>
    </xsl:template>

    <!-- MT 1 POINT SPACE -->
    <xsl:template match="char[mt_code_value='0xEF08' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x200A;</xsl:text>
    </xsl:template>


    <!-- MATHEMATICAL FRAKTUR CAPITAL A -->
    <xsl:template match="char[mt_code_value='0xF000' and variation='mathmode']">
        <mi mathvariant='fraktur'>A</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR CAPITAL B -->
    <xsl:template match="char[mt_code_value='0xF001' and variation='mathmode']">
        <mi mathvariant='fraktur'>B</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR CAPITAL C -->
    <xsl:template match="char[mt_code_value='0x212D' and variation='mathmode']">
        <mi><xsl:text disable-output-escaping="yes">&amp;#x212D;</xsl:text></mi>
    </xsl:template>

    <!-- UNSUPPORTED: range: Private Use Area -->
    <xsl:template match="char[mt_code_value='0xF002' and variation='mathmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR CAPITAL D -->
    <xsl:template match="char[mt_code_value='0xF003' and variation='mathmode']">
        <mi mathvariant='fraktur'>D</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR CAPITAL E -->
    <xsl:template match="char[mt_code_value='0xF004' and variation='mathmode']">
        <mi mathvariant='fraktur'>E</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR CAPITAL F -->
    <xsl:template match="char[mt_code_value='0xF005' and variation='mathmode']">
        <mi mathvariant='fraktur'>F</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR CAPITAL G -->
    <xsl:template match="char[mt_code_value='0xF006' and variation='mathmode']">
        <mi mathvariant='fraktur'>G</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR CAPITAL H -->
    <xsl:template match="char[mt_code_value='0x210C' and variation='mathmode']">
        <mi><xsl:text disable-output-escaping="yes">&amp;#x210C;</xsl:text></mi>
    </xsl:template>

    <!-- UNSUPPORTED -->
    <xsl:template match="char[mt_code_value='0xF007' and variation='mathmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR CAPITAL I -->
    <xsl:template match="char[mt_code_value='0x2111' and variation='mathmode']">
        <mi><xsl:text disable-output-escaping="yes">&amp;#x2111;</xsl:text></mi>
    </xsl:template>

    <!-- UNSUPPORTED -->
    <xsl:template match="char[mt_code_value='0xF008' and variation='mathmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>
    <!-- MATHEMATICAL FRAKTUR CAPITAL J -->
    <xsl:template match="char[mt_code_value='0xF009' and variation='mathmode']">
        <mi mathvariant='fraktur'>J</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR CAPITAL K -->
    <xsl:template match="char[mt_code_value='0xF00A' and variation='mathmode']">
        <mi mathvariant='fraktur'>K</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR CAPITAL L -->
    <xsl:template match="char[mt_code_value='0xF00B' and variation='mathmode']">
        <mi mathvariant='fraktur'>L</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR CAPITAL M -->
    <xsl:template match="char[mt_code_value='0xF00C' and variation='mathmode']">
        <mi mathvariant='fraktur'>M</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR CAPITAL N -->
    <xsl:template match="char[mt_code_value='0xF00D' and variation='mathmode']">
        <mi mathvariant='fraktur'>N</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR CAPITAL O -->
    <xsl:template match="char[mt_code_value='0xF00E' and variation='mathmode']">
        <mi mathvariant='fraktur'>O</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR CAPITAL P -->
    <xsl:template match="char[mt_code_value='0xF00F' and variation='mathmode']">
        <mi mathvariant='fraktur'>P</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR CAPITAL Q -->
    <xsl:template match="char[mt_code_value='0xF010' and variation='mathmode']">
        <mi mathvariant='fraktur'>Q</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR CAPITAL R -->
    <xsl:template match="char[mt_code_value='0x211C' and variation='mathmode']">
        <mi><xsl:text disable-output-escaping="yes">&amp;#x211C;</xsl:text></mi>
    </xsl:template>

    <!-- UNSUPPORTED -->
    <xsl:template match="char[mt_code_value='0xF011' and variation='mathmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>
    <!-- MATHEMATICAL FRAKTUR CAPITAL S -->
    <xsl:template match="char[mt_code_value='0xF012' and variation='mathmode']">
        <mi mathvariant='fraktur'>S</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR CAPITAL T -->
    <xsl:template match="char[mt_code_value='0xF013' and variation='mathmode']">
        <mi mathvariant='fraktur'>T</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR CAPITAL U -->
    <xsl:template match="char[mt_code_value='0xF014' and variation='mathmode']">
        <mi mathvariant='fraktur'>U</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR CAPITAL V -->
    <xsl:template match="char[mt_code_value='0xF015' and variation='mathmode']">
        <mi mathvariant='fraktur'>V</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR CAPITAL W -->
    <xsl:template match="char[mt_code_value='0xF016' and variation='mathmode']">
        <mi mathvariant='fraktur'>W</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR CAPITAL X -->
    <xsl:template match="char[mt_code_value='0xF017' and variation='mathmode']">
        <mi mathvariant='fraktur'>X</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR CAPITAL Y -->
    <xsl:template match="char[mt_code_value='0xF018' and variation='mathmode']">
        <mi mathvariant='fraktur'>Y</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR CAPITAL Z -->
    <xsl:template match="char[mt_code_value='0x2128' and variation='mathmode']">
        <mi><xsl:text disable-output-escaping="yes">&amp;#x2128;</xsl:text></mi>
    </xsl:template>

    <!-- UNSUPPORTED -->
    <xsl:template match="char[mt_code_value='0xF019' and variation='mathmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>
    <!-- MATHEMATICAL FRAKTUR SMALL A -->
    <xsl:template match="char[mt_code_value='0xF01A' and variation='mathmode']">
        <mi mathvariant='fraktur'>a</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR SMALL B -->
    <xsl:template match="char[mt_code_value='0xF01B' and variation='mathmode']">
        <mi mathvariant='fraktur'>b</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR SMALL C -->
    <xsl:template match="char[mt_code_value='0xF01C' and variation='mathmode']">
        <mi mathvariant='fraktur'>c</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR SMALL D -->
    <xsl:template match="char[mt_code_value='0xF01D' and variation='mathmode']">
        <mi mathvariant='fraktur'>d</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR SMALL E -->
    <xsl:template match="char[mt_code_value='0xF01E' and variation='mathmode']">
        <mi mathvariant='fraktur'>e</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR SMALL F -->
    <xsl:template match="char[mt_code_value='0xF01F' and variation='mathmode']">
        <mi mathvariant='fraktur'>f</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR SMALL G -->
    <xsl:template match="char[mt_code_value='0xF020' and variation='mathmode']">
        <mi mathvariant='fraktur'>g</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR SMALL H -->
    <xsl:template match="char[mt_code_value='0xF021' and variation='mathmode']">
        <mi mathvariant='fraktur'>h</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR SMALL I -->
    <xsl:template match="char[mt_code_value='0xF022' and variation='mathmode']">
        <mi mathvariant='fraktur'>i</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR SMALL J -->
    <xsl:template match="char[mt_code_value='0xF023' and variation='mathmode']">
        <mi mathvariant='fraktur'>j</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR SMALL K -->
    <xsl:template match="char[mt_code_value='0xF024' and variation='mathmode']">
        <mi mathvariant='fraktur'>k</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR SMALL L -->
    <xsl:template match="char[mt_code_value='0xF025' and variation='mathmode']">
        <mi mathvariant='fraktur'>l</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR SMALL M -->
    <xsl:template match="char[mt_code_value='0xF026' and variation='mathmode']">
        <mi mathvariant='fraktur'>m</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR SMALL N -->
    <xsl:template match="char[mt_code_value='0xF027' and variation='mathmode']">
        <mi mathvariant='fraktur'>n</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR SMALL O -->
    <xsl:template match="char[mt_code_value='0xF028' and variation='mathmode']">
        <mi mathvariant='fraktur'>o</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR SMALL P -->
    <xsl:template match="char[mt_code_value='0xF029' and variation='mathmode']">
        <mi mathvariant='fraktur'>p</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR SMALL Q -->
    <xsl:template match="char[mt_code_value='0xF02A' and variation='mathmode']">
        <mi mathvariant='fraktur'>q</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR SMALL R -->
    <xsl:template match="char[mt_code_value='0xF02B' and variation='mathmode']">
        <mi mathvariant='fraktur'>r</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR SMALL S -->
    <xsl:template match="char[mt_code_value='0xF02C' and variation='mathmode']">
        <mi mathvariant='fraktur'>s</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR SMALL T -->
    <xsl:template match="char[mt_code_value='0xF02D' and variation='mathmode']">
        <mi mathvariant='fraktur'>t</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR SMALL U -->
    <xsl:template match="char[mt_code_value='0xF02E' and variation='mathmode']">
        <mi mathvariant='fraktur'>u</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR SMALL V -->
    <xsl:template match="char[mt_code_value='0xF02F' and variation='mathmode']">
        <mi mathvariant='fraktur'>v</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR SMALL W -->
    <xsl:template match="char[mt_code_value='0xF030' and variation='mathmode']">
        <mi mathvariant='fraktur'>w</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR SMALL X -->
    <xsl:template match="char[mt_code_value='0xF031' and variation='mathmode']">
        <mi mathvariant='fraktur'>x</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR SMALL Y -->
    <xsl:template match="char[mt_code_value='0xF032' and variation='mathmode']">
        <mi mathvariant='fraktur'>y</mi>
    </xsl:template>

    <!-- MATHEMATICAL FRAKTUR SMALL Z -->
    <xsl:template match="char[mt_code_value='0xF033' and variation='mathmode']">
        <mi mathvariant='fraktur'>z</mi>
    </xsl:template>


    <!-- MATHEMATICAL DOUBLE-STRUCK CAPITAL A -->
    <xsl:template match="char[mt_code_value='0xF080' and variation='mathmode']">
        <mi mathvariant='double-struck'>A</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK CAPITAL B -->
    <xsl:template match="char[mt_code_value='0xF081' and variation='mathmode']">
        <mi mathvariant='double-struck'>B</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK CAPITAL C -->
    <xsl:template match="char[mt_code_value='0x2102' and variation='mathmode']">
        <mi><xsl:text disable-output-escaping="yes">&amp;#x2102;</xsl:text></mi>
    </xsl:template>

    <!-- UNSUPPORTED -->
    <xsl:template match="char[mt_code_value='0xF082' and variation='mathmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>
    <!-- MATHEMATICAL DOUBLE-STRUCK CAPITAL D -->
    <xsl:template match="char[mt_code_value='0xF083' and variation='mathmode']">
        <mi mathvariant='double-struck'>D</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK CAPITAL E -->
    <xsl:template match="char[mt_code_value='0xF084' and variation='mathmode']">
        <mi mathvariant='double-struck'>E</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK CAPITAL F -->
    <xsl:template match="char[mt_code_value='0xF085' and variation='mathmode']">
        <mi mathvariant='double-struck'>F</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK CAPITAL G -->
    <xsl:template match="char[mt_code_value='0xF086' and variation='mathmode']">
        <mi mathvariant='double-struck'>G</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK CAPITAL H -->
    <xsl:template match="char[mt_code_value='0x210D' and variation='mathmode']">
        <mi><xsl:text disable-output-escaping="yes">&amp;#x210D;</xsl:text></mi>
    </xsl:template>

    <!-- UNSUPPORTED -->
    <xsl:template match="char[mt_code_value='0xF087' and variation='mathmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>
    <!-- MATHEMATICAL DOUBLE-STRUCK CAPITAL I -->
    <xsl:template match="char[mt_code_value='0xF088' and variation='mathmode']">
        <mi mathvariant='double-struck'>I</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK CAPITAL J -->
    <xsl:template match="char[mt_code_value='0xF089' and variation='mathmode']">
        <mi mathvariant='double-struck'>J</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK CAPITAL K -->
    <xsl:template match="char[mt_code_value='0xF08A' and variation='mathmode']">
        <mi mathvariant='double-struck'>K</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK CAPITAL L -->
    <xsl:template match="char[mt_code_value='0xF08B' and variation='mathmode']">
        <mi mathvariant='double-struck'>L</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK CAPITAL M -->
    <xsl:template match="char[mt_code_value='0xF08C' and variation='mathmode']">
        <mi mathvariant='double-struck'>M</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK CAPITAL N -->
    <xsl:template match="char[mt_code_value='0x2115' and variation='mathmode']">
        <mi><xsl:text disable-output-escaping="yes">&amp;#x2115;</xsl:text></mi>
    </xsl:template>

    <!-- UNSUPPORTED -->
    <xsl:template match="char[mt_code_value='0xF08D' and variation='mathmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>
    <!-- MATHEMATICAL DOUBLE-STRUCK CAPITAL O -->
    <xsl:template match="char[mt_code_value='0xF08E' and variation='mathmode']">
        <mi mathvariant='double-struck'>O</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK CAPITAL P -->
    <xsl:template match="char[mt_code_value='0x2119' and variation='mathmode']">
        <mi><xsl:text disable-output-escaping="yes">&amp;#x2119;</xsl:text></mi>
    </xsl:template>

    <!-- UNSUPPORTED -->
    <xsl:template match="char[mt_code_value='0xF08F' and variation='mathmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>
    <!-- MATHEMATICAL DOUBLE-STRUCK CAPITAL Q -->
    <xsl:template match="char[mt_code_value='0x211A' and variation='mathmode']">
        <mi><xsl:text disable-output-escaping="yes">&amp;#x211A;</xsl:text></mi>
    </xsl:template>

    <!-- UNSUPPORTED -->
    <xsl:template match="char[mt_code_value='0xF090' and variation='mathmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK CAPITAL R -->
    <xsl:template match="char[mt_code_value='0x211D' and variation='mathmode']">
        <mi><xsl:text disable-output-escaping="yes">&amp;#x211D;</xsl:text></mi>
    </xsl:template>

    <!-- UNSUPPORTED -->
    <xsl:template match="char[mt_code_value='0xF091' and variation='mathmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK CAPITAL S -->
    <xsl:template match="char[mt_code_value='0xF092' and variation='mathmode']">
        <mi mathvariant='double-struck'>S</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK CAPITAL T -->
    <xsl:template match="char[mt_code_value='0xF093' and variation='mathmode']">
        <mi mathvariant='double-struck'>T</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK CAPITAL U -->
    <xsl:template match="char[mt_code_value='0xF094' and variation='mathmode']">
        <mi mathvariant='double-struck'>U</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK CAPITAL V -->
    <xsl:template match="char[mt_code_value='0xF095' and variation='mathmode']">
        <mi mathvariant='double-struck'>V</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK CAPITAL W -->
    <xsl:template match="char[mt_code_value='0xF096' and variation='mathmode']">
        <mi mathvariant='double-struck'>W</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK CAPITAL X -->
    <xsl:template match="char[mt_code_value='0xF097' and variation='mathmode']">
        <mi mathvariant='double-struck'>X</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK CAPITAL Y -->
    <xsl:template match="char[mt_code_value='0xF098' and variation='mathmode']">
        <mi mathvariant='double-struck'>Y</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK CAPITAL Z -->
    <xsl:template match="char[mt_code_value='0x2124' and variation='mathmode']">
        <mi><xsl:text disable-output-escaping="yes">&amp;#x2124;</xsl:text></mi>
    </xsl:template>

    <!-- UNSUPPORTED -->
    <xsl:template match="char[mt_code_value='0xF099' and variation='mathmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK SMALL A -->
    <xsl:template match="char[mt_code_value='0xF09A' and variation='mathmode']">
        <mi mathvariant='double-struck'>a</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK SMALL B -->
    <xsl:template match="char[mt_code_value='0xF09B' and variation='mathmode']">
        <mi mathvariant='double-struck'>b</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK SMALL C -->
    <xsl:template match="char[mt_code_value='0xF09C' and variation='mathmode']">
        <mi mathvariant='double-struck'>c</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK SMALL D -->
    <xsl:template match="char[mt_code_value='0xF09D' and variation='mathmode']">
        <mi mathvariant='double-struck'>d</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK SMALL E -->
    <xsl:template match="char[mt_code_value='0xF09E' and variation='mathmode']">
        <mi mathvariant='double-struck'>e</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK SMALL F -->
    <xsl:template match="char[mt_code_value='0xF09F' and variation='mathmode']">
        <mi mathvariant='double-struck'>f</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK SMALL G -->
    <xsl:template match="char[mt_code_value='0xF0A0' and variation='mathmode']">
        <mi mathvariant='double-struck'>g</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK SMALL H -->
    <xsl:template match="char[mt_code_value='0xF0A1' and variation='mathmode']">
        <mi mathvariant='double-struck'>h</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK SMALL I -->
    <xsl:template match="char[mt_code_value='0xF0A2' and variation='mathmode']">
        <mi mathvariant='double-struck'>i</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK SMALL J -->
    <xsl:template match="char[mt_code_value='0xF0A3' and variation='mathmode']">
        <mi mathvariant='double-struck'>j</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK SMALL K -->
    <xsl:template match="char[mt_code_value='0xF0A4' and variation='mathmode']">
        <mi mathvariant='double-struck'>k</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK SMALL L -->
    <xsl:template match="char[mt_code_value='0xF0A5' and variation='mathmode']">
        <mi mathvariant='double-struck'>l</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK SMALL M -->
    <xsl:template match="char[mt_code_value='0xF0A6' and variation='mathmode']">
        <mi mathvariant='double-struck'>m</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK SMALL N -->
    <xsl:template match="char[mt_code_value='0xF0A7' and variation='mathmode']">
        <mi mathvariant='double-struck'>n</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK SMALL O -->
    <xsl:template match="char[mt_code_value='0xF0A8' and variation='mathmode']">
        <mi mathvariant='double-struck'>o</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK SMALL P -->
    <xsl:template match="char[mt_code_value='0xF0A9' and variation='mathmode']">
        <mi mathvariant='double-struck'>p</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK SMALL Q -->
    <xsl:template match="char[mt_code_value='0xF0AA' and variation='mathmode']">
        <mi mathvariant='double-struck'>q</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK SMALL R -->
    <xsl:template match="char[mt_code_value='0xF0AB' and variation='mathmode']">
        <mi mathvariant='double-struck'>r</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK SMALL S -->
    <xsl:template match="char[mt_code_value='0xF0AC' and variation='mathmode']">
        <mi mathvariant='double-struck'>s</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK SMALL T -->
    <xsl:template match="char[mt_code_value='0xF0AD' and variation='mathmode']">
        <mi mathvariant='double-struck'>t</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK SMALL U -->
    <xsl:template match="char[mt_code_value='0xF0AE' and variation='mathmode']">
        <mi mathvariant='double-struck'>u</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK SMALL V -->
    <xsl:template match="char[mt_code_value='0xF0AF' and variation='mathmode']">
        <mi mathvariant='double-struck'>v</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK SMALL W -->
    <xsl:template match="char[mt_code_value='0xF0B0' and variation='mathmode']">
        <mi mathvariant='double-struck'>w</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK SMALL X -->
    <xsl:template match="char[mt_code_value='0xF0B1' and variation='mathmode']">
        <mi mathvariant='double-struck'>x</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK SMALL Y -->
    <xsl:template match="char[mt_code_value='0xF0B2' and variation='mathmode']">
        <mi mathvariant='double-struck'>y</mi>
    </xsl:template>

    <!-- MATHEMATICAL DOUBLE-STRUCK SMALL Z -->
    <xsl:template match="char[mt_code_value='0xF0B3' and variation='mathmode']">
        <mi mathvariant='double-struck'>z</mi>
    </xsl:template>



    <!-- BLACKBOARD-BOLD DIGIT ZERO -->
    <xsl:template match="char[mt_code_value='0xF0C0' and variation='mathmode']">
        <mn mathvariant='double-struck'>0</mn>
    </xsl:template>

    <!-- BLACKBOARD-BOLD DIGIT ONE -->
    <xsl:template match="char[mt_code_value='0xF0C1' and variation='mathmode']">
        <mn mathvariant='double-struck'>1</mn>
    </xsl:template>

    <!-- BLACKBOARD-BOLD DIGIT TWO -->
    <xsl:template match="char[mt_code_value='0xF0C2' and variation='mathmode']">
        <mn mathvariant='double-struck'>2</mn>
    </xsl:template>

    <!-- BLACKBOARD-BOLD DIGIT THREE -->
    <xsl:template match="char[mt_code_value='0xF0C3' and variation='mathmode']">
        <mn mathvariant='double-struck'>3</mn>
    </xsl:template>

    <!-- BLACKBOARD-BOLD DIGIT FOUR -->
    <xsl:template match="char[mt_code_value='0xF0C4' and variation='mathmode']">
        <mn mathvariant='double-struck'>4</mn>
    </xsl:template>

    <!-- BLACKBOARD-BOLD DIGIT FIVE -->
    <xsl:template match="char[mt_code_value='0xF0C5' and variation='mathmode']">
        <mn mathvariant='double-struck'>5</mn>
    </xsl:template>

    <!-- BLACKBOARD-BOLD DIGIT SIX -->
    <xsl:template match="char[mt_code_value='0xF0C6' and variation='mathmode']">
        <mn mathvariant='double-struck'>6</mn>
    </xsl:template>

    <!-- BLACKBOARD-BOLD DIGIT SEVEN -->
    <xsl:template match="char[mt_code_value='0xF0C7' and variation='mathmode']">
        <mn mathvariant='double-struck'>7</mn>
    </xsl:template>

    <!-- BLACKBOARD-BOLD DIGIT EIGHT -->
    <xsl:template match="char[mt_code_value='0xF0C8' and variation='mathmode']">
        <mn mathvariant='double-struck'>8</mn>
    </xsl:template>

    <!-- BLACKBOARD-BOLD DIGIT NINE -->
    <xsl:template match="char[mt_code_value='0xF0C9' and variation='mathmode']">
        <mn mathvariant='double-struck'>9</mn>
    </xsl:template>


    <!-- MATHEMATICAL SCRIPT CAPITAL A -->
    <xsl:template match="char[mt_code_value='0xF100' and variation='mathmode']">
        <mi mathvariant='script'>A</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL B -->
    <xsl:template match="char[mt_code_value='0x212C' and variation='mathmode']">
        <mi mathvariant='script'>B</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL B -->
    <xsl:template match="char[mt_code_value='0xF101' and variation='mathmode']">
        <mi mathvariant='script'>B</mi>
    </xsl:template>

    <!-- UNSUPPORTED -->
    <xsl:template match="char[mt_code_value='0xF101' and variation='mathmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL C -->
    <xsl:template match="char[mt_code_value='0xF102' and variation='mathmode']">
        <mi mathvariant='script'>C</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL D -->
    <xsl:template match="char[mt_code_value='0xF103' and variation='mathmode']">
        <mi mathvariant='script'>D</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL E -->
    <xsl:template match="char[mt_code_value='0x2130' and variation='mathmode']">
        <mi mathvariant='script'>E</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL E -->
    <xsl:template match="char[mt_code_value='0xF104' and variation='mathmode']">
        <mi mathvariant='script'>E</mi>
    </xsl:template>

    <!-- UNSUPPORTED -->
    <xsl:template match="char[mt_code_value='0xF104' and variation='mathmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL F -->
    <xsl:template match="char[mt_code_value='0x2131' and variation='mathmode']">
        <mi mathvariant='script'>F</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL F -->
    <xsl:template match="char[mt_code_value='0xF105' and variation='mathmode']">
        <mi mathvariant='script'>F</mi>
    </xsl:template>

    <!-- UNSUPPORTED -->
    <xsl:template match="char[mt_code_value='0xF105' and variation='mathmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL G -->
    <xsl:template match="char[mt_code_value='0xF106' and variation='mathmode']">
        <mi mathvariant='script'>G</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL H -->
    <xsl:template match="char[mt_code_value='0x210B' and variation='mathmode']">
        <mi mathvariant='script'>H</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL H -->
    <xsl:template match="char[mt_code_value='0xF107' and variation='mathmode']">
        <mi mathvariant='script'>H</mi>
    </xsl:template>

    <!-- UNSUPPORTED -->
    <xsl:template match="char[mt_code_value='0xF107' and variation='mathmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL I -->
    <xsl:template match="char[mt_code_value='0x2110' and variation='mathmode']">
        <mi mathvariant='script'>I</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL I -->
    <xsl:template match="char[mt_code_value='0xF108' and variation='mathmode']">
        <mi mathvariant='script'>I</mi>
    </xsl:template>

    <!-- UNSUPPORTED -->
    <xsl:template match="char[mt_code_value='0xF108' and variation='mathmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL J -->
    <xsl:template match="char[mt_code_value='0xF109' and variation='mathmode']">
        <mi mathvariant='script'>J</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL K -->
    <xsl:template match="char[mt_code_value='0xF10A' and variation='mathmode']">
        <mi mathvariant='script'>K</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL L -->
    <xsl:template match="char[mt_code_value='0x2112' and variation='mathmode']">
        <mi mathvariant='script'>L</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL L -->
    <xsl:template match="char[mt_code_value='0xF10B' and variation='mathmode']">
        <mi mathvariant='script'>L</mi>
    </xsl:template>

    <!-- UNSUPPORTED -->
    <xsl:template match="char[mt_code_value='0xF10B' and variation='mathmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL M -->
    <xsl:template match="char[mt_code_value='0x2133' and variation='mathmode']">
        <mi mathvariant='script'>M</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL M -->
    <xsl:template match="char[mt_code_value='0xF10C' and variation='mathmode']">
        <mi mathvariant='script'>M</mi>
    </xsl:template>

    <!-- UNSUPPORTED -->
    <xsl:template match="char[mt_code_value='0xF10C' and variation='mathmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL N -->
    <xsl:template match="char[mt_code_value='0xF10D' and variation='mathmode']">
        <mi mathvariant='script'>N</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL O -->
    <xsl:template match="char[mt_code_value='0xF10E' and variation='mathmode']">
        <mi mathvariant='script'>O</mi>
    </xsl:template>

    <!-- Weierstrass Elliptic Function -->
    <xsl:template match="char[mt_code_value='0x2118' and variation='mathmode']">
        <mi><xsl:text disable-output-escaping="yes">&amp;#x2118;</xsl:text></mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL P -->
    <xsl:template match="char[mt_code_value='0x2118' and variation='mathmode']">
        <mi><xsl:text disable-output-escaping="yes">&amp;#x1D4AB</xsl:text>;</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL P -->
    <xsl:template match="char[mt_code_value='0xF10F' and variation='mathmode']">
        <mi mathvariant='script'>P</mi>
    </xsl:template>

    <!-- UNSUPPORTED -->
    <xsl:template match="char[mt_code_value='0xF10F' and variation='mathmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL Q -->
    <xsl:template match="char[mt_code_value='0xF110' and variation='mathmode']">
        <mi mathvariant='script'>Q</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL R -->
    <xsl:template match="char[mt_code_value='0x211B' and variation='mathmode']">
        <mi mathvariant='script'>R</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL R -->
    <xsl:template match="char[mt_code_value='0xF111' and variation='mathmode']">
        <mi mathvariant='script'>R</mi>
    </xsl:template>

    <!-- UNSUPPORTED -->
    <xsl:template match="char[mt_code_value='0xF111' and variation='mathmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL S -->
    <xsl:template match="char[mt_code_value='0xF112' and variation='mathmode']">
        <mi mathvariant='script'>S</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL T -->
    <xsl:template match="char[mt_code_value='0xF113' and variation='mathmode']">
        <mi mathvariant='script'>T</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL U -->
    <xsl:template match="char[mt_code_value='0xF114' and variation='mathmode']">
        <mi mathvariant='script'>U</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL V -->
    <xsl:template match="char[mt_code_value='0xF115' and variation='mathmode']">
        <mi mathvariant='script'>V</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL W -->
    <xsl:template match="char[mt_code_value='0xF116' and variation='mathmode']">
        <mi mathvariant='script'>W</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL X -->
    <xsl:template match="char[mt_code_value='0xF117' and variation='mathmode']">
        <mi mathvariant='script'>X</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL Y -->
    <xsl:template match="char[mt_code_value='0xF118' and variation='mathmode']">
        <mi mathvariant='script'>Y</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT CAPITAL Z -->
    <xsl:template match="char[mt_code_value='0xF119' and variation='mathmode']">
        <mi mathvariant='script'>Z</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL A -->
    <xsl:template match="char[mt_code_value='0xF11A' and variation='mathmode']">
        <mi mathvariant='script'>a</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL B -->
    <xsl:template match="char[mt_code_value='0xF11B' and variation='mathmode']">
        <mi mathvariant='script'>b</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL C -->
    <xsl:template match="char[mt_code_value='0xF11C' and variation='mathmode']">
        <mi mathvariant='script'>c</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL D -->
    <xsl:template match="char[mt_code_value='0xF11D' and variation='mathmode']">
        <mi mathvariant='script'>d</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL E -->
    <xsl:template match="char[mt_code_value='0xF11E' and variation='mathmode']">
        <mi mathvariant='script'>e</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL E -->
    <xsl:template match="char[mt_code_value='0x212F' and variation='mathmode']">
        <mi mathvariant='script'>e</mi>
    </xsl:template>

    <!-- UNSUPPORTED -->
    <xsl:template match="char[mt_code_value='0xF11E' and variation='mathmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL F -->
    <xsl:template match="char[mt_code_value='0xF11F' and variation='mathmode']">
        <mi mathvariant='script'>f</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL G -->
    <xsl:template match="char[mt_code_value='0x210A' and variation='mathmode']">
        <mi mathvariant='script'>g</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL G -->
    <xsl:template match="char[mt_code_value='0xF120' and variation='mathmode']">
        <mi mathvariant='script'>g</mi>
    </xsl:template>

    <!-- UNSUPPORTED -->
    <xsl:template match="char[mt_code_value='0xF120' and variation='mathmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL H -->
    <xsl:template match="char[mt_code_value='0xF121' and variation='mathmode']">
        <mi mathvariant='script'>h</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL I -->
    <xsl:template match="char[mt_code_value='0xF122' and variation='mathmode']">
        <mi mathvariant='script'>i</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL J -->
    <xsl:template match="char[mt_code_value='0xF123' and variation='mathmode']">
        <mi mathvariant='script'>j</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL K -->
    <xsl:template match="char[mt_code_value='0xF124' and variation='mathmode']">
        <mi mathvariant='script'>k</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL L -->
    <xsl:template match="char[mt_code_value='0x2113' and variation='mathmode']">
        <mi mathvariant='script'>l</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL L -->
    <xsl:template match="char[mt_code_value='0xF125' and variation='mathmode']">
        <mi mathvariant='script'>l</mi>
    </xsl:template>

    <!-- UNSUPPORTED -->
    <xsl:template match="char[mt_code_value='0xF125' and variation='mathmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL M -->
    <xsl:template match="char[mt_code_value='0xF126' and variation='mathmode']">
        <mi mathvariant='script'>m</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL N -->
    <xsl:template match="char[mt_code_value='0xF127' and variation='mathmode']">
        <mi mathvariant='script'>n</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL O -->
    <xsl:template match="char[mt_code_value='0x2134' and variation='mathmode']">
        <mi mathvariant='script'>o</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL O -->
    <xsl:template match="char[mt_code_value='0xF128' and variation='mathmode']">
        <mi mathvariant='script'>o</mi>
    </xsl:template>

    <!-- UNSUPPORTED -->
    <xsl:template match="char[mt_code_value='0xF128' and variation='mathmode']">
        <xsl:text>Unsupported character</xsl:text>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL P -->
    <xsl:template match="char[mt_code_value='0xF129' and variation='mathmode']">
        <mi mathvariant='script'>p</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL Q -->
    <xsl:template match="char[mt_code_value='0xF12A' and variation='mathmode']">
        <mi mathvariant='script'>q</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL R -->
    <xsl:template match="char[mt_code_value='0xF12B' and variation='mathmode']">
        <mi mathvariant='script'>r</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL S -->
    <xsl:template match="char[mt_code_value='0xF12C' and variation='mathmode']">
        <mi mathvariant='script'>s</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL T -->
    <xsl:template match="char[mt_code_value='0xF12D' and variation='mathmode']">
        <mi mathvariant='script'>t</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL U -->
    <xsl:template match="char[mt_code_value='0xF12E' and variation='mathmode']">
        <mi mathvariant='script'>u</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL V -->
    <xsl:template match="char[mt_code_value='0xF12F' and variation='mathmode']">
        <mi mathvariant='script'>v</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL W -->
    <xsl:template match="char[mt_code_value='0xF130' and variation='mathmode']">
        <mi mathvariant='script'>w</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL X -->
    <xsl:template match="char[mt_code_value='0xF131' and variation='mathmode']">
        <mi mathvariant='script'>x</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL Y -->
    <xsl:template match="char[mt_code_value='0xF132' and variation='mathmode']">
        <mi mathvariant='script'>y</mi>
    </xsl:template>

    <!-- MATHEMATICAL SCRIPT SMALL Z -->
    <xsl:template match="char[mt_code_value='0xF133' and variation='mathmode']">
        <mi mathvariant='script'>z</mi>
    </xsl:template>

    <!-- ZERO WIDTH NO-BREAK SPACE -->
    <xsl:template match="char[mt_code_value='0xFEFF' and variation='mathmode']">
        <mtext><xsl:text disable-output-escaping="yes">&amp;#xFEFF;</xsl:text></mtext>
    </xsl:template>

    <xsl:template match="char[mt_code_value='0x2212' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2212;</xsl:text></mo>
    </xsl:template>

    <!-- DifferentialD -->
    <xsl:template match="char[mt_code_value='0xED10' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2146;</xsl:text></mo>
    </xsl:template>

    <!-- DifferentialD -->
    <xsl:template match="char[mt_code_value='0xED10' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2146;</xsl:text>
    </xsl:template>

    <!-- ExponentialE -->
    <xsl:template match="char[mt_code_value='0xED11' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2147;</xsl:text></mo>
    </xsl:template>

    <!-- ExponentialE -->
    <xsl:template match="char[mt_code_value='0xED11' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2147;</xsl:text>
    </xsl:template>

    <!-- ImaginaryI -->
    <xsl:template match="char[mt_code_value='0xED12' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2148;</xsl:text></mo>
    </xsl:template>

    <!-- ImaginaryI -->
    <xsl:template match="char[mt_code_value='0xED12' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2148;</xsl:text>
    </xsl:template>

    <!-- ImaginaryJ -->
    <xsl:template match="char[mt_code_value='0xED13' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2149;</xsl:text></mo>
    </xsl:template>

    <!-- ImaginaryJ -->
    <xsl:template match="char[mt_code_value='0xED13' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2149;</xsl:text>
    </xsl:template>

    <!-- CapitalDifferentialD -->
    <xsl:template match="char[mt_code_value='0xED16' and variation='mathmode']">
        <mo><xsl:text disable-output-escaping="yes">&amp;#x2145;</xsl:text></mo>
    </xsl:template>

    <!-- CapitalDifferentialD -->
    <xsl:template match="char[mt_code_value='0xED16' and variation='textmode']">
        <xsl:text disable-output-escaping="yes">&amp;#x2145;</xsl:text>
    </xsl:template>





    <!-- xsl/embellishment.xsl -->


    <!--
    edot        = "<(ns)mover accent='true'>$+$n#$n<(ns)mo>&$#x02D9;</(ns)mo>$-$n</(ns)mover>$n";
    edot/2      = "<(ns)mover accent='true'>$+$n#$n<(ns)mo>&$#x00A8;</(ns)mo>$-$n</(ns)mover>$n";
    edot/3      = "<(ns)mover accent='true'>$+$n#$n<(ns)mo>&$#x20DB;</(ns)mo>$-$n</(ns)mover>$n";
    edot/4      = "<(ns)mover accent='true'>$+$n#$n<(ns)mo>&$#x20DC;</(ns)mo>$-$n</(ns)mover>$n";

    2 => "emb1DOT", # over single dot
    3 => "emb2DOT", # over double dot
    4 => "emb3DOT", # over triple dot
    24 => "emb4DOT", # over quad dot
    -->
    <!-- Dots -->

    <xsl:template match="embell[embell='emb1DOT']">
        <mover accent="true">
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#x2D9;</mo>
        </mover>
    </xsl:template>


    <xsl:template match="embell[embell='emb2DOT']">
        <mover accent="true">
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#xA8;</mo>
        </mover>
    </xsl:template>

    <xsl:template match="embell[embell='emb3DOT']">
        <mover accent="true">
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#x20DB;</mo>
        </mover>
    </xsl:template>

    <xsl:template match="embell[embell='emb4DOT']">
        <mover accent="true">
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#x20DC;</mo>
        </mover>
    </xsl:template>

    <!--
    edot/u      = "<(ns)munder>$+$n#$n<(ns)mo>&$#x02D9;</(ns)mo>$-$n</(ns)munder>$n";
    edot/u/2    = "<(ns)munder>$+$n#$n<(ns)mo>&$#x00A8;</(ns)mo>$-$n</(ns)munder>$n";
    edot/u/3    = "<(ns)munder>$+$n#$n<(ns)mo>&$#x20DB;</(ns)mo>$-$n</(ns)munder>$n";
    edot/u/4    = "<(ns)munder>$+$n#$n<(ns)mo>&$#x20DC;</(ns)mo>$-$n</(ns)munder>$n";

    25 => "embU_1DOT", # under single dot
    26 => "embU_2DOT", # under double dot
    27 => "embU_3DOT", # under triple dot
    28 => "embU_4DOT", # under quad dot
    -->
    <!-- Under-dots -->

    <xsl:template match="embell[embell='embU_1DOT']">
        <munder>
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#x2D9;</mo>
        </munder>
    </xsl:template>


    <xsl:template match="embell[embell='embU_2DOT']">
        <munder>
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#xA8;</mo>
        </munder>
    </xsl:template>


    <xsl:template match="embell[embell='embU_3DOT']">
        <munder>
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#x20DB;</mo>
        </munder>
    </xsl:template>


    <xsl:template match="embell[embell='embU_4DOT']">
        <munder>
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#x20DC;</mo>
        </munder>
    </xsl:template>

    <!--
    eprime      = "<(ns)msup>$+$n#$n<(ns)mo>&$#x2032;</(ns)mo>$-$n</(ns)msup>$n";
    eprime/2    = "<(ns)msup>$+$n#$n<(ns)mo>&$#x2033;</(ns)mo>$-$n</(ns)msup>$n";
    eprime/3    = "<(ns)msup>$+$n#$n<(ns)mo>&$#x2034;</(ns)mo>$-$n</(ns)msup>$n";
    eprime/b    = "<(ns)mmultiscripts>$+$n#$n<(ns)mprescripts/>$n<(ns)none/>$n<(ns)mo>&$#x2035;</(ns)mo>$-$n</(ns)mmultiscripts>$n";

    5 => "emb1PRIME", # single prime
    6 => "emb2PRIME", # double prime
    7 => "embBPRIME", # backwards prime (left of character)
    18 => "emb3PRIME", # triple prime
     -->
    <!-- Primes -->

    <xsl:template match="embell[embell='emb1PRIME']">
        <msup>
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#x2032;</mo>
        </msup>
    </xsl:template>


    <xsl:template match="embell[embell='emb2PRIME']">
        <msup>
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#x2033;</mo>
        </msup>
    </xsl:template>


    <xsl:template match="embell[embell='emb3PRIME']">
        <msup>
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#x2034;</mo>
        </msup>
    </xsl:template>


    <xsl:template match="embell[embell='embBPRIME']">
        <mmultiscripts>
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mprescripts/><none/>
            <mo>&#x2035;</mo>
        </mmultiscripts>
    </xsl:template>

    <!--
    etilde      = "<(ns)mover accent='true'>$+$n#$n<(ns)mo>&$#x02DC;</(ns)mo>$-$n</(ns)mover>$n";
    etilde/u    = "<(ns)munder accentunder='true'>$+$n#$n<(ns)mo>&$#x02DC;</(ns)mo>$-$n</(ns)munder>$n";

    8 => "embTILDE", #  tilde
    30 => "embU_TILDE", #  under tilde (~)
    -->
    <!-- Tilde -->

    <xsl:template match="embell[embell='embTILDE']">
        <mover accent="true">
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#x2DC;</mo>
        </mover>
    </xsl:template>

    <xsl:template match="embell[embell='embU_TILDE']">
        <munder accentunder="true">
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#x2DC;</mo>
        </munder>
    </xsl:template>

    <!--
    ehat        = "<(ns)mover accent='true'>$+$n#$n<(ns)mo>&$#x005E;</(ns)mo>$-$n</(ns)mover>$n";
    9 => "embHAT", #  hat (circumflex)
    -->
    <!-- Hat -->

    <xsl:template match="embell[embell='embHAT']">
        <mover accent="true">
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>^</mo>
        </mover>
    </xsl:template>
    <!--
    evec        = "<(ns)mover accent='true'>$+$n#$n<(ns)mo>&$#x2192;</(ns)mo>$-$n</(ns)mover>$n";
    evec/l      = "<(ns)mover accent='true'>$+$n#$n<(ns)mo>&$#x2190;</(ns)mo>$-$n</(ns)mover>$n";
    evec/lr     = "<(ns)mover accent='true'>$+$n#$n<(ns)mo>&$#x2194;</(ns)mo>$-$n</(ns)mover>$n";
    evec/h      = "<(ns)mover accent='true'>$+$n#$n<(ns)mo>&$#x21C0;</(ns)mo>$-$n</(ns)mover>$n";
    evec/h/l    = "<(ns)mover accent='true'>$+$n#$n<(ns)mo>&$#x21BC;</(ns)mo>$-$n</(ns)mover>$n";
    evec/u      = "<(ns)munder>$+$n#$n<(ns)mo>&$#x2192;</(ns)mo>$-$n</(ns)munder>$n";
    evec/u/l    = "<(ns)munder>$+$n#$n<(ns)mo>&$#x2190;</(ns)mo>$-$n</(ns)munder>$n";
    evec/u/lr   = "<(ns)munder>$+$n#$n<(ns)mo>&$#x2194;</(ns)mo>$-$n</(ns)munder>$n";
    evec/u/h    = "<(ns)munder>$+$n#$n<(ns)mo>&$#x21C1;</(ns)mo>$-$n</(ns)munder>$n";
    evec/u/h/l  = "<(ns)munder>$+$n#$n<(ns)mo>&$#x21BD;</(ns)mo>$-$n</(ns)munder>$n";

    11 => "embRARROW", # over right arrow
    12 => "embLARROW", # over left arrow
    13 => "embBARROW", # over both arrow (left and right)
    14 => "embR1ARROW", #  over right single-barbed arrow
    15 => "embL1ARROW", #  over left single-barbed arrow
    33 => "embU_RARROW", # under right arrow
    34 => "embU_LARROW", # under left arrow
    35 => "embU_BARROW", # under both arrow (left and right)
    36 => "embU_R1ARROW", #  under right arrow (1 barb)
    37 => "embU_L1ARROW", #  under left arrow (1 barb)

    -->
    <!-- Arrows -->

    <xsl:template match="embell[embell='embRARROW']">
        <mover accent="true">
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#x2192;</mo>
        </mover>
    </xsl:template>

    <xsl:template match="embell[embell='embLARROW']">
        <mover accent="true">
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#x2190;</mo>
        </mover>
    </xsl:template>

    <xsl:template match="embell[embell='embBARROW']">
        <mover accent="true">
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#x2194;</mo>
        </mover>
    </xsl:template>

    <xsl:template match="embell[embell='embR1ARROW']">
        <mover accent="true">
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#x21C0;</mo>
        </mover>
    </xsl:template>


    <xsl:template match="embell[embell='embL1ARROW']">
        <mover accent="true">
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#x21BC;</mo>
        </mover>
    </xsl:template>


    <xsl:template match="embell[embell='embU_RARROW']">
        <munder>
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#x2192;</mo>
        </munder>
    </xsl:template>


    <xsl:template match="embell[embell='embU_LARROW']">
        <munder>
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#x2190;</mo>
        </munder>
    </xsl:template>


    <xsl:template match="embell[embell='embU_BARROW']">
        <munder>
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#x2194;</mo>
        </munder>
    </xsl:template>


    <xsl:template match="embell[embell='embU_R1ARROW']">
        <munder>
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#x21C1;</mo>
        </munder>
    </xsl:template>


    <xsl:template match="embell[embell='embU_L1ARROW']">
        <munder>
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#x21BD;</mo>
        </munder>
    </xsl:template>

    <!--
    eobar       = "<(ns)mover accent='true'>$+$n#$n<(ns)mo>&$#x00AF;</(ns)mo>$-$n</(ns)mover>$n";
    eubar       = "<(ns)munder accentunder='true'>$+$n#$n<(ns)mo>_</(ns)mo>$-$n</(ns)munder>$n";

    17 => "embOBAR", # over-bar
    29 => "embU_BAR", #  under bar
     -->
    <!-- Bars -->

    <xsl:template match="embell[embell='embOBAR']">
        <mover accent="true">
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#xAF;</mo>
        </mover>
    </xsl:template>


    <xsl:template match="embell[embell='embU_BAR']">
        <munder accentunder="true">
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>_</mo>
        </munder>
    </xsl:template>

    <!--
    earc        = "<(ns)mover accent='true'>$+$n#$n<(ns)mo>&$#x2322;</(ns)mo>$-$n</(ns)mover>$n";
    earc/u      = "<(ns)munder accentunder='true'>$+$n#$n<(ns)mo>&$#x2322;</(ns)mo>$-$n</(ns)munder>$n";
    earc/s      = "<(ns)mover accent='true'>$+$n#$n<(ns)mo>&$#x2323;</(ns)mo>$-$n</(ns)mover>$n";
    earc/u/s    = "<(ns)munder accentunder='true'>$+$n#$n<(ns)mo>&$#x2323;</(ns)mo>$-$n</(ns)munder>$n";
    19 => "embFROWN", #  over-arc, concave downward
    20 => "embSMILE", #  over-arc, concave upward
    31 => "embU_FROWN", #  under arc (ends point down)
    32 => "embU_SMILE", #  under arc (ends point up)
    -->
    <!-- Arcs -->

    <xsl:template match="embell[embell='embFROWN']">
        <mover accent="true">
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#x2322;</mo>
        </mover>
    </xsl:template>

    <xsl:template match="embell[embell='embSMILE']">
        <mover accent="true">
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#x2323;</mo>
        </mover>
    </xsl:template>

    <xsl:template match="embell[embell='embU_FROWN']">
        <munder accentunder="true">
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#x2322;</mo>
        </munder>
    </xsl:template>

    <xsl:template match="embell[embell='embU_SMILE']">
        <munder accentunder="true">
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
            <mo>&#x2323;</mo>
        </munder>
    </xsl:template>

    <!--
    enot = "<(ns)menclose notation='updiagonalstrike'>$+$n#$-$n</(ns)menclose>$n";
    estrike = "<(ns)menclose notation='updiagonalstrike downdiagonalstrike'>$+$n#$-$n</(ns)menclose>$n";
    estrike/m = "<(ns)menclose notation='horizontalstrike'>$+$n#$-$n</(ns)menclose>$n";
    estrike/up = "<(ns)menclose notation='updiagonalstrike'>$+$n#$-$n</(ns)menclose>$n";
    estrike/dn = "<(ns)menclose notation='downdiagonalstrike'>$+$n#$-$n</(ns)menclose>$n";

    10 => "embNOT", #  diagonal slash through character
    21 => "embX_BARS", # double diagonal bars
    16 => "embMBAR", # mid-height horizontal bar
    22 => "embUP_BAR", # bottom-left to top-right diagonal bar
    23 => "embDOWN_BAR", # top-left to bottom-right diagonal bar
    -->
    <!-- Strikes -->

    <xsl:template match="embell[embell='embNOT']">
        <menclose notation="updiagonalstrike">
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
        </menclose>
    </xsl:template>


    <xsl:template match="embell[embell='embX_BARS']">
        <menclose notation="updiagonalstrike downdiagonalstrike">
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
        </menclose>
    </xsl:template>


    <xsl:template match="embell[embell='embMBAR']">
        <menclose notation="horizontalstrike">
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
        </menclose>
    </xsl:template>


    <xsl:template match="embell[embell='embUP_BAR']">
        <menclose notation="updiagonalstrike">
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
        </menclose>
    </xsl:template>


    <xsl:template match="embell[embell='embDOWN_BAR']">
        <menclose notation="downdiagonalstrike">
            <xsl:apply-templates select="(char | mn | mo | mtext | mi)[1]"/>
        </menclose>
    </xsl:template>


    <!-- xsl/subsup.xsl -->

    <xsl:template match="tmpl[selector='tmSUP' and not(variation='tvSU_PRECEDES')]">
        <msup>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
            <xsl:apply-templates select="slot[3] | pile[3]"/>
        </msup>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmSUB' and not(variation='tvSU_PRECEDES')]">
        <msub>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </msub>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmSUBSUP' and not(variation='tvSU_PRECEDES')]">
        <msubsup>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
            <xsl:apply-templates select="slot[3] | pile[3]"/>
        </msubsup>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmSUP' and variation='tvSU_PRECEDES']">
        <mmultiscripts>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
            <mprescripts/>
            <none/>
            <xsl:apply-templates select="slot[3] | pile[3]"/>
        </mmultiscripts>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmSUB' and variation='tvSU_PRECEDES']">
        <mmultiscripts>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
            <mprescripts/>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
            <none/>
        </mmultiscripts>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmSUBSUP' and variation='tvSU_PRECEDES']">
        <mmultiscripts>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
            <mprescripts/>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
            <xsl:apply-templates select="slot[3] | pile[3]"/>
        </mmultiscripts>
    </xsl:template>

    <!-- xsl/sum.xsl: the templates of each limit case exclude the others (changed for SharpEquations) -->

    <xsl:template match="tmpl[selector='tmSUM' and not(variation='tvBO_LOWER') and not(variation='tvBO_UPPER')]">
        <mstyle displaystyle="true">
            <mo>&#x2211;</mo>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmSUM' and variation='tvBO_SUM' and variation='tvBO_LOWER' and not(variation='tvBO_UPPER')]">
        <mstyle displaystyle="true">
            <munder>
                <mo>&#x2211;</mo>
                <xsl:apply-templates select="slot[2] | pile[2]"/>
            </munder>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmSUM' and variation='tvBO_SUM' and variation='tvBO_UPPER' and not(variation='tvBO_LOWER')]">
        <mstyle displaystyle="true">
            <mover>
                <mo>&#x2211;</mo>
                <xsl:apply-templates select="slot[3] | pile[3]"/>
            </mover>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmSUM' and variation='tvBO_SUM' and variation='tvBO_LOWER' and variation='tvBO_UPPER']">
        <mstyle displaystyle="true">
            <munderover>
                <mo>&#x2211;</mo>
                <xsl:apply-templates select="slot[2] | pile[2]"/>
                <xsl:apply-templates select="slot[3] | pile[3]"/>
            </munderover>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmSUM' and not(variation='tvBO_SUM') and variation='tvBO_LOWER' and not(variation='tvBO_UPPER')]">
        <mstyle displaystyle="true">
            <msub>
                <mo>&#x2211;</mo>
                <xsl:apply-templates select="slot[2] | pile[2]"/>
            </msub>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmSUM' and not(variation='tvBO_SUM') and variation='tvBO_UPPER' and not(variation='tvBO_LOWER')]">
        <mstyle displaystyle="true">
            <msup>
                <mo>&#x2211;</mo>
                <xsl:apply-templates select="slot[3] | pile[3]"/>
            </msup>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmSUM' and not(variation='tvBO_SUM') and variation='tvBO_LOWER' and variation='tvBO_UPPER']">
        <mstyle displaystyle="true">
            <msubsup>
                <mo>&#x2211;</mo>
                <xsl:apply-templates select="slot[2] | pile[2]"/>
                <xsl:apply-templates select="slot[3] | pile[3]"/>
            </msubsup>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <!-- Sum operator -->
    <xsl:template match="tmpl[selector='tmSUMOP']">
        <munderover>
            <mstyle mathsize="140%" displaystyle="true"><xsl:apply-templates select="slot[4] | pile[4]"/></mstyle>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
            <xsl:apply-templates select="slot[3] | pile[3]"/>
        </munderover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmSUMOP' and variation='tvBO_UPPER' and not(variation='tvBO_LOWER')]">
        <mover>
            <mstyle mathsize="140%" displaystyle="true"><xsl:apply-templates select="slot[4] | pile[4]"/></mstyle>
            <xsl:apply-templates select="slot[3] | pile[3]"/>
        </mover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmSUMOP' and variation='tvBO_LOWER' and not(variation='tvBO_UPPER')]">
        <munder>
            <mstyle mathsize="140%" displaystyle="true"><xsl:apply-templates select="slot[4] | pile[4]"/></mstyle>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </munder>
    </xsl:template>


    <!-- xsl/product_coproduct.xsl: the templates of each limit case exclude the others (changed for SharpEquations) -->

    <xsl:template match="tmpl[selector='tmPROD' and not(variation='tvBO_LOWER') and not(variation='tvBO_UPPER')]">
        <mstyle displaystyle="true">
            <mo>&#x220F;</mo>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmPROD' and variation='tvBO_SUM' and variation='tvBO_LOWER' and not(variation='tvBO_UPPER')]">
        <mstyle displaystyle="true">
            <munder>
                <mo>&#x220F;</mo>
                <xsl:apply-templates select="slot[2] | pile[2]"/>
            </munder>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmPROD' and variation='tvBO_SUM' and variation='tvBO_UPPER' and not(variation='tvBO_LOWER')]">
        <mstyle displaystyle="true">
            <mover>
                <mo>&#x220F;</mo>
                <xsl:apply-templates select="slot[3] | pile[3]"/>
            </mover>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmPROD' and variation='tvBO_SUM' and variation='tvBO_LOWER' and variation='tvBO_UPPER']">
        <mstyle displaystyle="true">
            <munderover>
                <mo>&#x220F;</mo>
                <xsl:apply-templates select="slot[2] | pile[2]"/>
                <xsl:apply-templates select="slot[3] | pile[3]"/>
            </munderover>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmPROD' and not(variation='tvBO_SUM') and variation='tvBO_LOWER' and not(variation='tvBO_UPPER')]">
        <mstyle displaystyle="true">
            <msub>
                <mo>&#x220F;</mo>
                <xsl:apply-templates select="slot[2] | pile[2]"/>
            </msub>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmPROD' and not(variation='tvBO_SUM') and variation='tvBO_UPPER' and not(variation='tvBO_LOWER')]">
        <mstyle displaystyle="true">
            <msup>
                <mo>&#x220F;</mo>
                <xsl:apply-templates select="slot[3] | pile[3]"/>
            </msup>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmPROD' and not(variation='tvBO_SUM') and variation='tvBO_LOWER' and variation='tvBO_UPPER']">
        <mstyle displaystyle="true">
            <msubsup>
                <mo>&#x220F;</mo>
                <xsl:apply-templates select="slot[2] | pile[2]"/>
                <xsl:apply-templates select="slot[3] | pile[3]"/>
            </msubsup>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmCOPROD' and not(variation='tvBO_LOWER') and not(variation='tvBO_UPPER')]">
        <mstyle displaystyle="true">
            <mo>&#x2210;</mo>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmCOPROD' and variation='tvBO_SUM' and variation='tvBO_LOWER' and not(variation='tvBO_UPPER')]">
        <mstyle displaystyle="true">
            <munder>
                <mo>&#x2210;</mo>
                <xsl:apply-templates select="slot[2] | pile[2]"/>
            </munder>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmCOPROD' and variation='tvBO_SUM' and variation='tvBO_UPPER' and not(variation='tvBO_LOWER')]">
        <mstyle displaystyle="true">
            <mover>
                <mo>&#x2210;</mo>
                <xsl:apply-templates select="slot[3] | pile[3]"/>
            </mover>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmCOPROD' and variation='tvBO_SUM' and variation='tvBO_LOWER' and variation='tvBO_UPPER']">
        <mstyle displaystyle="true">
            <munderover>
                <mo>&#x2210;</mo>
                <xsl:apply-templates select="slot[2] | pile[2]"/>
                <xsl:apply-templates select="slot[3] | pile[3]"/>
            </munderover>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmCOPROD' and not(variation='tvBO_SUM') and variation='tvBO_LOWER' and not(variation='tvBO_UPPER')]">
        <mstyle displaystyle="true">
            <msub>
                <mo>&#x2210;</mo>
                <xsl:apply-templates select="slot[2] | pile[2]"/>
            </msub>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmCOPROD' and not(variation='tvBO_SUM') and variation='tvBO_UPPER' and not(variation='tvBO_LOWER')]">
        <mstyle displaystyle="true">
            <msup>
                <mo>&#x2210;</mo>
                <xsl:apply-templates select="slot[3] | pile[3]"/>
            </msup>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmCOPROD' and not(variation='tvBO_SUM') and variation='tvBO_LOWER' and variation='tvBO_UPPER']">
        <mstyle displaystyle="true">
            <msubsup>
                <mo>&#x2210;</mo>
                <xsl:apply-templates select="slot[2] | pile[2]"/>
                <xsl:apply-templates select="slot[3] | pile[3]"/>
            </msubsup>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <!-- xsl/union_intersection.xsl: the templates of each limit case exclude the others (changed for SharpEquations) -->

    <xsl:template match="tmpl[selector='tmINTER' and not(variation='tvBO_LOWER') and not(variation='tvBO_UPPER')]">
        <mstyle displaystyle="true">
            <mo>&#x2229;</mo>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTER' and variation='tvBO_SUM' and variation='tvBO_LOWER' and not(variation='tvBO_UPPER')]">
        <mstyle displaystyle="true">
            <munder>
                <mo>&#x2229;</mo>
                <xsl:apply-templates select="slot[2] | pile[2]"/>
            </munder>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTER' and variation='tvBO_SUM' and variation='tvBO_UPPER' and not(variation='tvBO_LOWER')]">
        <mstyle displaystyle="true">
            <mover>
                <mo>&#x2229;</mo>
                <xsl:apply-templates select="slot[3] | pile[3]"/>
            </mover>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTER' and variation='tvBO_SUM' and variation='tvBO_LOWER' and variation='tvBO_UPPER']">
        <mstyle displaystyle="true">
            <munderover>
                <mo>&#x2229;</mo>
                <xsl:apply-templates select="slot[2] | pile[2]"/>
                <xsl:apply-templates select="slot[3] | pile[3]"/>
            </munderover>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTER' and not(variation='tvBO_SUM') and variation='tvBO_LOWER' and not(variation='tvBO_UPPER')]">
        <mstyle displaystyle="true">
            <msub>
                <mo>&#x2229;</mo>
                <xsl:apply-templates select="slot[2] | pile[2]"/>
            </msub>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTER' and not(variation='tvBO_SUM') and variation='tvBO_UPPER' and not(variation='tvBO_LOWER')]">
        <mstyle displaystyle="true">
            <msup>
                <mo>&#x2229;</mo>
                <xsl:apply-templates select="slot[3] | pile[3]"/>
            </msup>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTER' and not(variation='tvBO_SUM') and variation='tvBO_LOWER' and variation='tvBO_UPPER']">
        <mstyle displaystyle="true">
            <msubsup>
                <mo>&#x2229;</mo>
                <xsl:apply-templates select="slot[2] | pile[2]"/>
                <xsl:apply-templates select="slot[3] | pile[3]"/>
            </msubsup>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmUNION' and not(variation='tvBO_LOWER') and not(variation='tvBO_UPPER')]">
        <mstyle displaystyle="true">
            <mo>&#x222A;</mo>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmUNION' and variation='tvBO_SUM' and variation='tvBO_LOWER' and not(variation='tvBO_UPPER')]">
        <mstyle displaystyle="true">
            <munder>
                <mo>&#x222A;</mo>
                <xsl:apply-templates select="slot[2] | pile[2]"/>
            </munder>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmUNION' and variation='tvBO_SUM' and variation='tvBO_UPPER' and not(variation='tvBO_LOWER')]">
        <mstyle displaystyle="true">
            <mover>
                <mo>&#x222A;</mo>
                <xsl:apply-templates select="slot[3] | pile[3]"/>
            </mover>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmUNION' and variation='tvBO_SUM' and variation='tvBO_LOWER' and variation='tvBO_UPPER']">
        <mstyle displaystyle="true">
            <munderover>
                <mo>&#x222A;</mo>
                <xsl:apply-templates select="slot[2] | pile[2]"/>
                <xsl:apply-templates select="slot[3] | pile[3]"/>
            </munderover>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmUNION' and not(variation='tvBO_SUM') and variation='tvBO_LOWER' and not(variation='tvBO_UPPER')]">
        <mstyle displaystyle="true">
            <msub>
                <mo>&#x222A;</mo>
                <xsl:apply-templates select="slot[2] | pile[2]"/>
            </msub>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmUNION' and not(variation='tvBO_SUM') and variation='tvBO_UPPER' and not(variation='tvBO_LOWER')]">
        <mstyle displaystyle="true">
            <msup>
                <mo>&#x222A;</mo>
                <xsl:apply-templates select="slot[3] | pile[3]"/>
            </msup>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmUNION' and not(variation='tvBO_SUM') and variation='tvBO_LOWER' and variation='tvBO_UPPER']">
        <mstyle displaystyle="true">
            <msubsup>
                <mo>&#x222A;</mo>
                <xsl:apply-templates select="slot[2] | pile[2]"/>
                <xsl:apply-templates select="slot[3] | pile[3]"/>
            </msubsup>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mstyle>
    </xsl:template>

    <!-- xsl/box.xsl -->

     <!-- Boxes -->

    <xsl:template match="tmpl[selector='tmBOX' and (variation='tvBX_TOP' and variation='tvBX_LEFT')]">
        <menclose notation="top left">
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </menclose>
    </xsl:template>


    <xsl:template match="tmpl[selector='tmBOX' and (variation='tvBX_TOP' and variation='tvBX_RIGHT')]">
        <menclose notation="actuarial">
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </menclose>
    </xsl:template>


    <xsl:template match="tmpl[selector='tmBOX' and (variation='tvBX_BOTTOM' and variation='tvBX_LEFT')]">
        <menclose notation="bottom left">
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </menclose>
    </xsl:template>


    <xsl:template match="tmpl[selector='tmBOX' and (variation='tvBX_BOTTOM' and variation='tvBX_RIGHT')]">
        <menclose notation="bottom right">
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </menclose>
    </xsl:template>

     <xsl:template match="tmpl[selector='tmBOX' and variation='tvBX_TOP' and variation='tvBX_BOTTOM' and variation='tvBX_LEFT' and variation='tvBX_RIGHT']">
        <menclose notation="box">
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </menclose>
    </xsl:template>


    <!-- xsl/fence.xsl -->

    <!-- Fences -->
    <xsl:template match="tmpl[selector='tmPAREN']">
        <mrow>
            <mo>(</mo>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
            <mo>)</mo>
        </mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmPAREN' and variation='tvFENCE_L' and not(variation='tvFENCE_R')]">
        <mrow><mo>(</mo> <xsl:apply-templates select="slot[1] | pile[1]"/></mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmPAREN' and variation='tvFENCE_R' and not(variation='tvFENCE_L')]">
        <mrow> <xsl:apply-templates select="slot[1] | pile[1]"/> <mo>)</mo></mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmBRACK']">
        <mrow>
            <mo>[</mo>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
            <mo>]</mo>
        </mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmBRACE']">
        <mrow>
            <mo>{</mo>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
            <mo>}</mo>
        </mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmANGLE']">
        <mrow><mo>&#x2329;</mo> <xsl:apply-templates select="slot[1] | pile[1]"/> <mo>&#x232A;</mo></mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmANGLE' and variation='tvFENCE_L' and not(variation='tvFENCE_R')]">
        <mrow>
            <mo>&#x2329;</mo>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmANGLE' and variation='tvFENCE_R' and not(variation='tvFENCE_L')]">
        <mrow>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
            <mo>&#x232A;</mo>
        </mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmBAR']">
        <mrow><mo>|</mo> <xsl:apply-templates select="slot[1] | pile[1]"/> <mo>|</mo></mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmBAR' and variation='tvFENCE_L' and not(variation='tvFENCE_R')]">
        <mrow>
            <mo>|</mo>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmBAR' and variation='tvFENCE_R' and not(variation='tvFENCE_L')]">
        <mrow>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
            <mo>|</mo>
        </mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmFLOOR']">
        <mrow><mo>&#x230A;</mo> <xsl:apply-templates select="slot[1] | pile[1]"/> <mo>&#x230B;</mo></mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmCEILING']">
        <mrow><mo>&#x2308;</mo> <xsl:apply-templates select="slot[1] | pile[1]"/> <mo>&#x2309;</mo></mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTERVAL' and variation='tvINTV_LBLB']">
        <mrow> <mo>[</mo> <xsl:apply-templates select="slot[1] | pile[1]"/> <mo>[</mo> </mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTERVAL' and variation='tvINTV_RBRB']">
        <mrow><mo>]</mo> <xsl:apply-templates select="slot[1] | pile[1]"/> <mo>]</mo></mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTERVAL' and variation='tvINTV_RBLB']">
        <mrow><mo>]</mo> <xsl:apply-templates select="slot[1] | pile[1]"/> <mo>[</mo></mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTERVAL' and variation='tvINTV_LBRP']">
        <mrow><mo>[</mo> <xsl:apply-templates select="slot[1] | pile[1]"/> <mo>)</mo></mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmINTERVAL' and variation='tvINTV_LPRB']">
        <mrow><mo>(</mo> <xsl:apply-templates select="slot[1] | pile[1]"/> <mo>]</mo></mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmBRACK' and variation='tvFENCE_L' and not(variation='tvFENCE_R')]">
        <mrow> <mo>[</mo> <xsl:apply-templates select="slot[1] | pile[1]"/> </mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmBRACK' and variation='tvFENCE_R' and not(variation='tvFENCE_L')]">
        <mrow><xsl:apply-templates select="slot[1] | pile[1]"/> <mo>]</mo></mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmBRACE' and variation='tvFENCE_L' and not(variation='tvFENCE_R')]">
        <mrow><mo>{</mo> <xsl:apply-templates select="slot[1] | pile[1]"/> </mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmBRACE' and variation='tvFENCE_R' and not(variation='tvFENCE_L')]">
        <mrow><xsl:apply-templates select="slot[1] | pile[1]"/> <mo>}</mo></mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmDIRAC']">
        <mrow>
            <mo>&#x2329;</mo>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
            <mo>|</mo>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
            <mo>&#x232A;</mo>
        </mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmDIRAC' and variation='tvDI_RIGHT' and not(variation='tvDI_LEFT')]">
        <mrow>
            <mo>|</mo>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
            <mo>&#x232A;</mo>
        </mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmDIRAC' and variation='tvDI_LEFT' and not(variation='tvDI_RIGHT')]">
        <mrow>
            <mo>&#x2329;</mo>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
            <mo>|</mo>
        </mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmDBAR']">
        <mrow><mo>&#x2016;</mo> <xsl:apply-templates select="slot[1] | pile[1]"/> <mo>&#x2016;</mo></mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmDBAR' and variation='tvFENCE_L' and not(variation='tvFENCE_R')]">
        <mrow>
            <mo>&#x2016;</mo>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmDBAR' and variation='tvFENCE_R' and not(variation='tvFENCE_L')]">
        <mrow>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
            <mo>&#x2016;</mo>
        </mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmOBRACK']">
        <mrow>
            <mo>&#x301A;</mo>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
            <mo>&#x301B;</mo>
        </mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmOBRACK' and variation='tvFENCE_L' and not(variation='tvFENCE_R')]">
        <mrow>
            <mo>&#x301A;</mo>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmOBRACK' and variation='tvFENCE_R' and not(variation='tvFENCE_L')]">
        <mrow>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
            <mo>&#x301B;</mo>
        </mrow>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmHBRACE']">
        <munder>
            <munder>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
                <mo stretchy="true">&#xFE38;</mo>
            </munder>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </munder>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmHBRACE' and variation='tvHB_TOP']">
        <mover>
            <mover>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
                <mo stretchy="true">&#xFE37;</mo>
            </mover>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </mover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmHBRACK']">
        <munder>
            <munder>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
                <mo stretchy="true">&#x23B5;</mo>
            </munder>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </munder>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmHBRACK' and variation='tvHB_TOP']">
        <mover>
            <mover>
                <xsl:apply-templates select="slot[1] | pile[1]"/>
                <mo stretchy="true">&#x23B4;</mo>
            </mover>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </mover>
    </xsl:template>




    <!-- xsl/arrow.xsl -->

    <!-- Arrows -->
    <xsl:template match="tmpl[selector='tmARROW']">
        <mover>
            <mo>&#x2192;</mo>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and variation='tvAR_LEFT']">
        <mover>
            <mo>&#x2190;</mo>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and variation='tvAR_BOTTOM']">
        <munder>
            <mo>&#x2192;</mo>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </munder>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and variation='tvAR_TOPBOTTOM']">
        <munderover>
            <mo>&#x2192;</mo>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </munderover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and variation='tvAR_DOUBLE']">
        <mover>
            <mo>&#x21C4;</mo>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and (variation='tvAR_LEFT' and variation='tvAR_RIGHT' )]">
        <mover>
            <mo>&#x2194;</mo>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and variation='tvAR_LEFT' and variation='tvAR_TOPBOTTOM']">
        <munderover>
            <mo>&#x2190;</mo>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </munderover>
    </xsl:template>


    <xsl:template match="tmpl[selector='tmARROW' and variation='tvAR_DOUBLE' and variation='tvAR_TOPBOTTOM']">
        <munderover>
            <mo>&#x21C4;</mo>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </munderover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and variation='tvAR_DOUBLE' and variation='tvAR_LOS']">
        <mover>
            <munder accentunder="true">
                <mstyle scriptlevel="-1"><mo>&#x2192;</mo></mstyle>
                <mstyle scriptlevel="+1"><mo>&#x2190;</mo></mstyle>
            </munder>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and variation='tvAR_DOUBLE' and variation='tvAR_SOL']">
        <mover>
            <munder accentunder="true">
                <mstyle scriptlevel="+1"><mo>&#x2192;</mo></mstyle>
                <mstyle scriptlevel="-1"><mo>&#x2190;</mo></mstyle>
            </munder>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and (variation='tvAR_DOUBLE' or variation='tvAR_HARPOON') and variation='tvAR_HARPOON']">
        <mover>
            <mo>&#x21CC;</mo>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and (variation='tvAR_LEFT' and variation='tvAR_RIGHT' ) and variation='tvAR_TOPBOTTOM']">
        <munderover>
            <mo>&#x2194;</mo>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </munderover>
    </xsl:template>


    <xsl:template match="tmpl[selector='tmARROW' and (variation='tvAR_DOUBLE' or variation='tvAR_HARPOON') and variation='tvAR_HARPOON']">
        <mover>
            <mo>&#x21CC;</mo>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and variation='tvAR_DOUBLE' and variation='tvAR_LOS' and variation='tvAR_TOPBOTTOM']">
        <munderover accentunder="true">
            <mstyle scriptlevel="-1"><mo>&#x2192;</mo></mstyle>
            <munder>
                <mstyle scriptlevel="+1"><mo>&#x2190;</mo></mstyle>
                <xsl:apply-templates select="slot[2] | pile[2]"/>
            </munder>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </munderover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and variation='tvAR_DOUBLE' and variation='tvAR_SOL' and variation='tvAR_TOPBOTTOM']">
        <munderover accentunder="true">
            <mstyle scriptlevel="+1"><mo>&#x2192;</mo></mstyle>
            <munder>
                <mstyle scriptlevel="-1"><mo>&#x2190;</mo></mstyle>
                <xsl:apply-templates select="slot[2] | pile[2]"/>
            </munder>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </munderover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and (variation='tvAR_DOUBLE' or variation='tvAR_HARPOON') and variation='tvAR_HARPOON' and variation='tvAR_SOL']">
        <mover>
            <munder accentunder="true">
                <mstyle scriptlevel="+1"><mo>&#x21C0;</mo></mstyle>
                <mstyle scriptlevel="-1"><mo>&#x21BD;</mo></mstyle>
            </munder>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and (variation='tvAR_DOUBLE' or variation='tvAR_HARPOON') and variation='tvAR_HARPOON' and variation='tvAR_LOS']">
        <mover>
            <munder accentunder="true">
                <mstyle scriptlevel="-1"><mo>&#x21C0;</mo></mstyle>
                <mstyle scriptlevel="+1"><mo>&#x21BD;</mo></mstyle>
            </munder>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </mover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and (variation='tvAR_DOUBLE' or variation='tvAR_HARPOON') and variation='tvAR_HARPOON' and variation='tvAR_TOPBOTTOM']">
        <munderover>
            <mo>&#x21CC;</mo>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </munderover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and (variation='tvAR_DOUBLE' or variation='tvAR_HARPOON') and variation='tvAR_HARPOON' and variation='tvAR_LOS' and variation='tvAR_TOPBOTTOM']">
        <munderover accentunder="true">
            <mstyle scriptlevel="-1"><mo>&#x21C0;</mo></mstyle>
            <munder>
                <mstyle scriptlevel="+1"><mo>&#x21BD;</mo></mstyle>
                <xsl:apply-templates select="slot[2] | pile[2]"/>
            </munder>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </munderover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and (variation='tvAR_DOUBLE' or variation='tvAR_HARPOON') and variation='tvAR_HARPOON' and variation='tvAR_SOL' and variation='tvAR_TOPBOTTOM']">
        <munderover accentunder="true">
            <mstyle scriptlevel="+1"><mo>&#x21C0;</mo></mstyle>
            <munder>
                <mstyle scriptlevel="-1"><mo>&#x21BD;</mo></mstyle>
                <xsl:apply-templates select="slot[2] | pile[2]"/>
            </munder>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
        </munderover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and variation='tvAR_LEFT' and variation='tvAR_BOTTOM']">
        <munder>
            <mo>&#x2190;</mo>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </munder>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and (variation='tvAR_LEFT' and variation='tvAR_RIGHT' ) and variation='tvAR_BOTTOM']">
        <munder>
            <mo>&#x2194;</mo>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </munder>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and variation='tvAR_DOUBLE' and variation='tvAR_BOTTOM']">
        <munder>
            <mo>&#x21C4;</mo>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </munder>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and variation='tvAR_DOUBLE' and variation='tvAR_LOS' and variation='tvAR_BOTTOM']">
        <munder>
            <munder accentunder="true">
                <mstyle scriptlevel="-1"><mo>&#x2192;</mo></mstyle>
                <mstyle scriptlevel="+1"><mo>&#x2190;</mo></mstyle>
            </munder>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </munder>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and variation='tvAR_DOUBLE' and variation='tvAR_SOL' and variation='tvAR_BOTTOM']">
        <munder>
            <munder accentunder="true">
                <mstyle scriptlevel="+1"><mo>&#x2192;</mo></mstyle>
                <mstyle scriptlevel="-1"><mo>&#x2190;</mo></mstyle>
            </munder>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </munder>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and (variation='tvAR_DOUBLE' or variation='tvAR_HARPOON') and variation='tvAR_HARPOON' and variation='tvAR_BOTTOM']">
        <munder>
            <mo>&#x21CC;</mo>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </munder>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and (variation='tvAR_DOUBLE' or variation='tvAR_HARPOON') and variation='tvAR_HARPOON' and variation='tvAR_LOS' and variation='tvAR_BOTTOM']">
        <munder>
            <munder accentunder="true">
                <mstyle scriptlevel="-1"><mo>&#x21C0;</mo></mstyle>
                <mstyle scriptlevel="+1"><mo>&#x21BD;</mo></mstyle>
            </munder>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </munder>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and (variation='tvAR_DOUBLE' or variation='tvAR_HARPOON') and variation='tvAR_HARPOON' and variation='tvAR_SOL' and variation='tvAR_BOTTOM']">
        <munder>
            <munder accentunder="true">
                <mstyle scriptlevel="+1"><mo>&#x21C0;</mo></mstyle>
                <mstyle scriptlevel="-1"><mo>&#x21BD;</mo></mstyle>
            </munder>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </munder>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and variation='tvAR_LEFT' and variation='tvAR_BOTTOM']">
        <munder>
            <mo>&#x2190;</mo>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </munder>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and (variation='tvAR_LEFT' and variation='tvAR_RIGHT' ) and variation='tvAR_BOTTOM']">
        <munder>
            <mo>&#x2194;</mo>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </munder>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and variation='tvAR_DOUBLE' and variation='tvAR_BOTTOM']">
        <munder>
            <mo>&#x21C4;</mo>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </munder>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and variation='tvAR_DOUBLE' and variation='tvAR_LOS' and variation='tvAR_BOTTOM']">
        <munder>
            <munder accentunder="true">
                <mstyle scriptlevel="-1"><mo>&#x2192;</mo></mstyle>
                <mstyle scriptlevel="+1"><mo>&#x2190;</mo></mstyle>
            </munder>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </munder>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and variation='tvAR_DOUBLE' and variation='tvAR_SOL' and variation='tvAR_BOTTOM']">
        <munder>
            <munder accentunder="true">
                <mstyle scriptlevel="+1"><mo>&#x2192;</mo></mstyle>
                <mstyle scriptlevel="-1"><mo>&#x2190;</mo></mstyle>
            </munder>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </munder>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and (variation='tvAR_DOUBLE' or variation='tvAR_HARPOON') and variation='tvAR_HARPOON' and variation='tvAR_BOTTOM']">
        <munder>
            <mo>&#x21CC;</mo>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </munder>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and (variation='tvAR_DOUBLE' or variation='tvAR_HARPOON') and variation='tvAR_HARPOON' and variation='tvAR_LOS' and variation='tvAR_BOTTOM']">
        <munder>
            <munder accentunder="true">
                <mstyle scriptlevel="-1"><mo>&#x21C0;</mo></mstyle>
                <mstyle scriptlevel="+1"><mo>&#x21BD;</mo></mstyle>
            </munder>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </munder>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARROW' and (variation='tvAR_DOUBLE' or variation='tvAR_HARPOON') and variation='tvAR_HARPOON' and variation='tvAR_SOL' and variation='tvAR_BOTTOM']">
        <munder>
            <munder accentunder="true">
                <mstyle scriptlevel="+1"><mo>&#x21C0;</mo></mstyle>
                <mstyle scriptlevel="-1"><mo>&#x21BD;</mo></mstyle>
            </munder>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </munder>
    </xsl:template>


    <!-- xsl/root.xsl (pull request #11) -->

     <!-- SQRT -->

     <xsl:template match="tmpl[selector='tmROOT' and variation='tvROOT_SQ']">
        <msqrt>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </msqrt>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmROOT' and variation='tvROOT_NTH']">
        <mroot>
            <xsl:apply-templates select="slot[1] | pile[1]"/>
            <xsl:apply-templates select="slot[2] | pile[2]"/>
        </mroot>
    </xsl:template>
<!-- xsl/matrix.xsl -->

    <!-- Matrices TODO -->
    <xsl:template match="matrix[h_just='left']">
        <mtable columnalign="left">
            <xsl:variable name="rows" select="number(rows)"/>
            <xsl:apply-templates select="(slot | pile)[position() mod $rows = 1]" mode="rows-left" />
        </mtable>
    </xsl:template>

    <xsl:template match="matrix[h_just='center']">
        <mtable>
            <xsl:variable name="rows" select="number(rows)"/>
            <xsl:apply-templates select="(slot | pile)[position() mod $rows = 1]" mode="rows-center" />
        </mtable>
    </xsl:template>

    <xsl:template match="matrix[h_just='right']">
        <mtable columnalign="right">
            <xsl:variable name="rows" select="number(rows)"/>
            <xsl:apply-templates select="(slot | pile)[position() mod $rows = 1]" mode="rows-right" />
        </mtable>
    </xsl:template>

    <xsl:template match="slot | pile" mode="rows-left">
        <xsl:variable name="rows" select="number(parent::*/rows)"/>
        <mtr columnalign="left">
            <xsl:apply-templates select="." mode="columns-left" />
            <xsl:apply-templates select="(following-sibling::slot | following-sibling::pile)[position() &lt; $rows]" mode="columns-left" />
        </mtr>
    </xsl:template>

    <xsl:template match="slot | pile" mode="rows-center">
        <xsl:variable name="rows" select="number(parent::*/rows)"/>
        <mtr columnalign="center">
            <xsl:apply-templates select="." mode="columns-center" />
            <xsl:apply-templates select="(following-sibling::slot | following-sibling::pile)[position() &lt; $rows]" mode="columns-center" />
        </mtr>
    </xsl:template>

    <xsl:template match="slot | pile" mode="rows-right">
        <xsl:variable name="rows" select="number(parent::*/rows)"/>
        <mtr columnalign="right">
            <xsl:apply-templates select="." mode="columns-right" />
            <xsl:apply-templates select="(following-sibling::slot | following-sibling::pile)[position() &lt; $rows]" mode="columns-right" />
        </mtr>
    </xsl:template>

    <xsl:template match="pile" mode="columns-left">
        <mtd columnalign="left">
            <xsl:apply-templates select="."/>
        </mtd>
    </xsl:template>

    <xsl:template match="pile" mode="columns-center">
        <mtd columnalign="center">
            <xsl:apply-templates select="."/>
        </mtd>
    </xsl:template>

    <xsl:template match="pile" mode="columns-right">
        <mtd columnalign="right">
            <xsl:apply-templates select="."/>
        </mtd>
    </xsl:template>

    <xsl:template match="slot" mode="columns-left">
        <mtd columnalign="left">
            <xsl:apply-templates />
        </mtd>
    </xsl:template>

    <xsl:template match="slot" mode="columns-center">
        <mtd columnalign="center">
            <xsl:apply-templates />
        </mtd>
    </xsl:template>

    <xsl:template match="slot" mode="columns-right">
        <mtd columnalign="right">
            <xsl:apply-templates />
        </mtd>
    </xsl:template>

    <!-- xsl/long_embellishment.xsl -->

     <!-- Long embellishments -->

     <xsl:template match="tmpl[selector='tmTILDE']">
        <mover accent="true">
            <xsl:apply-templates select="(slot | pile)[1]"/>
            <mo stretchy="true">&#x2DC;</mo>
        </mover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmARC']">
        <mover accent="true">
            <xsl:apply-templates select="(slot | pile)[1]"/>
            <mo stretchy="true">&#x2322;</mo>
        </mover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmHAT']">
        <mover accent="true">
            <xsl:apply-templates select="(slot | pile)[1]"/>
            <mo stretchy="true">^</mo>
        </mover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmJSTATUS']">
        <mover accent="true">
            <xsl:apply-templates select="(slot | pile)[1]"/>
            <mo stretchy="true">&#xFE39;</mo>
        </mover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmOBAR']">
        <mover accent="true">
            <xsl:apply-templates select="(slot | pile)[1]"/>
            <mo stretchy="true">&#xAF;</mo>
        </mover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmOBAR' and variation='tvBAR_DOUBLE']">
        <mover accent="true">
            <mover accent="true">
                <xsl:apply-templates select="(slot | pile)[1]"/>
                <mo stretchy="true">&#xAF;</mo>
            </mover>
            <mo stretchy="true">&#xAF;</mo>
        </mover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmUBAR']">
        <munder accentunder="true">
            <xsl:apply-templates select="(slot | pile)[1]"/>
            <mo stretchy="true">_</mo>
        </munder>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmUBAR' and variation='tvBAR_DOUBLE']">
        <munder accentunder="true">
            <munder accentunder="true">
                <xsl:apply-templates select="(slot | pile)[1]"/>
                <mo stretchy="true">_</mo>
            </munder>
            <mo stretchy="true">_</mo>
        </munder>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmVEC']">
        <mover accent="true">
            <xsl:apply-templates select="(slot | pile)[1]"/>
            <mo stretchy="true">&#x2192;</mo>
        </mover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmVEC' and variation='tvVE_LEFT']">
        <mover accent="true">
            <xsl:apply-templates select="(slot | pile)[1]"/>
            <mo stretchy="true">&#x2190;</mo>
        </mover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmVEC' and (variation='tvVE_LEFT' and variation='tvVE_RIGHT' )]">
        <mover accent="true">
            <xsl:apply-templates select="(slot | pile)[1]"/>
            <mo stretchy="true">&#x2194;</mo>
        </mover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmVEC' and variation='tvVE_HARPOON']">
        <mover accent="true">
            <xsl:apply-templates select="(slot | pile)[1]"/>
            <mo stretchy="true">&#x21C0;</mo>
        </mover>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmVEC' and variation='tvVE_UNDER']">
        <munder accentunder="true">
            <xsl:apply-templates select="(slot | pile)[1]"/>
            <mo stretchy="true">&#x2192;</mo>
        </munder>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmVEC' and variation='tvVE_UNDER' and variation='tvVE_LEFT']">
        <munder accentunder="true">
            <xsl:apply-templates select="(slot | pile)[1]"/>
            <mo stretchy="true">&#x2190;</mo>
        </munder>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmVEC' and variation='tvVE_UNDER' and (variation='tvVE_LEFT' and variation='tvVE_RIGHT' )]">
        <munder accentunder="true">
            <xsl:apply-templates select="(slot | pile)[1]"/>
            <mo stretchy="true">&#x2194;</mo>
        </munder>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmVEC' and variation='tvVE_UNDER' and variation='tvVE_HARPOON']">
        <munder accentunder="true">
            <xsl:apply-templates select="(slot | pile)[1]"/>
            <mo stretchy="true">&#x21C1;</mo>
        </munder>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmSTRIKE']">
        <menclose notation="updiagonalstrike downdiagonalstrike">
            <xsl:apply-templates select="(slot | pile)[1]"/>
        </menclose>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmSTRIKE' and variation='tvST_HORIZ' and not(variation='tvST_UP' or variation='tvST_DOWN')]">
        <menclose notation="horizontalstrike">
            <xsl:apply-templates select="(slot | pile)[1]"/>
        </menclose>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmSTRIKE' and variation='tvST_UP' and not(variation='tvST_HORIZ' or variation='tvST_DOWN')]">
        <menclose notation="updiagonalstrike">
            <xsl:apply-templates select="(slot | pile)[1]"/>
        </menclose>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmSTRIKE' and variation='tvST_DOWN' and not(variation='tvST_UP' or variation='tvST_HORIZ')]">
        <menclose notation="downdiagonalstrike">
            <xsl:apply-templates select="(slot | pile)[1]"/>
        </menclose>
    </xsl:template>

    <!-- xsl/long_division.xsl -->

     <!-- Long division -->

     <xsl:template match="tmpl[selector='tmLDIV']">
        <mtable>
            <mtr>
                <mtd columnalign="right">
                    <xsl:apply-templates select="(slot | pile)[2]"/>
                </mtd>
            </mtr>
            <mtr>
                <mtd columnalign="right">
                    <menclose notation="longdiv">
                        <xsl:apply-templates select="(slot | pile)[1]"/>
                    </menclose>
                </mtd>
            </mtr>
        </mtable>
    </xsl:template>

    <xsl:template match="tmpl[selector='tmLDIV' and not(variation='tvLD_UPPER')]">
        <menclose notation="longdiv">
            <xsl:apply-templates select="(slot | pile)[1]"/>
        </menclose>
    </xsl:template>


</xsl:stylesheet>
