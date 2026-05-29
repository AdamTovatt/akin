using System.Runtime.InteropServices;
using System.Text;
using Akin.Core.Interfaces;

namespace Akin.Core.Services
{
    /// <summary>
    /// macOS implementation of <see cref="IPowerSourceProvider"/> backed by
    /// IOKit. Calls <c>IOPSCopyPowerSourcesInfo</c> to take a snapshot of the
    /// current power sources, then <c>IOPSGetProvidingPowerSourceType</c> to
    /// ask which one is currently powering the system. The returned CFString
    /// matches one of "AC Power", "Battery Power", or "UPS Power"; we treat
    /// "Battery Power" as on-battery and everything else (including any
    /// error) as on-AC, so a broken IOKit call cannot silently disable
    /// indexing.
    /// </summary>
    internal sealed partial class MacOsPowerSourceProvider : IPowerSourceProvider
    {
        private const string IOKitFramework = "/System/Library/Frameworks/IOKit.framework/IOKit";
        private const string CoreFoundationFramework = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        // CFString.h: kCFStringEncodingUTF8.
        private const uint CFStringEncodingUtf8 = 0x08000100;

        // IOPSKeys.h: kIOPMBatteryPowerKey. The string IOKit hands back is
        // one of a small fixed set; this is the only value we care about.
        private const string BatteryPowerSourceType = "Battery Power";

        public Task<bool> IsOnBatteryAsync(CancellationToken cancellationToken = default)
        {
            // The IOKit calls below are cheap synchronous functions; wrap in
            // Task.FromResult so the interface stays async-friendly without
            // promising real async IO.
            return Task.FromResult(ReadProvidingSourceType() == BatteryPowerSourceType);
        }

        private static string? ReadProvidingSourceType()
        {
            IntPtr blob = IntPtr.Zero;
            try
            {
                blob = IOPSCopyPowerSourcesInfo();
                if (blob == IntPtr.Zero) return null;

                // IOPSGetProvidingPowerSourceType follows the CoreFoundation
                // "Get" rule: the returned CFStringRef is owned by `blob`, so
                // we must not CFRelease it and we must finish reading it
                // before the finally block releases the blob.
                IntPtr typeRef = IOPSGetProvidingPowerSourceType(blob);
                if (typeRef == IntPtr.Zero) return null;

                return CfStringToManaged(typeRef);
            }
            catch
            {
                // An unreachable or broken IOKit must not throw past the
                // interface boundary — the auto-pause logic treats "unknown"
                // as "on AC", which is the safer default for a desktop.
                return null;
            }
            finally
            {
                if (blob != IntPtr.Zero) CFRelease(blob);
            }
        }

        private static string? CfStringToManaged(IntPtr cfString)
        {
            // 256 bytes is comfortably larger than any IOKit power-source
            // type string ("AC Power", "Battery Power", "UPS Power", ...).
            byte[] buffer = new byte[256];
            if (!CFStringGetCString(cfString, buffer, buffer.Length, CFStringEncodingUtf8))
                return null;

            int length = Array.IndexOf(buffer, (byte)0);
            if (length < 0) length = buffer.Length;
            return Encoding.UTF8.GetString(buffer, 0, length);
        }

        [LibraryImport(IOKitFramework)]
        private static partial IntPtr IOPSCopyPowerSourcesInfo();

        [LibraryImport(IOKitFramework)]
        private static partial IntPtr IOPSGetProvidingPowerSourceType(IntPtr blob);

        [LibraryImport(CoreFoundationFramework)]
        private static partial void CFRelease(IntPtr cf);

        [LibraryImport(CoreFoundationFramework)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static partial bool CFStringGetCString(
            IntPtr cfString,
            [Out] byte[] buffer,
            nint bufferSize,
            uint encoding);
    }
}
