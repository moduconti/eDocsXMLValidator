using System;
using System.Collections.Generic;
using System.Linq;

namespace eDocument_Validator.Hybrid
{
    /// <summary>
    /// The hybrid-invoice flavour a PDF claims to be, derived from the XMP
    /// namespace it declares. Factur-X and ZUGFeRD share a document model but
    /// announce themselves under different URNs.
    /// </summary>
    public enum HybridFlavour
    {
        Unknown,
        FacturX,
        ZugferdV2,
        ZugferdV1,
        OrderX
    }

    /// <summary>
    /// Constants from the Factur-X 1.0.x / ZUGFeRD 2.x / Order-X specifications:
    /// XMP namespaces, permitted attachment file names, conformance levels, and
    /// the mapping from an EN 16931 guideline URN to the conformance level the
    /// XMP metadata must advertise.
    /// </summary>
    internal static class HybridSpecs
    {
        // --- XMP extension-schema namespaces --------------------------------

        public const string FacturXNamespace = "urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#";
        public const string ZugferdV2Namespace = "urn:zugferd:pdfa:CrossIndustryDocument:invoice:2p0#";
        public const string ZugferdV1Namespace = "urn:ferd:pdfa:CrossIndustryDocument:invoice:1p0#";
        public const string OrderXNamespace = "urn:factur-x:pdfa:CrossIndustryDocument:order:1p0#";

        /// <summary>Namespaces probed, in priority order, when detecting the flavour.</summary>
        public static readonly KeyValuePair<string, HybridFlavour>[] KnownNamespaces =
        {
            new KeyValuePair<string, HybridFlavour>(FacturXNamespace, HybridFlavour.FacturX),
            new KeyValuePair<string, HybridFlavour>(ZugferdV2Namespace, HybridFlavour.ZugferdV2),
            new KeyValuePair<string, HybridFlavour>(OrderXNamespace, HybridFlavour.OrderX),
            new KeyValuePair<string, HybridFlavour>(ZugferdV1Namespace, HybridFlavour.ZugferdV1)
        };

        // --- Standard XMP namespaces ----------------------------------------

        public const string RdfNamespace = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        public const string PdfaIdNamespace = "http://www.aiim.org/pdfa/ns/id/";
        public const string PdfaExtensionNamespace = "http://www.aiim.org/pdfa/ns/extension/";
        public const string PdfaSchemaNamespace = "http://www.aiim.org/pdfa/ns/schema#";
        public const string PdfaPropertyNamespace = "http://www.aiim.org/pdfa/ns/property#";
        public const string DublinCoreNamespace = "http://purl.org/dc/elements/1.1/";
        public const string AdobePdfNamespace = "http://ns.adobe.com/pdf/1.3/";
        public const string XmpBasicNamespace = "http://ns.adobe.com/xap/1.0/";

        // --- CII namespaces --------------------------------------------------

        public const string CrossIndustryInvoiceNamespace = "urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100";
        public const string CrossIndustryOrderNamespace = "urn:un:unece:uncefact:data:standard:SCRDMCCBDACIOMessageStructure:100";
        public const string RamNamespace = "urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100";
        public const string UdtNamespace = "urn:un:unece:uncefact:data:standard:UnqualifiedDataType:100";

        /// <summary>
        /// ZUGFeRD 1.0 predates the UN/CEFACT D16B message and uses its own root
        /// namespace, so the root-element check has to expect a different URN for
        /// that flavour.
        /// </summary>
        public const string ZugferdV1DocumentNamespace = "urn:ferd:CrossIndustryDocument:invoice:1p0";

        /// <summary>The root namespace the embedded XML must use for a given flavour.</summary>
        public static string ExpectedDocumentNamespace(HybridFlavour flavour)
        {
            switch (flavour)
            {
                case HybridFlavour.OrderX:
                    return CrossIndustryOrderNamespace;
                case HybridFlavour.ZugferdV1:
                    return ZugferdV1DocumentNamespace;
                default:
                    return CrossIndustryInvoiceNamespace;
            }
        }

