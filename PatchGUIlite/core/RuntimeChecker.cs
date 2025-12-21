using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using PatchGUIlite;

namespace PatchGUIlite.Core
{
    internal static class RuntimeChecker
    {
        private const string DesktopRuntimeFolder = "Microsoft.WindowsDesktop.App";
        private const string DownloadUrl = "https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime";

        public static bool EnsureWindowsDesktopRuntime()
        {
            if (HasBundledRuntime() || IsWindowsDesktopRuntimeInstalled())
            {
                return true;
            }

            string message = L("runtime.missing.message", "Required .NET 8.0 Windows Desktop Runtime is missing.\nThe download page will now open. Install it, then restart the app.");
            string title = L("runtime.missing.title", "Missing Runtime");
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            TryOpenDownloadPage();
            return false;
        }

        private static bool IsWindowsDesktopRuntimeInstalled()
        {
            return HasRegistryRuntime("SOFTWARE\\dotnet\\Setup\\InstalledVersions\\x64\\sharedfx\\Microsoft.WindowsDesktop.App")
                || HasRegistryRuntime("SOFTWARE\\dotnet\\Setup\\InstalledVersions\\x86\\sharedfx\\Microsoft.WindowsDesktop.App")
                || HasRuntimeInFolders();
        }

        private static bool HasBundledRuntime()
        {
            try
            {
                string baseDir = AppContext.BaseDirectory;
                return File.Exists(Path.Combine(baseDir, "hostfxr.dll"))
                    || File.Exists(Path.Combine(baseDir, "hostpolicy.dll"));
            }
            catch
            {
                return false;
            }
        }

        private static bool HasRegistryRuntime(string keyPath)
        {
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(keyPath, false);
                if (key == null)
                    return false;

                Version? max = key.GetSubKeyNames()
                    .Select(v => Version.TryParse(v, out var ver) ? ver : null)
                    .Where(v => v != null)
                    .DefaultIfEmpty()
                    .Max();

                return max != null && max >= new Version(8, 0);
            }
            catch
            {
                return false;
            }
        }

        private static bool HasRuntimeInFolders()
        {
            try
            {
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

                return ContainsRuntimeVersion(Path.Combine(programFiles, "dotnet", "shared", DesktopRuntimeFolder))
                    || ContainsRuntimeVersion(Path.Combine(programFilesX86, "dotnet", "shared", DesktopRuntimeFolder));
            }
            catch
            {
                return false;
            }
        }

        private static bool ContainsRuntimeVersion(string path)
        {
            if (!Directory.Exists(path))
                return false;

            Version? max = Directory.GetDirectories(path)
                .Select(Path.GetFileName)
                .Select(v => Version.TryParse(v, out var ver) ? ver : null)
                .Where(v => v != null)
                .DefaultIfEmpty()
                .Max();

            return max != null && max >= new Version(8, 0);
        }

        private static void TryOpenDownloadPage()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = DownloadUrl,
                    UseShellExecute = true
                });
            }
            catch
            {
                // ignore if browser cannot be opened
            }
        }

        private static string L(string key, string fallback) => LocalizationManager.Get(key, fallback);
    }
}
