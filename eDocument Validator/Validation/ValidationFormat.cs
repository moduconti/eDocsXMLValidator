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

        /// <summary>
        /// Schema files (.xsd), grouped into the sets they are used in.
        /// <para>
        /// Schemas are usually published as a bundle: one root schema plus many
        /// files it imports. A bundle can be put straight in the format folder or
        /// in a folder of its own inside it. The loose files in the format folder
        /// form one set, and each subfolder forms another set from every schema
        /// file anywhere below it. Bundles are kept apart because two of them often
        /// define the same namespaces, for example two versions of one standard,
        /// and loading both into one set would make them clash.
        /// </para>
        /// </summary>
        public List<SchemaSet> SchemaSets()
        {
            List<SchemaSet> sets = new List<SchemaSet>();
            if (!Directory.Exists(FolderPath))
            {
                return sets;
            }

            List<string> looseFiles = FilesWithExtension(FolderPath, ".xsd", SearchOption.TopDirectoryOnly);
            if (looseFiles.Count > 0)
            {
                sets.Add(new SchemaSet(null, looseFiles));
            }

            foreach (string subfolder in Directory.GetDirectories(FolderPath).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                List<string> bundleFiles = FilesWithExtension(subfolder, ".xsd", SearchOption.AllDirectories);
                if (bundleFiles.Count > 0)
                {
                    sets.Add(new SchemaSet(Path.GetFileName(subfolder), bundleFiles));
                }
            }

            return sets;
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

            return FilesWithExtension(FolderPath, extension, SearchOption.TopDirectoryOnly);
        }

        private static List<string> FilesWithExtension(string folderPath, string extension, SearchOption searchOption)
        {
            return Directory.GetFiles(folderPath, "*", searchOption)
                .Where(file => string.Equals(Path.GetExtension(file), extension, StringComparison.OrdinalIgnoreCase))
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToList();
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
