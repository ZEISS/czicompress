<?xml version="1.0" encoding="utf-8"?>
<!--
SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
SPDX-License-Identifier: GPL-3.0-or-later
-->
<xsl:stylesheet
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0" exclude-result-prefixes="xsl wix"
    xmlns:wix="http://wixtoolset.org/schemas/v4/wxs"
    xmlns="http://wixtoolset.org/schemas/v4/wxs">

    <xsl:output method="xml" indent="yes" omit-xml-declaration="yes" />

    <xsl:strip-space elements="*" />

    <xsl:key name="FilterExe" match="wix:Component[ substring( wix:File/@Source, string-length( wix:File/@Source ) - 3 ) = '.exe' ]" use="@Id" />
    <xsl:key name="FilterPdf" match="wix:Component[ substring( wix:File/@Source, string-length( wix:File/@Source ) - 3 ) = '.pdf' ]" use="@Id" />

    <!-- Copy all elements and attributes -->
    <xsl:template match="@*|node()">
        <xsl:copy>
            <xsl:apply-templates select="@*|node()" />
        </xsl:copy>
    </xsl:template>

    <!-- Skip everything that matches our filters -->
    <xsl:template match="*[ self::wix:Component or self::wix:ComponentRef ][ key( 'FilterExe', @Id ) ]" />
    <xsl:template match="*[ self::wix:Component or self::wix:ComponentRef ][ key( 'FilterPdf', @Id ) ]" />
</xsl:stylesheet>