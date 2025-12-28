using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace PatchGUIlite.Core
{
    public static class HashUtility
    {
        private static readonly uint[] Crc32Table = BuildCrc32Table();
        private static HashMismatchNativeCb? _nativeMismatchCallback;
        private static Action<string, string>? _managedMismatchHandler;

        public static (string crc, string md5, string sha1)? ComputeFileHashes(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            try
            {
                byte[] data = File.ReadAllBytes(path);

                uint crc = 0xFFFFFFFF;
                foreach (byte b in data)
                {
                    crc = Crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
                }
                crc ^= 0xFFFFFFFF;
                string crcHex = crc.ToString("X8");

                using var md5 = MD5.Create();
                string md5Hex = Convert.ToHexString(md5.ComputeHash(data));

                using var sha1 = SHA1.Create();
                string sha1Hex = Convert.ToHexString(sha1.ComputeHash(data));

                return (crcHex, md5Hex, sha1Hex);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Register a managed handler to receive hash mismatch notifications coming from the native DLL.
        /// </summary>
        public static void SetHashMismatchHandler(Action<string, string>? handler)
        {
            _managedMismatchHandler = handler;
        }

        /// <summary>
        /// Exposes a stable delegate that can be passed to native code as t3pp_hash_mismatch_cb.
        /// Keep this delegate alive (static) so the GC does not collect it while native code holds it.
        /// </summary>
        public static HashMismatchNativeCb GetNativeMismatchCallback()
        {
            return _nativeMismatchCallback ??= (relPath, context) =>
            {
                string rel = relPath ?? string.Empty;
                string ctx = context ?? string.Empty;
                _managedMismatchHandler?.Invoke(rel, ctx);
            };
        }

        private static uint[] BuildCrc32Table()
        {
            const uint poly = 0xEDB88320;
            var table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                uint entry = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((entry & 1) == 1)
                        entry = (entry >> 1) ^ poly;
                    else
                        entry >>= 1;
                }
                table[i] = entry;
            }
            return table;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public delegate void HashMismatchNativeCb(string relPath, string context);
}
