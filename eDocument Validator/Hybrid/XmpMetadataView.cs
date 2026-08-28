using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace eDocument_Validator.Hybrid
{
    /// <summary>
    /// A PDF/A extension schema declaration found in the XMP packet
    /// (pdfaExtension:schemas). Factur-X and ZUGFeRD both require one, because the
    /// fx: / zf: properties they add are not part of any standard XMP schema and
    /// PDF/A only permits custom properties that are described this way.
    /// </summary>
    public class XmpExtensionSchema
    {
        public string Description { get; set; }
        public string NamespaceUri { get; set; }
        public string Prefix { get; set; }
        public List<string> PropertyNames { get; set; }

        public XmpExtensionSchema()
        {
            PropertyNames = new List<string>();
        }
    }

    /// <summary>
    /// Read-only view over an XMP packet.
    /// <para>
    /// XMP allows the same property to be serialised either as a child element of
    /// an rdf:Description or as an attribute on it, and text values may be wrapped
    /// in an rdf:Alt / rdf:Seq / rdf:Bag container. Real-world Factur-X producers
    /// use all of these forms, so every lookup here accepts any of them rather than
    /// assuming one layout.
    /// </para>
    /// </summary>
    public class XmpMetadataView
    {
        private static readonly XNamespace Rdf = HybridSpecs.RdfNamespace;

        private readonly XDocument document;

        public XmpMetadataView(XDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }
            this.document = document;
        }

        /// <summary>Every namespace URI declared anywhere in the packet.</summary>
        public IEnumerable<string> DeclaredNamespaces
        {
            get
            {
                return document.Descendants()
                    .SelectMany(e => e.Attributes())
                    .Where(a => a.IsNamespaceDeclaration)
                    .Select(a => a.Value)
                    .Distinct(StringComparer.Ordinal);
            }
        }

        /// <summary>
        /// Returns the value of a simple XMP property, looking first for a child
        /// element and then for an attribute of the same qualified name.
        /// </summary>
        public string GetValue(string namespaceUri, string localName)
        {
            XNamespace ns = namespaceUri;

            XElement element = document.Descendants(ns + localName).FirstOrDefault();
            if (element != null)
            {
                string value = ReadElementValue(element);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            XAttribute attribute = document.Descendants()
                .SelectMany(e => e.Attributes())
                .FirstOrDefault(a => !a.IsNamespaceDeclaration && a.Name.Namespace == ns && a.Name.LocalName == localName);

            return attribute != null ? attribute.Value.Trim() : null;
        }

        /// <summary>True when the property is present, even with an empty value.</summary>
        public bool HasProperty(string namespaceUri, string localName)
        {
            XNamespace ns = namespaceUri;
            return document.Descendants(ns + localName).Any()
                || document.Descendants()
                    .SelectMany(e => e.Attributes())
                    .Any(a => !a.IsNamespaceDeclaration && a.Name.Namespace == ns && a.Name.LocalName == localName);
        }

        /// <summary>
        /// The prefix bound to a namespace URI in the packet, if any. Factur-X
        /// recommends "fx" and ZUGFeRD 2.0 "zf", and the prefix must match the one
        /// declared in the extension schema.
        /// </summary>
        public string GetPrefixFor(string namespaceUri)
        {
            XAttribute declaration = document.Descendants()
                .SelectMany(e => e.Attributes())
                .FirstOrDefault(a => a.IsNamespaceDeclaration
                                     && string.Equals(a.Value, namespaceUri, StringComparison.Ordinal)
                                     && a.Name.LocalName != "xmlns");

            return declaration != null ? declaration.Name.LocalName : null;
        }

        /// <summary>
        /// All PDF/A extension schema declarations in the packet.
        /// </summary>
        public List<XmpExtensionSchema> GetExtensionSchemas()
        {
            XNamespace schemaNs = HybridSpecs.PdfaSchemaNamespace;
            XNamespace propertyNs = HybridSpecs.PdfaPropertyNamespace;

            List<XmpExtensionSchema> results = new List<XmpExtensionSchema>();

            // Locate each schema by its namespaceURI property and work back up to the
            // container that holds the rest of the declaration. The container is an
            // rdf:li for the usual rdf:Bag serialisation, but a lone rdf:Description
            // is also valid.
            foreach (XElement namespaceElement in document.Descendants(schemaNs + "namespaceURI"))
            {
                XElement container = namespaceElement.Parent;
                if (container == null)
                {
                    continue;
                }

                XmpExtensionSchema schema = new XmpExtensionSchema
                {
                    NamespaceUri = ReadElementValue(namespaceElement),
                    Prefix = ReadChildOrAttribute(container, schemaNs, "prefix"),
                    Description = ReadChildOrAttribute(container, schemaNs, "schema")
                };

                schema.PropertyNames.AddRange(
                    container.Descendants(propertyNs + "name")
                        .Select(ReadElementValue)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Select(n => n.Trim()));

                // Producers that serialise property definitions as attributes leave no
                // pdfaProperty:name elements behind, so pick those up as well.
                schema.PropertyNames.AddRange(
                    container.Descendants()
                        .SelectMany(e => e.Attributes())
                        .Where(a => !a.IsNamespaceDeclaration
                                    && a.Name.Namespace == propertyNs
                                    && a.Name.LocalName == "name")
                        .Select(a => a.Value.Trim()));

                results.Add(schema);
            }

            // Schemas declared entirely through attributes carry no
            // pdfaSchema:namespaceURI element, so collect those separately.
            foreach (XElement container in document.Descendants())
            {
                XAttribute namespaceAttribute = container.Attributes()
                    .FirstOrDefault(a => !a.IsNamespaceDeclaration
                                         && a.Name.Namespace == schemaNs
                                         && a.Name.LocalName == "namespaceURI");

                if (namespaceAttribute == null)
                {
                    continue;
                }

                XmpExtensionSchema schema = new XmpExtensionSchema
                {
                    NamespaceUri = namespaceAttribute.Value.Trim(),
                    Prefix = ReadChildOrAttribute(container, schemaNs, "prefix"),
                    Description = ReadChildOrAttribute(container, schemaNs, "schema")
                };

                schema.PropertyNames.AddRange(
                    container.Descendants(propertyNs + "name")
                        .Select(ReadElementValue)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Select(n => n.Trim()));

                schema.PropertyNames.AddRange(
                    container.Descendants()
                        .SelectMany(e => e.Attributes())
                        .Where(a => !a.IsNamespaceDeclaration
                                    && a.Name.Namespace == propertyNs
                                    && a.Name.LocalName == "name")
                        .Select(a => a.Value.Trim()));

                results.Add(schema);
            }

            return results;
        }

        private static string ReadChildOrAttribute(XElement container, XNamespace ns, string localName)
        {
            XElement child = container.Element(ns + localName);
            if (child != null)
            {
                return ReadElementValue(child);
            }

            XAttribute attribute = container.Attributes()
                .FirstOrDefault(a => !a.IsNamespaceDeclaration && a.Name.Namespace == ns && a.Name.LocalName == localName);

            return attribute != null ? attribute.Value.Trim() : null;
        }

        /// <summary>
        /// Reads an element value, unwrapping the rdf:Alt / rdf:Seq / rdf:Bag
        /// container that XMP uses for language alternatives and ordered arrays.
        /// </summary>
        private static string ReadElementValue(XElement element)
        {
            XElement container = element.Element(Rdf + "Alt")
                                 ?? element.Element(Rdf + "Seq")
                                 ?? element.Element(Rdf + "Bag");

            if (container != null)
            {
                XElement item = container.Elements(Rdf + "li").FirstOrDefault();
                return item != null ? item.Value.Trim() : string.Empty;
            }

            return element.Value.Trim();
        }
    }
}
