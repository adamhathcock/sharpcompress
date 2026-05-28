// Compatibility shim for frameworks that don't expose MemberNotNullAttribute
#if NETSTANDARD2_1 || LEGACY_DOTNET
using System;

namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(
        AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor,
        AllowMultiple = true
    )]
    internal sealed class MemberNotNullAttribute : Attribute
    {
        public MemberNotNullAttribute(string member) { }

        public MemberNotNullAttribute(params string[] members) { }
    }
}
#endif
