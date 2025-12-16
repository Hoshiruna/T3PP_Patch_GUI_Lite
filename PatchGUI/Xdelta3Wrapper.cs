using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace PatchGUIlite.Core
{
    internal static class Xdelta3Wrapper
    {
        // 按你的项目改资源名
        private const string XdeltaResourceName = "PatchGUIlite.res.xdelta3.exe";

        private static string EnsureXdelta3Exe()
        {
            var asm = Assembly.GetExecutingAssembly();
            var tempDir = Path.Combine(Path.GetTempPath(), "PatchGUIlite_xdelta3");
            Directory.CreateDirectory(tempDir);

            var exePath = Path.Combine(tempDir, "xdelta3.exe");
            if (!File.Exists(exePath))
            {
                using var s = asm.GetManifestResourceStream(XdeltaResourceName)
                    ?? throw new InvalidOperationException($"找不到嵌入资源 {XdeltaResourceName}（xdelta3）。");
                using var fs = new FileStream(exePath, FileMode.Create, FileAccess.Write, FileShare.None);
                s.CopyTo(fs);
            }

            return exePath;
        }

        public static string CreateDeltaFile(string sourcePath, string targetPath)
        {
            if (string.IsNullOrEmpty(sourcePath)) throw new ArgumentNullException(nameof(sourcePath));
            if (string.IsNullOrEmpty(targetPath)) throw new ArgumentNullException(nameof(targetPath));

            sourcePath = Path.GetFullPath(sourcePath);
            targetPath = Path.GetFullPath(targetPath);

            if (!File.Exists(sourcePath)) throw new FileNotFoundException("源文件不存在", sourcePath);
            if (!File.Exists(targetPath)) throw new FileNotFoundException("目标文件不存在", targetPath);

            string exePath = EnsureXdelta3Exe();
            string patchPath = Path.Combine(Path.GetTempPath(), "PatchGUIlite_xdelta3", Guid.NewGuid().ToString("N") + ".vcdiff");

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"-e -s \"{sourcePath}\" \"{targetPath}\" \"{patchPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("无法启动 xdelta3 进程。");

            string stdOut = proc.StandardOutput.ReadToEnd();
            string stdErr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (proc.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"xdelta3 生成差分失败，ExitCode={proc.ExitCode}\nSTDOUT:\n{stdOut}\nSTDERR:\n{stdErr}");
            }

            if (!File.Exists(patchPath))
                throw new FileNotFoundException("xdelta3 未生成差分文件。", patchPath);

            return patchPath;
        }

        public static void ApplyDeltaFile(string sourcePath, string patchPath, string outputPath)
        {
            if (string.IsNullOrEmpty(sourcePath)) throw new ArgumentNullException(nameof(sourcePath));
            if (string.IsNullOrEmpty(patchPath)) throw new ArgumentNullException(nameof(patchPath));
            if (string.IsNullOrEmpty(outputPath)) throw new ArgumentNullException(nameof(outputPath));

            sourcePath = Path.GetFullPath(sourcePath);
            patchPath = Path.GetFullPath(patchPath);
            outputPath = Path.GetFullPath(outputPath);

            if (!File.Exists(sourcePath)) throw new FileNotFoundException("源文件不存在", sourcePath);
            if (!File.Exists(patchPath)) throw new FileNotFoundException("补丁文件不存在", patchPath);

            string exePath = EnsureXdelta3Exe();

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"-d -s \"{sourcePath}\" \"{patchPath}\" \"{outputPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("无法启动 xdelta3 进程（apply）。");

            string stdOut = proc.StandardOutput.ReadToEnd();
            string stdErr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (proc.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"xdelta3 应用差分失败，ExitCode={proc.ExitCode}\nSTDOUT:\n{stdOut}\nSTDERR:\n{stdErr}");
            }

            if (!File.Exists(outputPath))
                throw new FileNotFoundException("xdelta3 未生成输出文件。", outputPath);
        }
    }
}

