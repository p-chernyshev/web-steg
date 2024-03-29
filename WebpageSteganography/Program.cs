using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WebpageSteganography
{
    struct HtmlAttribute
    {
        // TODO Move Attribute parsing logic here
        public bool HasValue => Value != null;
        public string RawString;
        public readonly string Key;
        public string Value; // TODO setter
        public char Quotemark;
        public string Equals;
        public HtmlAttribute(string rawString, string key)
        {
            RawString = rawString;
            Key = key;
            Value = null;
            Quotemark = RawString.Last();
            Equals = null;
        }
        public HtmlAttribute(string rawValue, string key, string value = null)
        {
            RawString = rawValue;
            Key = key;
            Value = value;
            Quotemark = RawString.Last();
            Equals = RawString
                .Substring(Key.Length)
                .Replace($"{Quotemark}{Value}{Quotemark}", "");
        }

        public override string ToString()
        {
            if (HasValue)
            {
                return $"{Key}{Equals}{Quotemark}{Value}{Quotemark}";
            } else
            {
                return Key;
            }
        }
    }

    interface GenericStegContainer
    {
        void AddMessage<T>(Message messageBits, StegMethod<T> method);
        void GetMessage<T>(Message messageBits, StegMethod<T> method);
    }

    interface StegContainer<T>
    {
        void AddMessage(Message messageBits, StegMethod<T> method);
        void GetMessage(Message messageBits, StegMethod<T> method);
    }

    interface StegMethod<T>
    {
        T AddMessage(Message messageBits, T containerValue);
        void GetMessage(Message messageBits, T containerValue);
    }

    interface IReorderable
    {
        string[] Keys { get; }
        T[] Reorder<T>(T[] content, int[] newIndexes)
        {
            T[] reorderedContent = new T[content.Length];

            int length = Math.Min(content.Length, newIndexes.Length);
            for (int i = 0; i < length; i++)
            {
                int newIndex = newIndexes[i];
                reorderedContent[newIndex] = content[i];
            }
            for (int i = length; i < content.Length; i++)
            {
                reorderedContent[i] = content[i];
            }

            return reorderedContent;
        }

        void Reorder(int[] newIndexes);
    }

    class Program
    {
        static void Main(string[] args)
        {
            //var method = new TrailingSpacesMethod();
            //var method = new HtmlElementIdMethod();
            //var method = new DoubleSpacesMethod();
            //var method = new QuotemarkMethod();
            //var method = new EqualsSpaceMethod();
            //var method = new ColonSpaceMethod();
            var method = new SortingMethod();

            string messageString = "Pellentesque aliquet ante et enim lacinia malesuada.";
            Message message = new Message(messageString);
            var initialMessageLength = message.Length;
            Console.WriteLine($"Кодируемое сообщение:\n{message}");

            //Document document = new Html("../../../jisho.html");
            Document document = new Css("../../../jisho.css");
            document.AddMessage(message, method);
            //document.WriteToFile("../../../jisho.html");
            document.WriteToFile("../../../jisho.css");

            //Document document2 = new Html("../../../jisho.html");
            Document document2 = new Css("../../../jisho.css");
            Message decodedMessage = new Message();
            document2.GetMessage(decodedMessage, method);
            document.GetMessage(decodedMessage, method);
            Console.WriteLine($"\nДекодированное сообщение:\n{decodedMessage}");

            Console.WriteLine($"Длина кодируемого сообщения: {initialMessageLength} бит\nДлина закодированной части: {initialMessageLength - message.Length} бит ({(initialMessageLength - message.Length)/8} Байт)");
        }
    }

    class Message
    {
        public int Length => BitArray.Length;
        BitArray BitArray;

        public Message()
        {
            BitArray = new BitArray(0, false);
        }
        public Message(string str)
        {
            var lengthBytes = BitConverter.GetBytes(str.Length);
            var characterBytes = str
                .Select(character => Convert.ToByte(character)) // TODO .SelectMultiple(character => BitConverter.GetBytes(character)) for non-ASCII chars??
                .ToArray();
            BitArray = new BitArray(lengthBytes.Concat(characterBytes).ToArray());
        }

        public bool IsCompleteString()
        {
            if (Length < 32) return false;
            if (CompleteStringLength() * 8 > Length - 32) return false;
            return true;
        }

        public override string ToString()
        {
            if (Length < 32) return null;

            byte[] bytes = ToBytes();

            int length = Math.Min(CompleteStringLength(), ArrayLengthBytes() - 4);

            byte[] characterBytes = new byte[length];
            Array.Copy(bytes, 4, characterBytes, 0, length);

            char[] characters = characterBytes
                .Select(characterByte => Convert.ToChar(characterByte))
                .ToArray();

            return new string(characters);
        }

        byte[] ToBytes()
        {
            byte[] bytes = new byte[ArrayLengthBytes()];
            BitArray.CopyTo(bytes, 0);

            return bytes;
        }

        int ArrayLengthBytes()
        {
            int arrayLengthBytes = Length / 8;
            if (Length % 8 != 0) arrayLengthBytes++;
            return arrayLengthBytes;
        }

        int CompleteStringLength()
        {
            byte[] bytes = ToBytes();
            byte[] lengthBytes = new byte[4];
            Array.Copy(bytes, lengthBytes, 4);
            return BitConverter.ToInt32(lengthBytes);
        }

        public bool GetBit()
        {
            // TODO Randomize
            if (Length == 0) return false;

            bool bit = BitArray[0];
            BitArray.RightShift(1);
            BitArray.Length--;

            return bit;
        }
        public bool[] GetBits(int count)
        {
            bool[] bits = new bool[count];

            if (count > BitArray.Length) count = BitArray.Length;
            for (int i = 0; i < count; i++)
            {
                bits[i] = BitArray[i];
            }
            for (int i = BitArray.Length; i < bits.Length; i++)
            {
                // TODO Randomize
                bits[i] = false;
            }

            BitArray.RightShift(count);
            BitArray.Length -= count;

            return bits;
        }

        public void AddBit(bool bit) {
            BitArray.Length++;
            BitArray[BitArray.Length - 1] = bit;
        }
        public void AddBits(bool[] bits)
        {
            int oldLength = BitArray.Length;
            BitArray.Length += bits.Length;
            for (int i = 0; i < bits.Length; i++)
            {
                BitArray[oldLength + i] = bits[i];
            }
        }

        public byte[] GetBytes(int count)
        {
            int bytesTaken = Math.Min(count, ArrayLengthBytes());

            var bytes = ToBytes()
                // TODO Randomize
                .Concat(new byte[count - bytesTaken]);

            int bitsTaken = Math.Min(bytesTaken * 8, BitArray.Length);
            BitArray.RightShift(bitsTaken);
            BitArray.Length -= bitsTaken;

            return bytes
                .Take(count)
                .ToArray();
        }
        public void AddBytes(byte[] bytes)
        {
            BitArray newBits = new BitArray(bytes);
            int oldLength = BitArray.Length;
            BitArray.Length += newBits.Length;
            for (int i = 0; i < newBits.Length; i++)
            {
                BitArray[oldLength + i] = newBits[i];
            }
        }

        public ushort GetUshort() => BitConverter.ToUInt16(GetBytes(2));
        public void AddUshort(ushort value) => AddBytes(BitConverter.GetBytes(value));
    }

    #region DocumentParts

    interface DocumentPart
    {
        string SortKey { get; }
        string[] GenerateLines();
    }

    class DocumentBlock : DocumentPart, GenericStegContainer
    {
        public string SortKey => Parts[0].SortKey;
        public DocumentPart[] Parts;
        public int Length => Parts.Aggregate(0, (length, part) =>
        {
            if (part is DocumentBlock block)
            {
                return length + block.Length;
            }
            return length + 1;
        });

        public void AddMessage<T>(Message messageBits, StegMethod<T> method)
        {
            if (this is StegContainer<T> thisContainerOfType)
            {
                thisContainerOfType.AddMessage(messageBits, method);
            }
            foreach (DocumentPart part in Parts)
            {
                if (part is GenericStegContainer genericContainer)
                {
                    genericContainer.AddMessage(messageBits, method);
                }
                if (part is StegContainer<T> containerOfType)
                {
                    containerOfType.AddMessage(messageBits, method);
                }
            }
        }

        public void GetMessage<T>(Message messageBits, StegMethod<T> method)
        {
            if (this is StegContainer<T> thisContainerOfType)
            {
                thisContainerOfType.GetMessage(messageBits, method);
            }
            for (int i = 0; i < Parts.Length && !messageBits.IsCompleteString(); i++)
            {
                if (Parts[i] is GenericStegContainer genericContainer)
                {
                    genericContainer.GetMessage(messageBits, method);
                }
                if (Parts[i] is StegContainer<T> containerOfType)
                {
                    containerOfType.GetMessage(messageBits, method);
                }
            }
        }

        public string[] GenerateLines()
        {
            return Parts
                .SelectMany(part => part.GenerateLines())
                .ToArray();
        }

        protected void FilterEmptyLines()
        {
            foreach (DocumentPart part in Parts)
            {
                if (part is DocumentBlock block)
                {
                    block.FilterEmptyLines();
                }
            }
            Parts = Parts
                .Where(part =>
                {
                    if (part is DocumentLine line && line.IsEmpty) return false;
                    return true;
                })
                .ToArray();
        }
    }

    class DocumentLine : DocumentPart, StegContainer<string>
    {
        private static int Counter = 0;

        public string SortKey => $"{LineContent}{Number}";
        protected int Number;
        public string RawLine { get; protected set; }
        protected string LineContent; // TODO Remove?
        public bool IsEmpty => LineContent.Length == 0;
        static string CollapseWhitespace(string line)
        {
            string[] parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", parts);
        }

        private DocumentLine() { }
        public DocumentLine(string line)
        {
            Number = Counter++;
            RawLine = line;
            LineContent = CollapseWhitespace(line);
        }

        public void AddMessage(Message messageBits, StegMethod<string> method)
        {
            LineContent = method.AddMessage(messageBits, LineContent);
        }

        public void GetMessage(Message messageBits, StegMethod<string> method)
        {
            method.GetMessage(messageBits, RawLine);
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
    class CssRuleBlock : DocumentBlock, IReorderable, StegContainer<IReorderable>, StegContainer<CssPropertyLine>
    {
        public static bool CanParse(string line) => CssSelectorLine.CanParse(line);
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

        public void AddMessage(Message messageBits, StegMethod<IReorderable> method)
        {
            method.AddMessage(messageBits, this);
        }

        public void GetMessage(Message messageBits, StegMethod<IReorderable> method)
        {
            method.GetMessage(messageBits, this);
        }

        public string[] Keys => Parts.Where(part => part is CssPropertyLine).Select(line => line.SortKey).ToArray();//.Select(attribute => attribute.Key).ToArray();

        public void Reorder(int[] newIndexes)
        {
            int propertyIndex = Array.FindIndex(Parts, part => part is CssPropertyLine);
            var partsWithoutProperties = Parts.Where(part => !(part is CssPropertyLine)).ToArray();
            var properties = Parts.Where(part => part is CssPropertyLine).ToArray();
            properties = (this as IReorderable).Reorder(properties, newIndexes);

            var newParts = new DocumentPart[Parts.Length]; // unnecessary, can use Parts
            for (int i = 0; i < propertyIndex; i++)
            {
                newParts[i] = partsWithoutProperties[i];
            }
            for (int i = 0; i < properties.Length; i++)
            {
                newParts[propertyIndex + i] = properties[i];
            }
            for (int i = propertyIndex; i < partsWithoutProperties.Length; i++)
            {
                newParts[i + properties.Length] = partsWithoutProperties[i];
            }

            Parts = newParts;
        }

        public void AddMessage(Message messageBits, StegMethod<CssPropertyLine> method)
        {
            foreach(var part in Parts)
            {
                if (part is CssPropertyLine propertyLine)
                {
                    method.AddMessage(messageBits, propertyLine);
                }
            }
        }

        public void GetMessage(Message messageBits, StegMethod<CssPropertyLine> method)
        {
            foreach (var part in Parts)
            {
                if (part is CssPropertyLine propertyLine)
                {
                    method.GetMessage(messageBits, propertyLine);
                }
            }
        }
    }

    class CssSelectorLine : DocumentLine
    {
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
            LineContent = line + ";";
        }

        public void AddRemoveSpace(bool shouldAdd)
        {
            LineContent = $"{Key}:{(shouldAdd ? " " : "")}{Value};";
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

    class HtmlOpeningTagLine : DocumentLine, IReorderable, StegContainer<IReorderable>, StegContainer<HtmlAttribute>
    {
        public readonly bool isEmptyElement;
        public readonly string Name;
        private HtmlAttribute[] attributes;
        protected HtmlAttribute[] Attributes
        {
            get => attributes;
            set
            {
                attributes = value;

                var nameAndAttributes = attributes
                    .Select(attribute => attribute.ToString())
                    .Prepend(Name);
                string emptyElementSlash = isEmptyElement ? "/" : string.Empty;
                LineContent = $"<{string.Join(" ", nameAndAttributes)}{emptyElementSlash}>";
            }
        }
        public static bool CanParse(string line)
        {
            line = line.Trim();
            return line[0] == '<' && line[1] != '/';
        }
        public HtmlOpeningTagLine(string line) : base(line) {
            isEmptyElement = line[^2] == '/';
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
                    string value = line.Substring(openingQuoteIndex + 1, closingQuoteIndex - openingQuoteIndex - 1);

                    attributes.Add(new HtmlAttribute(rawKeyValue, key, value));

                    line = line
                        .Substring(rawKeyValue.Length)
                        .TrimStart();
                }
            }

            return attributes.ToArray();
        }

        public void AddMessage(Message messageBits, StegMethod<IReorderable> method)
        {
            method.AddMessage(messageBits, this);
        }

        public void GetMessage(Message messageBits, StegMethod<IReorderable> method)
        {
            method.GetMessage(messageBits, this);
        }

        public string[] Keys => Attributes.Select(attribute => attribute.Key).ToArray();

        public void Reorder(int[] newIndexes)
        {
            Attributes = (this as IReorderable).Reorder(Attributes, newIndexes);
        }

        public void AddMessage(Message messageBits, StegMethod<HtmlAttribute> method)
        {
            Attributes = Attributes
                .Select(attribute => method.AddMessage(messageBits, attribute))
                .ToArray();
        }

        public void GetMessage(Message messageBits, StegMethod<HtmlAttribute> method)
        {
            foreach(HtmlAttribute attribute in Attributes)
            {
                method.GetMessage(messageBits, attribute);
            }
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

    class HtmlBodyOpeningTagLine : HtmlOpeningTagLine, StegContainer<HtmlAttribute[]>
    {
        public HtmlBodyOpeningTagLine(string line) : base(line) { }

        public void AddMessage(Message messageBits, StegMethod<HtmlAttribute[]> method)
        {
            Attributes = method.AddMessage(messageBits, Attributes);
        }

        public void GetMessage(Message messageBits, StegMethod<HtmlAttribute[]> method)
        {
            method.GetMessage(messageBits, Attributes);
        }
    }

    // TODO class HtmlComment

    #endregion

    #region Documents

    abstract class Document : DocumentBlock
    {
        public Document(string fileName)
        {
            if (File.Exists(fileName))
            {
                string[] lines = File.ReadAllLines(fileName, Encoding.UTF8);
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
                    HtmlOpeningTagLine openingTag;
                    if (bodyStartIndex != 0 && bodyEndIndex == 0)
                    {
                        openingTag = new HtmlBodyOpeningTagLine(line);
                    }
                    else
                    {
                        openingTag = new HtmlOpeningTagLine(line);
                    }
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

    class EqualsSpaceMethod : StegMethod<HtmlAttribute>
    {
        public HtmlAttribute AddMessage(Message messageBits, HtmlAttribute containerValue)
        {
            if (containerValue.HasValue)
            {
                bool[] bits = messageBits.GetBits(2);
                string equals = containerValue.Equals.Trim();

                if (bits[0]) equals = " " + equals;
                if (bits[1]) equals = equals + " ";

                containerValue.Equals = equals;
            }
            return containerValue;
        }

        public void GetMessage(Message messageBits, HtmlAttribute containerValue)
        {
            if (containerValue.HasValue)
            {
                bool[] bits = new bool[2];
                string equals = containerValue.Equals;
                bits[0] = equals[0] == ' ';
                bits[1] = equals[^1] == ' ';
                messageBits.AddBits(bits);
            }
        }
    }

    class QuotemarkMethod : StegMethod<HtmlAttribute>
    {
        public HtmlAttribute AddMessage(Message messageBits, HtmlAttribute containerValue)
        {
            containerValue.Quotemark = messageBits.GetBit() ?
                '\'' :
                '"';
            return containerValue;
        }

        public void GetMessage(Message messageBits, HtmlAttribute containerValue)
        {
            messageBits.AddBit(containerValue.Quotemark == '\'');
        }
    }

    class DoubleSpacesMethod : StegMethod<string>
    {
        public string AddMessage(Message messageBits, string containerValue)
        {
            int[] spaceIndexes = containerValue
                .Select((character, index) => new {c = character, index = index})
                .Where(charIndex => charIndex.c == ' ')
                .Select(charIndex => charIndex.index)
                .ToArray();

            bool[] bits = messageBits.GetBits(spaceIndexes.Length);

            for (int i = spaceIndexes.Length - 1; i >= 0; i--)
            {
                if (bits[i])
                {
                    containerValue = containerValue.Insert(spaceIndexes[i], " ");
                }
            }

            return containerValue;
        }

        public void GetMessage(Message messageBits, string containerValue)
        {
            List<bool> bits = new List<bool>();

            for (int i = containerValue.IndexOf(' '); i != -1; i = containerValue.IndexOf(' ', i + 1))
            {
                if (containerValue.Length > i + 1 && containerValue[i + 1] == ' ')
                {
                    bits.Add(true);
                    i++;
                }
                else
                {
                    bits.Add(false);
                }
            }

            messageBits.AddBits(bits.ToArray());
        }
    }

    class TrailingSpacesMethod : StegMethod<string>
    {
        public string AddMessage(Message messageBits, string containerValue)
        {
            if (messageBits.GetBit())
            {
                containerValue += " ";
            }
            return containerValue;
        }

        public void GetMessage(Message messageBits, string containerValue)
        {
            bool bit = containerValue.Length != 0 && containerValue.Last() == ' ';
            messageBits.AddBit(bit);
        }
    }

    class HtmlElementIdMethod : StegMethod<HtmlAttribute[]>
    {
        readonly string Separator = "__";
        public HtmlAttribute[] AddMessage(Message messageBits, HtmlAttribute[] attributes)
        {
            int idIndex = Array.FindIndex(attributes, attribute => attribute.Key.ToLower() == "id");
            if (idIndex != -1)
            {
                attributes[idIndex].Value += $"{Separator}{messageBits.GetUshort()}";
            }
            else
            {
                string id = $"id{Separator}{messageBits.GetUshort()}";
                attributes = attributes
                    .Prepend(new HtmlAttribute($"id=\"{id}\"", "id", id))
                    .ToArray();
            }
            return attributes;
        }

        public void GetMessage(Message messageBits, HtmlAttribute[] attributes)
        {
            HtmlAttribute idAttribute = Array.Find(attributes, attribute => attribute.Key.ToLower() == "id");
            if (idAttribute.HasValue)
            {
                var separatorPosition = idAttribute.Value.LastIndexOf(Separator);
                string messageString = idAttribute.Value.Substring(separatorPosition + Separator.Length);
                if (separatorPosition != -1 && ushort.TryParse(messageString, out ushort messageUshort))
                {
                    messageBits.AddUshort(messageUshort);
                }
            }
        }
    }

    class SortingMethod : StegMethod<IReorderable>
    {
        public IReorderable AddMessage(Message messageBits, IReorderable containerValue)
        {
            var indexedKeys = containerValue.Keys
                .Select((key, index) => new { Key = key, Index = index })
                .OrderBy(key => key.Key)
                .ToArray();
            if (indexedKeys.Length < 2) return containerValue;

            bool[] bits = messageBits
                .GetBits(indexedKeys.Length - 1)
                .Prepend(true)
                .ToArray();

            var keysToDisplace = indexedKeys
                .Where((_, index) => bits[index])
                .ToArray();

            if (keysToDisplace.Length != 0)
            {
                var displacedKeys = keysToDisplace
                    .Skip(1)
                    .Append(keysToDisplace.First())
                    .ToArray();

                for (int i = 0, indexOfSorted = Array.FindIndex(bits, bit => bit); i < displacedKeys.Length; i++)
                {
                    indexedKeys[indexOfSorted] = displacedKeys[i];
                    indexOfSorted = Array.FindIndex(bits, indexOfSorted + 1, bit => bit);
                }
            }

            int[] newIndexes = new int[indexedKeys.Length];

            for (int i = 0; i < indexedKeys.Length; i++)
            {
                int oldIndex = indexedKeys[i].Index;
                newIndexes[oldIndex] = i;
            }

            containerValue.Reorder(newIndexes);
            return containerValue;
        }

        public void GetMessage(Message messageBits, IReorderable containerValue)
        {
            string[] keys = containerValue.Keys;
            if (keys.Length < 2) return;

            string[] keysSorted = (string[])keys.Clone();
            Array.Sort(keysSorted);

            bool[] bits = new bool[keys.Length - 1];
            for (int i = 1; i < keys.Length; i++)
            {
                bits[i - 1] = keys[i] != keysSorted[i];
            }

            messageBits.AddBits(bits);
        }
    }

    class ColonSpaceMethod : StegMethod<CssPropertyLine>
    {
        public CssPropertyLine AddMessage(Message messageBits, CssPropertyLine containerValue)
        {
            containerValue.AddRemoveSpace(messageBits.GetBit());
            return containerValue;
        }

        public void GetMessage(Message messageBits, CssPropertyLine containerValue)
        {
            string line = containerValue.RawLine;
            int colonPosition = line.IndexOf(':');
            messageBits.AddBit(line[colonPosition + 1] == ' ');
        }
    }

    #endregion
}
