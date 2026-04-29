using System.Collections.Generic;

namespace E_Book.Models
{
    public class ParsedReadingContent
    {
        /// <summary>
        /// Display title for the document.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Original file path.
        /// </summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>
        /// txt / html
        /// txt  -> TxtParagraphs is used
        /// html -> RawHtmlChapters / RawHtmlChapterKeys are used
        /// </summary>
        public string ContentKind { get; set; } = "html";

        /// <summary>
        /// For TXT reader pipeline.
        /// </summary>
        public List<string> TxtParagraphs { get; set; } = new();

        /// <summary>
        /// For EPUB / HTML / DOCX / RTF unified HTML reader pipeline.
        /// </summary>
        public List<string> RawHtmlChapters { get; set; } = new();

        /// <summary>
        /// Used for EPUB TOC href mapping or chapter identity.
        /// </summary>
        public List<string> RawHtmlChapterKeys { get; set; } = new();

        /// <summary>
        /// Optional metadata for future use.
        /// </summary>
        public string Author { get; set; } = string.Empty;
        public List<string> RawHtmlChapterTitles { get; set; } = new();
    }
}