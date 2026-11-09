using System;
using System.Collections.Generic;
using System.Text;

namespace Yang.Dialogue.Editor
{
    public static class CsvUtility
    {
        public static string ToCsv(IReadOnlyList<IReadOnlyList<string>> rows)
        {
            StringBuilder sb = new();

            for (int r = 0; r < rows.Count; r++)
            {
                IReadOnlyList<string> row = rows[r];

                for (int c = 0; c < row.Count; c++)
                {
                    if (c > 0) sb.Append(',');

                    sb.Append(Escape(row[c]));
                }

                sb.Append('\n');
            }

            return sb.ToString();
        }

        public static List<List<string>> FromCsv(string text) => FromCsv(text, out _);

        public static List<List<string>> FromCsv(string text, out List<int> lines)
        {
            List<List<string>> rows = new();
            lines = new List<int>();

            if (string.IsNullOrEmpty(text)) return rows;

            List<string> row = new();
            StringBuilder field = new();
            bool inQuotes = false;
            bool quoteClosed = false;
            int line = 1;
            int recordLine = 1;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (i == 0 && c == (char)0xFEFF) continue;

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                            quoteClosed = true;
                        }
                    }
                    else
                    {
                        field.Append(c);

                        if (c == '\n') line++;
                    }

                    continue;
                }

                if (c == ',')
                {
                    row.Add(field.ToString());
                    field.Clear();
                    quoteClosed = false;
                    continue;
                }

                if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;

                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    lines.Add(recordLine);
                    row = new();
                    quoteClosed = false;
                    line++;
                    recordLine = line;
                    continue;
                }

                if (c == '"')
                {
                    if (field.Length > 0 || quoteClosed)
                        throw new FormatException($"CSV line {line}: unexpected quote in an unquoted field.");

                    inQuotes = true;
                    continue;
                }

                if (quoteClosed) throw new FormatException($"CSV line {line}: unexpected character after a quoted field.");

                field.Append(c);
            }

            if (inQuotes) throw new FormatException($"CSV line {recordLine}: unclosed quoted field.");

            if (field.Length > 0 || row.Count > 0 || quoteClosed)
            {
                row.Add(field.ToString());
                rows.Add(row);
                lines.Add(recordLine);
            }

            return rows;
        }

        private static string Escape(string value)
        {
            value ??= "";
            bool needsQuote = value.IndexOf(',') >= 0 ||
                              value.IndexOf('"') >= 0 ||
                              value.IndexOf('\n') >= 0 ||
                              value.IndexOf('\r') >= 0 ||
                              (value.Length > 0 && (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1])));

            if (!needsQuote) return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
