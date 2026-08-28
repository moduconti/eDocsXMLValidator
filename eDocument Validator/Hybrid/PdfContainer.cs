using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Tokens;

namespace eDocument_Validator.Hybrid
{
    /// <summary>
    /// One entry of the PDF embedded-file name tree, kept in raw form. The
    /// Factur-X / ZUGFeRD rules constrain the file specification dictionary
    /// itself (AFRelationship, UF, Subtype, Params/ModDate), so the dictionary is
    /// carried through rather than just the decoded bytes.
    /// </summary>
    public class PdfEmbeddedFile
    {
        public string NameTreeKey { get; set; }
        public DictionaryToken FileSpecification { get; set; }
        public IndirectReference? FileSpecificationReference { get; set; }

        public string TypeEntry { get; set; }
        public string FileName { get; set; }
        public string UnicodeFileName { get; set; }
        public string Description { get; set; }
        public string AssociatedFileRelationship { get; set; }

        public DictionaryToken EmbeddedFileStreamDictionary { get; set; }
        public string Subtype { get; set; }
        public string ModificationDate { get; set; }
        public long? DeclaredSize { get; set; }

        public byte[] Content { get; set; }
        public string ContentError { get; set; }

        public bool ReferencedFromCatalogAssociatedFiles { get; set; }

        /// <summary>The name a consumer would use, preferring /UF as the spec requires.</summary>
        public string EffectiveName
        {
            get
            {
                if (!string.IsNullOrEmpty(UnicodeFileName)) { return UnicodeFileName; }
                if (!string.IsNullOrEmpty(FileName)) { return FileName; }
                return NameTreeKey;
            }
        }
    }

    /// <summary>
    /// Everything the hybrid checks need to know about a PDF file, read once and
    /// exposed as plain data. Reading is deliberately tolerant: a structurally
    /// broken PDF should still produce a report describing what is broken, rather
    /// than an exception.
    /// </summary>
    public class PdfContainer : IDisposable
    {
        private readonly PdfDocument document;

        private PdfContainer(PdfDocument document)
        {
            this.document = document;
            EmbeddedFiles = new List<PdfEmbeddedFile>();
            OutputIntents = new List<DictionaryToken>();
            ReadFailures = new List<string>();
        }

        public double Version { get; private set; }
        public bool IsEncrypted { get; private set; }
        public int PageCount { get; private set; }
        public DocumentInformation Information { get; private set; }
        public DictionaryToken Catalog { get; private set; }

        public List<PdfEmbeddedFile> EmbeddedFiles { get; private set; }
        public List<DictionaryToken> OutputIntents { get; private set; }

        public bool HasEmbeddedFilesNameTree { get; private set; }
        public int CatalogAssociatedFileCount { get; private set; }
        public bool HasCatalogAssociatedFiles { get { return CatalogAssociatedFileCount > 0; } }

        public bool? MarkedAsTagged { get; private set; }
        public bool HasJavaScriptNameTree { get; private set; }

        public byte[] XmpBytes { get; private set; }
        public XDocument Xmp { get; private set; }
        public string XmpError { get; private set; }

        /// <summary>Non-fatal problems hit while reading; surfaced in the report.</summary>
        public List<string> ReadFailures { get; private set; }

        public static PdfContainer Open(string path)
        {
            // Lenient parsing keeps malformed-but-readable invoices open so the
            // report can describe them, and fonts are irrelevant to these checks.
            PdfDocument document = PdfDocument.Open(path, new ParsingOptions
            {
                UseLenientParsing = true,
                SkipMissingFonts = true
            });

            PdfContainer container = new PdfContainer(document);
            container.Read();
            return container;
        }

        private void Read()
        {
            Version = document.Version;
            IsEncrypted = document.IsEncrypted;
            Information = document.Information;

            try
            {
                PageCount = document.NumberOfPages;
            }
            catch (Exception ex)
            {
                ReadFailures.Add("Could not determine the page count: " + ex.Message);
            }

            Catalog = document.Structure.Catalog.CatalogDictionary;

            ReadXmp();
            ReadOutputIntents();
            ReadMarkInfoAndActions();

            AssociatedFiles associatedFiles = ReadCatalogAssociatedFiles();
            ReadEmbeddedFiles(associatedFiles);
        }

        // --- catalog level ----------------------------------------------------

        private void ReadXmp()
        {
            try
            {
                XmpMetadata metadata;
                if (!document.TryGetXmpMetadata(out metadata))
                {
                    return;
                }

                XmpBytes = metadata.GetXmlBytes().ToArray();

                try
                {
                    Xmp = metadata.GetXDocument();
                }
                catch (Exception)
                {
                    // Some producers pad the packet or emit a byte-order mark inside
                    // it; retry from the raw bytes with the padding stripped before
                    // giving up.
                    Xmp = TryParseXmpBytes(XmpBytes, out string error);
                    XmpError = error;
                }
            }
            catch (Exception ex)
            {
                XmpError = ex.Message;
            }
        }

