using eDocument_Validator.Validation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UglyToad.PdfPig.Tokens;

namespace eDocument_Validator.Hybrid
{
    /// <summary>
    /// Validates a hybrid electronic invoice: a PDF/A file carrying a Factur-X,
    /// ZUGFeRD or Order-X CII document as an embedded attachment.
    /// <para>
    /// Four things are checked, in the order a reader would look at them: the PDF
    /// container, the embedded file specification, the XMP metadata, and finally
    /// the agreement between all three. Only the last group is specific to hybrid
    /// invoices - the first three are the rules the PDF itself has to satisfy
    /// before the attachment means anything.
    /// </para>
    /// <para>
    /// The checks here are structural. Full PDF/A conformance (fonts, colour
    /// spaces, transparency) is a much larger rule set and is covered separately
    /// by <see cref="VeraPdfRunner"/> when veraPDF is available.
    /// </para>
    /// </summary>
    public static class HybridValidator
    {
        private const string AttachmentSectionTitle = "Embedded XML attachment";

        public static HybridValidationResult Validate(string pdfPath, string extractionFolder)
        {
            HybridValidationResult result = new HybridValidationResult { PdfPath = pdfPath };

            PdfContainer container;
            try
            {
                container = PdfContainer.Open(pdfPath);
            }
            catch (Exception ex)
            {
                ValidationGroup failed = result.AddGroup("PDF container");
                failed.Error("PDF-01", "The file could not be read as a PDF: " + ex.Message);
                return result;
            }

            using (container)
            {
                CheckContainer(container, result);

                PdfEmbeddedFile attachment = CheckEmbeddedFile(container, result);

                XmpMetadataView xmp = CheckXmpMetadata(container, result);

                CheckConsistency(container, attachment, xmp, result);

                if (attachment != null && attachment.Content != null && extractionFolder != null)
                {
                    result.ExtractedXmlPath = WriteAttachment(attachment, extractionFolder, result);
                }
            }

            return result;
        }

        // ------------------------------------------------------------------
        // 1. PDF container
        // ------------------------------------------------------------------

        private static void CheckContainer(PdfContainer container, HybridValidationResult result)
        {
            ValidationGroup section = result.AddGroup("PDF container");

            section.Info("PDF-01", "PDF opened successfully (version "
                + container.Version.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                + ", " + container.PageCount + " page(s)).");

            section.Check(!container.IsEncrypted, ValidationSeverity.Error, "PDF-02",
                "The document is encrypted. PDF/A does not permit encryption, and an encrypted file cannot be a valid Factur-X or ZUGFeRD invoice.",
                "The document is not encrypted.");

            // PDF/A-3 is defined on top of PDF 1.7, but unlike PDF/A-1 it does not
            // constrain the header version, and conforming invoices with a 1.4
            // header are common. This is therefore reported for information only.
            if (container.Version < 1.7)
            {
                section.Info("PDF-03", "The PDF header declares version "
                    + container.Version.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                    + ". PDF/A-3 is based on PDF 1.7, but does not constrain the header version, so this alone is not a defect.");
            }
            else
            {
                section.Info("PDF-03", "The PDF header declares version 1.7 or later.");
            }

            section.Check(container.Xmp != null, ValidationSeverity.Error, "PDF-04",
                "The document catalogue has no readable XMP metadata stream"
                + (string.IsNullOrEmpty(container.XmpError) ? "." : ": " + container.XmpError),
                "The document catalogue carries an XMP metadata stream.");

            CheckOutputIntent(container, section);

            section.Check(container.HasEmbeddedFilesNameTree, ValidationSeverity.Error, "PDF-06",
                "The document catalogue has no /Names /EmbeddedFiles name tree, so no file is attached.",
                "The document catalogue has an /Names /EmbeddedFiles name tree.");

            section.Check(container.HasCatalogAssociatedFiles, ValidationSeverity.Error, "PDF-07",
                "The document catalogue has no /AF array. PDF/A-3 requires the invoice XML to be associated with the document through this array.",
                "The document catalogue has an /AF array with " + container.CatalogAssociatedFileCount + " entry/entries.");

            section.Check(!container.HasJavaScriptNameTree, ValidationSeverity.Error, "PDF-08",
                "The document contains a /Names /JavaScript tree. PDF/A prohibits JavaScript.",
                "The document contains no JavaScript name tree.");

            if (container.MarkedAsTagged == true)
            {
                section.Info("PDF-09", "The document is tagged (/MarkInfo /Marked true), as PDF/A-3a requires.");
            }
            else
            {
                section.Info("PDF-09", "The document is not tagged. This is only required for the accessible PDF/A-3a conformance level, not for 3b or 3u.");
            }

            foreach (string failure in container.ReadFailures)
            {
                section.Warning("PDF-10", failure);
            }
        }

