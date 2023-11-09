// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests;

using System.Collections.Generic;
using System.Reflection;
using global::FluentAssertions.Equivalency;

/// <summary>
/// A selection rule that excludes all properties with a particular attribute.
/// </summary>
public class ExcludePropertiesByAttributeSelectionRule : IMemberSelectionRule
{
    private readonly Type attributeType;

    public ExcludePropertiesByAttributeSelectionRule(Type attributeType)
    {
        this.attributeType = attributeType;
    }

    public bool IncludesMembers => false;

    public IEnumerable<IMember> SelectMembers(
        INode currentNode,
        IEnumerable<IMember> selectedMembers,
        MemberSelectionContext context)
    {
        return from m in selectedMembers
               where !this.IsPropertyWithExcludedAttribute(m)
               select m;
    }

    private bool IsPropertyWithExcludedAttribute(IMember member)
    {
        var prop = member.ReflectedType.GetProperty(member.Name);
        var result = prop?.GetCustomAttributes(this.attributeType).Any() == true;
        return result;
    }
}
