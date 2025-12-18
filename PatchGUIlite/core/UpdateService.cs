using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PatchGUIlite.Core
{
    internal enum UpdatePackageFailure
    {
        None,
        ReleaseMissing,
        AssetMissing
    }

    internal sealed class UpdatePackage
    {
        public string Version { get; }
        public string DownloadUrl { get; }
        public string? FileName { get; }

        public UpdatePackage(string version, string downloadUrl, string? fileName)
        {
            Version = version;
            DownloadUrl = downloadUrl;
            FileName = fileName;
        }
    }

    internal static class UpdateService
    {
        private const string LatestReleaseUrl = "https://api.github.com/repos/Hoshiruna/T3PP_Patch_GUI_Lite/releases/latest";
        private const string RemoteVersionUrl = "https://raw.githubusercontent.com/Hoshiruna/T3PP_Patch_GUI_Lite/master/PatchGUIlite/version";
        private static readonly HttpClient UpdateClient = CreateUpdateClient();

        public static string ReadLocalVersion()
        {
            try
            {
                string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    exePath = Assembly.GetExecutingAssembly().Location;
                }

                if (!string.IsNullOrWhiteSpace(exePath))
                {
                    var info = FileVersionInfo.GetVersionInfo(exePath);
                    if (!string.IsNullOrWhiteSpace(info.FileVersion))
                    {
                        return info.FileVersion.Trim();
                    }

                    if (!string.IsNullOrWhiteSpace(info.ProductVersion))
                    {
                        return info.ProductVersion.Trim();
                    }
                }
            }
            catch
            {
                // ignore
            }

            Version? assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
            return assemblyVersion?.ToString() ?? string.Empty;
        }

        public static async Task<string?> FetchRemoteVersionAsync()
        {
            try
            {
                using var response = await UpdateClient.GetAsync(RemoteVersionUrl);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                string text = (await response.Content.ReadAsStringAsync()).Trim();
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
            catch
            {
                return null;
            }
        }

        public static bool IsRemoteVersionNewer(string remoteVersion, string localVersion)
        {
            if (string.IsNullOrWhiteSpace(remoteVersion))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(localVersion))
            {
                return true;
            }

            bool remoteParsed = TryParseVersion(remoteVersion, out var remote);
            bool localParsed = TryParseVersion(localVersion, out var local);

            if (remoteParsed && localParsed)
            {
                return remote > local;
            }

            return !string.Equals(remoteVersion.Trim(), localVersion.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public static async Task<(UpdatePackage? package, UpdatePackageFailure failure)> FetchLatestPackageAsync()
        {
            GitHubRelease? release = await FetchLatestReleaseAsync();
            if (release == null)
            {
                return (null, UpdatePackageFailure.ReleaseMissing);
            }

            GitHubReleaseAsset? asset = SelectReleaseAsset(release);
            if (asset == null || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
            {
                return (null, UpdatePackageFailure.AssetMissing);
            }

            string version = release.TagName?.Trim() ?? string.Empty;
            return (new UpdatePackage(version, asset.BrowserDownloadUrl, asset.Name), UpdatePackageFailure.None);
        }

        public static async Task<bool> DownloadFileAsync(string url, string destinationPath)
        {
            try
            {
                using var response = await UpdateClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }

                await using var input = await response.Content.ReadAsStreamAsync();
                await using var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await input.CopyToAsync(output);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string? FindUpdateSourceDirectory(string extractRoot)
        {
            try
            {
                string? exePath = Directory.GetFiles(extractRoot, "PatchGUIlite.exe", SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(exePath))
                {
                    return Path.GetDirectoryName(exePath);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        public static bool StartUpdateApply(string sourceDir, string targetDir, string exePath, int processId, out string? error)
        {
            error = null;
            try
            {
                string tempRoot = Path.Combine(Path.GetTempPath(), "PatchGUIlite_update_apply");
                Directory.CreateDirectory(tempRoot);
                string scriptPath = Path.Combine(tempRoot, $"apply_update_{Guid.NewGuid():N}.ps1");

                string script = string.Join(Environment.NewLine, new[]
                {
                    "$ErrorActionPreference = 'Stop'",
                    "param(",
                    "  [int]$ProcessId,",
                    "  [string]$SourceDir,",
                    "  [string]$TargetDir,",
                    "  [string]$ExePath",
                    ")",
                    "while (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) {",
                    "  Start-Sleep -Milliseconds 200",
                    "}",
                    "Copy-Item -Path (Join-Path $SourceDir '*') -Destination $TargetDir -Recurse -Force",
                    "Start-Process -FilePath $ExePath"
                });

                File.WriteAllText(scriptPath, script, new UTF8Encoding(false));

                string args = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -ProcessId {processId} -SourceDir \"{sourceDir}\" -TargetDir \"{targetDir}\" -ExePath \"{exePath}\"";
                var startInfo = new ProcessStartInfo("powershell", args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process.Start(startInfo);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static HttpClient CreateUpdateClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PatchGUIlite");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }

        private sealed class GitHubRelease
        {
            [JsonPropertyName("tag_name")]
            public string? TagName { get; set; }

            [JsonPropertyName("assets")]
            public List<GitHubReleaseAsset>? Assets { get; set; }
        }

        private sealed class GitHubReleaseAsset
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("browser_download_url")]
            public string? BrowserDownloadUrl { get; set; }

            [JsonPropertyName("size")]
            public long Size { get; set; }
        }

        private static async Task<GitHubRelease?> FetchLatestReleaseAsync()
        {
            try
            {
                using var response = await UpdateClient.GetAsync(LatestReleaseUrl);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                string json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<GitHubRelease>(json);
            }
            catch
            {
                return null;
            }
        }

        private static GitHubReleaseAsset? SelectReleaseAsset(GitHubRelease release)
        {
            if (release.Assets == null || release.Assets.Count == 0)
            {
                return null;
            }

            var zipAssets = release.Assets
                .Where(asset => asset.Name != null
                                && asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                                && asset.BrowserDownloadUrl != null)
                .ToList();

            if (zipAssets.Count == 0)
            {
                return null;
            }

            return zipAssets
                .OrderByDescending(asset => ScoreAssetName(asset.Name))
                .ThenByDescending(asset => asset.Size)
                .FirstOrDefault();
        }

        private static int ScoreAssetName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return 0;
            }

            int score = 0;
            string lower = name.ToLowerInvariant();
            if (lower.Contains("patchguilite"))
            {
                score += 5;
            }
            if (lower.Contains("win"))
            {
                score += 3;
            }
            if (lower.Contains("x64"))
            {
                score += 2;
            }
            if (lower.Contains("release"))
            {
                score += 1;
            }
            return score;
        }

        private static bool TryParseVersion(string value, out Version version)
        {
            version = new Version(0, 0, 0, 0);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();
            if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(1);
            }

            int whitespaceIndex = trimmed.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
            if (whitespaceIndex >= 0)
            {
                trimmed = trimmed.Substring(0, whitespaceIndex);
            }

            if (!Version.TryParse(trimmed, out version))
            {
                return false;
            }

            int build = version.Build < 0 ? 0 : version.Build;
            int revision = version.Revision < 0 ? 0 : version.Revision;
            version = new Version(version.Major, version.Minor, build, revision);
            return true;
        }
    }
}