        /// <summary>
        /// Checks the PDF/A output intent.
        /// <para>
        /// Unlike PDF/A-1, PDF/A-2 and PDF/A-3 require an output intent only when
        /// the file actually uses device-dependent colour with no other way of
        /// determining the destination colour space. Deciding that needs the full
        /// content-stream analysis veraPDF performs, so a missing or unrecognised
        /// output intent is reported here as a warning rather than as a violation.
        /// An intent that is present but carries no colour profile is wrong under
        /// any reading of the rule, so that one is an error.
        /// </para>
        /// </summary>
        private static void CheckOutputIntent(PdfContainer container, ValidationGroup section)
        {
            if (container.OutputIntents.Count == 0)
            {
                section.Warning("PDF-05", "The document has no /OutputIntents entry. PDF/A-3 requires one only if the file uses device-dependent colour, which this check cannot determine on its own.");
                return;
            }

            DictionaryToken pdfaIntent = container.OutputIntents
                .FirstOrDefault(i => string.Equals(container.ReadName(i, "S"), "GTS_PDFA1", StringComparison.Ordinal));

            if (pdfaIntent == null)
            {
                ValidationMessage issue = section.Warning("PDF-05", "The document has output intents, but none of them is a PDF/A output intent.");
                issue.Expected = "/S /GTS_PDFA1";
                issue.Found = string.Join(", ", container.OutputIntents
                    .Select(i => "/" + (container.ReadName(i, "S") ?? "(missing)")));
                return;
            }

            if (!pdfaIntent.ContainsKey(NameToken.Create("DestOutputProfile")))
            {
                section.Error("PDF-05", "The PDF/A output intent has no /DestOutputProfile, so it embeds no ICC colour profile.");
                return;
            }

            string identifier = container.ReadText(pdfaIntent, "OutputConditionIdentifier");
            section.Info("PDF-05", "A PDF/A output intent with an embedded ICC profile is present"
                + (string.IsNullOrEmpty(identifier) ? "." : " (" + identifier + ")."));
        }

        // ------------------------------------------------------------------
        // 2. Embedded file specification
        // ------------------------------------------------------------------

        private static PdfEmbeddedFile CheckEmbeddedFile(PdfContainer container, HybridValidationResult result)
        {
            ValidationGroup section = result.AddGroup(AttachmentSectionTitle);

            if (container.EmbeddedFiles.Count == 0)
            {
                section.Error("EF-01", "The PDF contains no embedded files at all. A hybrid invoice must carry its CII XML as an attachment.");
                return null;
            }

            PdfEmbeddedFile attachment = SelectInvoiceAttachment(container, section);
            if (attachment == null)
            {
                return null;
            }

            result.AttachmentName = attachment.EffectiveName;

            section.Check(string.Equals(attachment.TypeEntry, "Filespec", StringComparison.Ordinal),
                ValidationSeverity.Warning, "EF-02",
                "The file specification does not declare /Type /Filespec (found "
                    + (attachment.TypeEntry == null ? "no /Type" : "/" + attachment.TypeEntry) + ").",
                "The file specification declares /Type /Filespec.");

            CheckAttachmentNames(attachment, section);
            CheckAttachmentRelationship(attachment, section);

            section.Check(!string.IsNullOrWhiteSpace(attachment.Description), ValidationSeverity.Warning, "EF-05",
                "The file specification has no /Desc entry. The specifications ask for a human-readable description of the attachment.",
                "The file specification has a description: " + attachment.Description);

            CheckAttachmentStream(attachment, section);

            section.Check(attachment.ReferencedFromCatalogAssociatedFiles, ValidationSeverity.Error, "EF-08",
                "The attachment is not referenced from the document catalogue /AF array, so it is not associated with the document as PDF/A-3 requires.",
                "The attachment is referenced from the document catalogue /AF array.");

            CheckAttachmentIsXml(attachment, section);

            List<PdfEmbeddedFile> others = container.EmbeddedFiles.Where(f => !ReferenceEquals(f, attachment)).ToList();
            if (others.Count > 0)
            {
                section.Info("EF-10", "The PDF carries " + others.Count + " further attachment(s): "
                    + string.Join(", ", others.Select(f => f.EffectiveName ?? "(unnamed)"))
                    + ". Additional attachments are permitted alongside the invoice XML.");
            }

            return attachment;
        }

