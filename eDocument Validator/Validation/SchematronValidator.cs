using Saxon.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace eDocument_Validator.Validation
{
    /// <summary>
    /// Runs a compiled Schematron file (an .xsl or .xslt) against an XML document
    /// and returns the SVRL report it produces.
    /// <para>
    /// The Schematron files in this project are large - several of them are close
    /// to a megabyte - and compiling one takes far longer than running it. Compiled
    /// files are therefore kept in memory and reused, and several files are
    /// compiled side by side, which roughly halves the wait when a format has more
    /// than one.
    /// </para>
    /// </summary>
    public class SchematronValidator
    {
        private readonly object processorLock = new object();
        private readonly object cacheLock = new object();

        private readonly Dictionary<string, CompiledSchematron> compiledFiles =
            new Dictionary<string, CompiledSchematron>(StringComparer.OrdinalIgnoreCase);

        private Processor processor;

        /// <summary>
        /// A compiled Schematron file together with the write time of the file it
        /// came from, so an edited file is compiled again instead of being served
        /// from memory.
        /// </summary>
        private class CompiledSchematron
        {
            public XsltExecutable Executable;
            public DateTime FileWriteTime;
        }

        /// <summary>
        /// Creating the XSLT engine takes a noticeable moment, so it is done the
        /// first time it is needed rather than when the window opens.
        /// </summary>
        private Processor Engine
        {
            get
            {
                lock (processorLock)
                {
                    // A Processor may be shared by several threads. The compilers
                    // and transformers made from it may not, so a new one is made
                    // for every use below.
                    return processor ?? (processor = new Processor());
                }
            }
        }

        /// <summary>
        /// Compiles the given files ahead of time, side by side, so that a later
        /// validation does not have to wait for them. Failures are ignored here;
        /// they are reported properly when the file is actually used.
        /// </summary>
        public void Prepare(IEnumerable<string> schematronFilePaths)
        {
            List<string> files = schematronFilePaths.ToList();
            if (files.Count == 0)
            {
                return;
            }

            Parallel.ForEach(files, file =>
            {
                try
                {
                    Compile(file);
                }
                catch (Exception)
                {
                    // Nothing to do: the file will be compiled again when it is
                    // used, and the error will be reported in the result then.
                }
            });
        }

        /// <summary>
        /// Validates a document and returns the SVRL report as text.
        /// </summary>
        /// <exception cref="Exception">
        /// Thrown when the Schematron file cannot be compiled or the document
        /// cannot be read. The caller reports this as a problem with the run rather
        /// than as a defect of the document.
        /// </exception>
        public string Validate(string schematronFilePath, string xmlDocumentPath)
        {
            Processor engine = Engine;

            XsltExecutable schematron = Compile(schematronFilePath);

            XdmNode document = engine.NewDocumentBuilder().Build(new Uri(xmlDocumentPath));

            XsltTransformer transformer = schematron.Load();
            transformer.InitialContextNode = document;

            Serializer serializer = engine.NewSerializer();
            using (MemoryStream output = new MemoryStream())
            {
                serializer.SetOutputStream(output);
                transformer.Run(serializer);

                output.Position = 0;
                using (StreamReader reader = new StreamReader(output))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private XsltExecutable Compile(string schematronFilePath)
        {
            DateTime writeTime = File.GetLastWriteTimeUtc(schematronFilePath);

            lock (cacheLock)
            {
                CompiledSchematron cached;
                if (compiledFiles.TryGetValue(schematronFilePath, out cached) && cached.FileWriteTime == writeTime)
                {
                    return cached.Executable;
                }
            }

            // Compiling happens outside the lock so that several files can be
            // compiled at the same time. Two threads asking for the same file would
            // compile it twice, which wastes a little work but is still correct.
            XsltExecutable executable = Engine.NewXsltCompiler().Compile(new Uri(schematronFilePath));

            lock (cacheLock)
            {
                compiledFiles[schematronFilePath] = new CompiledSchematron
                {
                    Executable = executable,
                    FileWriteTime = writeTime
                };
            }

            return executable;
        }
    }
}
