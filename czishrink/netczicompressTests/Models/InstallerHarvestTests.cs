// SPDX-FileCopyrightText: 2026 Amin Khosrozadeh
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;

/// <summary>Tests that installer harvesting retains all scientific runtimes.</summary>
public class InstallerHarvestTests
{
    [Theory]
    [InlineData("SourceDir\\")]
    [InlineData("C:/publish/")]
    [InlineData("")]
    public void OnlyHandAuthoredRootFilesAreRemoved(string prefix)
    {
        string xml = $$"""
            <Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
              <Fragment><DirectoryRef Id="INSTALLFOLDER">
                <Component Id="app"><File Source="{{prefix}}CziShrink.exe" /></Component>
                <Component Id="readme"><File Source="{{prefix}}README.pdf" /></Component>
                <Component Id="other"><File Source="{{prefix}}OtherCziShrink.exe" /></Component>
                <Directory Id="tools" Name="FormatTools">
                  <Component Id="python"><File Source="{{prefix}}FormatTools\python\python.exe" /></Component>
                  <Component Id="repack"><File Source="{{prefix}}FormatTools\hdf5\h5repack.exe" /></Component>
                  <Component Id="diff"><File Source="{{prefix}}FormatTools\hdf5\h5diff.exe" /></Component>
                  <Component Id="license"><File Source="{{prefix}}FormatTools\manual.pdf" /></Component>
                </Directory>
              </DirectoryRef></Fragment>
              <Fragment><ComponentGroup Id="dependencies">
                <ComponentRef Id="app" /><ComponentRef Id="readme" /><ComponentRef Id="other" />
                <ComponentRef Id="python" /><ComponentRef Id="repack" /><ComponentRef Id="diff" /><ComponentRef Id="license" />
              </ComponentGroup></Fragment>
            </Wix>
            """;
        var transform = new XslCompiledTransform();
        transform.Load(Path.Combine(AppContext.BaseDirectory, "InstallerFilter.xslt"));
        using var reader = new StringReader(xml);
        using var input = XmlReader.Create(reader);
        using var output = new StringWriter();
        transform.Transform(input, null, output);
        var document = XDocument.Parse(output.ToString());
        XNamespace ns = "http://wixtoolset.org/schemas/v4/wxs";
        string[] expected = ["other", "python", "repack", "diff", "license"];
        document.Descendants(ns + "Component").Select(element => (string?)element.Attribute("Id")).Should().BeEquivalentTo(expected);
        document.Descendants(ns + "ComponentRef").Select(element => (string?)element.Attribute("Id")).Should().BeEquivalentTo(expected);
    }
}
