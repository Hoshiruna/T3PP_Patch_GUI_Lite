using System;
using System.Runtime.InteropServices;

namespace PatchGUIlite.Core
{
    public static class T3ppDiff
    {
        // ---------------------
        // P/Invoke 声明
        // ---------------------

        // C++ t3pp_native.h bindings
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate void NativeLogCb(int level, string msg);

        private static class Native
        {
            // Use config-specific native binary so debug/release can coexist.
#if DEBUG
            private const string DllName = "T3ppNativelite_Debug_x64.dll";
#else
            private const string DllName = "T3ppNativelite_Release_x64.dll";
#endif

            [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
            internal static extern int t3pp_apply_patch_from_file(
                string patch_file,
                string target_root,
                NativeLogCb? logger,
                int dry_run);

            [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
            internal static extern int t3pp_create_patch_from_dirs(
                string old_dir,
                string new_dir,
                string output_file,
                NativeLogCb? logger);
        }

        // 这个仍然保留给 MainWindow 用
        public static Action<string>? DebugLog;

        // ---------------------
        // 对外 API：应用补丁
        // ---------------------
        public static void ApplyPatchToDirectory(
            string patchFile,
            string targetRoot,
            Action<string>? logger,
            bool dryRun)
        {
            if (string.IsNullOrWhiteSpace(patchFile))
                throw new ArgumentNullException(nameof(patchFile));
            if (string.IsNullOrWhiteSpace(targetRoot))
                throw new ArgumentNullException(nameof(targetRoot));

            // 将 native log 映射回托管 logger / DebugLog
            NativeLogCb? cb = null;

            if (logger != null || DebugLog != null)
            {
                cb = (level, msg) =>
                {
                    try
                    {
                        string prefix = level switch
                        {
                            0 => "[INFO] ",
                            1 => "[WARN] ",
                            _ => "[ERROR] "
                        };
                        if (logger != null) logger($"{prefix}{msg}");
                        else DebugLog?.Invoke($"{prefix}{msg}");
                    }
                    catch
                    {
                        // 日志失败直接吞掉，避免 native 回调崩溃
                    }
                };
            }

            int rc = Native.t3pp_apply_patch_from_file(
                patchFile,
                targetRoot,
                cb,
                dryRun ? 1 : 0);

            if (rc != 0)
            {
                throw new InvalidOperationException($"原生补丁应用失败，错误码：{rc}");
            }
        }

        // ---------------------
        // 对外 API：生成补丁
        // ---------------------
        public static void CreateDirectoryDiff(
            string oldDir,
            string newDir,
            string outputFile)
        {
            if (string.IsNullOrWhiteSpace(oldDir))
                throw new ArgumentNullException(nameof(oldDir));
            if (string.IsNullOrWhiteSpace(newDir))
                throw new ArgumentNullException(nameof(newDir));
            if (string.IsNullOrWhiteSpace(outputFile))
                throw new ArgumentNullException(nameof(outputFile));

            NativeLogCb? cb = null;
            if (DebugLog != null)
            {
                cb = (level, msg) =>
                {
                    try
                    {
                        DebugLog?.Invoke($"[NATIVE-{level}] {msg}");
                    }
                    catch { }
                };
            }

            int rc = Native.t3pp_create_patch_from_dirs(
                oldDir,
                newDir,
                outputFile,
                cb);

            if (rc == 1)
            {
                // 1 = 没有差异（和你原来抛异常的语义相近）
                throw new InvalidOperationException("两个目录完全一致，没有需要打包的差分文件。");
            }

            if (rc != 0)
            {
                throw new InvalidOperationException($"原生补丁生成失败，错误码：{rc}");
            }
        }
    }
}



