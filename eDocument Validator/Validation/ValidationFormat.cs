using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace eDocument_Validator.Validation
{
    /// <summary>
    /// One electronic document format the application can validate against, which
    /// is simply one folder inside the schematron folder. The user adds a format by
    /// creating a folder and putting validation files in it, so nothing here is
    /// hard-coded.
    /// </summary>
    public class ValidationFormat
    {
        public ValidationFormat(string folderPath)
        {
            FolderPath = folderPath;
            Name = Path.GetFileName(folderPath);
        }

        /// <summary>Name shown in the format list, which is the folder name.</summary>
        public string Name { get; private set; }

        public string FolderPath { get; private set; }

        /// <summary>Compiled Schematron files (.xsl and .xslt), each run separately.</summary>
        public IEnumerable<string> SchematronFiles()
        {
            return FilesWithExtension(".xsl").Concat(FilesWithExtension(".xslt")).OrderBy(f => f);
        }

        /// <summary>
        /// Schematron source files (.sch). These cannot be run directly and are
        /// compiled into .xslt first; see <see cref="SchematronCompiler"/>.
        /// </summary>
        public IEnumerable<string> SchematronSourceFiles()
        {
            return FilesWithExtension(".sch").OrderBy(f => f);
        }

        /// <summary>Schema files (.xsd), which are always used together as one set.</summary>
        public IEnumerable<string> SchemaFiles()
        {
            return FilesWithExtension(".xsd").OrderBy(f => f);
        }

        /// <summary>
        /// Whether this format validates CII documents, which is the syntax every
        /// hybrid PDF invoice carries. Decided from the folder name, because the
        /// folders belong to the user and only their name says what is inside.
        /// </summary>
        public bool IsForCrossIndustryInvoice()
        {
            string[] namesThatMeanCii = { "CII", "FACTURX", "FACTUR-X", "ZUGFERD", "ORDER-X" };

            return namesThatMeanCii.Any(part =>
                Name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private IEnumerable<string> FilesWithExtension(string extension)
        {
            if (!Directory.Exists(FolderPath))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.GetFiles(FolderPath)
                .Where(file => string.Equals(Path.GetExtension(file), extension, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Reads every format folder, sorted by name so the list in the window is
        /// stable between runs.
        /// </summary>
        public static List<ValidationFormat> ReadAllFrom(string schematronFolderPath)
        {
            if (!Directory.Exists(schematronFolderPath))
            {
                return new List<ValidationFormat>();
            }

            return Directory.GetDirectories(schematronFolderPath)
                .Select(folder => new ValidationFormat(folder))
                .OrderBy(format => format.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
