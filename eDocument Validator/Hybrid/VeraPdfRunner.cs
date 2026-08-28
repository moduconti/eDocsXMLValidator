using eDocument_Validator.Validation;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text;
using System.Xml.Linq;

namespace eDocument_Validator.Hybrid
{
    /// <summary>
    /// Optional bridge to veraPDF, the reference PDF/A validator.
    /// <para>
    /// The checks in <see cref="HybridValidator"/> cover the structural and
    /// metadata rules that make a file a valid Factur-X or ZUGFeRD invoice. They
    /// deliberately do not attempt full PDF/A conformance - font embedding,
    /// colour spaces, transparency and the rest are a far larger rule set, and
    /// veraPDF is the implementation the industry treats as authoritative.
    /// </para>
    /// <para>
    /// veraPDF is therefore treated as an optional add-on. When it is present the
    /// report gains a real conformance verdict; when it is not, the report says so
    /// plainly rather than implying the file passed. Nothing else in the
    /// application depends on it.
    /// </para>
    /// </summary>
    public static class VeraPdfRunner
    {
        /// <summary>Main class of the veraPDF greenfield command-line application.</summary>
        private const string EntryPoint = "org.verapdf.apps.GreenfieldCliWrapper";

        private const int TimeoutMilliseconds = 120000;

        /// <summary>
        /// Whether the veraPDF pass should run at all, from the enableVeraPdf
        /// application setting. It is off by default: the structural checks are the
        /// ones that decide whether a file is a usable Factur-X or ZUGFeRD invoice,
        /// and a veraPDF run costs several seconds per document.
        /// </summary>
        public static bool IsEnabled
        {
            get
            {
                bool enabled;
                return bool.TryParse(ConfigurationManager.AppSettings["enableVeraPdf"], out enabled) && enabled;
            }
        }

        /// <summary>
        /// Where veraPDF and a Java runtime were found, if they were.
        /// </summary>
        public class Installation
        {
            public string JavaExecutable { get; set; }
            public string VeraPdfHome { get; set; }
            public string Reason { get; set; }

            public bool IsAvailable
            {
                get { return !string.IsNullOrEmpty(JavaExecutable) && !string.IsNullOrEmpty(VeraPdfHome); }
            }
        }

        /// <summary>
        /// Looks for veraPDF, preferring an explicit App.config setting, then the
        /// copy under the repository tools folder, then a Java runtime on PATH.
        /// </summary>
        public static Installation Locate(string repositoryRoot)
        {
            Installation installation = new Installation();

            string configuredHome = ConfigurationManager.AppSettings["veraPdfHome"];
            string configuredJava = ConfigurationManager.AppSettings["javaExecutable"];

            string home = FirstExistingDirectory(
                configuredHome,
                repositoryRoot == null ? null : Path.Combine(repositoryRoot, "tools", "verapdf"));

            if (home == null)
            {
                installation.Reason = "veraPDF was not found. Install it under tools\\verapdf, or set the veraPdfHome key in the application configuration.";
                return installation;
            }

            if (!Directory.Exists(Path.Combine(home, "bin")))
            {
                installation.Reason = "The veraPDF folder '" + home + "' has no bin directory, so its jars could not be located.";
                return installation;
            }

            string java = FirstExistingFile(
                configuredJava,
                repositoryRoot == null ? null : Path.Combine(repositoryRoot, "tools", "jre", "bin", "java.exe"),
                JavaFromEnvironment());

            if (java == null)
            {
                installation.Reason = "veraPDF was found at '" + home + "' but no Java runtime is available to run it. Install a JRE under tools\\jre, set JAVA_HOME, or set the javaExecutable key in the application configuration.";
                return installation;
            }

            installation.VeraPdfHome = home;
            installation.JavaExecutable = java;
            return installation;
        }

        /// <summary>
        /// Runs veraPDF against the file and turns its machine-readable report into
        /// validation issues. Returns a section describing why it could not run
        /// when veraPDF is unavailable, so the caller does not have to branch.
        /// </summary>
        public static ValidationGroup Validate(string pdfPath, string repositoryRoot)
        {
            ValidationGroup section = new ValidationGroup("PDF/A conformance (veraPDF)");

            Installation installation = Locate(repositoryRoot);
            if (!installation.IsAvailable)
            {
                section.Warning("PDFA-00", "Full PDF/A conformance was not verified. " + installation.Reason);
                return section;
            }

            string report;
            try
            {
                report = Execute(installation, pdfPath);
            }
            catch (Exception ex)
            {
                section.Warning("PDFA-00", "veraPDF could not be run, so full PDF/A conformance was not verified: " + ex.Message);
                return section;
            }

            ParseReport(report, section);
            return section;
        }

