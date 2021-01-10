using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebpageSteganography
{
    class Program
    {
        static void Main(string[] args)
        {
        }
    }

    interface DocumentPart
    {

    }

    class DocumentBlock : DocumentPart
    {
        protected DocumentLine[] Lines;
        public int Length => Lines.Length;
    }

    class DocumentLine : DocumentPart
    {
        protected string RawLine;
        protected string LineContent;
        bool IsEmpty => LineContent.Length == 0;
        static string CollapseWhitespace(string line)
        {
            string[] parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            //var nonEmptyParts = parts.Where(part => part.Length != 0);
            return string.Join(" ", parts);
        }

        private DocumentLine() { }
        public DocumentLine(string line)
        {
            RawLine = line;
            LineContent = CollapseWhitespace(line);
        }
    }

    class CssRuleBlock : DocumentBlock
    {
        //nocheck
        //no empty lines
        public CssRuleBlock(string[] lines)
        {
            DocumentLine[] selectors = lines
                .TakeWhile(CssSelectorLine.CanParse)
                .Select(line => new CssSelectorLine(line))
                .ToArray();
            DocumentLine[] properties = lines
                .Skip(selectors.Length)
                .TakeWhile(line => !CssClosingBraceLine.CanParse(line))
                .Select(line => new CssPropertyLine(line))
                .ToArray();

            string closingBraceLine = lines.Skip(selectors.Length + properties.Length).First();
            DocumentLine closingBrace = new CssClosingBraceLine(closingBraceLine);

            Lines = selectors.Concat(properties).Append(closingBrace).ToArray();
        }
    }

    class CssSelectorLine : DocumentLine
    {
        //bool Last;
        public static bool CanParse(string line)
        {
            line = line.Trim();
            return line.Last() == ',' || line.Last() == '{';
        }
        public CssSelectorLine(string line) : base(line) { }

    }
    class CssPropertyLine : DocumentLine
    {
        string Key;
        string Value;
        public CssPropertyLine(string line) : base(line) {
            line = LineContent.TrimEnd(';');
            int colonPosition = line.IndexOf(':');
            Key = line.Substring(0, colonPosition);
            Value = line.Substring(colonPosition + 1);
        }
    }
    class CssClosingBraceLine : DocumentLine
    {
        public static bool CanParse(string line)
        {
            return line.Trim() == "}";
        }
        public CssClosingBraceLine(string line) : base(line) { }
    }

    class HtmlBodyBlock : DocumentBlock
    {
        public HtmlBodyBlock(DocumentLine[] lines)
        {
            Lines = lines;
        }
    }

    class HtmlOpeningTagLine : DocumentLine
    {
        struct Attribute
        {
            public readonly string Key;
            public readonly string Value;
            public Attribute(string key)
            {
                Key = key;
                Value = null;
            }
            public Attribute(string key, string value)
            {
                Key = key;
                Value = value;
            }
        }

        public readonly string Name;
        Attribute[] Attributes;
        public static bool CanParse(string line)
        {
            line = line.Trim();
            return line[0] == '<' && line[1] != '/';
        }
        public HtmlOpeningTagLine(string line) : base(line) {
            line = LineContent.Trim(new[] { '<', '/', '>' });

            int spaceIndex = line.IndexOf(' ');
            if (spaceIndex == -1)
            {
                Name = line;
                line = "";
            } else
            {
                Name = line.Substring(0, spaceIndex);
                line = line.Substring(spaceIndex + 1);
            }

            Attributes = GetAttributes(line);
        }

        private Attribute[] GetAttributes(string line)
        {
            List<Attribute> attributes = new List<Attribute>();

            line = line.Trim();
            while (line.Length != 0)
            {
                string key = line.Split(new[] { '=', ' ' }).First();

                line = line.Substring(key.Length).Trim();
                if (line.Length == 0 || line[0] != '=')
                {
                    attributes.Add(new Attribute(key));
                } else
                {
                    line = line.TrimStart(new[] { '=', ' ' });

                    char quote = line[0];
                    int closingQuoteIndex = line.IndexOf(quote, 1);
                    string value = line.Substring(0, closingQuoteIndex + 1);

                    attributes.Add(new Attribute(key, value));
                    line = line.Substring(value.Length);

                    line = line.TrimStart();
                }
            }

            return attributes.ToArray();
        }
    }
    class HtmlClosingTagLine : DocumentLine
    {
        public readonly string Name;
        public static bool CanParse(string line)
        {
            line = line.Trim();
            return line[0] == '<' && line[1] == '/';
        }
        public HtmlClosingTagLine(string line) : base(line)
        {
            Name = LineContent.Trim(new[] { '<', '>', '/' });
        }
    }
    //class HtmlTextLine : DocumentLine
    //{
    //    public HtmlTextLine(string line) : base(line) { }
    //}

    abstract class Document
    {
        DocumentPart[] Parts;
        public Document(string fileName)
        {
            if (File.Exists(fileName))
            {
                string[] lines = File.ReadAllLines(fileName, Encoding.UTF8); // CSS??
                //lines = TrimWhitespace(lines);
                Parts = ParseLines(lines);
            }
            else
            {
                throw new FileNotFoundException($"File '{fileName}' not found");
            }
        }

        //string[] TrimWhitespace(string[] lines)
        //{
        //    return lines
        //        .Select(TrimWhitespace)
        //        .Where(line => line.Length != 0)
        //        .ToArray();
        //}

        abstract protected DocumentPart[] ParseLines(string[] lines);
    }

    class Html : Document
    {
        public Html(string fileName) : base(fileName) { }

        override protected DocumentPart[] ParseLines(string[] lines)
        {
            List<DocumentLine> documentLines = new List<DocumentLine>();

            int bodyStartIndex = 0, bodyEndIndex = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                DocumentLine documentLine;

                if (HtmlOpeningTagLine.CanParse(line))
                {
                    HtmlOpeningTagLine openingTag = new HtmlOpeningTagLine(line);
                    documentLine = openingTag;
                    if (openingTag.Name == "body") bodyStartIndex = i;
                } else if (HtmlClosingTagLine.CanParse(line))
                {
                    HtmlClosingTagLine closingTag = new HtmlClosingTagLine(line);
                    documentLine = closingTag;
                    if (closingTag.Name == "body") bodyEndIndex = i;
                } else
                {
                    documentLine = new DocumentLine(line);
                }

                documentLines.Add(documentLine);
            }

            HtmlBodyBlock body = new HtmlBodyBlock(documentLines.GetRange(bodyStartIndex, bodyEndIndex - bodyStartIndex + 1).ToArray());

            List<DocumentPart> parts = documentLines.OfType<DocumentPart>().ToList();

            parts.RemoveRange(bodyStartIndex, bodyEndIndex - bodyStartIndex + 1);
            parts.Insert(bodyStartIndex, body);

            return parts.ToArray();
        }
    }

    class Css : Document
    {
        public Css(string fileName) : base(fileName) { }
        override protected DocumentPart[] ParseLines(string[] lines)
        {
            List<DocumentPart> parts = new List<DocumentPart>();

            while (lines.Length != 0)
            {
                CssRuleBlock block = new CssRuleBlock(lines);
                parts.Add(block);
                lines = lines.Skip(block.Length).ToArray();
            }
            
            return parts.ToArray();
        }

    }
}
