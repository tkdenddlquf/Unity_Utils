using System.Collections.Generic;
using System.Text;

namespace Yang.Dialogue.Editor
{
    /// <summary>
    /// Minimal RFC 4180 CSV reader/writer (handles quotes, commas, newlines inside fields).
    /// </summary>
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

        public static List<List<string>> FromCsv(string text)
        {
            List<List<string>> rows = new();

            if (string.IsNullOrEmpty(text)) return rows;

            List<string> row = new();
            StringBuilder field = new();

            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else inQuotes = false;
                    }
                    else field.Append(c);
                }
                else
                {
                    switch (c)
                    {
                        case '"':
                            inQuotes = true;
                            break;

                        case ',':
                            row.Add(field.ToString());
                            field.Clear();
                            break;

                        case '\r':
                            break;

                        case '\n':
                            row.Add(field.ToString());
                            field.Clear();

                            rows.Add(row);
                            row = new();
                            break;

                        default:
                            field.Append(c);
                            break;
                    }
                }
            }

            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row);
            }

            return rows;
        }

        private static string Escape(string value)
        {
            value ??= "";

            bool needsQuote = value.IndexOf(',') >= 0 ||
                              value.IndexOf('"') >= 0 ||
                              value.IndexOf('\n') >= 0 ||
                              value.IndexOf('\r') >= 0;

            if (!needsQuote) return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