        private static XDocument TryParseXmpBytes(byte[] bytes, out string error)
        {
            error = null;
            if (bytes == null || bytes.Length == 0)
            {
                error = "The XMP metadata stream is empty.";
                return null;
            }

            try
            {
                string text = new UTF8Encoding(false).GetString(bytes).Trim('\0', ' ', '\r', '\n', '\t');

                // Skipping forward to the packet header also discards any byte-order
                // mark the producer wrote inside the stream.
                int start = text.IndexOf("<?xpacket", StringComparison.Ordinal);
                if (start < 0) { start = text.IndexOf("<x:xmpmeta", StringComparison.Ordinal); }
                if (start < 0) { start = text.IndexOf("<rdf:RDF", StringComparison.Ordinal); }
                if (start > 0) { text = text.Substring(start); }

                return XDocument.Parse(text, LoadOptions.PreserveWhitespace);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }

        private void ReadOutputIntents()
        {
            ArrayToken outputIntents;
            if (Catalog == null || !Catalog.TryGet(NameToken.Create("OutputIntents"), document.Structure.TokenScanner, out outputIntents))
            {
                return;
            }

            foreach (IToken token in outputIntents.Data)
            {
                DictionaryToken intent = Resolve(token) as DictionaryToken;
                if (intent != null)
                {
                    OutputIntents.Add(intent);
                }
            }
        }

        private void ReadMarkInfoAndActions()
        {
            if (Catalog == null)
            {
                return;
            }

            DictionaryToken markInfo;
            if (Catalog.TryGet(NameToken.Create("MarkInfo"), document.Structure.TokenScanner, out markInfo))
            {
                BooleanToken marked;
                MarkedAsTagged = markInfo.TryGet(NameToken.Create("Marked"), document.Structure.TokenScanner, out marked)
                    && marked.Data;
            }

            DictionaryToken names;
            if (Catalog.TryGet(NameToken.Create("Names"), document.Structure.TokenScanner, out names))
            {
                HasJavaScriptNameTree = names.ContainsKey(NameToken.Create("JavaScript"));
            }
        }

        /// <summary>
        /// Reads the document-level /AF array. PDF/A-3 associates an embedded file
        /// with the document as a whole through this array; Factur-X requires the
        /// invoice XML to appear in it.
        /// </summary>
        private AssociatedFiles ReadCatalogAssociatedFiles()
        {
            AssociatedFiles associated = new AssociatedFiles();

            ArrayToken associatedFiles;
            if (Catalog == null || !Catalog.TryGet(NameToken.Create("AF"), document.Structure.TokenScanner, out associatedFiles))
            {
                return associated;
            }

            CatalogAssociatedFileCount = associatedFiles.Length;

            foreach (IToken token in associatedFiles.Data)
            {
                IndirectReferenceToken reference = token as IndirectReferenceToken;
                if (reference != null)
                {
                    associated.References.Add(reference.Data);
                }

                // The array may hold the file specification directly, and a
                // producer may also reference a different object with identical
                // content from the name tree. Keeping the resolved dictionaries
                // lets the association be recognised in both cases instead of
                // being reported as missing.
                DictionaryToken fileSpecification = Resolve(token) as DictionaryToken;
                if (fileSpecification != null)
                {
                    associated.Dictionaries.Add(fileSpecification);
                }
            }

            return associated;
        }

        /// <summary>
        /// The document-level /AF entries, held both by object reference and by
        /// resolved dictionary so an entry can be matched either way.
        /// </summary>
        private class AssociatedFiles
        {
            public AssociatedFiles()
            {
                References = new HashSet<IndirectReference>();
                Dictionaries = new List<DictionaryToken>();
            }

            public HashSet<IndirectReference> References { get; private set; }
            public List<DictionaryToken> Dictionaries { get; private set; }

            public bool Contains(IndirectReference? reference, DictionaryToken fileSpecification)
            {
                if (reference.HasValue && References.Contains(reference.Value))
                {
                    return true;
                }

                return fileSpecification != null && Dictionaries.Any(d => d.Equals(fileSpecification));
            }
        }

        // --- embedded files ---------------------------------------------------

        private void ReadEmbeddedFiles(AssociatedFiles associatedFiles)
        {
            DictionaryToken names;
            if (Catalog == null || !Catalog.TryGet(NameToken.Create("Names"), document.Structure.TokenScanner, out names))
            {
                return;
            }

            DictionaryToken embeddedFilesNode;
            if (!names.TryGet(NameToken.Create("EmbeddedFiles"), document.Structure.TokenScanner, out embeddedFilesNode))
            {
                return;
            }

            HasEmbeddedFilesNameTree = true;
            WalkNameTree(embeddedFilesNode, associatedFiles, 0);
        }

        /// <summary>
        /// Walks a PDF name tree node, which holds either leaf /Names pairs or
        /// intermediate /Kids. Depth is bounded so a cyclic tree cannot hang the app.
        /// </summary>
        private void WalkNameTree(DictionaryToken node, AssociatedFiles associatedFiles, int depth)
        {
            if (node == null || depth > 32)
            {
                return;
            }

            ArrayToken namePairs;
            if (node.TryGet(NameToken.Create("Names"), document.Structure.TokenScanner, out namePairs))
            {
                for (int i = 0; i + 1 < namePairs.Length; i += 2)
                {
                    StringToken key = Resolve(namePairs[i]) as StringToken;
                    IToken rawValue = namePairs[i + 1];

                    PdfEmbeddedFile entry = ReadFileSpecification(
                        key != null ? key.Data : null,
                        rawValue,
                        associatedFiles);

                    if (entry != null)
                    {
                        EmbeddedFiles.Add(entry);
                    }
                }
            }

            ArrayToken kids;
            if (node.TryGet(NameToken.Create("Kids"), document.Structure.TokenScanner, out kids))
            {
                foreach (IToken kid in kids.Data)
                {
                    WalkNameTree(Resolve(kid) as DictionaryToken, associatedFiles, depth + 1);
                }
            }
        }

        private PdfEmbeddedFile ReadFileSpecification(string nameTreeKey, IToken rawValue, AssociatedFiles associatedFiles)
        {
            IndirectReferenceToken reference = rawValue as IndirectReferenceToken;
            DictionaryToken fileSpecification = Resolve(rawValue) as DictionaryToken;
            if (fileSpecification == null)
            {
                ReadFailures.Add("Embedded file entry '" + (nameTreeKey ?? "(unnamed)") + "' is not a file specification dictionary.");
                return null;
            }

            PdfEmbeddedFile entry = new PdfEmbeddedFile
            {
                NameTreeKey = nameTreeKey,
                FileSpecification = fileSpecification,
                FileSpecificationReference = reference != null ? reference.Data : (IndirectReference?)null,
                TypeEntry = ReadName(fileSpecification, "Type"),
                FileName = ReadText(fileSpecification, "F"),
                UnicodeFileName = ReadText(fileSpecification, "UF"),
                Description = ReadText(fileSpecification, "Desc"),
                AssociatedFileRelationship = ReadName(fileSpecification, "AFRelationship")
            };

            entry.ReferencedFromCatalogAssociatedFiles =
                associatedFiles.Contains(entry.FileSpecificationReference, fileSpecification);

            ReadEmbeddedFileStream(fileSpecification, entry);

            return entry;
        }

        private void ReadEmbeddedFileStream(DictionaryToken fileSpecification, PdfEmbeddedFile entry)
        {
            DictionaryToken embeddedFileDictionary;
            if (!fileSpecification.TryGet(NameToken.Create("EF"), document.Structure.TokenScanner, out embeddedFileDictionary))
            {
                entry.ContentError = "The file specification has no /EF entry, so no file is actually embedded.";
                return;
            }

            // /F is the standard key; /UF is permitted and used by some producers.
            IToken streamToken;
            if (!embeddedFileDictionary.TryGet(NameToken.Create("F"), out streamToken)
                && !embeddedFileDictionary.TryGet(NameToken.Create("UF"), out streamToken))
            {
                entry.ContentError = "The /EF dictionary has neither an /F nor a /UF stream.";
                return;
            }

            StreamToken stream = Resolve(streamToken) as StreamToken;
            if (stream == null)
            {
                entry.ContentError = "The /EF entry does not resolve to a stream object.";
                return;
            }

            entry.EmbeddedFileStreamDictionary = stream.StreamDictionary;
            entry.Subtype = ReadName(stream.StreamDictionary, "Subtype");

            DictionaryToken parameters;
            if (stream.StreamDictionary.TryGet(NameToken.Create("Params"), document.Structure.TokenScanner, out parameters))
            {
                entry.ModificationDate = ReadText(parameters, "ModDate");

                NumericToken size;
                if (parameters.TryGet(NameToken.Create("Size"), document.Structure.TokenScanner, out size))
                {
                    entry.DeclaredSize = size.Long;
                }
            }

            try
            {
                entry.Content = stream.Decode(document.Structure.FilterProvider, document.Structure.TokenScanner).ToArray();
            }
            catch (Exception ex)
            {
                entry.ContentError = "The embedded file stream could not be decoded: " + ex.Message;
            }
        }

        // --- token helpers ----------------------------------------------------

        private IToken Resolve(IToken token)
        {
            IndirectReferenceToken reference = token as IndirectReferenceToken;
            if (reference == null)
            {
                return token;
            }

            try
            {
                ObjectToken resolved = document.Structure.GetObject(reference.Data);
                return resolved != null ? resolved.Data : null;
            }
            catch (Exception ex)
            {
                ReadFailures.Add("Could not resolve indirect object " + reference.Data + ": " + ex.Message);
                return null;
            }
        }

        public string ReadName(DictionaryToken dictionary, string key)
        {
            if (dictionary == null) { return null; }

            NameToken name;
            return dictionary.TryGet(NameToken.Create(key), document.Structure.TokenScanner, out name) ? name.Data : null;
        }

        public string ReadText(DictionaryToken dictionary, string key)
        {
            if (dictionary == null) { return null; }

            StringToken text;
            if (dictionary.TryGet(NameToken.Create(key), document.Structure.TokenScanner, out text))
            {
                return text.Data;
            }

            // A few producers write file names as names rather than strings.
            return ReadName(dictionary, key);
        }

        public void Dispose()
        {
            document.Dispose();
        }
    }
}