        /// <summary>
        /// Picks the attachment that carries the invoice. Matching on the file names
        /// the specifications reserve is tried first; if none matches, a single
        /// XML-looking attachment is accepted so that a document with the wrong file
        /// name still gets validated (and told that the name is wrong) rather than
        /// being reported as having no invoice at all.
        /// </summary>
        private static PdfEmbeddedFile SelectInvoiceAttachment(PdfContainer container, ValidationGroup section)
        {
            List<PdfEmbeddedFile> byName = container.EmbeddedFiles
                .Where(f => HybridSpecs.CandidateAttachmentNames
                    .Any(n => string.Equals(n, f.EffectiveName, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (byName.Count == 1)
            {
                section.Info("EF-01", "Found the invoice attachment: " + byName[0].EffectiveName);
                return byName[0];
            }

            if (byName.Count > 1)
            {
                ValidationMessage issue = section.Error("EF-01",
                    "The PDF carries more than one attachment using a reserved hybrid-invoice file name. A consumer cannot tell which one is authoritative.");
                issue.Found = string.Join(", ", byName.Select(f => f.EffectiveName));
                return byName[0];
            }

            List<PdfEmbeddedFile> xmlLike = container.EmbeddedFiles
                .Where(f => f.EffectiveName != null && f.EffectiveName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (xmlLike.Count == 1)
            {
                ValidationMessage issue = section.Error("EF-01",
                    "No attachment uses a file name reserved for hybrid invoices. Validating the only XML attachment found instead.");
                issue.Expected = string.Join(", ", HybridSpecs.CandidateAttachmentNames);
                issue.Found = xmlLike[0].EffectiveName;
                return xmlLike[0];
            }

            ValidationMessage notFound = section.Error("EF-01", "No embedded XML invoice could be identified.");
            notFound.Expected = string.Join(", ", HybridSpecs.CandidateAttachmentNames);
            notFound.Found = string.Join(", ", container.EmbeddedFiles.Select(f => f.EffectiveName ?? "(unnamed)"));
            return null;
        }

        private static void CheckAttachmentNames(PdfEmbeddedFile attachment, ValidationGroup section)
        {
            if (string.IsNullOrEmpty(attachment.UnicodeFileName))
            {
                section.Error("EF-03", "The file specification has no /UF entry. PDF/A-3 requires the Unicode file name alongside /F.");
            }
            else if (!string.IsNullOrEmpty(attachment.FileName)
                     && !string.Equals(attachment.FileName, attachment.UnicodeFileName, StringComparison.Ordinal))
            {
                ValidationMessage issue = section.Error("EF-03", "/F and /UF give different file names, so consumers may disagree about the attachment name.");
                issue.Found = "/F = " + attachment.FileName + ", /UF = " + attachment.UnicodeFileName;
            }
            else
            {
                section.Info("EF-03", "/F and /UF are both present and agree.");
            }

            if (!string.IsNullOrEmpty(attachment.NameTreeKey)
                && !string.Equals(attachment.NameTreeKey, attachment.EffectiveName, StringComparison.Ordinal))
            {
                ValidationMessage issue = section.Warning("EF-04",
                    "The key under which the attachment is registered in the embedded-files name tree differs from its file name.");
                issue.Found = "name tree key = " + attachment.NameTreeKey + ", file name = " + attachment.EffectiveName;
            }
        }

        private static void CheckAttachmentRelationship(PdfEmbeddedFile attachment, ValidationGroup section)
        {
            string relationship = attachment.AssociatedFileRelationship;

            if (string.IsNullOrEmpty(relationship))
            {
                ValidationMessage issue = section.Error("EF-06",
                    "The file specification has no /AFRelationship entry, so the PDF does not say how the attachment relates to the document.");
                issue.Expected = "/Data";
                return;
            }

            if (string.Equals(relationship, "Data", StringComparison.Ordinal))
            {
                section.Info("EF-06", "/AFRelationship is /Data, as Factur-X 1.0 and ZUGFeRD 2.1 and later require.");
                return;
            }

            if (string.Equals(relationship, "Alternative", StringComparison.Ordinal)
                || string.Equals(relationship, "Source", StringComparison.Ordinal))
            {
                // ZUGFeRD 2.0 and early Factur-X drafts used /Alternative. It is still
                // readable, so this is reported as a warning rather than an error.
                ValidationMessage issue = section.Warning("EF-06",
                    "/AFRelationship is /" + relationship + ", which older revisions used. Current Factur-X and ZUGFeRD releases require /Data.");
                issue.Expected = "/Data";
                issue.Found = "/" + relationship;
                return;
            }

            ValidationMessage unexpected = section.Error("EF-06", "/AFRelationship has an unexpected value.");
            unexpected.Expected = "/Data";
            unexpected.Found = "/" + relationship;
        }

        private static void CheckAttachmentStream(PdfEmbeddedFile attachment, ValidationGroup section)
        {
            if (attachment.EmbeddedFileStreamDictionary == null)
            {
                section.Error("EF-07", attachment.ContentError ?? "The attachment has no embedded file stream.");
                return;
            }

            CheckAttachmentMediaType(attachment, section);

            if (string.IsNullOrEmpty(attachment.ModificationDate))
            {
                section.Error("EF-09", "The embedded file stream has no /Params /ModDate. PDF/A-3 requires a modification date for every embedded file.");
            }
            else
            {
                section.Info("EF-09", "The embedded file stream carries /Params /ModDate " + attachment.ModificationDate + ".");
            }

            if (attachment.DeclaredSize.HasValue && attachment.Content != null
                && attachment.DeclaredSize.Value != attachment.Content.Length)
            {
                ValidationMessage issue = section.Warning("EF-09", "/Params /Size does not match the length of the decoded attachment.");
                issue.Expected = attachment.Content.Length + " bytes";
                issue.Found = attachment.DeclaredSize.Value + " bytes";
            }
        }

        /// <summary>
        /// Checks the media type of the attachment.
        /// <para>
        /// Factur-X and ZUGFeRD both require text/xml. A different value is reported
        /// as a warning rather than an error, because it does not stop a receiver
        /// from using the invoice: the attachment is found through the /AF array,
        /// its file name and its AFRelationship, not through its media type. This
        /// matches how the reference validators grade it.
        /// </para>
        /// </summary>
        private static void CheckAttachmentMediaType(PdfEmbeddedFile attachment, ValidationGroup section)
        {
            // PDF writes the media type as a name, so the solidus is escaped in the
            // file itself: text#2Fxml.
            string subtype = attachment.Subtype;

            if (string.Equals(subtype, "text/xml", StringComparison.OrdinalIgnoreCase))
            {
                section.Info("EF-07", "The embedded file stream declares /Subtype /text#2Fxml.");
                return;
            }

            if (string.IsNullOrEmpty(subtype))
            {
                ValidationMessage missing = section.Warning("EF-07",
                    "The embedded file stream has no /Subtype, so it does not say what kind of file is attached.");
                missing.Expected = "text/xml";
                missing.Found = "no /Subtype";
                return;
            }

            ValidationMessage issue = section.Warning("EF-07",
                "The embedded file stream declares a media type other than the one the specification requires. "
                + "A receiver that selects attachments by media type will not recognise this one as the invoice.");
            issue.Expected = "text/xml";
            issue.Found = subtype;
        }

        private static void CheckAttachmentIsXml(PdfEmbeddedFile attachment, ValidationGroup section)
        {
            if (attachment.Content == null)
            {
                section.Error("EF-11", attachment.ContentError ?? "The attachment content could not be read.");
                return;
            }

            if (attachment.Content.Length == 0)
            {
                section.Error("EF-11", "The attachment is empty.");
                return;
            }

            try
            {
                ParseXmlBytes(attachment.Content);
                section.Info("EF-11", "The attachment is well-formed XML (" + attachment.Content.Length + " bytes).");
            }
            catch (Exception ex)
            {
                section.Error("EF-11", "The attachment is not well-formed XML: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------
        // 3. XMP metadata
        // ------------------------------------------------------------------

        private static XmpMetadataView CheckXmpMetadata(PdfContainer container, HybridValidationResult result)
        {
            ValidationGroup section = result.AddGroup("XMP metadata");

            if (container.Xmp == null)
            {
                section.Error("XMP-01", "There is no readable XMP packet, so none of the metadata rules can be checked"
                    + (string.IsNullOrEmpty(container.XmpError) ? "." : ": " + container.XmpError));
                return null;
            }

            XmpMetadataView xmp = new XmpMetadataView(container.Xmp);
            section.Info("XMP-01", "The XMP packet parsed successfully.");

            CheckPdfaIdentification(container, xmp, section);

            // A missing hybrid namespace is reported by DetectFlavour, but the
            // remaining checks still run against a null namespace so the report
            // lists each property that is absent and the cross-checks state the
            // contradiction with the XML explicitly. Stopping here would leave the
            // rest of the report looking as though it had passed.
            string namespaceUri = DetectFlavour(xmp, result, section);

            if (namespaceUri != null)
            {
                CheckExtensionSchema(xmp, namespaceUri, section);
            }

            CheckHybridProperties(xmp, namespaceUri, result, section);
            CheckDocumentInformation(container, xmp, section);

            return xmp;
        }

        private static void CheckPdfaIdentification(PdfContainer container, XmpMetadataView xmp, ValidationGroup section)
        {
            string part = xmp.GetValue(HybridSpecs.PdfaIdNamespace, "part");
            string conformance = xmp.GetValue(HybridSpecs.PdfaIdNamespace, "conformance");

            if (string.IsNullOrEmpty(part))
            {
                ValidationMessage issue = section.Error("XMP-02",
                    "The XMP has no pdfaid:part property, so the file does not identify itself as PDF/A at all.");
                issue.Expected = "pdfaid:part = 3";
                return;
            }

            if (part.Trim() == "3")
            {
                section.Info("XMP-02", "pdfaid:part is 3, the PDF/A part Factur-X and ZUGFeRD require.");
            }
            else if (part.Trim() == "4")
            {
                section.Warning("XMP-02", "pdfaid:part is 4. PDF/A-4 is accepted by ZUGFeRD 2.3 and Factur-X 1.07.2 and later, but earlier profiles require PDF/A-3.");
            }
            else
            {
                ValidationMessage issue = section.Error("XMP-02", "The document claims a PDF/A part that cannot carry embedded files for a hybrid invoice.");
                issue.Expected = "3 (or 4 for the most recent profiles)";
                issue.Found = part;
            }

            // PDF/A-4 replaces the conformance letter with pdfaid:rev, so only
            // require it for part 3.
            if (part.Trim() == "4")
            {
                string revision = xmp.GetValue(HybridSpecs.PdfaIdNamespace, "rev");
                section.Check(!string.IsNullOrEmpty(revision), ValidationSeverity.Warning, "XMP-03",
                    "PDF/A-4 is declared but pdfaid:rev is missing.",
                    "pdfaid:rev is " + revision + ".");
                return;
            }

            if (string.IsNullOrEmpty(conformance))
            {
                ValidationMessage issue = section.Error("XMP-03", "The XMP has no pdfaid:conformance property.");
                issue.Expected = "A, B or U";
                return;
            }

            string level = conformance.Trim().ToUpperInvariant();
            if (level == "A" || level == "B" || level == "U")
            {
                section.Info("XMP-03", "pdfaid:conformance is " + level + ", so the file claims PDF/A-3" + level.ToLowerInvariant() + ".");
            }
            else
            {
                ValidationMessage issue = section.Error("XMP-03", "pdfaid:conformance has an invalid value.");
                issue.Expected = "A, B or U";
                issue.Found = conformance;
                return;
            }

            // Conformance level A is the accessible level, which requires the
            // document to be tagged. Claiming A without tagging is a contradiction
            // the file makes about itself.
            if (level == "A" && container.MarkedAsTagged != true)
            {
                ValidationMessage issue = section.Error("XMP-13",
                    "The file claims PDF/A-3a, which requires a tagged document, but the catalogue does not mark it as tagged.");
                issue.Expected = "/MarkInfo << /Marked true >>";
                issue.Found = container.MarkedAsTagged == null
                    ? "no /MarkInfo entry"
                    : "/MarkInfo << /Marked false >>";
            }
        }

        /// <summary>
        /// Determines which specification the file claims to follow from the XMP
        /// namespaces it declares, and returns that namespace URI.
        /// </summary>
        private static string DetectFlavour(XmpMetadataView xmp, HybridValidationResult result, ValidationGroup section)
        {
            HashSet<string> declared = new HashSet<string>(xmp.DeclaredNamespaces, StringComparer.Ordinal);

            foreach (KeyValuePair<string, HybridFlavour> candidate in HybridSpecs.KnownNamespaces)
            {
                if (!declared.Contains(candidate.Key))
                {
                    continue;
                }

                result.Flavour = candidate.Value;
                string prefix = xmp.GetPrefixFor(candidate.Key);
                section.Info("XMP-04", "The XMP declares " + HybridSpecs.Describe(candidate.Value)
                    + " through the namespace " + candidate.Key
                    + (prefix == null ? "." : " (prefix " + prefix + ")."));
                return candidate.Key;
            }

            ValidationMessage issue = section.Error("XMP-04",
                "The XMP declares none of the namespaces that identify a hybrid invoice, so a consumer has no way to recognise the file as Factur-X or ZUGFeRD.");
            issue.Expected = HybridSpecs.FacturXNamespace + " or " + HybridSpecs.ZugferdV2Namespace;
            return null;
        }

        private static void CheckExtensionSchema(XmpMetadataView xmp, string namespaceUri, ValidationGroup section)
        {
            List<XmpExtensionSchema> schemas = xmp.GetExtensionSchemas();

            XmpExtensionSchema schema = schemas
                .FirstOrDefault(s => string.Equals(s.NamespaceUri, namespaceUri, StringComparison.Ordinal));

            if (schema == null)
            {
                ValidationMessage issue = section.Error("XMP-05",
                    "The XMP has no PDF/A extension schema describing the hybrid-invoice namespace. PDF/A only permits custom XMP properties that are declared this way, so this makes the file invalid as PDF/A.");
                issue.Expected = "a pdfaExtension:schemas entry for " + namespaceUri;
                issue.Found = schemas.Count == 0
                    ? "no extension schemas at all"
                    : "extension schemas for " + string.Join(", ", schemas.Select(s => s.NamespaceUri));
                return;
            }

            section.Info("XMP-05", "A PDF/A extension schema is declared for the hybrid-invoice namespace"
                + (string.IsNullOrEmpty(schema.Description) ? "." : ": " + schema.Description));

            string prefix = xmp.GetPrefixFor(namespaceUri);
            if (!string.IsNullOrEmpty(prefix) && !string.IsNullOrEmpty(schema.Prefix)
                && !string.Equals(prefix, schema.Prefix, StringComparison.Ordinal))
            {
                ValidationMessage issue = section.Warning("XMP-06",
                    "The prefix declared in the extension schema differs from the prefix actually bound to the namespace.");
                issue.Expected = schema.Prefix;
                issue.Found = prefix;
            }

            string[] required = { "DocumentType", "DocumentFileName", "Version", "ConformanceLevel" };
            List<string> missing = required
                .Where(r => !schema.PropertyNames.Any(p => string.Equals(p, r, StringComparison.Ordinal)))
                .ToList();

            if (missing.Count > 0)
            {
                ValidationMessage issue = section.Error("XMP-07",
                    "The extension schema does not describe every property the specification defines. Undeclared properties are not permitted by PDF/A.");
                issue.Expected = string.Join(", ", required);
                issue.Found = schema.PropertyNames.Count == 0
                    ? "no property definitions"
                    : string.Join(", ", schema.PropertyNames);
            }
            else
            {
                section.Info("XMP-07", "The extension schema describes all four required properties.");
            }
        }

        /// <summary>
        /// Checks the four properties the specification defines. A null
        /// <paramref name="namespaceUri"/> means the file declares no hybrid
        /// namespace at all, in which case every property is by definition absent
        /// and is reported as such.
        /// </summary>
        private static void CheckHybridProperties(XmpMetadataView xmp, string namespaceUri, HybridValidationResult result, ValidationGroup section)
        {
            Func<string, string> read = name => namespaceUri == null ? null : xmp.GetValue(namespaceUri, name);

            string documentType = read("DocumentType");
            string fileName = read("DocumentFileName");
            string version = read("Version");
            string conformance = read("ConformanceLevel");

            result.DeclaredConformanceLevel = conformance;
            result.DeclaredDocumentFileName = fileName;
            result.DeclaredDocumentType = documentType;

            if (string.IsNullOrEmpty(documentType))
            {
                section.Error("XMP-08", "DocumentType is missing from the hybrid-invoice metadata.");
            }
            else if (HybridSpecs.DocumentTypes.Contains(documentType.Trim().ToUpperInvariant()))
            {
                section.Info("XMP-08", "DocumentType is " + documentType + ".");
            }
            else
            {
                ValidationMessage issue = section.Error("XMP-08", "DocumentType has an unexpected value.");
                issue.Expected = string.Join(" or ", HybridSpecs.DocumentTypes);
                issue.Found = documentType;
            }

            section.Check(!string.IsNullOrEmpty(fileName), ValidationSeverity.Error, "XMP-09",
                "DocumentFileName is missing, so the metadata does not say which attachment holds the invoice.",
                "DocumentFileName is " + fileName + ".");

            section.Check(!string.IsNullOrEmpty(version), ValidationSeverity.Error, "XMP-10",
                "Version is missing from the hybrid-invoice metadata.",
                "Version is " + version + ".");

            if (string.IsNullOrEmpty(conformance))
            {
                section.Error("XMP-11", "ConformanceLevel is missing, so the metadata does not say which profile the invoice follows.");
            }
            else if (HybridSpecs.IsKnownConformanceLevel(conformance))
            {
                section.Info("XMP-11", "ConformanceLevel is " + conformance + ".");
            }
            else
            {
                ValidationMessage issue = section.Error("XMP-11", "ConformanceLevel is not one of the defined profiles.");
                issue.Expected = string.Join(", ", HybridSpecs.ConformanceLevels);
                issue.Found = conformance;
            }
        }

        /// <summary>
        /// Compares the document information dictionary with the XMP.
        /// <para>
        /// PDF/A-1 required the two to be equivalent (ISO 19005-1, 6.7.3), but
        /// PDF/A-2 and PDF/A-3 dropped that requirement, and veraPDF's PDF/A-3
        /// profile does not check it. A mismatch is therefore reported for
        /// information only - it is often a sign of a sloppy producer and is worth
        /// seeing, but it is not a defect at this conformance level.
        /// </para>
        /// </summary>
        private static void CheckDocumentInformation(PdfContainer container, XmpMetadataView xmp, ValidationGroup section)
        {
            if (container.Information == null)
            {
                return;
            }

            CompareInformationEntry(section, "Title", container.Information.Title,
                xmp.GetValue(HybridSpecs.DublinCoreNamespace, "title"));

            CompareInformationEntry(section, "Author", container.Information.Author,
                xmp.GetValue(HybridSpecs.DublinCoreNamespace, "creator"));

            CompareInformationEntry(section, "Producer", container.Information.Producer,
                xmp.GetValue(HybridSpecs.AdobePdfNamespace, "Producer"));

            CompareInformationEntry(section, "Creator", container.Information.Creator,
                xmp.GetValue(HybridSpecs.XmpBasicNamespace, "CreatorTool"));
        }

        private static void CompareInformationEntry(ValidationGroup section, string label, string infoValue, string xmpValue)
        {
            bool hasInfo = !string.IsNullOrWhiteSpace(infoValue);
            bool hasXmp = !string.IsNullOrWhiteSpace(xmpValue);

            if (!hasInfo || !hasXmp)
            {
                return;
            }

            if (string.Equals(infoValue.Trim(), xmpValue.Trim(), StringComparison.Ordinal))
            {
                return;
            }

            ValidationMessage issue = section.Info("XMP-12",
                "The document information dictionary and the XMP disagree about " + label
                + ". PDF/A-1 required these to match; PDF/A-2 and PDF/A-3 do not, so this is reported for information only.");
            issue.Expected = "/Info " + label + " = " + infoValue;
            issue.Found = "XMP = " + xmpValue;
        }

        // ------------------------------------------------------------------
        // 4. Agreement between PDF, XMP and XML
        // ------------------------------------------------------------------

        private static void CheckConsistency(PdfContainer container, PdfEmbeddedFile attachment, XmpMetadataView xmp, HybridValidationResult result)
        {
            ValidationGroup section = result.AddGroup("PDF / XMP / XML consistency");

            if (attachment == null || xmp == null)
            {
                section.Warning("REL-00", "The cross-checks were skipped because the attachment or the XMP metadata could not be read.");
                return;
            }

            if (attachment.Content == null)
            {
                CheckDeclaredFileName(attachment, result, section);
                section.Warning("REL-01", "The attachment content is unavailable, so it cannot be compared with the metadata.");
                return;
            }

            XDocument invoice;
            try
            {
                invoice = ParseXmlBytes(attachment.Content);
            }
            catch (Exception ex)
            {
                CheckDeclaredFileName(attachment, result, section);
                section.Error("REL-02", "The embedded XML could not be parsed, so it cannot be compared with the metadata: " + ex.Message);
                return;
            }

            CheckInvoiceRootElement(invoice, result, section);

            // The guideline is read before the file name is judged, because which
            // names are correct depends on the profile the invoice follows.
            CheckGuidelineAgainstXmp(invoice, result, section);

            CheckDeclaredFileName(attachment, result, section);

            CheckDocumentTypeAgainstTypeCode(invoice, xmp, result, section);
            ReportInvoiceIdentity(invoice, section);
        }

        private static void CheckDeclaredFileName(PdfEmbeddedFile attachment, HybridValidationResult result, ValidationGroup section)
        {
            string declaredName = result.DeclaredDocumentFileName;

            if (string.IsNullOrEmpty(declaredName))
            {
                ValidationMessage missing = section.Error("REL-01",
                    "The XMP metadata does not name the attachment holding the invoice, so a consumer reading the metadata cannot tell which embedded file to use.");
                missing.Expected = "XMP DocumentFileName = " + attachment.EffectiveName;
                missing.Found = "no DocumentFileName in the XMP";
            }
            else if (string.Equals(declaredName, attachment.EffectiveName, StringComparison.Ordinal))
            {
                section.Info("REL-01", "The file name in the XMP matches the attachment: " + declaredName);
            }
            else
            {
                ValidationMessage issue = section.Error("REL-01",
                    "The XMP names a different file than the one attached, so a consumer following the metadata will not find the invoice.");
                issue.Expected = "XMP DocumentFileName = " + attachment.EffectiveName;
                issue.Found = declaredName;
            }

            // Which names are correct depends on the specification and on the
            // profile: an XRechnung invoice inside a PDF is normally called
            // xrechnung.xml, while other profiles use factur-x.xml.
            string profile = result.DeclaredConformanceLevel ?? result.ConformanceLevelFromXml;
            string[] acceptedNames = HybridSpecs.AcceptedAttachmentNames(result.Flavour, profile);

            bool nameIsAccepted = acceptedNames
                .Any(n => string.Equals(n, attachment.EffectiveName, StringComparison.OrdinalIgnoreCase));

            if (nameIsAccepted)
            {
                section.Info("REL-07", "The attachment uses a file name this profile expects: " + attachment.EffectiveName);
                return;
            }

            ValidationMessage wrongName = section.Warning("REL-07",
                "The attachment does not use a file name that this profile expects. Software that looks for the "
                + "invoice by name may not find it.");
            wrongName.Expected = string.Join(" or ", acceptedNames);
            wrongName.Found = attachment.EffectiveName;
        }

        private static void CheckInvoiceRootElement(XDocument invoice, HybridValidationResult result, ValidationGroup section)
        {
            XElement root = invoice.Root;
            if (root == null)
            {
                section.Error("REL-02", "The embedded XML has no root element.");
                return;
            }

            string expectedNamespace = HybridSpecs.ExpectedDocumentNamespace(result.Flavour);

            if (root.Name.NamespaceName == expectedNamespace)
            {
                section.Info("REL-02", "The embedded XML is a " + root.Name.LocalName + " in the expected UN/CEFACT namespace.");
                return;
            }

            ValidationMessage issue = section.Error("REL-02",
                "The embedded XML is not a UN/CEFACT Cross Industry document in the namespace this profile requires.");
            issue.Expected = expectedNamespace;
            issue.Found = root.Name.NamespaceName + " (" + root.Name.LocalName + ")";
        }

        private static void CheckGuidelineAgainstXmp(XDocument invoice, HybridValidationResult result, ValidationGroup section)
        {
            // Matched on local names: the reusable-entity namespace carries a
            // version number that differs between ZUGFeRD 1.0 and the D16B-based
            // profiles, and anchoring on the parent element keeps the lookup
            // unambiguous without a namespace table.
            XElement guideline = invoice.Descendants()
                .Where(e => e.Name.LocalName == "GuidelineSpecifiedDocumentContextParameter")
                .Elements()
                .FirstOrDefault(e => e.Name.LocalName == "ID");

            if (guideline == null)
            {
                section.Error("REL-03",
                    "The embedded XML has no GuidelineSpecifiedDocumentContextParameter/ID, so it does not say which profile it follows.");
                return;
            }

            result.GuidelineId = guideline.Value.Trim();

            bool exactMatch;
            string fromXml = HybridSpecs.ConformanceForGuideline(result.GuidelineId, out exactMatch);
            result.ConformanceLevelFromXml = fromXml;

            if (fromXml == null)
            {
                ValidationMessage issue = section.Warning("REL-03",
                    "The guideline identifier in the XML is not one this validator recognises, so it cannot be compared with the XMP.");
                issue.Found = result.GuidelineId;
                return;
            }

            if (!exactMatch)
            {
                section.Info("REL-03", "The guideline identifier is a national or extended variant; it was matched to the "
                    + fromXml + " profile by its suffix. Identifier: " + result.GuidelineId);
            }
            else
            {
                section.Info("REL-03", "The guideline identifier corresponds to the " + fromXml + " profile.");
            }

            if (string.IsNullOrEmpty(result.DeclaredConformanceLevel))
            {
                ValidationMessage missing = section.Error("REL-04",
                    "The invoice XML follows the " + fromXml + " profile, but the XMP metadata advertises no profile at all, so the PDF and the XML do not agree.");
                missing.Expected = "ConformanceLevel = " + fromXml + " (from " + result.GuidelineId + ")";
                missing.Found = "no ConformanceLevel in the XMP";
                return;
            }

            if (string.Equals(fromXml, result.DeclaredConformanceLevel.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                section.Info("REL-04", "The profile in the XMP matches the guideline in the XML (" + fromXml + ").");
            }
            else
            {
                ValidationMessage issue = section.Error("REL-04",
                    "The profile advertised in the XMP metadata contradicts the guideline inside the invoice XML.");
                issue.Expected = "ConformanceLevel = " + fromXml + " (from " + result.GuidelineId + ")";
                issue.Found = "ConformanceLevel = " + result.DeclaredConformanceLevel;
            }
        }

        private static void CheckDocumentTypeAgainstTypeCode(XDocument invoice, XmpMetadataView xmp, HybridValidationResult result, ValidationGroup section)
        {
            // Read the code from the document header rather than the first TypeCode
            // in the file: TypeCode also appears on tax, payment-means and other
            // sub-entities, and the header is the only one that describes the
            // document as a whole.
            XElement header = FindDocumentHeader(invoice);
            XElement typeCode = header == null
                ? null
                : header.Elements().FirstOrDefault(e => e.Name.LocalName == "TypeCode");

            if (typeCode == null)
            {
                section.Warning("REL-05", "The embedded XML has no document-level TypeCode, so the document type cannot be cross-checked.");
                return;
            }

            string code = typeCode.Value.Trim();
            string documentType = result.DeclaredDocumentType;

            if (!HybridSpecs.InvoiceTypeCodes.Contains(code))
            {
                ValidationMessage issue = section.Warning("REL-05", "The document type code is not one of the UNTDID 1001 codes normally used on an invoice.");
                issue.Found = code;
            }

            if (string.IsNullOrEmpty(documentType))
            {
                ValidationMessage missing = section.Error("REL-05",
                    "The embedded XML is an invoice, but the XMP metadata declares no document type, so the PDF and the XML do not agree.");
                missing.Expected = "DocumentType = " + (result.Flavour == HybridFlavour.OrderX ? "ORDER" : "INVOICE")
                    + " (XML type code " + code + ")";
                missing.Found = "no DocumentType in the XMP";
                return;
            }

            bool isOrder = string.Equals(documentType.Trim(), "ORDER", StringComparison.OrdinalIgnoreCase);
            bool xmlIsOrder = result.Flavour == HybridFlavour.OrderX;

            if (isOrder != xmlIsOrder)
            {
                ValidationMessage issue = section.Error("REL-05",
                    "The document type in the XMP does not match the kind of document that is actually embedded.");
                issue.Expected = xmlIsOrder ? "ORDER" : "INVOICE";
                issue.Found = documentType;
            }
            else
            {
                section.Info("REL-05", "The document type in the XMP matches the embedded document (type code " + code + ").");
            }
        }

        private static void ReportInvoiceIdentity(XDocument invoice, ValidationGroup section)
        {
            XElement header = FindDocumentHeader(invoice);
            if (header == null)
            {
                return;
            }

            XElement id = header.Elements().FirstOrDefault(e => e.Name.LocalName == "ID");
            XElement issued = header.Elements()
                .Where(e => e.Name.LocalName == "IssueDateTime")
                .Elements()
                .FirstOrDefault(e => e.Name.LocalName == "DateTimeString");

            section.Info("REL-06", "Embedded invoice: "
                + (id != null ? id.Value.Trim() : "(no number)")
                + (issued != null ? ", issued " + issued.Value.Trim() : string.Empty));
        }

        /// <summary>
        /// Finds the element describing the document as a whole. D16B-based profiles
        /// call it ExchangedDocument; ZUGFeRD 1.0 calls it HeaderExchangedDocument.
        /// </summary>
        private static XElement FindDocumentHeader(XDocument invoice)
        {
            return invoice.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "ExchangedDocument"
                                     || e.Name.LocalName == "HeaderExchangedDocument");
        }

        // ------------------------------------------------------------------
        // Extraction
        // ------------------------------------------------------------------

        private static string WriteAttachment(PdfEmbeddedFile attachment, string extractionFolder, HybridValidationResult result)
        {
            try
            {
                if (!Directory.Exists(extractionFolder))
                {
                    Directory.CreateDirectory(extractionFolder);
                }

                string fileName = attachment.EffectiveName;
                if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    fileName = "embedded-invoice.xml";
                }

                string target = Path.Combine(extractionFolder, fileName);
                File.WriteAllBytes(target, attachment.Content);
                return target;
            }
            catch (Exception ex)
            {
                ValidationGroup section = result.Groups
                    .FirstOrDefault(s => s.Title == AttachmentSectionTitle) ?? result.AddGroup(AttachmentSectionTitle);

                section.Error("EF-12", "The embedded XML could not be written to the results folder: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Parses the attachment from its raw bytes rather than from a decoded
        /// string, so that the encoding named in the XML declaration is actually
        /// applied. Decoding to text first would silently ignore a declaration
        /// naming an encoding that does not exist, which is a defect a recipient
        /// would hit as soon as it parsed the file properly.
        /// </summary>
        private static XDocument ParseXmlBytes(byte[] content)
        {
            using (MemoryStream stream = new MemoryStream(content))
            using (XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                CloseInput = false
            }))
            {
                return XDocument.Load(reader);
            }
        }
    }
}