        // --- Attachment file names -------------------------------------------

        /// <summary>
        /// Every attachment name that identifies a hybrid-invoice payload, used to
        /// find the XML inside a PDF before the profile is known. This is the list
        /// the reference implementation of ZUGFeRD recognises.
        /// </summary>
        public static readonly string[] CandidateAttachmentNames =
        {
            "factur-x.xml",
            "zugferd-invoice.xml",
            "ZUGFeRD-invoice.xml",
            "xrechnung.xml",
            "order-x.xml",
            "cida.xml"
        };

        /// <summary>
        /// The attachment names that are correct for a document, which depends on
        /// both the specification it follows and the profile inside it.
        /// <para>
        /// The XRECHNUNG profile is the reason this is not a single name. An
        /// XRechnung invoice carried in a PDF is named "xrechnung.xml" by the
        /// German reference tools and by the official ZUGFeRD test files, while
        /// "factur-x.xml" is also accepted. Reporting either of those as wrong
        /// would be a false alarm.
        /// </para>
        /// </summary>
        public static string[] AcceptedAttachmentNames(HybridFlavour flavour, string conformanceLevel)
        {
            bool isXRechnung = conformanceLevel != null
                && conformanceLevel.Trim().Equals("XRECHNUNG", StringComparison.OrdinalIgnoreCase);

            switch (flavour)
            {
                case HybridFlavour.OrderX:
                    return new[] { "order-x.xml" };

                case HybridFlavour.ZugferdV1:
                    return new[] { "ZUGFeRD-invoice.xml" };

                case HybridFlavour.ZugferdV2:
                    return isXRechnung
                        ? new[] { "zugferd-invoice.xml", "factur-x.xml", "xrechnung.xml" }
                        : new[] { "zugferd-invoice.xml", "factur-x.xml" };

                case HybridFlavour.FacturX:
                    return isXRechnung
                        ? new[] { "factur-x.xml", "xrechnung.xml" }
                        : new[] { "factur-x.xml" };

                default:
                    // The specification could not be identified, so any of the
                    // recognised names is as good as another.
                    return CandidateAttachmentNames;
            }
        }

        // --- Conformance levels -----------------------------------------------

        /// <summary>Values permitted in fx:ConformanceLevel / zf:ConformanceLevel.</summary>
        public static readonly string[] ConformanceLevels =
        {
            "MINIMUM",
            "BASIC WL",
            "BASIC",
            "EN 16931",
            "EXTENDED",
            "XRECHNUNG"
        };

        /// <summary>Values permitted in fx:DocumentType / zf:DocumentType.</summary>
        public static readonly string[] DocumentTypes = { "INVOICE", "ORDER" };

        /// <summary>
        /// Exact guideline URN (ram:GuidelineSpecifiedDocumentContextParameter/ram:ID)
        /// mapped to the conformance level the XMP metadata must declare.
        /// </summary>
        private static readonly Dictionary<string, string> GuidelineToConformance =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "urn:factur-x.eu:1p0:minimum", "MINIMUM" },
                { "urn:factur-x.eu:1p0:basicwl", "BASIC WL" },
                { "urn:cen.eu:en16931:2017#compliant#urn:factur-x.eu:1p0:basic", "BASIC" },
                { "urn:cen.eu:en16931:2017", "EN 16931" },
                { "urn:cen.eu:en16931:2017#conformant#urn:factur-x.eu:1p0:extended", "EXTENDED" },

                { "urn:zugferd.de:2p0:minimum", "MINIMUM" },
                { "urn:zugferd.de:2p0:basicwl", "BASIC WL" },
                { "urn:cen.eu:en16931:2017#compliant#urn:zugferd.de:2p0:basic", "BASIC" },
                { "urn:cen.eu:en16931:2017#conformant#urn:zugferd.de:2p0:extended", "EXTENDED" },

