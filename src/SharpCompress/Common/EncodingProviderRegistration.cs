using System.Runtime.CompilerServices;
using System.Text;

namespace SharpCompress.Common;

/// <summary>
/// Registers <see cref="CodePagesEncodingProvider"/> so that legacy code pages (e.g. 437, 866)
/// used by archive headers are resolvable via <see cref="Encoding.GetEncoding(int)"/>.
/// </summary>
/// <remarks>
/// <para>
/// This runs from a module initializer rather than a static constructor. Registration must happen
/// before <em>any</em> encoding lookup, including lookups a caller performs itself while building an
/// <see cref="ArchiveEncoding"/> — for example <c>Encoding.GetEncoding(866)</c>. A static constructor
/// only fires when its own type is first touched, which made registration order-dependent and caused
/// <see cref="System.NotSupportedException"/> for callers that resolved a code page before touching
/// any other SharpCompress type.
/// </para>
/// <para>
/// .NET Framework resolves these code pages natively, so registration is only needed elsewhere.
/// </para>
/// </remarks>
internal static class EncodingProviderRegistration
{
#if !NETFRAMEWORK
    // CA2255 discourages [ModuleInitializer] in libraries because load-time work can surprise consumers.
    // Registering an encoding provider is the exception it does not account for: the registration has to be
    // in place before the first Encoding.GetEncoding call, and that call may be made by the consumer before
    // it touches any SharpCompress type. Every lazier trigger reintroduces the ordering bug. Callers who need
    // registration to be explicit (e.g. to keep it trimmable) can call RegisterCodePagesProvider themselves.
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Initialize() => RegisterCodePagesProvider();

    private static bool _registered;

    /// <summary>
    /// Registers the code pages provider if it has not already been registered. Idempotent.
    /// </summary>
    internal static void RegisterCodePagesProvider()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
#endif
}
