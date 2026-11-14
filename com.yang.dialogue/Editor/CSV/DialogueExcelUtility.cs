using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Yang.Dialogue.Editor
{
    // A single-sheet XLSX transport for the CSV model. Export text to preserve identifiers; accept numeric input.
    internal static class DialogueExcelUtility
    {
        private const string SheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string PackageNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
        private const string DocumentRelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private const string ContentNamespace = "http://schemas.openxmlformats.org/package/2006/content-types";
        private const int MaxRows = 1048576;
        private const int MaxColumns = 16384;

        public static void Save(string path, IReadOnlyList<IReadOnlyList<string>> rows)
        {
            if (rows.Count == 0 || rows.Count > MaxRows || rows[0].Count == 0 || rows[0].Count > MaxColumns)
                throw new FormatException("The Dialogue table exceeds Excel's worksheet limits or has no headers.");

            using (FileStream stream = File.Create(path))
            using (ZipArchive archive = new(stream, ZipArchiveMode.Create))
            {
                WritePart(archive, "[Content_Types].xml", WriteContentTypes);
                WritePart(archive, "_rels/.rels", WriteRootRelationships);
                WritePart(archive, "xl/workbook.xml", WriteWorkbook);
                WritePart(archive, "xl/_rels/workbook.xml.rels", WriteWorkbookRelationships);
                WritePart(archive, "xl/styles.xml", WriteStyles);
                WritePart(archive, "xl/worksheets/sheet1.xml", writer => WriteWorksheet(writer, rows));
            }
        }

        public static string Load(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (ZipArchive archive = new(stream, ZipArchiveMode.Read))
            {
                string sheetPath = FindWorksheetPath(archive);
                List<string> sharedStrings = LoadSharedStrings(archive);
                List<List<string>> rows = ReadWorksheet(archive, sheetPath, sharedStrings);

                if (rows.Count == 0) throw new FormatException("The Excel sheet is empty.");

                return CsvUtility.ToCsv(rows);
            }
        }

        private static void WritePart(ZipArchive archive, string path, Action<XmlWriter> write)
        {
            ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal);

            using (Stream stream = entry.Open())
            using (XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), NewLineHandling = NewLineHandling.Entitize, CloseOutput = false }))
            {
                writer.WriteStartDocument();
                write(writer);
                writer.WriteEndDocument();
            }
        }

        private static void WriteContentTypes(XmlWriter writer)
        {
            writer.WriteStartElement("Types", ContentNamespace);
            WriteContentType(writer, "rels", "application/vnd.openxmlformats-package.relationships+xml");
            WriteContentType(writer, "xml", "application/xml");
            WriteOverride(writer, "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
            WriteOverride(writer, "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
            WriteOverride(writer, "/xl/worksheets/sheet1.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
            writer.WriteEndElement();
        }

        private static void WriteContentType(XmlWriter writer, string extension, string contentType)
        {
            writer.WriteStartElement("Default", ContentNamespace);
            writer.WriteAttributeString("Extension", extension);
            writer.WriteAttributeString("ContentType", contentType);
            writer.WriteEndElement();
        }

        private static void WriteOverride(XmlWriter writer, string name, string contentType)
        {
            writer.WriteStartElement("Override", ContentNamespace);
            writer.WriteAttributeString("PartName", name);
            writer.WriteAttributeString("ContentType", contentType);
            writer.WriteEndElement();
        }

        private static void WriteRootRelationships(XmlWriter writer)
        {
            writer.WriteStartElement("Relationships", PackageNamespace);
            WriteRelationship(writer, "rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "xl/workbook.xml");
            writer.WriteEndElement();
        }

        private static void WriteWorkbookRelationships(XmlWriter writer)
        {
            writer.WriteStartElement("Relationships", PackageNamespace);
            WriteRelationship(writer, "rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", "worksheets/sheet1.xml");
            WriteRelationship(writer, "rId2", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml");
            writer.WriteEndElement();
        }

        private static void WriteRelationship(XmlWriter writer, string id, string type, string target)
        {
            writer.WriteStartElement("Relationship", PackageNamespace);
            writer.WriteAttributeString("Id", id);
            writer.WriteAttributeString("Type", type);
            writer.WriteAttributeString("Target", target);
            writer.WriteEndElement();
        }

        private static void WriteWorkbook(XmlWriter writer)
        {
            writer.WriteStartElement("workbook", SheetNamespace);
            writer.WriteAttributeString("xmlns", "r", null, DocumentRelationshipNamespace);
            writer.WriteStartElement("sheets", SheetNamespace);
            writer.WriteStartElement("sheet", SheetNamespace);
            writer.WriteAttributeString("name", "Dialogue");
            writer.WriteAttributeString("sheetId", "1");
            writer.WriteAttributeString("r", "id", DocumentRelationshipNamespace, "rId1");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteStyles(XmlWriter writer)
        {
            writer.WriteStartElement("styleSheet", SheetNamespace);
            writer.WriteStartElement("fonts", SheetNamespace);
            writer.WriteAttributeString("count", "2");
            writer.WriteStartElement("font", SheetNamespace);
            writer.WriteStartElement("sz", SheetNamespace);
            writer.WriteAttributeString("val", "11");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteStartElement("font", SheetNamespace);
            writer.WriteElementString("b", SheetNamespace, "");
            writer.WriteStartElement("color", SheetNamespace);
            writer.WriteAttributeString("rgb", "FFFFFFFF");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteStartElement("fills", SheetNamespace);
            writer.WriteAttributeString("count", "3");
            WriteFill(writer, "none", null);
            WriteFill(writer, "gray125", null);
            WriteFill(writer, "solid", "FF283D55");
            writer.WriteEndElement();
            writer.WriteStartElement("borders", SheetNamespace);
            writer.WriteAttributeString("count", "1");
            writer.WriteStartElement("border", SheetNamespace);
            writer.WriteElementString("left", SheetNamespace, "");
            writer.WriteElementString("right", SheetNamespace, "");
            writer.WriteElementString("top", SheetNamespace, "");
            writer.WriteElementString("bottom", SheetNamespace, "");
            writer.WriteElementString("diagonal", SheetNamespace, "");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteStartElement("cellStyleXfs", SheetNamespace);
            writer.WriteAttributeString("count", "1");
            WriteStyle(writer, "0", "0", "0", "0", false);
            writer.WriteEndElement();
            writer.WriteStartElement("cellXfs", SheetNamespace);
            writer.WriteAttributeString("count", "2");
            WriteStyle(writer, "0", "0", "0", "49", true);
            WriteStyle(writer, "1", "2", "0", "49", true);
            writer.WriteEndElement();
            writer.WriteStartElement("cellStyles", SheetNamespace);
            writer.WriteAttributeString("count", "1");
            writer.WriteStartElement("cellStyle", SheetNamespace);
            writer.WriteAttributeString("name", "Normal");
            writer.WriteAttributeString("xfId", "0");
            writer.WriteAttributeString("builtinId", "0");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteFill(XmlWriter writer, string pattern, string color)
        {
            writer.WriteStartElement("fill", SheetNamespace);
            writer.WriteStartElement("patternFill", SheetNamespace);
            writer.WriteAttributeString("patternType", pattern);

            if (color != null)
            {
                writer.WriteStartElement("fgColor", SheetNamespace);
                writer.WriteAttributeString("rgb", color);
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteStyle(XmlWriter writer, string font, string fill, string border, string format, bool text)
        {
            writer.WriteStartElement("xf", SheetNamespace);
            writer.WriteAttributeString("fontId", font);
            writer.WriteAttributeString("fillId", fill);
            writer.WriteAttributeString("borderId", border);
            writer.WriteAttributeString("numFmtId", format);
            writer.WriteAttributeString("xfId", "0");

            if (text) writer.WriteAttributeString("applyNumberFormat", "1");

            writer.WriteEndElement();
        }

        private static void WriteWorksheet(XmlWriter writer, IReadOnlyList<IReadOnlyList<string>> rows)
        {
            writer.WriteStartElement("worksheet", SheetNamespace);
            writer.WriteStartElement("sheetViews", SheetNamespace);
            writer.WriteStartElement("sheetView", SheetNamespace);
            writer.WriteAttributeString("workbookViewId", "0");
            writer.WriteStartElement("pane", SheetNamespace);
            writer.WriteAttributeString("ySplit", "1");
            writer.WriteAttributeString("topLeftCell", "A2");
            writer.WriteAttributeString("activePane", "bottomLeft");
            writer.WriteAttributeString("state", "frozen");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteStartElement("cols", SheetNamespace);
            int columns = rows.Count == 0 ? 0 : rows[0].Count;

            for (int c = 0; c < columns; c++)
            {
                writer.WriteStartElement("col", SheetNamespace);
                writer.WriteAttributeString("min", (c + 1).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("max", (c + 1).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("width", ColumnWidth(rows[0][c]).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("customWidth", "1");
                writer.WriteAttributeString("style", "0");
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteStartElement("sheetData", SheetNamespace);

            for (int r = 0; r < rows.Count; r++)
            {
                writer.WriteStartElement("row", SheetNamespace);
                writer.WriteAttributeString("r", (r + 1).ToString(CultureInfo.InvariantCulture));
                IReadOnlyList<string> row = rows[r];

                if (row.Count > MaxColumns) throw new FormatException("The Dialogue table exceeds Excel's column limit.");

                for (int c = 0; c < row.Count; c++)
                {
                    writer.WriteStartElement("c", SheetNamespace);
                    writer.WriteAttributeString("r", ColumnName(c) + (r + 1).ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("s", r == 0 ? "1" : "0");
                    writer.WriteAttributeString("t", "inlineStr");
                    writer.WriteStartElement("is", SheetNamespace);
                    writer.WriteStartElement("t", SheetNamespace);
                    writer.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
                    writer.WriteString(row[c] ?? "");
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static int ColumnWidth(string header)
        {
            if (header == "ID" || header == "Next") return 38;
            if (header == "X" || header == "Y" || header == "Type") return 15;
            if (header.EndsWith("Guid", StringComparison.Ordinal)) return 38;
            if (header.EndsWith("EntryId", StringComparison.Ordinal)) return 26;
            if (header.StartsWith("Speaker[", StringComparison.Ordinal) || header.StartsWith("Text[", StringComparison.Ordinal)) return 46;
            if (header == "Message" || header == "Data") return 36;

            return 25;
        }

        private static string ColumnName(int index)
        {
            string name = "";

            do
            {
                name = (char)('A' + index % 26) + name;
                index = index / 26 - 1;
            }
            while (index >= 0);

            return name;
        }

        private static XmlDocument ReadXml(ZipArchive archive, string path)
        {
            ZipArchiveEntry entry = archive.GetEntry(path);

            if (entry == null) throw new FormatException($"The Excel file is missing '{path}'.");

            XmlDocument document = new() { XmlResolver = null };

            using (Stream stream = entry.Open()) document.Load(stream);

            return document;
        }

        private static string FindWorksheetPath(ZipArchive archive)
        {
            XmlDocument workbook = ReadXml(archive, "xl/workbook.xml");
            XmlNamespaceManager workbookNamespaces = new(workbook.NameTable);
            workbookNamespaces.AddNamespace("s", SheetNamespace);
            XmlNode sheet = workbook.SelectSingleNode("/s:workbook/s:sheets/s:sheet", workbookNamespaces);

            if (sheet == null) throw new FormatException("The Excel workbook has no worksheet.");

            string relationId = sheet.Attributes?["id", DocumentRelationshipNamespace]?.Value;
            XmlDocument relationships = ReadXml(archive, "xl/_rels/workbook.xml.rels");
            XmlNamespaceManager relationNamespaces = new(relationships.NameTable);
            relationNamespaces.AddNamespace("p", PackageNamespace);
            XmlNodeList entries = relationships.SelectNodes("/p:Relationships/p:Relationship", relationNamespaces);

            foreach (XmlNode entry in entries)
            {
                if (entry.Attributes?["Id"]?.Value != relationId) continue;
                if (entry.Attributes?["TargetMode"]?.Value == "External") break;

                return ResolvePartPath("xl", entry.Attributes?["Target"]?.Value);
            }

            throw new FormatException("Could not locate the Excel worksheet.");
        }

        private static string ResolvePartPath(string folder, string target)
        {
            if (string.IsNullOrEmpty(target)) throw new FormatException("The Excel worksheet path is missing.");

            string[] parts = (target.StartsWith("/", StringComparison.Ordinal) ? target.Substring(1) : folder + "/" + target).Replace('\\', '/').Split('/');
            List<string> resolved = new();

            foreach (string part in parts)
            {
                if (part == "" || part == ".") continue;
                if (part == "..")
                {
                    if (resolved.Count == 0) throw new FormatException("The Excel worksheet path is invalid.");
                    resolved.RemoveAt(resolved.Count - 1);
                }
                else resolved.Add(part);
            }

            return string.Join("/", resolved);
        }

        private static List<string> LoadSharedStrings(ZipArchive archive)
        {
            List<string> strings = new();

            if (archive.GetEntry("xl/sharedStrings.xml") == null) return strings;

            XmlDocument document = ReadXml(archive, "xl/sharedStrings.xml");
            XmlNamespaceManager namespaces = new(document.NameTable);
            namespaces.AddNamespace("s", SheetNamespace);
            XmlNodeList items = document.SelectNodes("/s:sst/s:si", namespaces);

            foreach (XmlNode item in items) strings.Add(ReadText(item, namespaces));

            return strings;
        }

        private static string ReadText(XmlNode parent, XmlNamespaceManager namespaces)
        {
            StringBuilder builder = new();
            XmlNodeList pieces = parent.SelectNodes("./s:t | ./s:r/s:t", namespaces);

            foreach (XmlNode piece in pieces) builder.Append(piece.InnerText);

            return builder.ToString();
        }

        private static List<List<string>> ReadWorksheet(ZipArchive archive, string sheetPath, List<string> sharedStrings)
        {
            XmlDocument document = ReadXml(archive, sheetPath);
            XmlNamespaceManager namespaces = new(document.NameTable);
            namespaces.AddNamespace("s", SheetNamespace);
            XmlNodeList xmlRows = document.SelectNodes("/s:worksheet/s:sheetData/s:row", namespaces);
            SortedDictionary<int, Dictionary<int, string>> cells = new();

            foreach (XmlNode xmlRow in xmlRows)
            {
                int row = ParseRow(xmlRow.Attributes?["r"]?.Value);
                Dictionary<int, string> values = new();
                XmlNodeList xmlCells = xmlRow.SelectNodes("./s:c", namespaces);

                foreach (XmlNode cell in xmlCells)
                {
                    string address = cell.Attributes?["r"]?.Value;
                    int column = ParseColumn(address);

                    if (values.ContainsKey(column)) throw new FormatException($"Duplicate Excel cell {address}.");

                    values[column] = ReadCell(cell, namespaces, sharedStrings, address);
                }

                if (values.Count > 0) cells[row] = values;
            }

            if (!cells.TryGetValue(1, out Dictionary<int, string> firstRow)) throw new FormatException("Excel row 1 must contain the CSV headers.");

            int columnCount = 0;

            foreach (int column in firstRow.Keys)
            {
                if (column + 1 > columnCount) columnCount = column + 1;
            }

            List<List<string>> rows = new();

            foreach (KeyValuePair<int, Dictionary<int, string>> record in cells)
            {
                foreach (KeyValuePair<int, string> item in record.Value)
                {
                    if (item.Key >= columnCount && !string.IsNullOrEmpty(item.Value))
                        throw new FormatException($"Excel row {record.Key} contains data outside the header columns.");
                }

                List<string> row = new();
                bool hasContent = false;

                for (int c = 0; c < columnCount; c++)
                {
                    record.Value.TryGetValue(c, out string value);
                    row.Add(value ?? "");

                    if (!string.IsNullOrEmpty(value)) hasContent = true;
                }

                if (hasContent) rows.Add(row);
            }

            return rows;
        }

        private static int ParseRow(string raw)
        {
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int row) || row < 1 || row > MaxRows)
                throw new FormatException("Excel contains an invalid row index.");

            return row;
        }

        private static int ParseColumn(string reference)
        {
            int column = 0;
            int index = 0;

            if (string.IsNullOrEmpty(reference)) throw new FormatException("Excel contains a cell without a position.");

            while (index < reference.Length && reference[index] >= 'A' && reference[index] <= 'Z')
            {
                column = column * 26 + reference[index] - 'A' + 1;
                index++;
            }

            if (index == 0 || column > MaxColumns) throw new FormatException($"Invalid Excel column in '{reference}'.");

            return column - 1;
        }

        private static string ReadCell(XmlNode cell, XmlNamespaceManager namespaces, List<string> sharedStrings, string address)
        {
            if (cell.SelectSingleNode("./s:f", namespaces) != null)
                throw new FormatException($"Excel cell {address} contains a formula. Change the cell format to Text and enter a literal value.");

            string type = cell.Attributes?["t"]?.Value;
            XmlNode value = cell.SelectSingleNode("./s:v", namespaces);

            if (type == "inlineStr")
            {
                XmlNode inline = cell.SelectSingleNode("./s:is", namespaces);

                return inline == null ? "" : ReadText(inline, namespaces);
            }

            if (type == "s")
            {
                if (!int.TryParse(value?.InnerText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) || index < 0 || index >= sharedStrings.Count)
                    throw new FormatException($"Excel cell {address} has an invalid shared string.");

                return sharedStrings[index];
            }

            if (type == "str") return value?.InnerText ?? "";

            if (type == null || type == "" || type == "n")
            {
                if (value == null) return "";

                string number = value.InnerText;

                if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double numeric) || double.IsNaN(numeric) || double.IsInfinity(numeric))
                    throw new FormatException($"Excel cell {address} contains an invalid numeric value.");

                return NormalizeNumericCell(number);
            }

            throw new FormatException($"Excel cell {address} has an unsupported cell type '{type}'. Use text or numeric values.");
        }

        private static string NormalizeNumericCell(string number)
        {
            int exponentIndex = number.IndexOfAny(new[] { 'e', 'E' });
            string significand = exponentIndex < 0 ? number : number.Substring(0, exponentIndex);
            int exponent = 0;

            if (exponentIndex >= 0 && !int.TryParse(number.Substring(exponentIndex + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out exponent)) return number;

            bool negative = significand.StartsWith("-", StringComparison.Ordinal);
            string unsigned = negative || significand.StartsWith("+", StringComparison.Ordinal) ? significand.Substring(1) : significand;
            int decimalIndex = unsigned.IndexOf('.');
            int decimalPlaces = decimalIndex < 0 ? 0 : unsigned.Length - decimalIndex - 1;
            string digits = decimalIndex < 0 ? unsigned : unsigned.Remove(decimalIndex, 1);
            long shift = (long)exponent - decimalPlaces;

            if (shift < 0)
            {
                long fractionLength = -shift;

                if (fractionLength > digits.Length) return number;

                for (int i = digits.Length - (int)fractionLength; i < digits.Length; i++)
                {
                    if (digits[i] != '0') return number;
                }

                digits = digits.Substring(0, digits.Length - (int)fractionLength);
            }
            else if (shift > 0)
            {
                if (shift > 32 || digits.Length + shift > 32) return number;

                digits += new string('0', (int)shift);
            }

            digits = digits.TrimStart('0');

            return digits.Length == 0 ? "0" : negative ? "-" + digits : digits;
        }
    }
}
