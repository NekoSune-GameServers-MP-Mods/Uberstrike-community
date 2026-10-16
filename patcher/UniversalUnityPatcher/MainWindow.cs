using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace UniversalUnityPatcher {
	public partial class MainWindow : Form {
		private string assemblyPath;
		private string patchPath;
		private string backupPath;

		private int toolTipIndex = -1;

		public static MainWindow Instance;

		public MainWindow() {
			Instance = this;
			InitializeComponent();
			ApplyNekoSuneTheme();

			Patcher.BackupFiles = CreateBackupCheckbox.Checked;

			WriteToConsole($"{Properties.Resources.ApplicationName} v{typeof(MainWindow).Assembly.GetName().Version}\nAwaiting input...\n");

			if (!string.IsNullOrWhiteSpace(Patcher.AssemblyPath)) {
				assemblyPath = Patcher.AssemblyPath;

				AssemblyPathUpdated();
			}

			if (!string.IsNullOrWhiteSpace(Patcher.PatchPath)) {
				patchPath = Path.GetFullPath(Patcher.PatchPath);

				PatchPathUpdated();
			}

			if (!string.IsNullOrWhiteSpace(Program.CLIOptions.BackupDirectory)) {
				backupPath = Path.GetFullPath(Program.CLIOptions.BackupDirectory);
			}

			BackupPathUpdated();
		}


		private static readonly Color AppBackground = Color.FromArgb(7, 11, 9);
		private static readonly Color PanelBackground = Color.FromArgb(13, 20, 16);
		private static readonly Color SurfaceBackground = Color.FromArgb(17, 27, 21);
		private static readonly Color BorderColor = Color.FromArgb(35, 58, 42);
		private static readonly Color AccentColor = Color.FromArgb(57, 255, 136);
		private static readonly Color TextColor = Color.FromArgb(224, 241, 229);

		private void ApplyNekoSuneTheme() {
			SuspendLayout();
			BackColor = AppBackground;
			ForeColor = TextColor;
			Font = new Font("Segoe UI", 9F, FontStyle.Regular);
			MinimumSize = new Size(880, 430);
			ApplyThemeToControl(this);

			PatchButton.UseVisualStyleBackColor = false;
			PatchButton.BackColor = AccentColor;
			PatchButton.ForeColor = Color.FromArgb(5, 15, 9);
			PatchButton.FlatStyle = FlatStyle.Flat;
			PatchButton.FlatAppearance.BorderColor = AccentColor;
			PatchButton.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
			ResumeLayout(true);
		}

		private void ApplyThemeToControl(Control control) {
			if (control is Form) {
				control.BackColor = AppBackground;
				control.ForeColor = TextColor;
			} else if (control is GroupBox groupBox) {
				groupBox.BackColor = PanelBackground;
				groupBox.ForeColor = AccentColor;
				groupBox.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
			} else if (control is RichTextBox richTextBox) {
				richTextBox.BackColor = Color.FromArgb(8, 13, 10);
				richTextBox.ForeColor = TextColor;
				richTextBox.Font = new Font("Consolas", 9F, FontStyle.Regular);
				richTextBox.BorderStyle = BorderStyle.FixedSingle;
			} else if (control is CheckedListBox checkedListBox) {
				checkedListBox.BackColor = SurfaceBackground;
				checkedListBox.ForeColor = TextColor;
				checkedListBox.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
				checkedListBox.BorderStyle = BorderStyle.FixedSingle;
			} else if (control is Button button) {
				button.UseVisualStyleBackColor = false;
				button.FlatStyle = FlatStyle.Flat;
				button.FlatAppearance.BorderSize = 1;
				button.FlatAppearance.BorderColor = BorderColor;
				button.BackColor = SurfaceBackground;
				button.ForeColor = AccentColor;
				button.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
				button.Cursor = Cursors.Hand;
			} else if (control is CheckBox checkBox) {
				checkBox.BackColor = PanelBackground;
				checkBox.ForeColor = TextColor;
				checkBox.FlatStyle = FlatStyle.Flat;
			} else if (control is Label label) {
				label.BackColor = label.Parent is GroupBox ? PanelBackground : AppBackground;
				label.ForeColor = TextColor;
			} else if (control is Panel || control is TableLayoutPanel || control is FlowLayoutPanel) {
				control.BackColor = PanelBackground;
				control.ForeColor = TextColor;
			} else {
				control.ForeColor = TextColor;
			}

			foreach (Control child in control.Controls) {
				ApplyThemeToControl(child);
			}
		}

		public void ClearConsole() {
			ConsoleWindow.Clear();
		}

		public void WriteToConsole(string text, bool newLine = true) {
			if (newLine) {
				ConsoleWindow.Text += text + "\n";
			} else {
				ConsoleWindow.Text += text;
			}

			ConsoleWindow.SelectionStart = ConsoleWindow.Text.Length;
			ConsoleWindow.ScrollToCaret();
		}

		private void LoadAssemblyButton_Click(object sender, EventArgs e) {
			using (var openFile = new CommonOpenFileDialog { IsFolderPicker = true, Title = "Select an Assembly Directory" }) {
				if (openFile.ShowDialog() == CommonFileDialogResult.Ok) {
					assemblyPath = openFile.FileName;
					Patcher.AssemblyPath = assemblyPath;

					AssemblyPathUpdated();
					BackupPathUpdated();
				}
			}
		}

		private void LoadPatchesButton_Click(object sender, EventArgs e) {
			OpenFileDialog openFile = new OpenFileDialog {
				Title = "Select a Patch file",
				Filter = "Patch XML|*.xml"
			};

			if (openFile.ShowDialog() == DialogResult.OK) {
				patchPath = Path.GetFullPath(openFile.FileName);

				PatchPathUpdated();
			}
		}

		private void SetBackupPathButton_Click(object sender, EventArgs e) {
			using (var openFile = new CommonOpenFileDialog { IsFolderPicker = true, Title = "Select a Backup Directory" }) {
				if (openFile.ShowDialog() == CommonFileDialogResult.Ok) {
					backupPath = openFile.FileName;
					Patcher.BackupPath = backupPath;

					BackupPathUpdated();
				}
			}
		}

		private void ResetBackupPathButton_Click(object sender, EventArgs e) {
			backupPath = null;
			Patcher.BackupPath = null;

			BackupPathUpdated();
		}

		private void PatchButton_Click(object sender, EventArgs e) {
			try {
				Patcher.ApplyPatches();
			} catch (Exception ex) {
				MessageBox.Show($"Failed to apply patches:\n{ex.Message}\n\n{ex.StackTrace}", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
				//WriteToConsole($"Failed to apply patches:\n{ex.Message}\n\n{ex.StackTrace}");
			}
		}

		private void AvailablePatches_ItemCheck(object sender, ItemCheckEventArgs e) {
			Patcher.LoadedPatches.Patches.ElementAt(e.Index).IsEnabled = (e.NewValue == CheckState.Checked);

			UpdatePatchButtonEnabled();
		}

		private void AvailablePatches_MouseMove(object sender, MouseEventArgs e) {
			if (toolTipIndex != AvailablePatches.IndexFromPoint(e.Location)) {
				toolTipIndex = AvailablePatches.IndexFromPoint(e.Location);

				if (toolTipIndex >= 0) {
					patchToolTip.Active = true;
					patchToolTip.SetToolTip(AvailablePatches, Patcher.LoadedPatches.Patches.ElementAt(toolTipIndex).Description.Trim());
				} else {
					patchToolTip.Hide(AvailablePatches);
					patchToolTip.Active = false;
				}
			}
		}

		private void CreateBackupCheckbox_CheckedChanged(object sender, EventArgs e) {
			Patcher.BackupFiles = CreateBackupCheckbox.Checked;
		}

		#region GUI Updates
		private void AssemblyPathUpdated() {
			AssemblyPathLabel.Text = assemblyPath;

			WriteToConsole($"[INFO] Selected Assembly directory: {assemblyPath}");

			UpdatePatchButtonEnabled();
		}

		private void PatchPathUpdated() {
			PatchesPathLabel.Text = Path.GetFileName(patchPath);

			try {
				int loadedPatchesCount = Patcher.LoadPatches(patchPath);
				WriteToConsole($"[INFO] Loaded {loadedPatchesCount} patches from {patchPath}");
			} catch (Exception ex) {
				WriteToConsole($"[ERROR] Failed to load patches from \"{Path.GetFullPath(patchPath)}\":\n{ex.Message}");
			}

			if (Patcher.HasLoadedPatches) {
				AvailablePatches.Items.Clear();

				foreach (var patch in Patcher.LoadedPatches.Patches) {
					if (!patch.IsEnabled && Program.CLIOptions.EnabledPatches.Contains(Patcher.LoadedPatches.Patches.IndexOf(patch) + 1)) {
						patch.IsEnabled = true;
					} else if (patch.IsEnabled && Program.CLIOptions.DisabledPatches.Contains(Patcher.LoadedPatches.Patches.IndexOf(patch) + 1)) {
						patch.IsEnabled = false;
					}

					if (patch.ShortDescription != null && patch.ShortDescription.Length > 0) {
						AvailablePatches.Items.Add(patch.ShortDescription);
					} else {
						AvailablePatches.Items.Add(patch.Name);
					}

					AvailablePatches.SetItemChecked(AvailablePatches.Items.Count - 1, patch.IsEnabled);
				}

				AvailablePatches.Enabled = true;
			} else {
				AvailablePatches.Enabled = false;
				AvailablePatches.Items.Clear();
				AvailablePatches.Items.Add("No patches available");
			}

			UpdatePatchButtonEnabled();
		}

		private void BackupPathUpdated() {
			var path = string.IsNullOrWhiteSpace(backupPath) ? Patcher.BackupPath : backupPath;

			WriteToConsole($"[INFO] Selected backup directory: {path}");

			BackupPathLabel.Text = path;
			UpdateResetBackupPathButtonEnabled();
		}

		private void UpdatePatchButtonEnabled() {
			PatchButton.Enabled = (!string.IsNullOrWhiteSpace(assemblyPath) && !string.IsNullOrWhiteSpace(patchPath) && Patcher.EnabledPatches.Count > 0);
		}

		private void UpdateResetBackupPathButtonEnabled() {
			ResetBackupPathButton.Enabled = !string.IsNullOrWhiteSpace(backupPath);
		}
		#endregion
	}
}
