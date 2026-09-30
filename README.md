# eDocsXMLValidator
eDocuments XML Validator

# Functionalities:
* Schematron validation (.xsl and .xslt files)
* Schematron source files (.sch) are compiled automatically before use
* XSD schema validation
* Hybrid PDF/A invoice validation (Factur-X, ZUGFeRD, Order-X): PDF structure, XMP metadata, embedded XML, and the relationships between them
* Result window with colours, so errors, warnings and passed checks are easy to tell apart

# How to use
1. Run the app (.\eDocument Validator\bin\Debug\eDocument Validator.exe)
2. Select electronic format which you want to use (Formats are taken from .\schematron. You can add more folders and you will be able to select those folders as electronic formats. You can also replace validation files in folders with newer versions or your own ones before validating)
3. Select electronic document file (.xml or .pdf) and click on Validate
4. Full validation result log file can be found in (./results)

## Reading the result

Every finding carries a word as well as a colour, so it stays readable when
printed or when colours are hard to tell apart:

| Word | Meaning |
|---|---|
| `ERROR` | A rule is broken. The receiver is expected to reject the document. |
| `WARNING` | The document deviates from a recommendation but can still be processed. |
| `OK` | The check passed. |

Warnings do not make a document invalid. A document with warnings but no errors
is reported as valid.

Two boxes control how much is shown. **Show warnings** hides warnings when
unticked, leaving only errors. **Show all checks** also lists everything that
passed, which is useful as evidence that a document really was checked.

**Dark colours** switches the window between the dark and light appearance. The
choice, and the two filter boxes, are remembered for the next start.

## XSD schema bundles

Schemas usually come as a bundle: one root schema plus many files it imports.
Put the `.xsd` files straight in the format folder, or put the whole bundle in a
folder of its own inside the format folder (for example
`schematron\France EXTENDED-CTC CII\1xsd-CII_D22B_uncoupled`). Folders inside
that folder are searched as well.

The loose `.xsd` files in the format folder are checked together as one set, and
each subfolder is checked as a separate set with its own result section, so two
bundles that define the same namespaces do not clash.

## Schematron source files (.sch)

Some formats are published as `.sch` files, which are the rules as their author
wrote them and cannot be run directly. When a `.sch` file is found in a format
folder it is compiled into an `.xslt` file next to it, and the `.sch` file is
then removed so the same rules are not run twice. Compilation uses the ISO
Schematron stylesheets, which are carried inside the program, so nothing has to
be installed.

If a `.sch` file cannot be compiled it is left alone and the reason is reported.

Note that the original `.sch` file is deleted once it has been compiled. Keep a
copy elsewhere if you need the source form.

## Speed

The Schematron files of a format are compiled the first time they are used and
then kept in memory. The first validation after starting the program takes a
couple of seconds; every validation after that takes a fraction of a second.

## Validating hybrid PDF invoices

Pick `FACTURX` or `ZUGFeRD` as the format and select a `.pdf` file. The app then:

1. Reads the PDF container and checks the rules a hybrid invoice depends on:
   encryption, the XMP metadata stream, the output intent, the embedded-file name
   tree and the document-level `/AF` array.
2. Checks the embedded file specification: the reserved file name, `/F` and `/UF`,
   `/AFRelationship`, the `text/xml` subtype and the `/Params /ModDate` that
   PDF/A-3 requires.
3. Checks the XMP metadata: `pdfaid:part` and `pdfaid:conformance`, the Factur-X
   or ZUGFeRD namespace, the PDF/A extension schema that declares it, and the
   four `DocumentType` / `DocumentFileName` / `Version` / `ConformanceLevel`
   properties.
4. Cross-checks all three against each other: the file name in the XMP against
   the attachment actually present, the profile in the XMP against the
   `GuidelineSpecifiedDocumentContextParameter` inside the XML, and the document
   type against the invoice type code.
5. Extracts the embedded CII document into `.\results` and runs it through the
   same Schematron and XSD validation as a standalone XML file.

The full report is written to `.\results\pdf_hybrid_validation.txt`, and the
extracted XML is left alongside it.

Findings are graded. An **error** means the document breaks a rule a recipient
relies on; a **warning** means it deviates from what the specification prefers
but is still processable (for example `/AFRelationship /Alternative`, which older
ZUGFeRD revisions used).

### Optional: full PDF/A conformance (veraPDF)

The checks above are structural. Full PDF/A conformance - embedded fonts, colour
spaces, transparency - is a much larger rule set, and veraPDF is the
implementation the industry treats as authoritative. It runs as a separate pass
and its findings are merged into the report under their own section.

This pass is **disabled** by default (`enableVeraPdf` in
`eDocument Validator\App.config`). On the invoices seen so far it reported
nothing the structural checks had not already found, and it adds roughly two
seconds per document. Turn it on when a file is suspected of a PDF/A defect that
is invisible at the structural level - an incomplete CIDSet, a non-embedded font,
a device-dependent colour space:

1. Run `.\tools\setup-verapdf.ps1` if it has not been run on this machine. This
   installs veraPDF and a Java runtime under `.\tools` (about 200 MB, not
   committed to the repository).
2. Set `enableVeraPdf` to `true`.

If the pass is enabled but the tooling is missing, the report says plainly that
conformance was not verified rather than implying the file passed; nothing else
is affected.

Note that the two layers catch different things. A file can be perfectly valid
PDF/A yet be a broken Factur-X invoice - a wrong attachment name or an
unparseable XML encoding are invisible to veraPDF - which is why the structural
checks always run.

# How the code is arranged

| Folder | What is in it |
|---|---|
| `Forms` | The window, the colours, and drawing the result. No validation rules. |
| `Validation` | Running Schematron and XSD, reading their results, and the shared result model. |
| `Hybrid` | Everything specific to hybrid PDF invoices: the PDF, its XMP metadata and the cross-checks. |
| `Validation\IsoSchematron` | The ISO Schematron stylesheets used to compile `.sch` files (MIT licence). |

The result of every check, whatever produced it, is a `ValidationMessage` with a
severity, a rule name and a text. Groups of those become a `ValidationReport`,
which is what both the window and the result file are built from.

# Changelog
1. Added .xsd file validation
2. Added hybrid PDF/A (Factur-X / ZUGFeRD) validation with XMP metadata and
   PDF-to-XML consistency checks, plus optional veraPDF conformance checking
3. Split the code out of the single form file into Forms, Validation and Hybrid
   folders; Schematron results are now read properly instead of being shown as
   raw XML, so errors and warnings are told apart
4. Added a colour-coded result window with a dark appearance, filters, and
   progress while validating
5. Added automatic compilation of `.sch` files, caching of compiled Schematron
   files, and correct drawing on screens set to 125% or 150%
