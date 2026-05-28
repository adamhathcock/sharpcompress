// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// This file is required for init-only properties to work on older target frameworks (.NET Framework 4.8, .NET Standard 2.0)
// The IsExternalInit type is used by the compiler for records and init-only properties

#if NETFRAMEWORK || NETSTANDARD2_0 || NETSTANDARD2_1
using System.ComponentModel;

namespace System.Runtime.CompilerServices;

/// <summary>
/// Reserved to be used by the compiler for tracking metadata.
/// This class should not be used by developers in source code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class IsExternalInit { }
#endif
