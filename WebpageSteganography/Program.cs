using System;
using System.Collections;
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

    class BitStack
    {
        public int Length => bitArray.Length;
        BitArray bitArray;

        public BitStack(string str)
        {
            byte[] bytes = str
                .Select(character => Convert.ToByte(character))
                .ToArray();
            bitArray = new BitArray(bytes);
        }
        public bool Pop()
        {
            if (bitArray.Length == 0) throw new InvalidOperationException("Bit stack has no elements");
            bool bit = bitArray[0];
            bitArray.RightShift(1);
            bitArray.Length--;
            return bit;
        }
    }

    interface StegContainer<T>
    {
        void AddMessage(BitStack messageBits, StegMethod<T> method);
    }

    #region DocumentParts

    interface DocumentPart : StegContainer<string>
    {
        string[] GenerateLines();
    }

    class DocumentBlock : DocumentPart
    {
        public DocumentPart[] Parts;
        public int Length => Parts.Aggregate(0, (length, part) =>
        {
            if (part is DocumentBlock block)
            {
                return length + block.Length;
            }
            return length + 1;
        });

        public void AddMessage(BitStack messageBits, StegMethod<string> method) {
            foreach (DocumentPart part in Parts)
            {
                part.AddMessage(messageBits, method);
            }
        }

        public string[] GenerateLines()
        {
            return Parts
                .SelectMany(part => part.GenerateLines())
                .ToArray();
    }
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

        public void AddMessage(BitStack messageBits, StegMethod<string> method)
        {
            LineContent = method.AddMessage(messageBits, LineContent);
        }
        public string[] GenerateLines() => new string[] { LineContent };
    }

    class CssMediaBlock : DocumentBlock
    {
        public static bool CanParse(string line)
        {
            line = line.Trim();
            return line.Length != 0 && line[0] == '@';
        }
        public CssMediaBlock(string[] lines)
        {
            List<DocumentPart> parts = new List<DocumentPart>();

            parts.Add(new DocumentLine(lines[0]));
            lines = lines.Skip(1).ToArray();
            while (!CssClosingBraceLine.CanParse(lines[0]))
            {
                if (CssSelectorLine.CanParse(lines[0]))
                {
                    CssRuleBlock ruleBlock = new CssRuleBlock(lines);
                    parts.Add(ruleBlock);
                    lines = lines.Skip(ruleBlock.Length).ToArray();
                } else
                {
                    parts.Add(new DocumentLine(lines[0]));
                    lines = lines.Skip(1).ToArray();
                }
            }
            parts.Add(new DocumentLine(lines[0]));

            Parts = parts.ToArray();
        }

    }

    class CssRuleBlock : DocumentBlock
    {
        public static bool CanParse(string line) => CssSelectorLine.CanParse(line);
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

            Parts = selectors.Concat(properties).Append(closingBrace).ToArray();
        }
    }

    class CssSelectorLine : DocumentLine
    {
        //bool Last;
        public static bool CanParse(string line)
        {
            line = line.Trim();
            return line.Length != 0 && (line.Last() == ',' || line.Last() == '{');
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
            Parts = lines;
        }
    }

    struct HtmlAttribute
    {
        public readonly string RawString;
        public readonly string Key;
        public readonly string Value;
        public HtmlAttribute(string rawString, string key)
        {
            RawString = rawString;
            Key = key;
            Value = null;
        }
        public HtmlAttribute(string rawValue, string key, string value)
        {
            RawString = rawValue;
            Key = key;
            Value = value;
        }
    }
    class HtmlOpeningTagLine : DocumentLine
    {
        public readonly string Name;
        HtmlAttribute[] Attributes;
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

        private HtmlAttribute[] GetAttributes(string line)
        {
            List<HtmlAttribute> attributes = new List<HtmlAttribute>();

            line = line.Trim();
            while (line.Length != 0)
            {
                string key = line.Split(new[] { '=', ' ' }).First();

                string lineWithoutKey = line
                        .Substring(key.Length)
                        .TrimStart();
                if (lineWithoutKey.Length == 0 || lineWithoutKey[0] != '=')
                {
                    attributes.Add(new HtmlAttribute(key, key));
                    line = lineWithoutKey;
                } else
                {
                    char quote = lineWithoutKey.TrimStart(new[] { '=', ' ' })[0];

                    int openingQuoteIndex = line.IndexOf(quote);
                    int closingQuoteIndex = line.IndexOf(quote, openingQuoteIndex + 1);
                    string rawKeyValue = line.Substring(0, closingQuoteIndex + 1);
                    string value = line.Substring(openingQuoteIndex, closingQuoteIndex - openingQuoteIndex + 1);

                    attributes.Add(new HtmlAttribute(rawKeyValue, key, value));

                    line = line
                        .Substring(rawKeyValue.Length)
                        .TrimStart();
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

    #endregion

    #region Documents

    abstract class Document : DocumentBlock, StegContainer<string>
    {
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

        public void WriteToFile(string fileName)
        {
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            File.WriteAllLines(fileName, GenerateLines());
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
                if (CssMediaBlock.CanParse(lines[0]))
                {
                    CssMediaBlock mediaBlock = new CssMediaBlock(lines);
                    parts.Add(mediaBlock);
                    lines = lines.Skip(mediaBlock.Length).ToArray();
                } else if (CssSelectorLine.CanParse(lines[0]))
                {
                    CssRuleBlock ruleBlock = new CssRuleBlock(lines);
                    parts.Add(ruleBlock);
                    lines = lines.Skip(ruleBlock.Length).ToArray();
                } else
                {
                    parts.Add(new DocumentLine(lines[0]));
                    lines = lines.Skip(1).ToArray();
                }
            }

            return parts.ToArray();
        }

    }

    #endregion

    #region Methods

    interface StegMethod<T>
    {
        T AddMessage(BitStack messageBits, T containerValue);
        // GetMessage
    }

    class TrailingSpacesMethod : StegMethod<string>
    {
        public string AddMessage(BitStack messageBits, string containerValue)
        {
            if (messageBits.Pop())
            {
                containerValue += " ";
            }
            return containerValue;
        }
    }

    #endregion
}
