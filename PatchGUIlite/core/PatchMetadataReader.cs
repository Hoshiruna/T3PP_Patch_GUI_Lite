using System;
using System.IO;
using System.Linq;
using System.Text;

namespace PatchGUIlite.Core
{
    public enum PatchModeHint
    {
        Directory,
        File
    }

    public sealed class PatchMetadata
    {
        public bool IsSecurityPatch { get; init; }
        public PatchModeHint? Mode { get; init; }
    }

    public static class PatchMetadataReader
    {
        public static PatchMetadata? Read(string patchPath)
        {
            if (string.IsNullOrWhiteSpace(patchPath) || !File.Exists(patchPath))
                return null;

            try
            {
                using var fs = new FileStream(patchPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (fs.Length == 0)
                    return null;

                int len = (int)Math.Min(1024, fs.Length);
                fs.Seek(-len, SeekOrigin.End);
                byte[] buffer = new byte[len];
                int read = fs.Read(buffer, 0, len);
                string tail = Encoding.UTF8.GetString(buffer, 0, read);

                bool isSecurityPatch = tail.IndexOf("tenumuinoti", StringComparison.OrdinalIgnoreCase) >= 0;
                PatchModeHint? mode = ExtractMode(tail);

                return new PatchMetadata
                {
                    IsSecurityPatch = isSecurityPatch,
                    Mode = mode
                };
            }
            catch
            {
                return null;
            }
        }

        private static PatchModeHint? ExtractMode(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            int idx = text.LastIndexOf("PATCH_MODE:", StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return null;

            string suffix = text.Substring(idx + "PATCH_MODE:".Length);
            string token = new string(suffix
                .TakeWhile(c => !char.IsWhiteSpace(c) && c != '\0')
                .ToArray())
                .Trim();

            if (token.Equals("DIRECTORY", StringComparison.OrdinalIgnoreCase))
                return PatchModeHint.Directory;
            if (token.Equals("FILE", StringComparison.OrdinalIgnoreCase))
                return PatchModeHint.File;

            return null;
        }
    }
}
