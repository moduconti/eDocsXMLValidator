using eDocument_Validator.Hybrid;
using eDocument_Validator.Properties;
using eDocument_Validator.Validation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace eDocument_Validator.Forms
{
    /// <summary>
    /// The single window of the application. It collects the choices the user
    /// makes, starts the validation on a background thread so the window stays
    /// responsive, and shows the result.
    /// <para>
    /// No validation rule lives here. This class only connects the controls to the
    /// classes in the Validation and Hybrid folders.
    /// </para>
    /// </summary>
    public partial class MainForm : Form
    {
        private readonly ApplicationFolders folders;
        private readonly DocumentValidator documentValidator = new DocumentValidator();
        private readonly ValidationReportView reportView;

        private Theme theme;
        private ValidationReport lastReport;

        /// <summary>
        /// True while the saved settings are being applied, so that changing a box
        /// does not immediately try to draw a report that does not exist yet.
        /// </summary>
        private bool isLoadingSettings;

        public MainForm()
        {
            InitializeComponent();

            theme = Theme.For(Settings.Default.UseDarkTheme);
            reportView = new ValidationReportView(resultBox, theme);

            LoadSettings();
            ApplyTheme();

            try
            {
                folders = ApplicationFolders.Find();
            }
            catch (DirectoryNotFoundException exception)
            {
                reportView.ShowText(exception.Message);
                SetStatus("The schematron folder was not found.");
                validateButton.Enabled = false;
                return;
            }

            FillFormatList();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WindowTitleBar.UseDarkColours(Handle, theme.IsDark);
        }

        private void LoadSettings()
        {
            isLoadingSettings = true;

            showWarningsBox.Checked = Settings.Default.ShowWarnings;
            showAllChecksBox.Checked = Settings.Default.ShowAllChecks;
            darkThemeBox.Checked = Settings.Default.UseDarkTheme;

            isLoadingSettings = false;
        }

        private void SaveSettings()
        {
            Settings.Default.ShowWarnings = showWarningsBox.Checked;
            Settings.Default.ShowAllChecks = showAllChecksBox.Checked;
            Settings.Default.UseDarkTheme = darkThemeBox.Checked;
            Settings.Default.Save();
        }

        private void FillFormatList()
        {
            List<ValidationFormat> formats = ValidationFormat.ReadAllFrom(folders.SchematronFolder);

            formatBox.Items.Clear();
            foreach (ValidationFormat format in formats)
            {
                formatBox.Items.Add(format);
            }

            if (formats.Count == 0)
            {
                reportView.ShowText(
                    "No formats were found in '" + folders.SchematronFolder + "'." + Environment.NewLine
                    + "Create one folder per format there and put the validation files inside it.");
            }
        }

        // --- appearance -------------------------------------------------------

        /// <summary>
        /// Applies the chosen colours to every control. Windows Forms controls do
        /// not inherit colours from a theme by themselves, so each one is set here.
        /// </summary>
        private void ApplyTheme()
        {
            BackColor = theme.Background;

            foreach (Panel panel in new[] { inputPanel, filterPanel })
            {
                panel.BackColor = theme.PanelBackground;
            }

            titleLabel.ForeColor = theme.Heading;
            resultLabel.ForeColor = theme.Heading;

            foreach (Label label in new[] { formatLabel, documentLabel })
            {
                label.ForeColor = theme.Text;
            }

            statusLabel.ForeColor = theme.Quiet;

            foreach (CheckBox box in new[] { showWarningsBox, showAllChecksBox, darkThemeBox })
            {
                box.ForeColor = theme.Text;
                box.BackColor = theme.PanelBackground;
            }

            pathBox.BackColor = theme.InputBackground;
            pathBox.ForeColor = theme.Text;
            pathBox.BorderStyle = BorderStyle.FixedSingle;

            formatBox.BackColor = theme.InputBackground;
            formatBox.ForeColor = theme.Text;
            formatBox.FlatStyle = theme.IsDark ? FlatStyle.Flat : FlatStyle.Standard;

            foreach (Button button in new[] { browseButton, validateButton, openResultsButton })
            {
                button.ForeColor = theme.Text;
                button.BackColor = theme.ButtonBackground;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = theme.Border;
                button.UseVisualStyleBackColor = false;
            }

            resultBox.BackColor = theme.Background;
            resultBox.ForeColor = theme.Text;

            reportView.UseTheme(theme);
            WindowTitleBar.UseDarkColours(Handle, theme.IsDark);
        }

        private void darkThemeBox_CheckedChanged(object sender, EventArgs e)
        {
            theme = Theme.For(darkThemeBox.Checked);
            ApplyTheme();

            if (!isLoadingSettings)
            {
                SaveSettings();
                ShowReport();
            }
        }

        // --- user actions ---------------------------------------------------

        /// <summary>
        /// Starts compiling the rules of the chosen format straight away, in the
        /// background. Compiling the large Schematron files is by far the slowest
        /// part of a validation, and doing it now means it is usually finished
        /// before the user has chosen a document and pressed Validate.
        /// </summary>
        private void formatBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ValidationFormat format = formatBox.SelectedItem as ValidationFormat;
            if (format == null)
            {
                return;
            }

            SetStatus("Preparing the rules of " + format.Name + "...");

            Task.Run(() => documentValidator.Prepare(format))
                .ContinueWith(
                    task => SetStatus("The rules of " + format.Name + " are ready. Choose a document and press Validate."),
                    TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>
        /// Sets the status text, unless the window has been closed in the meantime.
        /// Preparing the rules and validating both run in the background and finish
        /// after the window may already be gone.
        /// </summary>
        private void SetStatus(string status)
        {
            if (IsDisposed || statusLabel.IsDisposed)
            {
                return;
            }

            statusLabel.Text = status;
        }

        private void browseButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select the document to validate";
                dialog.Filter = "Electronic documents (*.xml;*.pdf)|*.xml;*.pdf"
                              + "|XML documents (*.xml)|*.xml"
                              + "|Hybrid PDF invoices (*.pdf)|*.pdf";

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    pathBox.Text = dialog.FileName;
                }
            }
        }

        private async void validateButton_Click(object sender, EventArgs e)
        {
            ValidationFormat format = formatBox.SelectedItem as ValidationFormat;

            if (format == null)
            {
                MessageBox.Show(this, "Please choose a format first.", "No format chosen",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string documentPath = pathBox.Text.Trim();
            if (!File.Exists(documentPath))
            {
                MessageBox.Show(this, "The file was not found:" + Environment.NewLine + documentPath,
                    "File not found", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool isPdf = string.Equals(Path.GetExtension(documentPath), ".pdf", StringComparison.OrdinalIgnoreCase);

            // A hybrid PDF always carries a CII document inside it. Validating that
            // against a UBL format would produce a long list of failures that only
            // mean the wrong format was chosen, so ask first.
            if (isPdf && !format.IsForCrossIndustryInvoice() && !ConfirmFormatChoice(format))
            {
                return;
            }

            await RunValidation(format, documentPath, isPdf);
        }

        private bool ConfirmFormatChoice(ValidationFormat format)
        {
            string question =
                "A hybrid PDF invoice contains a CII document, but the format '" + format.Name
                + "' does not look like a CII format." + Environment.NewLine + Environment.NewLine
                + "The checks on the PDF itself will still run, but the rules will probably report "
                + "many failures that only mean the wrong format was chosen." + Environment.NewLine + Environment.NewLine
                + "Do you want to continue?";

            return MessageBox.Show(this, question, "The format may not match the document",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        private async Task RunValidation(ValidationFormat format, string documentPath, bool isPdf)
        {
            SetBusy(true, "Preparing...");

            try
            {
                folders.ClearResults();

                // The work runs on a background thread so the window keeps
                // repainting; large Schematron files take several seconds.
                ValidationReport report = await Task.Run(() =>
                    BuildReport(format, documentPath, isPdf));

                if (IsDisposed)
                {
                    return;
                }

                lastReport = report;
                ShowReport();

                string reportPath = ValidationReportWriter.Write(report, folders.ResultsFolder);
                SetStatus(report.Summary() + "  Full report: " + reportPath);
            }
            catch (Exception exception)
            {
                lastReport = null;
                reportView.ShowText("The validation could not be completed." + Environment.NewLine
                    + Environment.NewLine + exception.Message);
                SetStatus("The validation stopped because of an error.");
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        /// <summary>
        /// Runs every check and collects the messages into one report. This runs on
        /// a background thread, so it must not touch any control.
        /// </summary>
        private ValidationReport BuildReport(ValidationFormat format, string documentPath, bool isPdf)
        {
            ValidationReport report = new ValidationReport(documentPath);
            report.AddFact("Format", format.Name);

            string xmlToValidate = documentPath;

            if (isPdf)
            {
                ReportProgress("Checking the PDF container...");

                HybridValidationResult hybrid = HybridValidator.Validate(documentPath, folders.ResultsFolder);

                if (VeraPdfRunner.IsEnabled)
                {
                    ReportProgress("Checking PDF/A conformance with veraPDF...");
                    hybrid.Groups.Add(VeraPdfRunner.Validate(documentPath, folders.RootFolder));
                }

                report.AddGroups(hybrid.Groups);
                AddHybridFacts(report, hybrid);

                if (hybrid.ExtractedXmlPath == null)
                {
                    ValidationGroup group = report.AddGroup("Rules for the embedded document");
                    group.Warning("RUN-01",
                        "No XML invoice could be taken out of the PDF, so the rules of the format '"
                        + format.Name + "' were not run.");
                    return report;
                }

                xmlToValidate = hybrid.ExtractedXmlPath;
                report.AddFact("Extracted", Path.GetFileName(xmlToValidate));
            }

            documentValidator.FileStarted = fileName => ReportProgress("Checking with " + fileName + "...");
            report.AddGroups(documentValidator.Validate(format, xmlToValidate));

            return report;
        }

        private static void AddHybridFacts(ValidationReport report, HybridValidationResult hybrid)
        {
            report.AddFact("Standard", HybridSpecs.Describe(hybrid.Flavour));
            report.AddFact("Attachment", hybrid.AttachmentName);
            report.AddFact("Profile", hybrid.DeclaredConformanceLevel);
            report.AddFact("Guideline", hybrid.GuidelineId);
        }

        // --- showing the result ---------------------------------------------

        private void ShowReport()
        {
            if (lastReport == null)
            {
                return;
            }

            ValidationSeverity lowest = showAllChecksBox.Checked
                ? ValidationSeverity.Information
                : (showWarningsBox.Checked ? ValidationSeverity.Warning : ValidationSeverity.Error);

            reportView.Show(lastReport, lowest);
        }

        private void filter_CheckedChanged(object sender, EventArgs e)
        {
            if (isLoadingSettings)
            {
                return;
            }

            // "Show all checks" includes the warnings, so keep the two boxes
            // consistent instead of letting them contradict each other.
            if (showAllChecksBox.Checked && !showWarningsBox.Checked)
            {
                showWarningsBox.Checked = true;
                return;
            }

            SaveSettings();
            ShowReport();
        }

        private void openResultsButton_Click(object sender, EventArgs e)
        {
            if (folders == null || !Directory.Exists(folders.ResultsFolder))
            {
                return;
            }

            Process.Start("explorer.exe", "\"" + folders.ResultsFolder + "\"");
        }

        // --- window state ----------------------------------------------------

        private void SetBusy(bool busy, string status)
        {
            validateButton.Enabled = !busy;
            browseButton.Enabled = !busy;
            formatBox.Enabled = !busy;
            pathBox.Enabled = !busy;

            progressBar.Visible = busy;

            UseWaitCursor = busy;

            if (status != null)
            {
                SetStatus(status);
            }
        }

        /// <summary>
        /// Shows progress from the background thread. Control properties may only
        /// be changed on the thread that created the window, so the update is
        /// handed over to it.
        /// </summary>
        private void ReportProgress(string status)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            try
            {
                if (statusLabel.InvokeRequired)
                {
                    statusLabel.BeginInvoke(new Action(() => SetStatus(status)));
                }
                else
                {
                    SetStatus(status);
                }
            }
            catch (ObjectDisposedException)
            {
                // The window was closed between the check above and the call. There
                // is nothing left to show the progress on.
            }
        }
    }
}
