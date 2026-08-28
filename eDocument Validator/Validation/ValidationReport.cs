using System.Collections.Generic;
using System.Linq;

namespace eDocument_Validator.Validation
{
    /// <summary>
    /// The complete result of validating one document: a few facts about the
    /// document itself, followed by the groups of messages the checks produced.
    /// </summary>
    public class ValidationReport
    {
        private readonly List<ValidationGroup> groups = new List<ValidationGroup>();
        private readonly List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>();

        public ValidationReport(string documentPath)
        {
            DocumentPath = documentPath;
        }

        /// <summary>The document that was validated.</summary>
        public string DocumentPath { get; private set; }

        public IReadOnlyList<ValidationGroup> Groups { get { return groups; } }

        /// <summary>
        /// Short facts shown at the top of the report, such as the format that was
        /// used or the profile the document declares.
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, string>> Facts { get { return facts; } }

        public int ErrorCount { get { return groups.Sum(g => g.ErrorCount); } }

        public int WarningCount { get { return groups.Sum(g => g.WarningCount); } }

        /// <summary>
        /// True when no rule was broken. Warnings do not make a document invalid,
        /// so they do not change this value.
        /// </summary>
        public bool IsValid { get { return ErrorCount == 0; } }

        public ValidationGroup AddGroup(string title)
        {
            ValidationGroup group = new ValidationGroup(title);
            groups.Add(group);
            return group;
        }

        public void AddGroup(ValidationGroup group)
        {
            groups.Add(group);
        }

        public void AddGroups(IEnumerable<ValidationGroup> groupsToAdd)
        {
            groups.AddRange(groupsToAdd);
        }

        public void AddFact(string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                facts.Add(new KeyValuePair<string, string>(label, value));
            }
        }

        /// <summary>
        /// One short sentence describing the outcome, used as the headline of the
        /// report and in the window title bar.
        /// </summary>
        public string Summary()
        {
            if (IsValid && WarningCount == 0)
            {
                return "The document is valid.";
            }

            if (IsValid)
            {
                return "The document is valid, with " + CountText(WarningCount, "warning") + ".";
            }

            string summary = "The document is not valid: " + CountText(ErrorCount, "error");
            if (WarningCount > 0)
            {
                summary += " and " + CountText(WarningCount, "warning");
            }

            return summary + ".";
        }

        private static string CountText(int count, string word)
        {
            return count + " " + word + (count == 1 ? string.Empty : "s");
        }
    }
}
