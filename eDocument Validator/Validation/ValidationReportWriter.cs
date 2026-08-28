using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace eDocument_Validator.Validation
{
    /// <summary>
    /// Writes a report to the results folder as plain text, and keeps the untouched
    /// output of each tool next to it as evidence.
    /// </summary>
    public static class ValidationReportWriter
    {
        private const string ReportFileName = "validation-report.txt";

        /// <summary>
        /// Writes the readable report plus one file per group that produced raw
        /// output. Returns the path of the readable report.
        /// </summary>
        public static string Write(ValidationReport report, string resultsFolder)
        {
            Directory.CreateDirectory(resultsFolder);

            string reportPath = Path.Combine(resultsFolder, ReportFileName);
            File.WriteAllText(reportPath, BuildText(report), new UTF8Encoding(true));

            foreach (ValidationGroup group in report.Groups)
            {
                if (string.IsNullOrEmpty(group.RawReport))
                {
                    continue;
                }

                string rawPath = Path.Combine(resultsFolder, MakeSafeFileName(group.Title) + ".xml");
                try
                {
                    File.WriteAllText(rawPath, group.RawReport, new UTF8Encoding(true));
                }
                catch (IOException)
                {
                    // Keeping the evidence file is helpful but not essential; the
                    // readable report has already been written.
                }
            }

            return reportPath;
        }

        public static string BuildText(ValidationReport report)
        {
            StringBuilder text = new StringBuilder();

            text.AppendLine("Validation report");
            text.AppendLine("=================");
            // All labels are padded to the same width so the values line up, however
            // long the longest label happens to be.
            int labelWidth = Math.Max(
                report.Facts.Count == 0 ? 0 : report.Facts.Max(f => f.Key.Length),
                "Document".Length) + 2;

            text.AppendLine("Document".PadRight(labelWidth) + report.DocumentPath);
            text.AppendLine("Checked".PadRight(labelWidth) + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            foreach (KeyValuePair<string, string> fact in report.Facts)
            {
                text.AppendLine(fact.Key.PadRight(labelWidth) + fact.Value);
            }

            text.AppendLine();
            text.AppendLine("Result".PadRight(labelWidth) + report.Summary());
            text.AppendLine();

            foreach (ValidationGroup group in report.Groups)
            {
                text.AppendLine(group.Title);
                text.AppendLine(new string('-', Math.Max(group.Title.Length, 3)));

                if (!string.IsNullOrEmpty(group.Subtitle))
                {
                    text.AppendLine(group.Subtitle);
                }

                text.AppendLine(DescribeCounts(group));
                text.AppendLine();

                foreach (ValidationMessage message in OrderBySeverity(group.Messages))
                {
                    text.AppendLine(message.ToString());

                    if (!string.IsNullOrWhiteSpace(message.Technical))
                    {
                        text.AppendLine("    rule      " + message.Technical);
                    }
                }

                text.AppendLine();
            }

            return text.ToString();
        }

        /// <summary>
        /// Puts errors first, then warnings, then the checks that passed, so the
        /// most important lines are at the top of every group.
        /// </summary>
        public static IEnumerable<ValidationMessage> OrderBySeverity(IEnumerable<ValidationMessage> messages)
        {
            return messages.OrderByDescending(m => m.Severity);
        }

        public static string DescribeCounts(ValidationGroup group)
        {
            if (group.IsClean)
            {
                return "No problems found.";
            }

            List<string> parts = new List<string>();
            if (group.ErrorCount > 0)
            {
                parts.Add(group.ErrorCount + (group.ErrorCount == 1 ? " error" : " errors"));
            }
            if (group.WarningCount > 0)
            {
                parts.Add(group.WarningCount + (group.WarningCount == 1 ? " warning" : " warnings"));
            }

            return string.Join(", ", parts);
        }

        private static string MakeSafeFileName(string name)
        {
            StringBuilder safe = new StringBuilder(name.Length);

            foreach (char character in name)
            {
                safe.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), character) >= 0 ? '_' : character);
            }

            return safe.ToString();
        }
    }
}
