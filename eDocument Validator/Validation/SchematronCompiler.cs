using Saxon.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace eDocument_Validator.Validation
{
    /// <summary>
    /// Turns a Schematron source file (.sch) into a compiled stylesheet (.xslt)
    /// that can be run against a document.
    /// <para>
    /// A .sch file is the rules as their author wrote them. It cannot be run
    /// directly; it first has to be translated into XSLT. The official way to do
    /// that is a chain of three stylesheets published with the ISO Schematron
    /// standard, which are carried inside this program so that no extra files or
    /// settings are needed on the machine where it runs:
    /// </para>
    /// <list type="number">
    /// <item>include: copies in any rules the file refers to elsewhere;</item>
    /// <item>expand: replaces abstract patterns with real ones;</item>
    /// <item>compile: writes the XSLT that reports results as SVRL.</item>
    /// </list>
    /// </summary>
    public static class SchematronCompiler
    {
        /// <summary>The three stages, in the order they must run.</summary>
        private static readonly string[] StageResources =
        {
            "iso_dsdl_include.xsl",
            "iso_abstract_expand.xsl",
            "iso_svrl_for_xslt2.xsl"
        };

        /// <summary>
        /// Needed by the third stage but never run on its own, so it only has to be
        /// present next to the others.
        /// </summary>
        private const string SupportResource = "iso_schematron_skeleton_for_saxon.xsl";

        private const string ResourcePrefix = "eDocument_Validator.Validation.IsoSchematron.";

        private static readonly object PreparationLock = new object();
        private static string stylesheetFolder;

        /// <summary>The file a given Schematron source is compiled into.</summary>
        public static string CompiledFileFor(string schematronSourcePath)
        {
            string folder = Path.GetDirectoryName(schematronSourcePath) ?? string.Empty;
            return Path.Combine(folder, Path.GetFileNameWithoutExtension(schematronSourcePath) + ".xslt");
        }

        /// <summary>
        /// Compiles one .sch file and writes the result next to it. Returns the path
        /// of the file that was written.
        /// </summary>
        /// <exception cref="Exception">
        /// Thrown when the file is not valid Schematron or cannot be compiled.
        /// </exception>
        public static string Compile(string schematronSourcePath)
        {
            string stylesheets = PrepareStylesheets();

            Processor processor = new Processor();
            XdmNode document = processor.NewDocumentBuilder().Build(new Uri(schematronSourcePath));

            // Each stage takes the result of the one before it, so the whole chain
            // runs in memory and no half-finished files are left behind if a stage
            // fails.
            foreach (string stage in StageResources)
            {
                document = RunStage(processor, Path.Combine(stylesheets, stage), document);
            }

            string compiledPath = CompiledFileFor(schematronSourcePath);

            Serializer serializer = processor.NewSerializer();
            using (FileStream output = File.Create(compiledPath))
            {
                serializer.SetOutputStream(output);
                processor.WriteXdmValue(document, serializer);
            }

            return compiledPath;
        }

        private static XdmNode RunStage(Processor processor, string stylesheetPath, XdmNode input)
        {
            XsltCompiler compiler = processor.NewXsltCompiler();
            XsltExecutable executable = compiler.Compile(new Uri(stylesheetPath));

            XsltTransformer transformer = executable.Load();
            transformer.InitialContextNode = input;

            XdmDestination destination = new XdmDestination();
            transformer.Run(destination);

            return destination.XdmNode;
        }

        /// <summary>
        /// Writes the ISO stylesheets carried inside this program to a folder on
        /// disk, because the XSLT engine loads them by file name and one of them
        /// refers to another. This is done once per run of the program.
        /// </summary>
        private static string PrepareStylesheets()
        {
            lock (PreparationLock)
            {
                if (stylesheetFolder != null && Directory.Exists(stylesheetFolder))
                {
                    return stylesheetFolder;
                }

                string folder = Path.Combine(Path.GetTempPath(), "eDocumentValidator", "schematron-compiler");
                Directory.CreateDirectory(folder);

                List<string> allResources = new List<string>(StageResources) { SupportResource };

                foreach (string resource in allResources)
                {
                    WriteResource(resource, Path.Combine(folder, resource));
                }

                stylesheetFolder = folder;
                return folder;
            }
        }

        private static void WriteResource(string resourceName, string targetPath)
        {
            Assembly assembly = typeof(SchematronCompiler).Assembly;

            using (Stream source = assembly.GetManifestResourceStream(ResourcePrefix + resourceName))
            {
                if (source == null)
                {
                    throw new FileNotFoundException(
                        "The Schematron compiler stylesheet '" + resourceName + "' is missing from the program.");
                }

                // The file is only written when it is not already there in full.
                // The folder is shared by every copy of the program running on the
                // machine, so a second copy must not truncate a file the first one
                // is reading.
                if (File.Exists(targetPath) && new FileInfo(targetPath).Length == source.Length)
                {
                    return;
                }

                try
                {
                    using (FileStream target = File.Create(targetPath))
                    {
                        source.CopyTo(target);
                    }
                }
                catch (IOException)
                {
                    // Another copy of the program is writing the same file. Its copy
                    // is the same as ours, so as long as it ends up complete there
                    // is nothing to do.
                    if (!File.Exists(targetPath) || new FileInfo(targetPath).Length != source.Length)
                    {
                        throw;
                    }
                }
            }
        }
    }
}
