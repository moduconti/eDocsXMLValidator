using System.Collections.Generic;
using System.Linq;

namespace eDocument_Validator.Validation
{
    /// <summary>
    /// The messages produced by one part of the validation, for example the checks
    /// on the PDF container or the run of a single Schematron file. Groups keep the
    /// report readable: the reader can see at a glance which part of the document
    /// is at fault.
    /// </summary>
    public class ValidationGroup
    {
        private readonly List<ValidationMessage> messages = new List<ValidationMessage>();

        public ValidationGroup(string title)
        {
            Title = title;
        }

        /// <summary>Name shown as the heading of this group.</summary>
        public string Title { get; private set; }

        /// <summary>
        /// Short note shown next to the heading, for example the name of the file
        /// the rules came from. Optional.
        /// </summary>
        public string Subtitle { get; set; }

        /// <summary>
        /// The untouched output of the tool that produced this group, such as the
        /// SVRL document from a Schematron run. It is saved next to the report so
        /// the original evidence is kept, and is never shown in the window.
        /// </summary>
        public string RawReport { get; set; }

        public IReadOnlyList<ValidationMessage> Messages { get { return messages; } }

        public int ErrorCount { get { return messages.Count(m => m.Severity == ValidationSeverity.Error); } }

        public int WarningCount { get { return messages.Count(m => m.Severity == ValidationSeverity.Warning); } }

        /// <summary>True when nothing in this group needs the reader's attention.</summary>
        public bool IsClean { get { return ErrorCount == 0 && WarningCount == 0; } }

        public ValidationMessage Add(ValidationSeverity severity, string rule, string text)
        {
            ValidationMessage message = new ValidationMessage(severity, rule, text);
            messages.Add(message);
            return message;
        }

        public ValidationMessage Add(ValidationMessage message)
        {
            messages.Add(message);
            return message;
        }

        public ValidationMessage Error(string rule, string text)
        {
            return Add(ValidationSeverity.Error, rule, text);
        }

        public ValidationMessage Warning(string rule, string text)
        {
            return Add(ValidationSeverity.Warning, rule, text);
        }

        public ValidationMessage Info(string rule, string text)
        {
            return Add(ValidationSeverity.Information, rule, text);
        }

        /// <summary>
        /// Records the result of one check in a single call, so a list of checks
        /// reads as a list of requirements instead of a chain of if/else blocks.
        /// </summary>
        public ValidationMessage Check(bool passed, ValidationSeverity severityIfFailed, string rule, string textIfFailed, string textIfPassed)
        {
            return Add(
                passed ? ValidationSeverity.Information : severityIfFailed,
                rule,
                passed ? textIfPassed : textIfFailed);
        }
    }
}
