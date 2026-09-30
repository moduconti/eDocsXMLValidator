using System.Collections.Generic;

namespace eDocument_Validator.Validation
{
    /// <summary>
    /// Schema files that are loaded together and checked as one, usually a root
    /// schema plus the files it imports. See <see cref="ValidationFormat.SchemaSets"/>.
    /// </summary>
    public class SchemaSet
    {
        public SchemaSet(string folderName, List<string> files)
        {
            FolderName = folderName;
            Files = files;
        }

        /// <summary>
        /// The subfolder the files came from, or null for the files placed
        /// directly in the format folder.
        /// </summary>
        public string FolderName { get; private set; }

        public List<string> Files { get; private set; }
    }
}
