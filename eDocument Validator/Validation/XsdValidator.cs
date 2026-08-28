using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;

namespace eDocument_Validator.Validation
{
    /// <summary>
    /// Validates an XML document against a set of XSD schema files.
    /// <para>
    /// All schema files of a format are loaded together into one schema set and the
    /// document is checked once against all of them. This is necessary because a
    /// format folder holds one root schema plus a number of files that only define
    /// types and code lists. Checking the document against each file on its own
    /// would report that the root element is unknown for every file except one.
    /// </para>
    /// <para>
    /// Loading and compiling a schema set is slow for formats with many files, so
    /// the finished set is kept and reused until one of its files changes.
    /// </para>
    /// </summary>
    public class XsdValidator
    {
        private readonly object cacheLock = new object();

        private string cacheKey;
        private XmlSchemaSet cachedSchemas;
        private List<ValidationMessage> cachedLoadMessages;

        /// <summary>
        /// Loads the schema files ahead of time so that a later validation does not
        /// have to wait for them.
        /// </summary>
        public void Prepare(IEnumerable<string> schemaFilePaths)
        {
            List<string> files = schemaFilePaths.ToList();
            if (files.Count > 0)
            {
                GetSchemas(files);
            }
        }

        /// <summary>
        /// Checks the document and returns one message per problem found. An empty
        /// list means the document matches the schema.
        /// </summary>
        public List<ValidationMessage> Validate(IEnumerable<string> schemaFilePaths, string xmlDocumentPath)
        {
            List<string> files = schemaFilePaths.ToList();

            List<ValidationMessage> loadMessages;
            XmlSchemaSet schemas = GetSchemas(files, out loadMessages);

            List<ValidationMessage> messages = new List<ValidationMessage>(loadMessages);

            XmlReaderSettings settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.Schema,
                Schemas = schemas,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            settings.ValidationFlags |= XmlSchemaValidationFlags.ProcessInlineSchema;
            settings.ValidationFlags |= XmlSchemaValidationFlags.ProcessSchemaLocation;
            settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;

            settings.ValidationEventHandler += (sender, e) => messages.Add(BuildMessage(e));

            try
            {
                using (XmlReader reader = XmlReader.Create(xmlDocumentPath, settings))
                {
                    while (reader.Read())
                    {
                        // Reading the whole document is what triggers validation.
                    }
                }
            }
            catch (XmlException exception)
            {
                // The document is not well formed, so schema validation stops here.
                ValidationMessage message = new ValidationMessage(ValidationSeverity.Error, "XSD-XML",
                    "The document is not well-formed XML: " + exception.Message);
                message.Location = DescribePosition(exception.LineNumber, exception.LinePosition);
                messages.Add(message);
            }

            return messages;
        }

        private XmlSchemaSet GetSchemas(List<string> files)
        {
            List<ValidationMessage> ignored;
            return GetSchemas(files, out ignored);
        }

        private XmlSchemaSet GetSchemas(List<string> files, out List<ValidationMessage> loadMessages)
        {
            string key = BuildKey(files);

            lock (cacheLock)
            {
                if (cacheKey == key && cachedSchemas != null)
                {
                    loadMessages = cachedLoadMessages;
                    return cachedSchemas;
                }
            }

            List<ValidationMessage> messages = new List<ValidationMessage>();
            XmlSchemaSet schemas = new XmlSchemaSet();

            // Report real problems with the schema files themselves, but ignore
            // warnings such as an import of a namespace that no file provides,
            // which is common in unused code-list fragments.
            schemas.ValidationEventHandler += (sender, e) =>
            {
                if (e.Severity == XmlSeverityType.Error)
                {
                    messages.Add(new ValidationMessage(ValidationSeverity.Error, "XSD-SCHEMA",
                        "A schema file could not be loaded: " + e.Message));
                }
            };

            foreach (string schemaFilePath in files)
            {
                try
                {
                    schemas.Add(null, schemaFilePath);
                }
                catch (Exception exception)
                {
                    messages.Add(new ValidationMessage(ValidationSeverity.Error, "XSD-SCHEMA",
                        "The schema file '" + Path.GetFileName(schemaFilePath) + "' could not be loaded: " + exception.Message));
                }
            }

            // Compiling now rather than during the first validation keeps the cost
            // in the step that is meant to be slow.
            try
            {
                schemas.Compile();
            }
            catch (Exception exception)
            {
                messages.Add(new ValidationMessage(ValidationSeverity.Error, "XSD-SCHEMA",
                    "The schema set could not be compiled: " + exception.Message));
            }

            lock (cacheLock)
            {
                cacheKey = key;
                cachedSchemas = schemas;
                cachedLoadMessages = messages;
            }

            loadMessages = messages;
            return schemas;
        }

        /// <summary>
        /// Identifies a set of schema files by their paths and write times, so an
        /// edited schema is loaded again instead of being served from memory.
        /// </summary>
        private static string BuildKey(IEnumerable<string> files)
        {
            return string.Join("|", files.Select(file =>
                file + "@" + File.GetLastWriteTimeUtc(file).Ticks));
        }

        private static ValidationMessage BuildMessage(ValidationEventArgs e)
        {
            ValidationSeverity severity = e.Severity == XmlSeverityType.Error
                ? ValidationSeverity.Error
                : ValidationSeverity.Warning;

            ValidationMessage message = new ValidationMessage(severity, "XSD", e.Message);

            if (e.Exception != null)
            {
                message.Location = DescribePosition(e.Exception.LineNumber, e.Exception.LinePosition);
            }

            return message;
        }

        private static string DescribePosition(int lineNumber, int linePosition)
        {
            return lineNumber <= 0 ? null : "line " + lineNumber + ", position " + linePosition;
        }
    }
}
