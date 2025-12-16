using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;        // WPF controls
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using PatchGUIlite.Core;
using WinForms = System.Windows.Forms; // WinForms via alias only

//

namespace PatchGUIlite
{
    public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
    {
        //
        private const string DirectoryModeTag = "dir";
        private const string FileModeTag = "file";

        private const string DirectoryPatchTag = "PATCH_MODE:DIRECTORY";
        private const string FilePatchTag = "PATCH_MODE:FILE";

        private bool _useDirectoryMode = true;
        private string? _selectedPatchPath;
        private string? _crc32;
        private string? _md5;
        private string? _sha1;
        private static readonly uint[] Crc32Table = BuildCrc32Table();
        private TextBlock? _hashCrcText;
        private TextBlock? _hashMd5Text;
        private TextBlock? _hashSha1Text;
        private static readonly string ErrorLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
        private static readonly string RunLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "run.log");
        private CancellationTokenSource? _patchCts;
        public MainWindow()
        {
            InitializeComponent();

            PatchGUIlite.Core.T3ppDiff.DebugLog = msg => AppendConsoleLine(msg);

            InitMode();               // DEBUG/RELEASE nav control
            LocalizationManager.LoadLanguage("zh_CN");
            ApplyLocalization();
            LanguageSelector.SelectedIndex = 0;

        }

        #region Init
        private void InitMode()
        {
#if DEBUG
            DebugNavBar.Visibility = Visibility.Visible;

            PatchView.Visibility = Visibility.Visible; PatchView.Opacity = 1;
            GenerateView.Visibility = Visibility.Collapsed; GenerateView.Opacity = 0;
            SettingsView.Visibility = Visibility.Collapsed; SettingsView.Opacity = 0;
            GameSelectionPanel.Visibility = Visibility.Visible;
#else
            //
            DebugNavBar.Visibility = Visibility.Collapsed;

            PatchView.Visibility = Visibility.Visible; PatchView.Opacity = 1;
            GenerateView.Visibility = Visibility.Collapsed; GenerateView.Opacity = 0;
            SettingsView.Visibility = Visibility.Collapsed; SettingsView.Opacity = 0;
            GameSelectionPanel.Visibility = Visibility.Visible;
#endif
        }

        #endregion

        #region Mode selection

        private void GameSelectButton_Click(object sender, RoutedEventArgs e)
        {
            if (GameSelectButton.ContextMenu is ContextMenu menu)
            {
                menu.PlacementTarget = GameSelectButton;
                menu.Placement = PlacementMode.Bottom;
                menu.IsOpen = true;
            }
        }

