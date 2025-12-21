using System;
using System.Runtime.InteropServices;

namespace PatchGUIlite.Core
{
    internal static class NativeMethods
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void T3ppLogCallback(
            int level,
            [MarshalAs(UnmanagedType.LPWStr)] string msg);

        private const string NativeDll =
#if DEBUG
            "T3ppNativelite_Debug_x64.dll";
#else
            "T3ppNativelite_Release_x64.dll";
#endif

        [DllImport(NativeDll,
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Unicode)]
        public static extern int t3pp_rules_encrypt(
            string json,
            out IntPtr outBuf,
            out int outLen);

        [DllImport(NativeDll,
            CallingConvention = CallingConvention.StdCall)]
        public static extern int t3pp_rules_decrypt(
            byte[] data,
            int dataLen,
            out IntPtr outJson,
            out int outJsonLen);

        [DllImport(NativeDll,
            CallingConvention = CallingConvention.StdCall)]
        public static extern void t3pp_rules_free(IntPtr p);

        [DllImport(NativeDll,
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Unicode)]
        public static extern int t3pp_apply_patch_from_file(
            string patchFile,
            string targetRoot,
            T3ppLogCallback? logger,
            int dryRun);
    }
}

