using Saxon.Api;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Schema;

namespace eDocument_Validator
{
    public partial class Form1 : Form
    {
        private DirectoryInfo rootDir = Directory.GetParent(Environment.CurrentDirectory);
        private DirectoryInfo parentDir;
        public Form1()
        {
            InitializeComponent();
            parentDir = rootDir.Parent.Parent;
            ReadAndPopulateFormatSelection();
        }

        private void lookupButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select File";
            ofd.Filter = "XML files (*.xml)|*.xml";
            if (ofd.ShowDialog() == DialogResult.OK) 
            {
                if (ofd.FileName != "")
                {
                    pathBox.Text = ofd.FileName;
                }
                else
                {
                    pathBox.Text = "";
                }
            }
        }

        private void ReadAndPopulateFormatSelection()
        {
            string formatFoldersPath = parentDir.FullName + @"\schematron";
            string[] subdirectories = Directory.GetDirectories(formatFoldersPath)
                                                .Select(Path.GetFileName)
                                                .ToArray();
            foreach (string subdir in subdirectories)
            {
                formatBox.Items.Add(subdir);
            }
        }

        private void validateButton_Click(object sender, EventArgs e)
        {
            resultBox.Text = "";
            if (File.Exists(pathBox.Text))
            {
                string formatFolder = parentDir.FullName + @"\schematron" + @"\" + formatBox.Text;
                if (formatBox.Text != "")
                {
                    // Delete files from results
                    string resultFolder = parentDir.FullName + @"\results";
                    if (Directory.GetFiles(resultFolder).Length != 0)
                    {
                        foreach (string resultFile in Directory.GetFiles(resultFolder))
                        {
                            File.Delete(resultFile);
                        }
                    }

                    string[] files = Directory.GetFiles(formatFolder);

                    foreach (string file in files)
                    {
                        resultBox.AppendText("Validating with: " + Path.GetFileName(file) + Environment.NewLine);

                        List<string> xsdErrors = new List<string>();
                        List<string> failedAsserts = new List<string>();
                        List<string> errorElements = new List<string>();
                        string validationResult = null;

                        if ((Path.GetExtension(file) == ".xsl") || (Path.GetExtension(file) == ".xslt"))
                        {
                            validationResult = ValidateXmlWithSchematron(Path.GetFullPath(file), pathBox.Text);

                            failedAsserts = ExtractFailedAsserts(validationResult);
                            errorElements = ExtractErrorElements(validationResult);
                        }
                        else if (Path.GetExtension(file) == ".xsd")
                        {
                            validationResult = ValidateXmlWithXsdFile(Path.GetFullPath(file), pathBox.Text);

                            xsdErrors = ExtractXsdErrorElements(validationResult);
                        }
                        else
                        {
                            resultBox.AppendText("-" + Environment.NewLine);
                            continue;
                        }

                        // Write full validation result to file
                        File.WriteAllText(resultFolder + @"\" + Path.GetFileNameWithoutExtension(file) + ".txt", validationResult);


                        if ((failedAsserts.Count == 0) && (errorElements.Count == 0) && (xsdErrors.Count == 0))
                        {
                            resultBox.AppendText("Validation successfull" + Environment.NewLine + Environment.NewLine);
                            continue;
                        }

                        foreach (string assert in failedAsserts)
                        {
                            resultBox.AppendText(assert + Environment.NewLine + Environment.NewLine);
                        }
                        foreach (string error in errorElements)
                        {
                            resultBox.AppendText(error + Environment.NewLine + Environment.NewLine);
                        }
                        foreach (string xsdError in xsdErrors)
                        {
                            resultBox.AppendText(xsdError + Environment.NewLine + Environment.NewLine);
                        }

                        resultBox.AppendText(Environment.NewLine);
                    }

                }
                else
                {
                    MessageBox.Show("Select format to proceed");
                }
            }
            else
            {
                MessageBox.Show("File does not exist in the path. Please select path that contains XML file to validate");
            }
        }

        private static List<string> ExtractXsdErrorElements(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return new List<string>();

            return input
                .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrEmpty(line))
                .ToList();
        }

        private static string ValidateXmlWithXsdFile(string xsdPath, string xmlDocumentPath)
        {
            string validationErrors = "";
            XmlSchemaSet schemas = new XmlSchemaSet();
            schemas.Add(null, xsdPath);

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.ValidationType = ValidationType.Schema;
            settings.Schemas = schemas;
            settings.ValidationFlags |= XmlSchemaValidationFlags.ProcessInlineSchema;
            settings.ValidationFlags |= XmlSchemaValidationFlags.ProcessSchemaLocation;
            settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
            settings.ValidationEventHandler += (sender, e) =>
            {
                validationErrors += $"{e.Severity}: {e.Message}{Environment.NewLine}";
            };

            using (XmlReader reader = XmlReader.Create(xmlDocumentPath, settings))
            {
                while (reader.Read()) { }
            }
            Console.WriteLine($"XML validation completed: {validationErrors}");
            return validationErrors;
        }

        /// <summary>
        /// Validates an XML document using a compiled Schematron XSLT
        /// </summary>
        private static string ValidateXmlWithSchematron(string schematronXsltPath, string xmlDocumentPath)
        {
            Console.WriteLine("Validating XML document...");

            Processor processor = new Processor();

            // Load the compiled Schematron schema
            XsltCompiler compiler = processor.NewXsltCompiler();
            XsltExecutable schematronExecutable = compiler.Compile(new Uri(schematronXsltPath));

            // Load the XML document to validate
            XdmNode inputDocument = processor.NewDocumentBuilder().Build(new Uri(xmlDocumentPath));

            // Create a transformer for Schematron validation
            XsltTransformer transformer = schematronExecutable.Load();
            transformer.InitialContextNode = inputDocument;

            // Run the validation
            string validationResult = TransformToString(processor, transformer);

            return validationResult;
        }

        /// <summary>
        /// Helper method to transform XSLT to a string
        /// </summary>
        private static string TransformToString(Processor processor, XsltTransformer transformer)
        {
            Serializer serializer = processor.NewSerializer();
            using (MemoryStream results = new MemoryStream())
            {
                serializer.SetOutputStream(results);
                transformer.Run(serializer);

                results.Position = 0;
                using (StreamReader reader = new StreamReader(results))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// Returns a list of all <svrl:failed-assert ...>...</svrl:failed-assert> elements found in validationResult
        /// </summary>
        private static List<string> ExtractFailedAsserts(string validationResult)
        {
            var matches = Regex.Matches(
                validationResult,
                @"<svrl:failed-assert\b[^>]*>.*?</svrl:failed-assert>",
                RegexOptions.Singleline);

            return matches.Cast<Match>().Select(m => m.Value).ToList();
        }

        /// <summary>
        /// Returns a list of all <Error ...>...</Error> elements found in the input string
        /// </summary>
        private static List<string> ExtractErrorElements(string input)
        {
            var matches = Regex.Matches(
                input,
                @"<Error\b[^>]*>.*?</Error>",
                RegexOptions.Singleline);

            return matches.Cast<Match>().Select(m => m.Value).ToList();
        }
    }
}
