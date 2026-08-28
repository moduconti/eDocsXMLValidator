using System;
using System.IO;

namespace eDocument_Validator
{
    /// <summary>
    /// Finds the folders the application works with.
    /// <para>
    /// The validation files and the results live next to the project rather than
    /// next to the program file, so the folder holding them has to be found at
    /// start-up. The search starts at the folder the program was started from and
    /// walks upwards until it finds a folder that contains a "schematron" folder.
    /// This keeps working whether the program runs from bin\Debug, from bin\Release
    /// or from a copy somewhere else, and it does not depend on the working
    /// directory, which the user can change by starting the program from a
    /// shortcut.
    /// </para>
    /// </summary>
    public class ApplicationFolders
    {
        private const string SchematronFolderName = "schematron";
        private const string ResultsFolderName = "results";
        private const string ToolsFolderName = "tools";

        private ApplicationFolders(string rootFolder)
        {
            RootFolder = rootFolder;
            SchematronFolder = Path.Combine(rootFolder, SchematronFolderName);
            ResultsFolder = Path.Combine(rootFolder, ResultsFolderName);
            ToolsFolder = Path.Combine(rootFolder, ToolsFolderName);
        }

        /// <summary>The folder that contains the schematron and results folders.</summary>
        public string RootFolder { get; private set; }

        /// <summary>The folder holding one sub-folder per document format.</summary>
        public string SchematronFolder { get; private set; }

        /// <summary>The folder where result files are written.</summary>
        public string ResultsFolder { get; private set; }

        /// <summary>The folder holding optional extra programs, such as veraPDF.</summary>
        public string ToolsFolder { get; private set; }

        /// <summary>
        /// Finds the folders. Throws when the schematron folder cannot be found,
        /// because the application cannot do anything useful without it.
        /// </summary>
        public static ApplicationFolders Find()
        {
            string startFolder = AppDomain.CurrentDomain.BaseDirectory;

            DirectoryInfo folder = new DirectoryInfo(startFolder);
            while (folder != null)
            {
                if (Directory.Exists(Path.Combine(folder.FullName, SchematronFolderName)))
                {
                    return new ApplicationFolders(folder.FullName);
                }

                folder = folder.Parent;
            }

            throw new DirectoryNotFoundException(
                "The '" + SchematronFolderName + "' folder was not found. It was searched for in '"
                + startFolder + "' and in every folder above it. "
                + "Please run the program from inside the project folder.");
        }

        /// <summary>
        /// Empties the results folder and creates it if it does not exist yet, so
        /// that what remains after a run belongs only to that run.
        /// </summary>
        public void ClearResults()
        {
            Directory.CreateDirectory(ResultsFolder);

            foreach (string file in Directory.GetFiles(ResultsFolder))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    // A result file left open in another program, or made read-only,
                    // cannot be deleted. Skipping it is better than stopping the
                    // whole validation; writing the new result will fail loudly if
                    // it really matters.
                }
            }
        }
    }
}
