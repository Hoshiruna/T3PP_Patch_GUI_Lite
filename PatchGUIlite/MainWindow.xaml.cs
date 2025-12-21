using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
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
        private bool _isUpdating;
        private string _updateStatusKey = "update.status.ready";
        private string _updateStatusFallback = "Ready";
        private bool _isInitializing;
        public MainWindow()
        {
            _isInitializing = true;
            InitializeComponent();

            PatchGUIlite.Core.T3ppDiff.DebugLog = msg => AppendConsoleLine(msg);

            InitMode();               // DEBUG/RELEASE nav control
            LocalizationManager.LoadLanguage("zh_CN");
            ApplyLocalization();
            LanguageSelector.SelectedIndex = 0;
            _isInitializing = false;

        }

        #region Init
        private void InitMode()
        {
            DebugNavBar.Visibility = Visibility.Visible;

            PatchView.Visibility = Visibility.Visible; PatchView.Opacity = 1;
            GenerateView.Visibility = Visibility.Collapsed; GenerateView.Opacity = 0;
            SettingsView.Visibility = Visibility.Collapsed; SettingsView.Opacity = 0;
            GameSelectionPanel.Visibility = Visibility.Visible;
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
            SelectDirButton.IsEnabled = false;
            UpdateHashDisplay(null);

            // Sync pack option with mode
            PackDirectoryCheckBox.IsChecked = dirMode;
            ApplyLocalization();

            string info = dirMode
                ? LF("log.mode.switchedDirectory", "Switched to directory mode. Please reselect the directory.")
                : LF("log.mode.switchedFile", "Switched to file mode. Please reselect the file.");
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
                Title = L("dialog.title.selectPatchFile", "Select T3PP patch file"),
                Filter = L("dialog.filter.patch", "Touhou 3rd-party Patch (*.t3pp)|*.t3pp|All files (*.*)|*.*")
            };

            if (dialog.ShowDialog() != WinForms.DialogResult.OK)
                return null;

            _selectedPatchPath = dialog.FileName;
            if (PatchFileTextBox != null)
            {
                PatchFileTextBox.Text = _selectedPatchPath;
            }
            SelectDirButton.IsEnabled = true;
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
                        Description = L("dialog.title.selectTargetDirectory", "Select target directory")
                    };

                    if (!string.IsNullOrWhiteSpace(DirectoryTextBox.Text) &&
                        Directory.Exists(DirectoryTextBox.Text))
                    {
                        dialog.SelectedPath = DirectoryTextBox.Text;
                    }

                    if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                    {
                        DirectoryTextBox.Text = dialog.SelectedPath;
                        AppendConsoleLine($"[INFO] {LF("log.selection.directory", "Selected directory: {0}", dialog.SelectedPath)}");
                    }
                }
                else
                {
                    using var dialog = new WinForms.OpenFileDialog
                    {
                        Title = L("dialog.title.selectTargetFile", "Select target file"),
                        Filter = L("dialog.filter.allFiles", "All files (*.*)|*.*")
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
                        AppendConsoleLine($"[INFO] {LF("log.selection.file", "Selected file: {0}", dialog.FileName)}");
                        UpdateHashDisplay(dialog.FileName);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError("dialog.error.pathSelect", "Failed to select path: {0}", ex.Message);
            }
        }

        private void SelectPatchFileButton_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.OpenFileDialog
            {
                Title = L("dialog.title.selectPatchFile", "Select T3PP patch file"),
                Filter = L("dialog.filter.patch", "Touhou 3rd-party Patch (*.t3pp)|*.t3pp|All files (*.*)|*.*")
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
                SelectDirButton.IsEnabled = true;
                AppendConsoleLine($"[INFO] {LF("log.selection.patch", "Selected patch: {0}", _selectedPatchPath)}");
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
                ShowInfo("dialog.info.patchRunning", "Patch is already running.");
                return;
            }

            string targetPath = DirectoryTextBox.Text.Trim();
            if (_useDirectoryMode)
            {
                if (string.IsNullOrEmpty(targetPath) || !Directory.Exists(targetPath))
                {
                    ShowInfo("dialog.info.selectGameDirectory", "Please select a valid game directory.");
                    return;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath))
                {
                    ShowInfo("dialog.info.selectTargetFile", "Please select a valid target file.");
                    return;
                }
            }

            string gameRoot = _useDirectoryMode
                ? targetPath
                : (Path.GetDirectoryName(targetPath) ?? targetPath);
            if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
            {
                ShowInfo("dialog.info.resolveDirectoryFailed", "Cannot resolve the directory of the selected file.");
                return;
            }
            if (!_useDirectoryMode && string.IsNullOrWhiteSpace(_crc32))
            {
                ShowInfo("dialog.info.hashUnavailable", "Failed to compute hash info. Please reselect the target file.");
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
                AppendConsoleLine($"[INFO] {L("log.selection.cancelPatch", "User canceled patch selection.")}");
                return;
            }
            ApplyPatchModeFromFile(patchPath);
            if (!_useDirectoryMode && !VerifyHashes(targetPath))
            {
                ShowError("dialog.error.hashMismatch", "Hash mismatch or calculation failed. Please verify the file.");
                return;
            }

            AppendConsoleLine(_useDirectoryMode
                ? $"[INFO] {LF("log.patch.targetDirectory", "Target directory: {0}", gameRoot)}"
                : $"[INFO] {LF("log.patch.targetFile", "Target file: {0} (will use its directory for patching)", targetPath)}");
            AppendConsoleLine($"[INFO] {LF("log.selection.patch", "Selected patch: {0}", patchPath)}");

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

                AppendConsoleLine($"[INFO] {L("log.patch.applySuccess", "Patch applied successfully.")}");

            }
            catch (Exception ex)
            {
                AppendConsoleLine($"[ERROR] {LF("log.patch.applyFailed", "Patch apply failed: {0}", ex)}");
                ShowError("dialog.error.pathSelect", "Failed to select path: {0}", ex.Message);
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
                        Description = L("dialog.description.selectSourceDirectory", "Select source directory (before changes)")
                    };

                    if (!string.IsNullOrWhiteSpace(GenSourceBox.Text) &&
                        Directory.Exists(GenSourceBox.Text))
                    {
                        dialog.SelectedPath = GenSourceBox.Text;
                    }

                    if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                    {
                        GenSourceBox.Text = dialog.SelectedPath;
                        AppendGenConsoleLine($"[INFO] {LF("log.gen.sourcePath", "Source path: {0}", dialog.SelectedPath)}");
                    }
                }
                else
                {
                    using var dialog = new WinForms.OpenFileDialog
                    {
                        Title = L("dialog.title.selectSourceFile", "Select source file"),
                        Filter = L("dialog.filter.allFiles", "All files (*.*)|*.*")
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
                        AppendGenConsoleLine($"[INFO] {LF("log.gen.sourceFile", "Source file: {0}", dialog.FileName)}");
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError("dialog.error.pathSelect", "Failed to select path: {0}", ex.Message);
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
                        Description = L("dialog.description.selectTargetDirectory", "Select target directory (after changes)")
                    };

                    if (!string.IsNullOrWhiteSpace(GenTargetBox.Text) &&
                        Directory.Exists(GenTargetBox.Text))
                    {
                        dialog.SelectedPath = GenTargetBox.Text;
                    }

                    if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                    {
                        GenTargetBox.Text = dialog.SelectedPath;
                        AppendGenConsoleLine($"[INFO] {LF("log.gen.targetPath", "Target path: {0}", dialog.SelectedPath)}");
                    }
                }
                else
                {
                    using var dialog = new WinForms.OpenFileDialog
                    {
                        Title = L("dialog.title.selectTargetFile", "Select target file"),
                        Filter = L("dialog.filter.allFiles", "All files (*.*)|*.*")
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
                        AppendGenConsoleLine($"[INFO] {LF("log.gen.targetFile", "Target file: {0}", dialog.FileName)}");
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError("dialog.error.pathSelect", "Failed to select path: {0}", ex.Message);
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
                    ShowInfo("dialog.info.selectSourceDirectory", "Please select the source directory first.");
                    return;
                }

                if (string.IsNullOrEmpty(targetPath) || !Directory.Exists(targetPath))
                {
                    ShowInfo("dialog.info.selectTargetDirectory", "Please select the target directory first.");
                    return;
                }

                bool packDirectory = PackDirectoryCheckBox.IsChecked == true;
                string baseDir = Directory.GetParent(sourcePath)?.FullName ?? sourcePath;
                string defaultName = $"patch_{DateTime.Now:yyyyMMdd_HHmmss}.t3pp";

                string outFile;
                using (var dialog = new WinForms.SaveFileDialog
                {
                    Title = L("dialog.title.selectPatchOutput", "Select patch output file"),
                    Filter = L("dialog.filter.patch", "Touhou 3rd-party Patch (*.t3pp)|*.t3pp|All files (*.*)|*.*"),
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

                AppendGenConsoleLine($"[INFO] {LF("log.gen.sourcePath", "Source path: {0}", sourcePath)}");
                AppendGenConsoleLine($"[INFO] {LF("log.gen.targetPath", "Target path: {0}", targetPath)}");
                AppendGenConsoleLine($"[INFO] {L("log.gen.start", "Start generating diff patch:")}");
                AppendGenConsoleLine($"       {LF("log.gen.detail.source", "Source: {0}", sourcePath)}");
                AppendGenConsoleLine($"       {LF("log.gen.detail.target", "Target: {0}", targetPath)}");
                AppendGenConsoleLine($"       {LF("log.gen.detail.output", "Output: {0}", outFile)}");
                string modeLine = packDirectory
                    ? LF("log.gen.detail.modeDirectory", "Mode: Directory diff")
                    : LF("log.gen.detail.modeFileList", "Mode: File-list mode (handled as directory for now)");
                AppendGenConsoleLine($"       {modeLine}");

                try
                {
                    await Task.Run(() => T3ppDiff.CreateDirectoryDiff(sourcePath, targetPath, outFile));
                    var modeTag = packDirectory ? DirectoryPatchTag : FilePatchTag;
                    File.AppendAllText(outFile, $"{modeTag}");
                    AppendGenConsoleLine($"[INFO] {L("log.gen.completed", "Diff generation completed.")}");
                }
                catch (Exception ex)
                {
                    AppendGenConsoleLine($"[ERROR] {LF("log.gen.failed", "Diff generation failed: {0}", ex)}");
                    ShowError("dialog.error.pathSelect", "Failed to select path: {0}", ex.Message);
                }
            }
            else
            {
                if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                {
                    ShowInfo("dialog.info.selectSourceFile", "Please select the source file first.");
                    return;
                }

                if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath))
                {
                    ShowInfo("dialog.info.selectTargetFileFirst", "Please select the target file first.");
                    return;
                }

                string baseDir = Path.GetDirectoryName(targetPath)
                                 ?? Path.GetDirectoryName(sourcePath)
                                 ?? Path.GetTempPath();
                string defaultName = $"patch_{DateTime.Now:yyyyMMdd_HHmmss}.t3pp";

                string outFile;
                using (var dialog = new WinForms.SaveFileDialog
                {
                    Title = L("dialog.title.selectPatchOutput", "Select patch output file"),
                    Filter = L("dialog.filter.patch", "Touhou 3rd-party Patch (*.t3pp)|*.t3pp|All files (*.*)|*.*"),
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

                AppendGenConsoleLine($"[INFO] {LF("log.gen.sourceFile", "Source file: {0}", sourcePath)}");
                AppendGenConsoleLine($"[INFO] {LF("log.gen.targetFile", "Target file: {0}", targetPath)}");
                AppendGenConsoleLine($"[INFO] {L("log.gen.start", "Start generating diff patch:")}");
                AppendGenConsoleLine($"       {LF("log.gen.detail.source", "Source: {0}", sourcePath)}");
                AppendGenConsoleLine($"       {LF("log.gen.detail.target", "Target: {0}", targetPath)}");
                AppendGenConsoleLine($"       {LF("log.gen.detail.output", "Output: {0}", outFile)}");
                string modeLine = LF("log.gen.detail.modeFile", "Mode: File diff");
                AppendGenConsoleLine($"       {modeLine}");

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
                    AppendGenConsoleLine($"[INFO] {L("log.gen.completed", "Diff generation completed.")}");
                }
                catch (Exception ex)
                {
                    AppendGenConsoleLine($"[ERROR] {LF("log.gen.failed", "Diff generation failed: {0}", ex)}");
                    ShowError("dialog.error.pathSelect", "Failed to select path: {0}", ex.Message);
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

        private string LF(string key, string fallback, params object[] args)
        {
            string format = L(key, fallback);
            return args.Length > 0 ? string.Format(format, args) : format;
        }

        private MessageBoxResult ShowMessage(string messageKey, string messageFallback, string titleKey, string titleFallback, MessageBoxButton buttons, MessageBoxImage image, params object[] args)
        {
            string message = LF(messageKey, messageFallback, args);
            string title = L(titleKey, titleFallback);
            return System.Windows.MessageBox.Show(this, message, title, buttons, image);
        }

        private MessageBoxResult ShowInfo(string key, string fallback, params object[] args) =>
            ShowMessage(key, fallback, "dialog.title.info", "Info", MessageBoxButton.OK, MessageBoxImage.Information, args);

        private MessageBoxResult ShowError(string key, string fallback, params object[] args) =>
            ShowMessage(key, fallback, "dialog.title.error", "Error", MessageBoxButton.OK, MessageBoxImage.Error, args);

        private MessageBoxResult ShowUpdate(string key, string fallback, MessageBoxButton buttons, MessageBoxImage image, params object[] args) =>
            ShowMessage(key, fallback, "dialog.title.update", "Update", buttons, image, args);

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

            string securityKey = metadata.IsSecurityPatch
                ? "dialog.info.verifiedPatch"
                : "dialog.info.unverifiedPatch";
            string securityFallback = metadata.IsSecurityPatch
                ? "This is a verified patch."
                : "This is an unverified patch; exercise caution when applying.";
            ShowInfo(securityKey, securityFallback);

            if (metadata.Mode == null)
                return;

            bool dirMode = metadata.Mode == PatchModeHint.Directory;
            string messageKey = dirMode
                ? "dialog.info.detectedDirectoryPatch"
                : "dialog.info.detectedFilePatch";
            string messageFallback = dirMode
                ? "Current patch detected as directory patch. Switched to directory mode."
                : "Current patch detected as file patch. Switched to file mode.";

            _useDirectoryMode = dirMode;
            PackDirectoryCheckBox.IsChecked = dirMode;
            DirectoryTextBox.Text = string.Empty;
            UpdateHashDisplay(null);
            ApplyLocalization();

            ShowInfo(messageKey, messageFallback);
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
                ShowInfo("dialog.error.hashCompute", "Failed to compute hashes. Please try again.");
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

        private void SetUpdateStatus(string key, string fallback)
        {
            _updateStatusKey = key;
            _updateStatusFallback = fallback;
            UpdateStatusText.Text = L(key, fallback);
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
                AppendConsoleLine($"[ERROR] {L("log.hash.mismatch", "Hash values differ from previous calculation. Please verify the file.")}");
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
            if (GameSelectButton.ContextMenu is ContextMenu modeMenu)
            {
                foreach (object? item in modeMenu.Items)
                {
                    if (item is not MenuItem menuItem)
                    {
                        continue;
                    }

                    string tag = menuItem.Tag?.ToString() ?? string.Empty;
                    if (string.Equals(tag, DirectoryModeTag, StringComparison.OrdinalIgnoreCase))
                    {
                        menuItem.Header = L("menu.mode.directory", "Directory Mode");
                    }
                    else if (string.Equals(tag, FileModeTag, StringComparison.OrdinalIgnoreCase))
                    {
                        menuItem.Header = L("menu.mode.file", "File Mode");
                    }
                }
            }

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
            HashInfoTitleText.Text = L("label.hashInfo", "Validation Information");

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
            UpdateSectionTitle.Text = L("settings.updateSection", "Updates");
            UpdateStatusLabel.Text = L("settings.updateLabel", "Update status");
            PullUpdatesButton.Content = L("settings.updateButton", "Check for Updates");
            UpdateStatusText.Text = L(_updateStatusKey, _updateStatusFallback);

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
            if (_isInitializing)
                return;

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

        private async void PullUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdating)
                return;

            _isUpdating = true;
            PullUpdatesButton.IsEnabled = false;
            SetUpdateStatus("update.status.checking", "Checking for updates...");
            bool shouldExit = false;

            try
            {
                string localVersion = UpdateService.ReadLocalVersion();
                string? remoteVersion = await UpdateService.FetchRemoteVersionAsync();
                if (string.IsNullOrWhiteSpace(remoteVersion))
                {
                    SetUpdateStatus("update.status.checkFailed", "Failed to check the remote version.");
                    ShowUpdate("dialog.update.remoteVersionMissing", "Failed to read the remote version file from GitHub.",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                string localDisplay = string.IsNullOrWhiteSpace(localVersion) ? "unknown" : localVersion;
                if (!UpdateService.IsRemoteVersionNewer(remoteVersion, localVersion))
                {
                    SetUpdateStatus("update.status.uptodate", "Already up to date.");
                    ShowUpdate("dialog.update.alreadyLatest", "Already up to date.",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                SetUpdateStatus("update.status.updateAvailable", "Update available.");
                (UpdatePackage? package, UpdatePackageFailure failure) = await UpdateService.FetchLatestPackageAsync();
                if (package == null)
                {
                    if (failure == UpdatePackageFailure.ReleaseMissing)
                    {
                        SetUpdateStatus("update.status.releaseMissing", "Release information not available.");
                        ShowUpdate("dialog.update.releaseMissing", "Failed to load release information from GitHub.",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }

                    SetUpdateStatus("update.status.assetMissing", "Release package not found.");
                    ShowUpdate("dialog.update.assetMissing", "No .zip release package was found. Publish a release asset first.",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                string tempRoot = Path.Combine(Path.GetTempPath(), $"PatchGUIlite_update_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempRoot);
                string zipName = string.IsNullOrWhiteSpace(package.FileName) ? "PatchGUIlite_update.zip" : package.FileName;
                string zipPath = Path.Combine(tempRoot, zipName);

                SetUpdateStatus("update.status.downloading", "Downloading update...");
                if (!await UpdateService.DownloadFileAsync(package.DownloadUrl, zipPath))
                {
                    SetUpdateStatus("update.status.downloadFailed", "Download failed.");
                    ShowUpdate("dialog.update.downloadFailed", "Failed to download the release package.",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                SetUpdateStatus("update.status.extracting", "Extracting update...");
                string extractRoot = Path.Combine(tempRoot, "extract");
                Directory.CreateDirectory(extractRoot);
                try
                {
                    ZipFile.ExtractToDirectory(zipPath, extractRoot, true);
                }
                catch (Exception ex)
                {
                    SetUpdateStatus("update.status.extractFailed", "Invalid update package.");
                    ShowUpdate("dialog.update.extractFailed", "Failed to extract the update package: {0}",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error,
                        ex.Message);
                    return;
                }

                string? sourceDir = UpdateService.FindUpdateSourceDirectory(extractRoot);
                if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
                {
                    SetUpdateStatus("update.status.extractFailed", "Invalid update package.");
                    ShowUpdate("dialog.update.invalidPackage", "The update package does not contain PatchGUIlite.exe.",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                string targetDir = AppDomain.CurrentDomain.BaseDirectory;
                string exePath = Process.GetCurrentProcess().MainModule?.FileName
                                 ?? Path.Combine(targetDir, "PatchGUIlite.exe");

                SetUpdateStatus("update.status.readyToInstall", "Update ready to install.");
                var result = ShowUpdate(
                    "dialog.update.confirmApply",
                    "Update downloaded.{0}Local: {1}{0}Remote: {2}{0}{0}The app will close to install the update. Continue?",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Information,
                    Environment.NewLine,
                    localDisplay,
                    remoteVersion);
                if (result != MessageBoxResult.OK)
                {
                    SetUpdateStatus("update.status.readyToInstall", "Update ready to install.");
                    return;
                }

                if (!UpdateService.StartUpdateApply(sourceDir, targetDir, exePath, Process.GetCurrentProcess().Id, out string? error))
                {
                    SetUpdateStatus("update.status.applyFailed", "Failed to start updater.");
                    ShowUpdate("dialog.update.updaterFailed", "Failed to start the updater: {0}",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error,
                        error);
                    return;
                }

                shouldExit = true;
                SetUpdateStatus("update.status.closing", "Closing to install update...");
                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                SetUpdateStatus("update.status.failed", "Update failed.");
                ShowUpdate("dialog.update.failed", "Update failed: {0}",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error,
                    ex.Message);
            }
            finally
            {
                if (!shouldExit)
                {
                    PullUpdatesButton.IsEnabled = true;
                    _isUpdating = false;
                }
            }
        }

        #endregion
    }
}
