using eDocument_Validator.Validation;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace eDocument_Validator.Forms
{
    /// <summary>
    /// Draws a validation report into a rich text box using colour, weight and
    /// spacing, so that the reader can see at a glance whether the document passed
    /// and where the problems are.
    /// <para>
    /// Colour is never the only signal. Every line also carries a word - ERROR,
    /// WARNING or OK - because colour alone is unreliable for readers with colour
    /// blindness and disappears when the report is printed.
    /// </para>
    /// </summary>
    public class ValidationReportView
    {
        /// <summary>Left edge of every message line, in characters.</summary>
        private const string LinePrefix = "   ";

        /// <summary>
        /// Width of the column holding the words ERROR and WARNING. "WARNING" is the
        /// longest of them, and one space separates the column from the next.
        /// </summary>
        private const int SeverityColumnWidth = 8;

        /// <summary>Width of the column holding the labels under a message.</summary>
        private const int LabelColumnWidth = 10;

        private readonly RichTextBox output;

        private readonly Font textFont;
        private readonly Font boldFont;
        private readonly Font headingFont;
        private readonly Font bannerFont;
        private readonly Font fixedFont;
        private readonly Font fixedBoldFont;

        private int ruleColumnWidth = 8;
        private int messageIndent;

        private Theme theme;

        public ValidationReportView(RichTextBox output, Theme theme)
        {
            this.output = output;
            this.theme = theme;

            textFont = new Font("Segoe UI", 10f, FontStyle.Regular);
            boldFont = new Font("Segoe UI", 10f, FontStyle.Bold);
            headingFont = new Font("Segoe UI", 11.5f, FontStyle.Bold);
            bannerFont = new Font("Segoe UI", 14f, FontStyle.Bold);
            fixedFont = new Font("Consolas", 9.5f, FontStyle.Regular);
            fixedBoldFont = new Font("Consolas", 9.5f, FontStyle.Bold);
        }

        /// <summary>
        /// Changes the appearance. The caller draws the report again afterwards,
        /// because the colours are written into the text as it is added.
        /// </summary>
        public void UseTheme(Theme newTheme)
        {
            theme = newTheme;
        }

        /// <summary>
        /// Replaces the contents of the box with the report. Messages below
        /// <paramref name="lowestSeverityShown"/> are left out, which lets the user
        /// hide the checks that passed and look only at the problems.
        /// </summary>
        public void Show(ValidationReport report, ValidationSeverity lowestSeverityShown)
        {
            output.Clear();

            try
            {
                // The rule column is made just wide enough for the rule names that
                // are actually shown. Schematron rule names are much longer than the
                // built-in ones, so a fixed width would either waste space or push
                // the text out of line.
                MeasureColumns(report, lowestSeverityShown);

                WriteBanner(report);
                WriteFacts(report);

                foreach (ValidationGroup group in report.Groups)
                {
                    WriteGroup(group, lowestSeverityShown);
                }

                WriteHiddenNotice(report, lowestSeverityShown);
            }
            finally
            {
                output.SelectionStart = 0;
                output.SelectionLength = 0;
                output.ScrollToCaret();
            }
        }

        public void ShowText(string text)
        {
            output.Clear();
            SetIndent(0, 0);
            WriteLine(text, textFont, theme.Information);
        }

        // --- parts of the report -------------------------------------------

        private void WriteBanner(ValidationReport report)
        {
            Color background;
            Color foreground;
            string headline;

            if (!report.IsValid)
            {
                background = theme.ErrorBackground;
                foreground = theme.Error;
                headline = "NOT VALID";
            }
            else if (report.WarningCount > 0)
            {
                background = theme.WarningBackground;
                foreground = theme.Warning;
                headline = "VALID, WITH WARNINGS";
            }
            else
            {
                background = theme.SuccessBackground;
                foreground = theme.Success;
                headline = "VALID";
            }

            SetIndent(0, 0);
            WriteLine("  " + headline, bannerFont, foreground, background);
            WriteLine("  " + report.Summary(), textFont, foreground, background);
            WriteLine(string.Empty, textFont, theme.Text);
        }

        private void WriteFacts(ValidationReport report)
        {
            if (report.Facts.Count == 0)
            {
                return;
            }

            int labelWidth = report.Facts.Max(f => f.Key.Length) + 2;

            SetIndent(0, WidthOfFixedText(labelWidth));
            foreach (KeyValuePair<string, string> fact in report.Facts)
            {
                Write(fact.Key.PadRight(labelWidth), fixedFont, theme.Quiet);
                WriteLine(fact.Value, fixedFont, theme.Information);
            }

            SetIndent(0, 0);
            WriteLine(string.Empty, textFont, theme.Text);
        }

        private void WriteGroup(ValidationGroup group, ValidationSeverity lowestSeverityShown)
        {
            SetIndent(0, 0);

            Write(group.Title + "   ", headingFont, theme.Heading);

            if (group.IsClean)
            {
                WriteLine("OK", boldFont, theme.Success);
            }
            else
            {
                WriteLine(ValidationReportWriter.DescribeCounts(group), boldFont,
                    group.ErrorCount > 0 ? theme.Error : theme.Warning);
            }

            if (!string.IsNullOrEmpty(group.Subtitle))
            {
                WriteLine(group.Subtitle, fixedFont, theme.Quiet);
            }

            List<ValidationMessage> shown = ValidationReportWriter
                .OrderBySeverity(group.Messages)
                .Where(m => m.Severity >= lowestSeverityShown)
                .ToList();

            foreach (ValidationMessage message in shown)
            {
                WriteMessage(message);
            }

            if (shown.Count == 0 && group.Messages.Count > 0)
            {
                SetIndent(0, 0);
                WriteLine(LinePrefix + group.Messages.Count + " check(s) passed, hidden by the filter above.",
                    fixedFont, theme.Quiet);
            }

            SetIndent(0, 0);
            WriteLine(string.Empty, textFont, theme.Text);
        }

        private void WriteMessage(ValidationMessage message)
        {
            Color colour = ColourFor(message.Severity);

            // A long message must wrap under itself rather than under the severity
            // word, otherwise the columns stop lining up as soon as one line is
            // wider than the window.
            SetIndent(0, messageIndent);

            // The severity word and the rule name are both written in the
            // fixed-width font. Padding with spaces only lines columns up when every
            // character has the same width; in the proportional font used for the
            // message text it would not.
            Write(LinePrefix + LabelFor(message.Severity).PadRight(SeverityColumnWidth), fixedBoldFont, colour);
            Write((message.Rule ?? string.Empty).PadRight(ruleColumnWidth) + " ", fixedFont, theme.Quiet);

            WriteLine(message.Text, textFont, colour);

            foreach (KeyValuePair<string, string> line in message.ExtraLines())
            {
                SetIndent(messageIndent, 0);
                Write(line.Key.PadRight(LabelColumnWidth), fixedFont, theme.Quiet);
                WriteLine(line.Value, fixedFont, theme.Information);
            }
        }

        /// <summary>
        /// Works out how wide the rule column has to be, and how far wrapped lines
        /// have to be pushed in so that they start under the message text.
        /// </summary>
        private void MeasureColumns(ValidationReport report, ValidationSeverity lowestSeverityShown)
        {
            IEnumerable<ValidationMessage> shown = report.Groups
                .SelectMany(g => g.Messages)
                .Where(m => m.Severity >= lowestSeverityShown);

            int longestRule = 0;
            foreach (ValidationMessage message in shown)
            {
                if (message.Rule != null && message.Rule.Length > longestRule)
                {
                    longestRule = message.Rule.Length;
                }
            }

            // A very long rule name would push the text off the window, so the
            // column stops growing at a sensible width.
            ruleColumnWidth = Math.Min(Math.Max(longestRule, 6), 22);

            int prefixCharacters = LinePrefix.Length + SeverityColumnWidth + ruleColumnWidth + 1;
            messageIndent = WidthOfFixedText(prefixCharacters);
        }

        /// <summary>
        /// Measures how wide a number of characters of the fixed-width font is, so
        /// the indent of wrapped lines matches the text written before them.
        /// </summary>
        private int WidthOfFixedText(int characterCount)
        {
            using (Graphics graphics = output.CreateGraphics())
            {
                // A single character is measured and multiplied, because measuring a
                // string of spaces is unreliable: trailing spaces are ignored.
                SizeF size = graphics.MeasureString("0", fixedFont, int.MaxValue, StringFormat.GenericTypographic);
                return (int)Math.Round(size.Width * characterCount);
            }
        }

        private void WriteHiddenNotice(ValidationReport report, ValidationSeverity lowestSeverityShown)
        {
            if (lowestSeverityShown == ValidationSeverity.Information)
            {
                return;
            }

            int hidden = report.Groups
                .SelectMany(g => g.Messages)
                .Count(m => m.Severity < lowestSeverityShown);

            if (hidden == 0)
            {
                return;
            }

            SetIndent(0, 0);
            WriteLine(hidden + " message(s) are hidden. Tick 'Show all checks' to see everything that was verified.",
                fixedFont, theme.Quiet);
        }

        // --- helpers --------------------------------------------------------

        private Color ColourFor(ValidationSeverity severity)
        {
            switch (severity)
            {
                case ValidationSeverity.Error:
                    return theme.Error;
                case ValidationSeverity.Warning:
                    return theme.Warning;
                default:
                    return theme.Information;
            }
        }

        /// <summary>
        /// The word shown beside every message. "OK" is used for information,
        /// because at that level the message describes a check that passed.
        /// </summary>
        private static string LabelFor(ValidationSeverity severity)
        {
            switch (severity)
            {
                case ValidationSeverity.Error:
                    return "ERROR";
                case ValidationSeverity.Warning:
                    return "WARNING";
                default:
                    return "OK";
            }
        }

        /// <summary>
        /// Sets the indentation of the paragraphs written next. The hanging indent
        /// applies to every line of a paragraph after the first.
        /// </summary>
        private void SetIndent(int firstLine, int followingLines)
        {
            output.SelectionStart = output.TextLength;
            output.SelectionLength = 0;
            output.SelectionIndent = firstLine;
            output.SelectionHangingIndent = followingLines;
        }

        private void Write(string text, Font font, Color colour)
        {
            Write(text, font, colour, theme.Background);
        }

        private void Write(string text, Font font, Color colour, Color background)
        {
            output.SelectionStart = output.TextLength;
            output.SelectionLength = 0;
            output.SelectionFont = font;
            output.SelectionColor = colour;
            output.SelectionBackColor = background;
            output.AppendText(text);
        }

        private void WriteLine(string text, Font font, Color colour)
        {
            WriteLine(text, font, colour, theme.Background);
        }

        private void WriteLine(string text, Font font, Color colour, Color background)
        {
            Write(text + Environment.NewLine, font, colour, background);
        }
    }
}
