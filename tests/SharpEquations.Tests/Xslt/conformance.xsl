<?xml version="1.0" encoding="UTF-8"?>
<!-- What the generator compiles, in one stylesheet: Xslt/GeneratorConformanceTests runs it compiled and with
     XslCompiledTransform. Each t element is one case. -->
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:p="urn:p">
  <xsl:output method="xml"/>

  <xsl:variable name="letters" select="'abcdefghij'"/>
  <xsl:variable name="dashes">
    <xsl:call-template name="repeat">
      <xsl:with-param name="text" select="'-'"/>
      <xsl:with-param name="count" select="string-length($letters)"/>
    </xsl:call-template>
  </xsl:variable>
  <xsl:variable name="items" select="/root/item"/>
  <xsl:variable name="first" select="/root/item[1]"/>
  <xsl:variable name="tree"><a>1</a><b>2</b></xsl:variable>

  <xsl:template name="repeat">
    <xsl:param name="text"/>
    <xsl:param name="count" select="0"/>
    <xsl:if test="$count &gt; 0">
      <xsl:value-of select="$text"/>
      <xsl:call-template name="repeat">
        <xsl:with-param name="text" select="$text"/>
        <xsl:with-param name="count" select="$count - 1"/>
      </xsl:call-template>
    </xsl:if>
  </xsl:template>

  <xsl:template name="describe">
    <xsl:param name="node" select="."/>
    <xsl:param name="label"/>
    <xsl:param name="mixed" select="'default'"/>
    <d label="{$label}" name="{name($node)}" id="{$node/@id}" count="{count($node)}">
      <xsl:if test="$mixed">
        <xsl:attribute name="mixed"><xsl:value-of select="$mixed"/></xsl:attribute>
      </xsl:if>
      <xsl:if test="$mixed = 'x'">is-x</xsl:if>
      <xsl:if test="$mixed &gt; 2">gt-2</xsl:if>
      <xsl:value-of select="$node/name"/>
    </d>
  </xsl:template>

  <xsl:template name="context">
    <xsl:param name="given" select="name"/>
    <c at="{position()}" of="{last()}" name="{name()}" given="{$given}"/>
  </xsl:template>

  <xsl:template name="nodes">
    <xsl:param name="set" select="/.."/>
    <xsl:param name="frag"/>
    <n count="{count($set)}" first="{$set/name}" second="{$set[2]/name}" last="{$set[last()]/name}" frag="{$frag}">
      <xsl:for-each select="$set/v">
        <xsl:value-of select="."/>
        <xsl:if test="position() != last()">,</xsl:if>
      </xsl:for-each>
      <xsl:if test="$frag">!</xsl:if>
      <xsl:copy-of select="$frag"/>
    </n>
  </xsl:template>

  <xsl:template match="/">
    <out>
      <xsl:apply-templates select="root"/>
    </out>
  </xsl:template>

  <xsl:template match="root">
    <xsl:variable name="second" select="item[2]"/>
    <xsl:variable name="none" select="item[99]"/>
    <xsl:variable name="all" select="item | p:item"/>
    <xsl:variable name="text">
      <xsl:choose>
        <xsl:when test="@a = 1">one</xsl:when>
        <xsl:otherwise>other</xsl:otherwise>
      </xsl:choose>
    </xsl:variable>
    <xsl:variable name="empty"><xsl:if test="@a = 2">x</xsl:if></xsl:variable>
    <xsl:variable name="number" select="count(item) * 2"/>

    <t n="child"><xsl:value-of select="item/name"/></t>
    <t n="child-last"><xsl:value-of select="item[last()]/name"/></t>
    <t n="child-2"><xsl:value-of select="item[2]/name"/></t>
    <t n="child-pos"><xsl:value-of select="count(item[position() &gt; 1])"/></t>
    <t n="child-last-1"><xsl:value-of select="item[last() - 1]/@id"/></t>
    <t n="two-predicates"><xsl:value-of select="item[v][last()]/@id"/>,<xsl:value-of select="item[last()][v]/@id"/>,<xsl:value-of select="item[@k][2]/@id"/></t>
    <t n="three-predicates"><xsl:value-of select="*[name][position() &gt; 1][last()]/@id"/>,<xsl:value-of select="*[name][position() &lt; last()][2]/@id"/></t>
    <t n="attribute"><xsl:value-of select="@b"/>|<xsl:value-of select="count(@*)"/>|<xsl:value-of select="item/@id"/>|<xsl:value-of select="count(item/@*)"/></t>
    <t n="descendant"><xsl:value-of select="count(.//v)"/>|<xsl:value-of select=".//v[1]"/>|<xsl:value-of select="count(.//v[1])"/>|<xsl:value-of select="count(descendant::sub)"/></t>
    <t n="absolute"><xsl:value-of select="count(//v)"/>|<xsl:value-of select="/root/@a"/>|<xsl:value-of select="count(/)"/>|<xsl:value-of select="name(/*)"/></t>
    <t n="nested-descendant"><xsl:value-of select="count(.//sub//v)"/>|<xsl:value-of select="count(.//sub/v)"/>|<xsl:value-of select=".//sub//v"/></t>
    <t n="parent"><xsl:value-of select="count(item/..)"/>|<xsl:value-of select="name(item/v/..)"/>|<xsl:value-of select="count(.//v/..)"/>|<xsl:value-of select="count(..)"/></t>
    <t n="union"><xsl:value-of select="count(item | p:item)"/>|<xsl:value-of select="name((p:item | item)[1])"/>|<xsl:value-of select="count(item | item)"/>|<xsl:value-of select="(item/v | item/name)[1]"/></t>
    <t n="namespace"><xsl:value-of select="count(p:*)"/>|<xsl:value-of select="name(p:item)"/>|<xsl:value-of select="local-name(p:item)"/>|<xsl:value-of select="namespace-uri(p:item)"/>|<xsl:value-of select="namespace-uri(item)"/></t>
    <t n="node-tests"><xsl:value-of select="count(node())"/>|<xsl:value-of select="count(text())"/>|<xsl:value-of select="count(comment())"/>|<xsl:value-of select="count(processing-instruction())"/>|<xsl:value-of select="count(processing-instruction('pi'))"/>|<xsl:value-of select="count(*)"/>|<xsl:value-of select="name(processing-instruction())"/></t>
    <t n="variables"><xsl:value-of select="$second/name"/>|<xsl:value-of select="count($none)"/>|<xsl:value-of select="count($all)"/>|<xsl:value-of select="$all[3]/name"/>|<xsl:value-of select="$all/name"/>|<xsl:value-of select="count($all/v)"/>|<xsl:value-of select="$items[last()]/@id"/>|<xsl:value-of select="$first/@id"/></t>
    <t n="booleans"><xsl:value-of select="boolean($second)"/>|<xsl:value-of select="boolean($none)"/>|<xsl:value-of select="boolean($text)"/>|<xsl:value-of select="boolean($empty)"/>|<xsl:value-of select="boolean(string($empty))"/>|<xsl:value-of select="not(item)"/>|<xsl:value-of select="boolean($number)"/>|<xsl:value-of select="boolean(0)"/></t>
    <t n="compare-string"><xsl:value-of select="item/name = 'beta'"/>|<xsl:value-of select="item/name != 'beta'"/>|<xsl:value-of select="'beta' = item/name"/>|<xsl:value-of select="$none = ''"/>|<xsl:value-of select="$none != ''"/>|<xsl:value-of select="$text = 'one'"/>|<xsl:value-of select="$empty = ''"/></t>
    <t n="compare-number"><xsl:value-of select="item/v &gt; 4"/>|<xsl:value-of select="item/v &lt; 0"/>|<xsl:value-of select="4 &lt; item/v"/>|<xsl:value-of select="item/v = 5"/>|<xsl:value-of select=".//v &lt;= -1.5"/>|<xsl:value-of select="item/name &gt; 0"/>|<xsl:value-of select="item/v &gt;= '5'"/></t>
    <t n="compare-boolean"><xsl:value-of select="item = true()"/>|<xsl:value-of select="$none = false()"/>|<xsl:value-of select="$none != true()"/>|<xsl:value-of select="true() = $text"/>|<xsl:value-of select="item &gt; false()"/></t>
    <t n="compare-nodes"><xsl:value-of select="item/v = .//sub/v"/>|<xsl:value-of select="item/@id = item/v"/>|<xsl:value-of select="item/@id != item/@id"/>|<xsl:value-of select="$second/v &gt; $first/v"/>|<xsl:value-of select="$none = $none"/></t>
    <t n="numbers"><xsl:value-of select="sum(.//v)"/>|<xsl:value-of select="$number div 4"/>|<xsl:value-of select="7 mod 3"/>|<xsl:value-of select="-$number"/>|<xsl:value-of select="number('x')"/>|<xsl:value-of select="1 div 0"/>|<xsl:value-of select="round(2.5)"/>|<xsl:value-of select="floor(-1.5)"/>|<xsl:value-of select="ceiling(1.2)"/>|<xsl:value-of select="number(item/v) + 1"/>|<xsl:value-of select="0.1 + 0.2"/>|<xsl:value-of select="1000000 * 1000000"/></t>
    <t n="strings"><xsl:value-of select="concat(@a, '-', @b)"/>|<xsl:value-of select="substring('abcdef', 2, 3)"/>|<xsl:value-of select="substring('abcdef', 0)"/>|<xsl:value-of select="substring('abcdef', 1.5, 2.6)"/>|<xsl:value-of select="substring-before(@b, 'w')"/>|<xsl:value-of select="substring-after(@b, 'w')"/>|<xsl:value-of select="string-length(@b)"/>|<xsl:value-of select="normalize-space('  a   b ')"/>|<xsl:value-of select="starts-with(@b, 'tw')"/>|<xsl:value-of select="contains(@b, 'x')"/></t>
    <t n="translate"><xsl:value-of select="translate(@b, 'two', 'TW')"/>|<xsl:value-of select="translate('bad', $letters, $dashes)"/>|<xsl:value-of select="translate(item/name, 'abcdefghijklmnopqrstuvwxyz', 'ABCDEFGHIJKLMNOPQRSTUVWXYZ')"/>|<xsl:value-of select="translate('aabb', 'aa', 'xy')"/></t>
    <t n="globals"><xsl:value-of select="$dashes"/>|<xsl:value-of select="count($items)"/>|<xsl:value-of select="$tree"/>|<xsl:value-of select="boolean($tree)"/></t>
    <t n="copies"><xsl:copy-of select="$tree"/><xsl:copy-of select="$text"/><xsl:copy-of select="$empty"/><xsl:copy-of select="$number"/><xsl:copy-of select="item[2]/v"/><xsl:copy-of select="'s'"/></t>
    <t n="copy-attribute"><xsl:copy-of select="$second/@k"/><xsl:copy-of select="@*"/></t>
    <t n="for-each">
      <xsl:for-each select="item">
        <xsl:value-of select="position()"/>/<xsl:value-of select="last()"/>:<xsl:value-of select="name"/>;
      </xsl:for-each>
      <xsl:for-each select="item/v"><xsl:value-of select="position()"/>=<xsl:value-of select="."/>;</xsl:for-each>
      <xsl:for-each select=".//v"><xsl:value-of select="."/> </xsl:for-each>
      <xsl:for-each select="$all"><xsl:value-of select="@id"/><xsl:value-of select="count(current()/v)"/> </xsl:for-each>
      <xsl:for-each select="item[1]/ancestor-or-self::*"><xsl:value-of select="name()"/><xsl:value-of select="position()"/> </xsl:for-each>
      <xsl:for-each select="item/v/.."><xsl:value-of select="@id"/> </xsl:for-each>
      <xsl:for-each select="@*"><xsl:value-of select="name()"/>=<xsl:value-of select="."/> </xsl:for-each>
    </t>
    <t n="calls">
      <xsl:call-template name="describe">
        <xsl:with-param name="node" select="item[2]"/>
        <xsl:with-param name="label" select="'second'"/>
        <xsl:with-param name="mixed" select="'x'"/>
      </xsl:call-template>
      <xsl:call-template name="describe">
        <xsl:with-param name="node" select="item[99]"/>
        <xsl:with-param name="label" select="$text"/>
        <xsl:with-param name="mixed" select="3"/>
      </xsl:call-template>
      <xsl:call-template name="describe">
        <xsl:with-param name="label"><xsl:value-of select="@b"/></xsl:with-param>
        <xsl:with-param name="mixed" select="item/@k"/>
      </xsl:call-template>
      <xsl:call-template name="describe">
        <xsl:with-param name="node" select="p:item"/>
        <xsl:with-param name="mixed" select="$empty"/>
      </xsl:call-template>
      <xsl:call-template name="nodes">
        <xsl:with-param name="set" select="item"/>
        <xsl:with-param name="frag"><i>in</i>side</xsl:with-param>
      </xsl:call-template>
      <xsl:call-template name="nodes">
        <xsl:with-param name="set" select="$second"/>
        <xsl:with-param name="frag" select="$empty"/>
      </xsl:call-template>
      <xsl:call-template name="nodes"/>
      <xsl:for-each select="item">
        <xsl:call-template name="context"/>
        <xsl:call-template name="context">
          <xsl:with-param name="given" select="v[9]"/>
        </xsl:call-template>
      </xsl:for-each>
    </t>
    <t n="apply">
      <xsl:apply-templates select="item | p:item" mode="row">
        <xsl:with-param name="prefix" select="'#'"/>
        <xsl:with-param name="depth" select="1"/>
      </xsl:apply-templates>
      <xsl:apply-templates select="item[2]" mode="row"/>
      <xsl:apply-templates select="item/v" mode="plain"/>
      <xsl:apply-templates select=".//sub" mode="plain"/>
      <xsl:apply-templates select="@a | comment() | processing-instruction()" mode="plain"/>
    </t>
    <t n="chars"><xsl:apply-templates select="ch"/></t>
    <t n="raw"><xsl:text disable-output-escaping="yes">&amp;#x22</xsl:text><xsl:text disable-output-escaping="yes">11;</xsl:text><xsl:value-of select="'&lt;'"/><xsl:text disable-output-escaping="yes">&lt;r a="1"&gt;x&lt;/r&gt;</xsl:text></t>
    <t n="empty"><xsl:value-of select="$empty"/></t>
    <t n="instructions">
      <xsl:element name="made"><xsl:attribute name="at">a<xsl:value-of select="@a"/></xsl:attribute></xsl:element>
      <xsl:element name="{@b}"/>
      <xsl:element name="p:made"/>
      <xsl:comment>c<xsl:value-of select="@a"/></xsl:comment>
      <xsl:processing-instruction name="target">data</xsl:processing-instruction>
      <xsl:for-each select="item[1]"><xsl:copy><xsl:copy-of select="@*"/><xsl:apply-templates select="name" mode="copy"/></xsl:copy></xsl:for-each>
    </t>
    <t n="siblings"><xsl:apply-templates select="item/v | .//sub/v" mode="siblings"/></t>
  </xsl:template>

  <xsl:template match="item" mode="row">
    <xsl:param name="prefix"/>
    <xsl:param name="depth" select="0"/>
    <row at="{position()}" of="{last()}" depth="{$depth + 1}"><xsl:value-of select="$prefix"/><xsl:value-of select="name"/></row>
  </xsl:template>
  <xsl:template match="p:*" mode="row"><prow at="{position()}"><xsl:value-of select="name"/></prow></xsl:template>
  <xsl:template match="item[@k = 'y']" mode="row" priority="2"><yrow><xsl:apply-templates mode="row"/></yrow></xsl:template>
  <xsl:template match="v[2]" mode="row">[second <xsl:value-of select="."/>]</xsl:template>
  <xsl:template match="v[last()]" mode="row" priority="0.4">[last <xsl:value-of select="."/>]</xsl:template>
  <xsl:template match="sub/v" mode="plain">(sub <xsl:value-of select="."/>)</xsl:template>
  <xsl:template match="root//sub/sub" mode="plain">(deep)</xsl:template>
  <xsl:template match="/root/item/v" mode="plain">(top <xsl:value-of select="."/>)</xsl:template>
  <xsl:template match="@a" mode="plain">(attribute <xsl:value-of select="."/>)</xsl:template>
  <xsl:template match="comment()" mode="plain">(comment <xsl:value-of select="."/>)</xsl:template>
  <xsl:template match="processing-instruction('pi')" mode="plain">(pi <xsl:value-of select="."/>)</xsl:template>
  <xsl:template match="node()" mode="copy"><xsl:copy><xsl:apply-templates select="node()" mode="copy"/></xsl:copy></xsl:template>

  <xsl:template match="v" mode="siblings">
    <s v="{.}" before="{count(preceding-sibling::*)}" after="{count(following-sibling::*)}" previous="{preceding-sibling::*[1]}" first="{preceding-sibling::*[last()]}"
       next="{following-sibling::*[1]}" before-v="{preceding-sibling::v}" ancestors="{count(ancestor::*)}" nearest="{name(ancestor::*[1])}" far="{name(ancestor::*[last()])}"
       top="{name(ancestor::*)}" self="{count(ancestor-or-self::v)}" in-sub="{boolean(ancestor::sub)}" self-test="{count(self::v)}{count(self::name)}{count(self::node())}"/>
  </xsl:template>

  <xsl:template match="ch">[other <xsl:value-of select="code"/>]</xsl:template>
  <xsl:template match="ch[mode = 'm']">[m <xsl:value-of select="code"/>]</xsl:template>
  <xsl:template match="ch[code = 'c01' and mode = 'm']">A</xsl:template>
  <xsl:template match="ch[code = 'c01' and mode = 't']">a</xsl:template>
  <xsl:template match="ch[code = 'c02']">B</xsl:template>
  <xsl:template match="ch[code = 'c03' and mode = 'm']">C</xsl:template>
  <xsl:template match="ch[code = 'c04']">D</xsl:template>
  <xsl:template match="ch[code = 'c05']">E</xsl:template>
  <xsl:template match="ch[@plain]">[plain <xsl:value-of select="code"/>]</xsl:template>
  <xsl:template match="ch[code = 'c06']">F</xsl:template>
  <xsl:template match="ch[code = 'c07']">G</xsl:template>
  <xsl:template match="ch[code = 'c08']">H</xsl:template>
  <xsl:template match="ch[code = 'c02' and mode = 't']">b</xsl:template>
  <xsl:template match="ch[code = 'c09']">I</xsl:template>
</xsl:stylesheet>
