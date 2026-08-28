using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace eDocument_Validator.Validation
{
    /// <summary>
    /// Turns the SVRL document a Schematron run produces into readable messages.
    /// <para>
    /// SVRL reports two kinds of finding. A failed assertion means a rule was
    /// broken. A successful report means a condition the rule author wanted to be
    /// told about was met; those are normally warnings rather than defects. Both
    /// carry a "flag" saying how serious the author considered them, which is what
    /// separates a real error from advice.
    /// </para>
    /// </summary>
    public static class SchematronReportReader
    {
        private static readonly XNamespace Svrl = "http://purl.oclc.org/dsdl/svrl";

        /// <summary>
        /// Removes the namespace test that Schematron writes into every step of a
        /// location path. The raw form is unreadable:
        /// <c>/*:CrossIndustryInvoice[namespace-uri()='urn:...'][1]/*:SupplyChainTradeTransaction[...][1]</c>
        /// while the reader only needs
        /// <c>/CrossIndustryInvoice[1]/SupplyChainTradeTransaction[1]</c>.
        /// </summary>
        private static readonly Regex NamespaceTest =
            new Regex(@"\[namespace-uri\(\)\s*=\s*'[^']*'\]", RegexOptions.Compiled);

        /// <summary>
        /// Matches the rule identifier that Schematron authors repeat at the start
        /// of the message text, for example "[BR-S-08]-". It is removed because the
        /// identifier is already shown in its own column.
        /// </summary>
        private static readonly Regex LeadingRuleName =
            new Regex(@"^\s*\[(?<rule>[^\]]{1,40})\]\s*-?\s*", RegexOptions.Compiled);

        /// <summary>
        /// Reads an SVRL document. Returns an empty list when the document contains
        /// no findings, which is what a valid document produces.
        /// </summary>
        public static List<ValidationMessage> Read(string svrlXml)
        {
            List<ValidationMessage> messages = new List<ValidationMessage>();

            if (string.IsNullOrWhiteSpace(svrlXml))
            {
                return messages;
            }

            XDocument report;
            try
            {
                report = XDocument.Parse(svrlXml);
            }
            catch (Exception)
            {
                // A Schematron file that produces something other than SVRL is a
                // problem with that file, not with the document being validated.
                return messages;
            }

            foreach (XElement element in report.Descendants())
            {
                if (element.Name == Svrl + "failed-assert")
                {
                    messages.Add(ReadFinding(element, ValidationSeverity.Error));
                }
                else if (element.Name == Svrl + "successful-report")
                {
                    // A successful report is a notice by design, so it only becomes
                    // an error when the rule author explicitly marked it as one.
                    messages.Add(ReadFinding(element, ValidationSeverity.Warning));
                }
            }

            return messages;
        }

        private static ValidationMessage ReadFinding(XElement element, ValidationSeverity defaultSeverity)
        {
            string rule = (string)element.Attribute("id");
            string flag = (string)element.Attribute("flag");
            string role = (string)element.Attribute("role");

            string text = ReadText(element);

            // Where the author gave no identifier, fall back to the one repeated at
            // the start of the message text.
            if (string.IsNullOrWhiteSpace(rule))
            {
                Match match = LeadingRuleName.Match(text);
                if (match.Success)
                {
                    rule = match.Groups["rule"].Value;
                }
            }

            ValidationMessage message = new ValidationMessage(
                SeverityFrom(flag, role, defaultSeverity),
                rule ?? string.Empty,
                RemoveLeadingRuleName(text));

            message.Location = SimplifyLocation((string)element.Attribute("location"));
            message.Technical = Flatten((string)element.Attribute("test"));

            return message;
        }

        /// <summary>
        /// Reads how serious the rule author considered a finding. Schematron files
        /// in this project use "fatal", "warning" and "information"; "error" and
        /// "warn" are accepted as well because other authors use those spellings.
        /// </summary>
        private static ValidationSeverity SeverityFrom(string flag, string role, ValidationSeverity defaultSeverity)
        {
            string value = !string.IsNullOrWhiteSpace(flag) ? flag : role;

            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultSeverity;
            }

            switch (value.Trim().ToLowerInvariant())
            {
                case "fatal":
                case "error":
                    return ValidationSeverity.Error;

                case "warning":
                case "warn":
                    return ValidationSeverity.Warning;

                case "information":
                case "info":
                    return ValidationSeverity.Information;

                default:
                    return defaultSeverity;
            }
        }

        private static string ReadText(XElement element)
        {
            XElement textElement = element.Elements(Svrl + "text").FirstOrDefault();
            string text = textElement != null ? textElement.Value : element.Value;
            return Flatten(text);
        }

        private static string RemoveLeadingRuleName(string text)
        {
            return string.IsNullOrEmpty(text) ? text : LeadingRuleName.Replace(text, string.Empty);
        }

        private static string SimplifyLocation(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return null;
            }

            string simplified = NamespaceTest.Replace(location, string.Empty);

            // What remains looks like /*:Name[1]; the star and colon carry no
            // information once the namespace test is gone.
            simplified = simplified.Replace("/*:", "/");

            return simplified.Trim();
        }

        /// <summary>
        /// Joins the text onto one line. Schematron messages are usually written
        /// across several indented lines in the source file, which would otherwise
        /// appear as ragged gaps in the report.
        /// </summary>
        private static string Flatten(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string[] parts = value.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", parts.Select(p => p.Trim()).Where(p => p.Length > 0));
        }
    }
}