        private static string Execute(Installation installation, string pdfPath)
        {
            // The jars are passed as a classpath wildcard and the entry point is
            // invoked directly rather than through verapdf.bat, so that paths
            // containing spaces are not re-parsed by the command processor.
            string classpath = Path.Combine(installation.VeraPdfHome, "bin") + Path.DirectorySeparatorChar + "*";

            StringBuilder arguments = new StringBuilder();
            arguments.Append("-classpath \"").Append(classpath).Append("\" ");
            arguments.Append("-Dfile.encoding=UTF8 -XX:+IgnoreUnrecognizedVMOptions ");
            arguments.Append("--add-exports=java.base/sun.security.pkcs=ALL-UNNAMED ");
            arguments.Append(EntryPoint);
            // Flavour 0 asks veraPDF to take the claimed conformance level from the
            // file's own XMP, which is what a recipient of the invoice would do.
            arguments.Append(" --format xml --flavour 0 ");
            // The process runs with veraPDF's own folder as its working directory,
            // so the file has to be named absolutely.
            arguments.Append('"').Append(Path.GetFullPath(pdfPath)).Append('"');

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = installation.JavaExecutable,
                Arguments = arguments.ToString(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = installation.VeraPdfHome,
                StandardOutputEncoding = Encoding.UTF8
            };

            using (Process process = Process.Start(startInfo))
            {
                // Both pipes are drained concurrently. veraPDF writes a large XML
                // report to stdout and its logging to stderr; reading one to
                // completion before touching the other deadlocks as soon as the
                // unread pipe's buffer fills, which a multi-megabyte report does.
                StringBuilder standardOutput = new StringBuilder();
                StringBuilder standardError = new StringBuilder();

                using (AutoResetEvent outputDone = new AutoResetEvent(false))
                using (AutoResetEvent errorDone = new AutoResetEvent(false))
                {
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data == null) { outputDone.Set(); } else { standardOutput.AppendLine(e.Data); }
                    };
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data == null) { errorDone.Set(); } else { standardError.AppendLine(e.Data); }
                    };

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    if (!process.WaitForExit(TimeoutMilliseconds)
                        || !outputDone.WaitOne(TimeoutMilliseconds)
                        || !errorDone.WaitOne(TimeoutMilliseconds))
                    {
                        try { process.Kill(); } catch { }
                        throw new TimeoutException("veraPDF did not finish within " + (TimeoutMilliseconds / 1000) + " seconds.");
                    }
                }

                string output = standardOutput.ToString();
                string error = standardError.ToString();

                if (string.IsNullOrWhiteSpace(output))
                {
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
                        ? "veraPDF produced no output."
                        : error.Trim());
                }

                return output;
            }
        }

        /// <summary>
        /// Reads a veraPDF machine-readable report. Each failed rule becomes one
        /// issue carrying the specification clause and test number, so a finding can
        /// be looked up in the PDF/A standard.
        /// </summary>
        private static void ParseReport(string reportXml, ValidationGroup section)
        {
            XDocument document;
            try
            {
                document = XDocument.Parse(reportXml);
            }
            catch (Exception ex)
            {
                section.Warning("PDFA-00", "The veraPDF report could not be parsed: " + ex.Message);
                return;
            }

            XElement validationReport = document.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "validationReport");

            if (validationReport == null)
            {
                XElement taskException = document.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == "exceptionMessage" || e.Name.LocalName == "taskException");

                section.Warning("PDFA-00", taskException != null
                    ? "veraPDF could not validate the file: " + taskException.Value.Trim()
                    : "The veraPDF report contains no validation result.");
                return;
            }

            string profile = (string)validationReport.Attribute("profileName");
            bool compliant = string.Equals((string)validationReport.Attribute("isCompliant"), "true", StringComparison.OrdinalIgnoreCase);

            List<XElement> failedRules = validationReport.Descendants()
                .Where(e => e.Name.LocalName == "rule"
                            && string.Equals((string)e.Attribute("status"), "FAILED", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (compliant && failedRules.Count == 0)
            {
                section.Info("PDFA-01", "veraPDF reports the file as conforming"
                    + (string.IsNullOrEmpty(profile) ? "." : " to " + profile + "."));
                return;
            }

            section.Error("PDFA-01", "veraPDF reports the file as NOT conforming"
                + (string.IsNullOrEmpty(profile) ? "." : " to " + profile + ".")
                + " " + failedRules.Count + " rule(s) failed.");

            foreach (XElement rule in failedRules)
            {
                string clause = (string)rule.Attribute("clause");
                string test = (string)rule.Attribute("testNumber");
                string failedChecks = (string)rule.Attribute("failedChecks");

                XElement description = rule.Elements().FirstOrDefault(e => e.Name.LocalName == "description");

                ValidationMessage issue = section.Error(
                    "PDFA-" + (clause ?? "?") + "-" + (test ?? "?"),
                    description != null ? Flatten(description.Value) : "Rule failed.");

                XElement firstContext = rule.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == "context");

                if (firstContext != null)
                {
                    issue.Found = Flatten(firstContext.Value)
                        + (failedChecks != null && failedChecks != "1" ? " (and " + failedChecks + " occurrences in total)" : string.Empty);
                }
            }
        }

        private static string Flatten(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return string.Join(" ", value.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => part.Length > 0));
        }

        private static string FirstExistingDirectory(params string[] candidates)
        {
            return candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c) && Directory.Exists(c));
        }

        private static string FirstExistingFile(params string[] candidates)
        {
            return candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c) && File.Exists(c));
        }

        /// <summary>Resolves java.exe from JAVA_HOME, then from PATH.</summary>
        private static string JavaFromEnvironment()
        {
            string javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrWhiteSpace(javaHome))
            {
                string candidate = Path.Combine(javaHome, "bin", "java.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            string path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            foreach (string directory in path.Split(Path.PathSeparator))
            {
                try
                {
                    string candidate = Path.Combine(directory.Trim(), "java.exe");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch (ArgumentException)
                {
                    // PATH entries can contain characters that are invalid in a path;
                    // skip those rather than failing the whole lookup.
                }
            }

            return null;
        }
    }
}
