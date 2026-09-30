using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace eDocument_Validator.Validation
{
    /// <summary>
    /// Validates one XML document against every validation file of one format.
    /// <para>
    /// Schematron files are run one at a time, because each one is a complete set
    /// of rules and its result is reported on its own. Schema files are used
    /// together as a single set; see <see cref="XsdValidator"/> for why.
    /// </para>
    /// </summary>
    public class DocumentValidator
    {
        private readonly SchematronValidator schematronValidator = new SchematronValidator();
        private readonly XsdValidator xsdValidator = new XsdValidator();

        /// <summary>
        /// Called before each validation file is run, so the window can show which
        /// file is being worked on. May be null.
        /// </summary>
        public Action<string> FileStarted { get; set; }

        /// <summary>
        /// Compiles the rules of a format ahead of time. Calling this as soon as the
        /// user chooses a format means the waiting happens while they are still
        /// choosing a document, instead of after they press Validate.
        /// <para>
        /// Schematron source files are deliberately not touched here: compiling one
        /// removes it, and that should only happen as part of a real validation, not
        /// because a name was picked from a list.
        /// </para>
        /// </summary>
        public void Prepare(ValidationFormat format)
        {
            schematronValidator.Prepare(format.SchematronFiles());
            foreach (SchemaSet schemaSet in format.SchemaSets())
            {
                xsdValidator.Prepare(schemaSet.Files);
            }
        }

        /// <summary>
        /// Runs every validation file of the format and returns one group of
        /// messages per file.
        /// </summary>
        public List<ValidationGroup> Validate(ValidationFormat format, string xmlDocumentPath)
        {
            List<ValidationGroup> groups = new List<ValidationGroup>();

            ValidationGroup conversion = CompileSchematronSources(format);
            if (conversion != null)
            {
                groups.Add(conversion);
            }

            foreach (string schematronFile in format.SchematronFiles())
            {
                groups.Add(RunSchematron(schematronFile, xmlDocumentPath));
            }

            foreach (SchemaSet schemaSet in format.SchemaSets())
            {
                groups.Add(RunSchemas(schemaSet, xmlDocumentPath));
            }

            if (groups.Count == 0)
            {
                ValidationGroup empty = new ValidationGroup("Format " + format.Name);
                empty.Warning("FORMAT-01",
                    "This format folder contains no Schematron or schema files, so nothing was checked. "
                    + "Add .xsl, .xslt or .xsd files to the folder, or a folder with an .xsd bundle.");
                groups.Add(empty);
            }

            return groups;
        }

        /// <summary>
        /// Compiles any Schematron source files in the format folder into runnable
        /// stylesheets, then removes the source files so that the same rules are not
        /// compiled and run twice.
        /// <para>
        /// A source file is only removed once its compiled form has been written
        /// successfully. If compilation fails the source is kept, so nothing is lost
        /// and the problem can be looked at.
        /// </para>
        /// </summary>
        private ValidationGroup CompileSchematronSources(ValidationFormat format)
        {
            List<string> sourceFiles = format.SchematronSourceFiles().ToList();
            if (sourceFiles.Count == 0)
            {
                return null;
            }

            ValidationGroup group = new ValidationGroup("Preparing the rules");
            group.Subtitle = sourceFiles.Count + " Schematron source file(s) found in this format";

            foreach (string sourceFile in sourceFiles)
            {
                string fileName = Path.GetFileName(sourceFile);
                ReportFileStarted("Compiling " + fileName);

                try
                {
                    string compiledFile = SchematronCompiler.Compile(sourceFile);

                    File.Delete(sourceFile);

                    group.Info("PREPARE-01", "'" + fileName + "' was compiled into '"
                        + Path.GetFileName(compiledFile) + "' and the source file was removed.");
                }
                catch (Exception exception)
                {
                    group.Error("PREPARE-02", "'" + fileName + "' could not be compiled, so its rules were not used: "
                        + exception.Message);
                }
            }

            return group;
        }

        private ValidationGroup RunSchematron(string schematronFile, string xmlDocumentPath)
        {
            string fileName = Path.GetFileName(schematronFile);
            ReportFileStarted(fileName);

            ValidationGroup group = new ValidationGroup(Path.GetFileNameWithoutExtension(schematronFile));
            group.Subtitle = "Schematron rules from " + fileName;

            string svrlReport;
            try
            {
                svrlReport = schematronValidator.Validate(schematronFile, xmlDocumentPath);
            }
            catch (Exception exception)
            {
                // A failure here is a problem with the validation file or with
                // reading the document, not a defect of the document itself, so it
                // is reported as such and the other files still run.
                group.Error("SCHEMATRON-01",
                    "This Schematron file could not be run: " + exception.Message);
                return group;
            }

            group.RawReport = svrlReport;

            List<ValidationMessage> messages = SchematronReportReader.Read(svrlReport);
            foreach (ValidationMessage message in messages)
            {
                group.Add(message);
            }

            if (messages.Count == 0)
            {
                group.Info("SCHEMATRON-00", "All rules in this file passed.");
            }

            return group;
        }

        private ValidationGroup RunSchemas(SchemaSet schemaSet, string xmlDocumentPath)
        {
            List<string> schemaFiles = schemaSet.Files;
            string title = schemaSet.FolderName == null ? "XSD schema" : "XSD schema - " + schemaSet.FolderName;

            ReportFileStarted(schemaSet.FolderName == null ? "XSD schemas" : "XSD schemas in " + schemaSet.FolderName);

            ValidationGroup group = new ValidationGroup(title);
            group.Subtitle = schemaFiles.Count == 1
                ? "Schema " + Path.GetFileName(schemaFiles[0])
                : schemaFiles.Count + " schema files used together";

            List<ValidationMessage> messages = xsdValidator.Validate(schemaFiles, xmlDocumentPath);
            foreach (ValidationMessage message in messages)
            {
                group.Add(message);
            }

            if (messages.Count == 0)
            {
                group.Info("XSD-00", "The document matches the schema.");
            }

            return group;
        }

        private void ReportFileStarted(string fileName)
        {
            Action<string> callback = FileStarted;
            if (callback != null)
            {
                callback(fileName);
            }
        }
    }
}
