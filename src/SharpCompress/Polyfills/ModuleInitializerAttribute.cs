// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// This file is required for [ModuleInitializer] to work on older target frameworks
// (.NET Framework 4.8, .NET Standard 2.0/2.1). The attribute is recognised by the C# compiler
// by name, so supplying our own definition is enough to enable module initializers there.

#if NETFRAMEWORK || NETSTANDARD2_0 || NETSTANDARD2_1
using System.ComponentModel;

namespace System.Runtime.CompilerServices;

/// <summary>
/// Used to indicate to the compiler that a method should be called in its containing module's initializer.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
internal sealed class ModuleInitializerAttribute : Attribute { }
#endif
