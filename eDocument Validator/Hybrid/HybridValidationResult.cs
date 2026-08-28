using eDocument_Validator.Validation;
using System.Collections.Generic;
using System.Linq;

namespace eDocument_Validator.Hybrid
{
    /// <summary>
    /// What was learned by validating a hybrid PDF invoice: the groups of messages
    /// the checks produced, the facts that identify the document, and the place the
    /// embedded XML was written to so it can be validated afterwards.
    /// </summary>
    public class HybridValidationResult
    {
        public HybridValidationResult()
        {
            Groups = new List<ValidationGroup>();
        }

        public List<ValidationGroup> Groups { get; private set; }

        public string PdfPath { get; set; }

        /// <summary>Which specification the PDF says it follows.</summary>
        public HybridFlavour Flavour { get; set; }

        /// <summary>Profile written in the XMP metadata.</summary>
        public string DeclaredConformanceLevel { get; set; }

        /// <summary>Attachment file name written in the XMP metadata.</summary>
        public string DeclaredDocumentFileName { get; set; }

        /// <summary>Document type written in the XMP metadata.</summary>
        public string DeclaredDocumentType { get; set; }

        /// <summary>Guideline identifier found inside the embedded CII document.</summary>
        public string GuidelineId { get; set; }

        /// <summary>Name of the attachment that holds the invoice.</summary>
        public string AttachmentName { get; set; }

        /// <summary>
        /// Where the embedded XML was written, or null when no XML could be taken
        /// out of the PDF.
        /// </summary>
        public string ExtractedXmlPath { get; set; }

        public int ErrorCount { get { return Groups.Sum(g => g.ErrorCount); } }

        public int WarningCount { get { return Groups.Sum(g => g.WarningCount); } }

        public bool HasErrors { get { return ErrorCount > 0; } }

        public ValidationGroup AddGroup(string title)
        {
            ValidationGroup group = new ValidationGroup(title);
            Groups.Add(group);
            return group;
        }
    }
}