                { "urn:cen.eu:en16931:2017#compliant#urn:xoev-de:kosit:standard:xrechnung_2.1", "XRECHNUNG" },
                { "urn:cen.eu:en16931:2017#compliant#urn:xoev-de:kosit:standard:xrechnung_2.2", "XRECHNUNG" },
                { "urn:cen.eu:en16931:2017#compliant#urn:xoev-de:kosit:standard:xrechnung_2.3", "XRECHNUNG" },
                { "urn:cen.eu:en16931:2017#compliant#urn:xoev-de:kosit:standard:xrechnung_3.0", "XRECHNUNG" },
                // XRechnung 3.0 moved the specification identifier from xoev-de to
                // xeinkauf.de; both forms are in circulation.
                { "urn:cen.eu:en16931:2017#compliant#urn:xeinkauf.de:kosit:xrechnung_3.0", "XRECHNUNG" },
                { "urn:cen.eu:en16931:2017#conformant#urn:xeinkauf.de:kosit:xrechnung_3.0", "XRECHNUNG" },

                { "urn:order-x.eu:1p0:basic", "BASIC" },
                { "urn:order-x.eu:1p0:comfort", "EN 16931" },
                { "urn:order-x.eu:1p0:extended", "EXTENDED" }
            };

        /// <summary>
        /// Resolves a guideline URN to the conformance level the XMP is expected to
        /// carry. Falls back to matching on the profile suffix so that national
        /// variants extending the standard URN (for example the French CTC EXTENDED
        /// profile) still resolve instead of being reported as unknown.
        /// <paramref name="exactMatch"/> reports which of the two paths was taken.
        /// </summary>
        public static string ConformanceForGuideline(string guidelineId, out bool exactMatch)
        {
            exactMatch = false;
            if (string.IsNullOrWhiteSpace(guidelineId))
            {
                return null;
            }

            string trimmed = guidelineId.Trim();
            string conformance;
            if (GuidelineToConformance.TryGetValue(trimmed, out conformance))
            {
                exactMatch = true;
                return conformance;
            }

            string lower = trimmed.ToLowerInvariant();
            if (lower.Contains("xrechnung")) { return "XRECHNUNG"; }
            if (lower.Contains("extended")) { return "EXTENDED"; }
            if (lower.Contains("basicwl")) { return "BASIC WL"; }
            if (lower.Contains(":basic")) { return "BASIC"; }
            if (lower.Contains("minimum")) { return "MINIMUM"; }
            if (lower.StartsWith("urn:cen.eu:en16931:2017")) { return "EN 16931"; }

            return null;
        }

        /// <summary>
        /// UNTDID 1001 document type codes a Factur-X / ZUGFeRD invoice may carry
        /// in rsm:ExchangedDocument/ram:TypeCode.
        /// </summary>
        public static readonly string[] InvoiceTypeCodes =
        {
            "71", "80", "82", "84", "102", "130", "202", "203", "204", "211", "218", "219",
            "261", "262", "295", "296", "308", "325", "326", "331", "380", "381", "382",
            "383", "384", "385", "386", "387", "388", "389", "390", "393", "394", "395",
            "396", "420", "456", "457", "458", "527", "532", "575", "623", "633", "751",
            "780", "817", "870", "875", "876", "877"
        };

        public static string Describe(HybridFlavour flavour)
        {
            switch (flavour)
            {
                case HybridFlavour.FacturX:
                    return "Factur-X 1.0.x (also used by ZUGFeRD 2.1 and later)";
                case HybridFlavour.ZugferdV2:
                    return "ZUGFeRD 2.0";
                case HybridFlavour.ZugferdV1:
                    return "ZUGFeRD 1.0";
                case HybridFlavour.OrderX:
                    return "Order-X 1.0";
                default:
                    return "unknown";
            }
        }

        public static bool IsKnownConformanceLevel(string value)
        {
            return value != null
                && ConformanceLevels.Any(c => string.Equals(c, value.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }
}