        private void ModeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem item)
                return;

            string tag = item.Tag?.ToString() ?? string.Empty;
            bool dirMode = string.Equals(tag, DirectoryModeTag, StringComparison.OrdinalIgnoreCase);
            _useDirectoryMode = dirMode;

            // Clear previous selections to avoid mode mismatch
            GenSourceBox.Text = string.Empty;
            GenTargetBox.Text = string.Empty;
            PatchFileTextBox.Text = string.Empty;
            DirectoryTextBox.Text = string.Empty;
            _selectedPatchPath = null;
            UpdateHashDisplay(null);

            // Sync pack option with mode
            PackDirectoryCheckBox.IsChecked = dirMode;
            ApplyLocalization();

            string info = dirMode
                ? "Switched to directory mode. Please reselect the directory."
                : "Switched to file mode. Please reselect the file.";
            AppendConsoleLine($"[INFO] {info}");
        }

        #endregion

        #region Patch page: mode detect + target path + apply patch
        private async Task<string?> ResolvePatchFilePathAsync()
        {
            if (!string.IsNullOrWhiteSpace(_selectedPatchPath) && File.Exists(_selectedPatchPath))
                return _selectedPatchPath;

            using var dialog = new WinForms.OpenFileDialog
            {
                Title = "Select T3PP patch file",
                Filter = "Touhou 3rd-party Patch (*.t3pp)|*.t3pp|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() != WinForms.DialogResult.OK)
                return null;

            _selectedPatchPath = dialog.FileName;
            if (PatchFileTextBox != null)
            {
                PatchFileTextBox.Text = _selectedPatchPath;
            }
            return _selectedPatchPath;
        }
        private void SelectDirButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_useDirectoryMode)
                {
                    using var dialog = new WinForms.FolderBrowserDialog
                    {
                        Description = "Select target directory"
                    };

                    if (!string.IsNullOrWhiteSpace(DirectoryTextBox.Text) &&
                        Directory.Exists(DirectoryTextBox.Text))
                    {
                        dialog.SelectedPath = DirectoryTextBox.Text;
                    }

                    if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                    {
                        DirectoryTextBox.Text = dialog.SelectedPath;
                        AppendConsoleLine($"[INFO] Selected directory: {dialog.SelectedPath}");
                    }
                }
                else
                {
                    using var dialog = new WinForms.OpenFileDialog
                    {
                        Title = "Select target file",
                        Filter = "All files (*.*)|*.*"
                    };

                    if (!string.IsNullOrWhiteSpace(DirectoryTextBox.Text) &&
                        File.Exists(DirectoryTextBox.Text))
                    {
                        dialog.FileName = DirectoryTextBox.Text;
                        dialog.InitialDirectory = Path.GetDirectoryName(DirectoryTextBox.Text);
                    }

                    if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                    {
                        DirectoryTextBox.Text = dialog.FileName;
                        AppendConsoleLine($"[INFO] Selected file: {dialog.FileName}");
                        UpdateHashDisplay(dialog.FileName);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, $"Failed to select path: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectPatchFileButton_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.OpenFileDialog
            {
                Title = "Select T3PP patch file",
                Filter = "Touhou 3rd-party Patch (*.t3pp)|*.t3pp|All files (*.*)|*.*"
            };

            if (!string.IsNullOrWhiteSpace(_selectedPatchPath) && File.Exists(_selectedPatchPath))
            {
                dialog.InitialDirectory = Path.GetDirectoryName(_selectedPatchPath);
                dialog.FileName = _selectedPatchPath;
            }

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                _selectedPatchPath = dialog.FileName;
                PatchFileTextBox.Text = _selectedPatchPath;
                AppendConsoleLine($"[INFO] Selected patch: {_selectedPatchPath}");
                ApplyPatchModeFromFile(_selectedPatchPath);
                // Re-validate hashes if in file mode
                if (!_useDirectoryMode && File.Exists(DirectoryTextBox.Text))
                {
                    UpdateHashDisplay(DirectoryTextBox.Text);
                }
            }
        }
        //
        //
        private async void RunPatchButton_Click(object sender, RoutedEventArgs e)
        {
            if (_patchCts != null)
            {
                System.Windows.MessageBox.Show(this, "Patch is already running.", "Info",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string targetPath = DirectoryTextBox.Text.Trim();
            if (_useDirectoryMode)
            {
                if (string.IsNullOrEmpty(targetPath) || !Directory.Exists(targetPath))
                {
                    System.Windows.MessageBox.Show(this, "Please select a valid game directory.", "Info",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath))
                {
                    System.Windows.MessageBox.Show(this, "Please select a valid target file.", "Info",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }

            string gameRoot = _useDirectoryMode
                ? targetPath
                : (Path.GetDirectoryName(targetPath) ?? targetPath);
            if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
            {
                System.Windows.MessageBox.Show(this, "Cannot resolve the directory of the selected file.", "Info",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!_useDirectoryMode && string.IsNullOrWhiteSpace(_crc32))
            {
                System.Windows.MessageBox.Show(this, "Failed to compute hash info. Please reselect the target file.", "Info",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!_useDirectoryMode && PatchFileTextBox.Text is string p && !string.IsNullOrWhiteSpace(p))
            {
                // hashes already computed; verify they exist
                SetHashTexts(_crc32 ?? "N/A", _md5 ?? "N/A", _sha1 ?? "N/A");
            }


            // Decide which .t3pp to use
            string? patchPath = await ResolvePatchFilePathAsync();
            if (string.IsNullOrWhiteSpace(patchPath))
            {
                AppendConsoleLine("[INFO] User canceled patch selection.");
                return;
            }
            ApplyPatchModeFromFile(patchPath);
            if (!_useDirectoryMode && !VerifyHashes(targetPath))
            {
                System.Windows.MessageBox.Show(this, "Hash mismatch or calculation failed. Please verify the file.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            AppendConsoleLine(_useDirectoryMode
                ? $"[INFO] Target directory: {gameRoot}"
                : $"[INFO] Target file: {targetPath} (will use its directory for patching)");
            AppendConsoleLine($"[INFO] Selected patch: {patchPath}");

            _patchCts = new CancellationTokenSource();

            try
            {
                string patchFileLocal = patchPath; // avoid closure capturing UI variable

                await Task.Run(() =>
                {
                    if (_patchCts.IsCancellationRequested)
                        return;

                    T3ppDiff.ApplyPatchToDirectory(
                        patchFile: patchFileLocal,
                        targetRoot: gameRoot,
                        logger: AppendConsoleLine,
                        dryRun: false);
                });

                AppendConsoleLine("[INFO] Patch applied successfully.");

            }
            catch (Exception ex)
            {
                AppendConsoleLine($"[ERROR] Patch apply failed: {ex}");
                System.Windows.MessageBox.Show(this, $"Failed to select path: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _patchCts?.Dispose();
                _patchCts = null;
            }
        }


        #endregion

        #region Generate page: select paths + generate t3pp
        #endregion

        #region Generate page: select paths + generate t3pp

        private void GenSelectSourceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_useDirectoryMode)
                {
                    using var dialog = new WinForms.FolderBrowserDialog
                    {
                        Description = "Select source directory (before changes)"
                    };

                    if (!string.IsNullOrWhiteSpace(GenSourceBox.Text) &&
                        Directory.Exists(GenSourceBox.Text))
                    {
                        dialog.SelectedPath = GenSourceBox.Text;
                    }

                    if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                    {
                        GenSourceBox.Text = dialog.SelectedPath;
                        AppendGenConsoleLine($"[INFO] Source path: {dialog.SelectedPath}");
                    }
                }
                else
                {
                    using var dialog = new WinForms.OpenFileDialog
                    {
                        Title = "Select target file",
                        Filter = "All files (*.*)|*.*"
                    };

                    if (!string.IsNullOrWhiteSpace(GenSourceBox.Text) &&
                        File.Exists(GenSourceBox.Text))
                    {
                        dialog.FileName = GenSourceBox.Text;
                        dialog.InitialDirectory = Path.GetDirectoryName(GenSourceBox.Text);
                    }

                    if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                    {
                        GenSourceBox.Text = dialog.FileName;
                        AppendGenConsoleLine($"[INFO] Source path: {dialog.FileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, $"Failed to select path: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GenSelectTargetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_useDirectoryMode)
                {
                    using var dialog = new WinForms.FolderBrowserDialog
                    {
                        Description = "Select target directory (after changes)"
                    };

                    if (!string.IsNullOrWhiteSpace(GenTargetBox.Text) &&
                        Directory.Exists(GenTargetBox.Text))
                    {
                        dialog.SelectedPath = GenTargetBox.Text;
                    }

                    if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                    {
                        GenTargetBox.Text = dialog.SelectedPath;
                        AppendGenConsoleLine($"[INFO] Target path: {dialog.SelectedPath}");
                    }
                }
                else
                {
                    using var dialog = new WinForms.OpenFileDialog
                    {
                        Title = "Select target file",
                        Filter = "All files (*.*)|*.*"
                    };

                    if (!string.IsNullOrWhiteSpace(GenTargetBox.Text) &&
                        File.Exists(GenTargetBox.Text))
                    {
                        dialog.FileName = GenTargetBox.Text;
                        dialog.InitialDirectory = Path.GetDirectoryName(GenTargetBox.Text);
                    }

                    if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                    {
                        GenTargetBox.Text = dialog.FileName;
                        AppendGenConsoleLine($"[INFO] Target path: {dialog.FileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, $"Failed to select path: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void GenStartDiffButton_Click(object sender, RoutedEventArgs e)
        {
            string sourcePath = GenSourceBox.Text.Trim();
            string targetPath = GenTargetBox.Text.Trim();

            if (_useDirectoryMode)
            {
                if (string.IsNullOrEmpty(sourcePath) || !Directory.Exists(sourcePath))
                {
                    System.Windows.MessageBox.Show(this, "Please select the source directory first.", "Info",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (string.IsNullOrEmpty(targetPath) || !Directory.Exists(targetPath))
                {
                    System.Windows.MessageBox.Show(this, "Please select the target directory first.", "Info",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                bool packDirectory = PackDirectoryCheckBox.IsChecked == true;
                string baseDir = Directory.GetParent(sourcePath)?.FullName ?? sourcePath;
                string defaultName = $"patch_{DateTime.Now:yyyyMMdd_HHmmss}.t3pp";

                string outFile;
                using (var dialog = new WinForms.SaveFileDialog
                {
                    Title = "Select patch output file",
                    Filter = "Touhou 3rd-party Patch (*.t3pp)|*.t3pp|All files (*.*)|*.*",
                    FileName = defaultName,
                    InitialDirectory = baseDir
                })
                {
                    if (dialog.ShowDialog() != WinForms.DialogResult.OK)
                    {
                        return;
                    }

                    outFile = dialog.FileName;
                }

                AppendGenConsoleLine($"[INFO] Source path: {sourcePath}");
                AppendGenConsoleLine($"[INFO] Target path: {targetPath}");
                AppendGenConsoleLine("[INFO] Start generating diff patch:");
                AppendGenConsoleLine($"       Source: {sourcePath}");
                AppendGenConsoleLine($"       Target: {targetPath}");
                AppendGenConsoleLine($"       Output: {outFile}");
                AppendGenConsoleLine($"       Mode: {(packDirectory ? "Directory diff" : "File-list mode (handled as directory for now)")}");

                try
                {
                    await Task.Run(() => T3ppDiff.CreateDirectoryDiff(sourcePath, targetPath, outFile));
                    var modeTag = packDirectory ? DirectoryPatchTag : FilePatchTag;
                    File.AppendAllText(outFile, $"{modeTag}");
                    AppendGenConsoleLine("[INFO] Diff generation completed.");
                }
                catch (Exception ex)
                {
                    AppendGenConsoleLine($"[ERROR] Diff generation failed: {ex}");
                    System.Windows.MessageBox.Show(this, $"Failed to select path: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                {
                    System.Windows.MessageBox.Show(this, "Please select the source file first.", "Info",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath))
                {
                    System.Windows.MessageBox.Show(this, "Please select the target file first.", "Info",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string baseDir = Path.GetDirectoryName(targetPath)
                                 ?? Path.GetDirectoryName(sourcePath)
                                 ?? Path.GetTempPath();
                string defaultName = $"patch_{DateTime.Now:yyyyMMdd_HHmmss}.t3pp";

                string outFile;
                using (var dialog = new WinForms.SaveFileDialog
                {
                    Title = "Select patch output file",
                    Filter = "Touhou 3rd-party Patch (*.t3pp)|*.t3pp|All files (*.*)|*.*",
                    FileName = defaultName,
                    InitialDirectory = baseDir
                })
                {
                    if (dialog.ShowDialog() != WinForms.DialogResult.OK)
                    {
                        return;
                    }

                    outFile = dialog.FileName;
                }

                AppendGenConsoleLine($"[INFO] Source file: {sourcePath}");
                AppendGenConsoleLine($"[INFO] Target file: {targetPath}");
                AppendGenConsoleLine("[INFO] Start generating diff patch:");
                AppendGenConsoleLine($"       Source: {sourcePath}");
                AppendGenConsoleLine($"       Target: {targetPath}");
                AppendGenConsoleLine($"       Output: {outFile}");
                AppendGenConsoleLine("       Mode: File diff");

                string tempOldDir = Path.Combine(Path.GetTempPath(), "t3pp_old_" + Guid.NewGuid().ToString("N"));
                string tempNewDir = Path.Combine(Path.GetTempPath(), "t3pp_new_" + Guid.NewGuid().ToString("N"));
                string relativeName = Path.GetFileName(targetPath);
                if (string.IsNullOrWhiteSpace(relativeName))
                {
                    relativeName = Path.GetFileName(sourcePath);
                }
                if (string.IsNullOrWhiteSpace(relativeName))
                {
                    relativeName = "file.bin";
                }

                Directory.CreateDirectory(tempOldDir);
                Directory.CreateDirectory(tempNewDir);

                string oldCopy = Path.Combine(tempOldDir, relativeName);
                string newCopy = Path.Combine(tempNewDir, relativeName);

                File.Copy(sourcePath, oldCopy, true);
                File.Copy(targetPath, newCopy, true);

                try
                {
                    await Task.Run(() => T3ppDiff.CreateDirectoryDiff(tempOldDir, tempNewDir, outFile));
                    File.AppendAllText(outFile, $"{FilePatchTag}");
                    AppendGenConsoleLine("[INFO] Diff generation completed.");
                }
                catch (Exception ex)
                {
                    AppendGenConsoleLine($"[ERROR] Diff generation failed: {ex}");
                    System.Windows.MessageBox.Show(this, $"Failed to select path: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    TryDeleteDirectory(tempOldDir);
                    TryDeleteDirectory(tempNewDir);
                }
            }
        }


        #endregion

        #region UI helpers



        private string L(string key, string fallback) => LocalizationManager.Get(key, fallback);

        private void AppendConsoleLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            string sanitized = line.TrimEnd('\r', '\n');
            bool isError = sanitized.IndexOf("[ERROR]", StringComparison.OrdinalIgnoreCase) >= 0;

            // Keep run.log only for debug builds; always record errors.
#if DEBUG
            WriteLogEntry(RunLogPath, sanitized);
#endif
            if (isError)
            {
                WriteLogEntry(ErrorLogPath, sanitized);
            }
        }

        private void AppendGenConsoleLine(string line)
        {
            AppendConsoleLine(line);
        }

        private static void WriteLogEntry(string path, string message)
        {
            try
            {
                string timestamped = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
                File.AppendAllText(path, timestamped);
            }
            catch
            {
                // ignore file write failure
            }
        }

        private void ApplyPatchModeFromFile(string patchPath)
        {
            var metadata = PatchMetadataReader.Read(patchPath);
            if (metadata == null)
                return;

            string securityMessage = metadata.IsSecurityPatch
                ? "This is a verified patch."
                : "This is an unverified patch; exercise caution when applying.";
            System.Windows.MessageBox.Show(this, securityMessage, "Info",
                MessageBoxButton.OK, MessageBoxImage.Information);

            if (metadata.Mode == null)
                return;

            bool dirMode = metadata.Mode == PatchModeHint.Directory;
            string message = dirMode
                ? "Current patch detected as directory patch. Switched to directory mode."
                : "Current patch detected as file patch. Switched to file mode.";

            _useDirectoryMode = dirMode;
            PackDirectoryCheckBox.IsChecked = dirMode;
            DirectoryTextBox.Text = string.Empty;
            UpdateHashDisplay(null);
            ApplyLocalization();

            System.Windows.MessageBox.Show(this, message, "Info",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UpdateHashDisplay(string? path)
        {
            if (_useDirectoryMode)
            {
                _crc32 = _md5 = _sha1 = null;
                SetHashTexts("N/A", "N/A", "N/A");
                return;
            }

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                _crc32 = _md5 = _sha1 = null;
                SetHashTexts("N/A", "N/A", "N/A");
                return;
            }

            var hashes = ComputeHashes(path);
            if (hashes == null)
            {
                _crc32 = _md5 = _sha1 = null;
                SetHashTexts("N/A", "N/A", "N/A");
                System.Windows.MessageBox.Show(this, "Failed to compute hashes. Please try again.", "Info",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            (_crc32, _md5, _sha1) = hashes.Value;
            SetHashTexts(_crc32!, _md5!, _sha1!);
        }

        private void SetHashTexts(string crc, string md5, string sha1)
        {
            _hashCrcText ??= FindName("HashCrcText") as TextBlock;
            _hashMd5Text ??= FindName("HashMd5Text") as TextBlock;
            _hashSha1Text ??= FindName("HashSha1Text") as TextBlock;

            if (_hashCrcText != null) _hashCrcText.Text = $"CRC32: {crc}";
            if (_hashMd5Text != null) _hashMd5Text.Text = $"MD5: {md5}";
            if (_hashSha1Text != null) _hashSha1Text.Text = $"SHA-1: {sha1}";
        }

        private bool VerifyHashes(string path)
        {
            var hashes = ComputeHashes(path);
            if (hashes == null)
                return false;

            var (crc, md5, sha1) = hashes.Value;
            bool match = string.Equals(crc, _crc32, StringComparison.OrdinalIgnoreCase)
                      && string.Equals(md5, _md5, StringComparison.OrdinalIgnoreCase)
                      && string.Equals(sha1, _sha1, StringComparison.OrdinalIgnoreCase);

            _crc32 = crc;
            _md5 = md5;
            _sha1 = sha1;
            SetHashTexts(crc, md5, sha1);

            if (!match)
            {
                AppendConsoleLine("[ERROR] Hash values differ from previous calculation. Please verify the file.");
            }

            return match;
        }

        private static (string crc, string md5, string sha1)? ComputeHashes(string path)
        {
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

        private static void TryDeleteDirectory(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
                // Swallow cleanup failures; temp folders will be purged by the OS.
            }
        }

        private void ApplyLocalization()
        {
            string localizedTitle = L("window.title", "Patch Tool");
            Title = localizedTitle;

            PatchTabRadio.Content = L("nav.patch", "Patch");
            GenerateTabRadio.Content = L("nav.generate", "Generate");
            RuleTabRadio.Content = L("nav.rule", "Rules");
            SettingsTabRadio.Content = L("nav.settings", "Settings");

            GameSelectButton.Content = L("button.gameSelect", "Select Mode");
            SelectedGameText.Text = _useDirectoryMode
                ? L("label.currentModeDirectory", "Current mode: Directory")
                : L("label.currentModeFile", "Current mode: File");

            SelectDirButton.Content = L("button.selectDir", "Select Target");
            SelectDirButton.ToolTip = L("tooltip.selectDir", "Browse target path");
            DirectoryTextBox.PlaceholderText = _useDirectoryMode
                ? L("placeholder.gameDir", "Select a target directory...")
                : L("placeholder.gameFile", "Select a target file...");
            SelectPatchButton.Content = L("button.selectPatch", "Select Patch");
            SelectPatchButton.ToolTip = L("tooltip.selectPatch", "Choose a .t3pp patch file");
            PatchFileTextBox.PlaceholderText = L("placeholder.patchFile", "Select a patch file...");
            SetHashTexts(_crc32 ?? "N/A", _md5 ?? "N/A", _sha1 ?? "N/A");
            RunPatchButton.Content = L("button.runPatch", "Apply Patch");

            string genSourceLabel = _useDirectoryMode
                ? L("gen.button.source", "Select Folder")
                : L("gen.button.source.file", "Select File");
            string genSourceTip = _useDirectoryMode
                ? L("gen.tooltip.source", "Choose the original folder (before changes)")
                : L("gen.tooltip.source.file", "Choose the original file (before changes)");
            string genSourcePlaceholder = _useDirectoryMode
                ? L("gen.placeholder.source", "Pick the original folder...")
                : L("gen.placeholder.source.file", "Pick the original file...");
            GenSelectSourceButton.Content = genSourceLabel;
            GenSelectSourceButton.ToolTip = genSourceTip;
            GenSourceBox.PlaceholderText = genSourcePlaceholder;

            string genTargetLabel = _useDirectoryMode
                ? L("gen.button.target", "Select Folder")
                : L("gen.button.target.file", "Select File Before/After Changes");
            string genTargetTip = _useDirectoryMode
                ? L("gen.tooltip.target", "Choose the modified/output folder")
                : L("gen.tooltip.target.file", "Choose the modified file (after changes)");
            string genTargetPlaceholder = _useDirectoryMode
                ? L("gen.placeholder.target", "Pick the modified folder...")
                : L("gen.placeholder.target.file", "Pick the modified file...");
            GenSelectTargetButton.Content = genTargetLabel;
            GenSelectTargetButton.ToolTip = genTargetTip;
            GenTargetBox.PlaceholderText = genTargetPlaceholder;

            PackDirectoryCheckBox.Content = L("gen.packDirectory", "Package directory");
            PackDirectoryCheckBox.ToolTip = L("gen.packDirectory.tooltip", "On: diff by directory; Off: file-list mode (backend can switch).");
            PackDirectoryCheckBox.Visibility = _useDirectoryMode ? Visibility.Visible : Visibility.Collapsed;

            GenStartDiffButton.Content = L("gen.button.start", "Start");

            SettingsTitleText.Text = L("settings.title", "Settings");
            LanguageSectionTitle.Text = L("settings.languageSection", "Language");
            LanguageLabel.Text = L("settings.languageLabel", "UI Language");

            foreach (var item in LanguageSelector.Items)
            {
                if (item is ComboBoxItem cbItem)
                {
                    string tag = cbItem.Tag?.ToString() ?? string.Empty;
                    bool isZh = tag.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
                    cbItem.Content = isZh
                        ? L("settings.language.zh", "Chinese")
                        : L("settings.language.en", "English");
                }
            }

            // Keep selector aligned with current language
            for (int i = 0; i < LanguageSelector.Items.Count; i++)
            {
                if (LanguageSelector.Items[i] is ComboBoxItem cbItem &&
                    string.Equals(cbItem.Tag?.ToString(), LocalizationManager.CurrentLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    LanguageSelector.SelectedIndex = i;
                    break;
                }
            }

            WindowTitleBar.Title = localizedTitle;
        }

        private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageSelector.SelectedItem is ComboBoxItem item)
            {
                string lang = item.Tag?.ToString() ?? "zh";
                if (!string.Equals(LocalizationManager.CurrentLanguage, lang, StringComparison.OrdinalIgnoreCase))
                {
                    LocalizationManager.LoadLanguage(lang);
                }
                ApplyLocalization();
            }
        }

        #endregion
    }
}
