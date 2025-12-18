using System.Threading;

namespace SharpCompress.Compressors.ZStandard;

internal static unsafe class SynchronizationWrapper
{
    private static object UnwrapObject(void** obj) => UnmanagedObject.Unwrap<object>(*obj);

    public static void Init(void** obj) => *obj = UnmanagedObject.Wrap(new object());

    public static void Free(void** obj) => UnmanagedObject.Free(*obj);

    public static void Enter(void** obj) => Monitor.Enter(UnwrapObject(obj));

    public static void Exit(void** obj) => Monitor.Exit(UnwrapObject(obj));

    public static void Pulse(void** obj) => Monitor.Pulse(UnwrapObject(obj));

    public static void PulseAll(void** obj) => Monitor.PulseAll(UnwrapObject(obj));

    public static void Wait(void** mutex) => Monitor.Wait(UnwrapObject(mutex));
}
