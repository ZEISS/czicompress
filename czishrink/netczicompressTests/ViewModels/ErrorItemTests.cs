// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.ViewModels;

using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

using netczicompress.ViewModels;

/// <summary>
/// Tests for <see cref="ErrorItem"/>.
/// </summary>
public class ErrorItemTests
{
    /// <summary>
    /// Tests that <see cref="ErrorItem"/> is indeed immutable.
    /// </summary>
    [Fact]
    public void WritableProperties_AreInitOnly()
    {
        var writablePropertySetters = from p in WritableProperties()
                                      select p.SetMethod;

        foreach (var setMethod in writablePropertySetters)
        {
            // Get the modifiers applied to the return parameter.
            var setMethodReturnParameterModifiers = setMethod!.ReturnParameter.GetRequiredCustomModifiers();

            // Init-only properties are marked with the IsExternalInit type.
            bool isInitOnly = setMethodReturnParameterModifiers.Any(x =>
                x.FullName ==
                    "System.Runtime.CompilerServices.IsExternalInit");

            isInitOnly.Should().BeTrue();
        }
    }

    /// <summary>
    /// Tests that <see cref="ErrorItem"/> is indeed immutable.
    /// </summary>
    [Fact]
    public void WritableProperties_AreRequired()
    {
        foreach (var property in WritableProperties())
        {
            property.GetCustomAttributes(false).OfType<RequiredMemberAttribute>().Should().NotBeEmpty();
        }
    }

    [Fact]
    public void ImplementsINotifyPropertyChanged()
    {
        INotifyPropertyChanged sut = new ErrorItem { File = "foo", ErrorMessage = "bar" };
        var handler = new Mock<PropertyChangedEventHandler>(MockBehavior.Strict).Object;

        sut.PropertyChanged += handler;
        sut.ToString().Should().Be("foo : bar");
        sut.PropertyChanged -= handler;
    }

    private static IEnumerable<PropertyInfo> WritableProperties()
    {
        return typeof(ErrorItem).GetProperties().Where(p => p.CanWrite);
    }
}
