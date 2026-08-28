using System;
using System.Collections.Generic;
using System.Text;

namespace eDocument_Validator.Validation
{
    /// <summary>
    /// One finding about a document. Every check that runs produces one of these,
    /// whether it passed or failed, so the report shows both what is wrong and what
    /// was verified.
    /// </summary>
    public class ValidationMessage
    {
        public ValidationMessage(ValidationSeverity severity, string rule, string text)
        {
            Severity = severity;
            Rule = rule;
            Text = text;
        }

        /// <summary>How serious this finding is.</summary>
        public ValidationSeverity Severity { get; private set; }

        /// <summary>
        /// Short identifier of the rule that produced the message, for example
        /// "EF-07" for a built-in check or "BR-S-08" for a Schematron rule. Empty
        /// when the source does not give one.
        /// </summary>
        public string Rule { get; private set; }

        /// <summary>The finding, written for a person to read.</summary>
        public string Text { get; private set; }

        /// <summary>What the specification asks for. Optional.</summary>
        public string Expected { get; set; }

        /// <summary>What the document contains instead. Optional.</summary>
        public string Found { get; set; }

        /// <summary>
        /// Where in the document the problem is, for example the path to an XML
        /// element or a line number. Optional.
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// Extra technical information, such as the rule expression a Schematron
        /// evaluated. It is written to the result file but kept out of the window,
        /// because it is long and only useful when investigating a rule itself.
        /// </summary>
        public string Technical { get; set; }

        /// <summary>The extra lines shown under the message text, in display order.</summary>
        public IEnumerable<KeyValuePair<string, string>> ExtraLines()
        {
            if (!string.IsNullOrWhiteSpace(Expected))
            {
                yield return new KeyValuePair<string, string>("expected", Expected);
            }

            if (!string.IsNullOrWhiteSpace(Found))
            {
                yield return new KeyValuePair<string, string>("found", Found);
            }

            if (!string.IsNullOrWhiteSpace(Location))
            {
                yield return new KeyValuePair<string, string>("position", Location);
            }
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();

            builder.Append("[").Append(Severity.ToString().ToUpperInvariant()).Append("] ");
            if (!string.IsNullOrEmpty(Rule))
            {
                builder.Append(Rule).Append(": ");
            }
            builder.Append(Text);

            foreach (KeyValuePair<string, string> line in ExtraLines())
            {
                builder.Append(Environment.NewLine)
                       .Append("    ")
                       .Append(line.Key.PadRight(9))
                       .Append(line.Value);
            }

            return builder.ToString();
        }
    }
}
