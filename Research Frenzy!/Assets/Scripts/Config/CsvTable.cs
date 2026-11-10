// CsvTable - reads a spreadsheet that was saved as a .csv file (Excel, Google Sheets, LibreOffice, Numbers).
// The first row is the header row. Columns are found by their header name, so the person writing the
// file can put the columns in any order and add their own extra columns (like "Notes") without breaking anything.
// Plain C#, no Unity objects, so it can be used anywhere (and tested outside Unity).

using System;
using System.Collections.Generic;
using System.Text;

namespace Config
{
    public class CsvTable
    {
        /// <summary>The file this came from, only used in error messages.</summary>
        public string FileName { get; }

        private readonly Dictionary<string, int> columnIndex = new Dictionary<string, int>();
        private readonly List<string[]> rows = new List<string[]>();
        private readonly List<int> rowNumbers = new List<int>();   // the row number you'd see in the spreadsheet

        /// <summary>Number of data rows (blank rows and # comment rows are already skipped).</summary>
        public int RowCount => rows.Count;

        private CsvTable(string fileName)
        {
            FileName = fileName;
        }

        /// <summary>True if the header row has this column. Matching ignores case, spaces and punctuation.</summary>
        public bool HasColumn(params string[] names)
        {
            return FindColumn(names) >= 0;
        }

        /// <summary>
        /// The text in one cell, trimmed. Pass several names to accept alternatives
        /// (e.g. "Client Name" or "Name"). Returns "" if the column or cell doesn't exist.
        /// </summary>
        public string Get(int row, params string[] columnNames)
        {
            int col = FindColumn(columnNames);
            if (col < 0 || row < 0 || row >= rows.Count) return "";
            string[] cells = rows[row];
            return col < cells.Length ? cells[col].Trim() : "";
        }

        /// <summary>The row number as the spreadsheet shows it (the header is row 1), for error messages.</summary>
        public int SpreadsheetRow(int row)
        {
            return row >= 0 && row < rowNumbers.Count ? rowNumbers[row] : -1;
        }

        private int FindColumn(string[] names)
        {
            foreach (string name in names)
            {
                if (columnIndex.TryGetValue(Normalize(name), out int index))
                    return index;
            }
            return -1;
        }

        // ---------- Parsing ----------

        /// <summary>Turns the text of a .csv file into a table.</summary>
        public static CsvTable Parse(string text, string fileName)
        {
            CsvTable table = new CsvTable(fileName);
            if (string.IsNullOrEmpty(text)) return table;

            // Excel's "CSV UTF-8" option puts an invisible marker (a BOM) at the very start. Remove it,
            // or the first header would be "﻿ID" and the ID column would never be found.
            if (text[0] == '﻿') text = text.Substring(1);

            char delimiter = DetectDelimiter(text);
            List<string[]> records = SplitRecords(text, delimiter);
            if (records.Count == 0) return table;

            // header row
            string[] header = records[0];
            for (int i = 0; i < header.Length; i++)
            {
                string key = Normalize(header[i]);
                if (key.Length > 0 && !table.columnIndex.ContainsKey(key))
                    table.columnIndex[key] = i;
            }

            // data rows. record index 0 is spreadsheet row 1, so record r is row r + 1
            for (int r = 1; r < records.Count; r++)
            {
                string[] cells = records[r];
                if (IsBlank(cells)) continue;                         // empty rows spreadsheets leave at the end
                if (cells[0].TrimStart().StartsWith("#")) continue;   // "# ..." rows are comments for the writer
                table.rows.Add(cells);
                table.rowNumbers.Add(r + 1);
            }

            return table;
        }

        /// <summary>
        /// Lowercase letters and digits only, so "Client Name", "client_name" and "CLIENT-NAME" all match.
        /// Also used for matching methodology names.
        /// </summary>
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            StringBuilder sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Splits a cell that holds a list, like "Field Research; Market Research".
        /// Accepts ; , | or a new line between items, and drops empty items.
        /// </summary>
        public static List<string> SplitList(string cell)
        {
            List<string> items = new List<string>();
            if (string.IsNullOrWhiteSpace(cell)) return items;

            foreach (string part in cell.Split(new[] { ';', ',', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string item = part.Trim();
                if (item.Length > 0) items.Add(item);
            }
            return items;
        }

        // Excel in some countries saves with ; or tabs instead of commas. Look at the header row
        // (outside quotes) and use whichever of the three appears most. Commas win a tie.
        private static char DetectDelimiter(string text)
        {
            int commas = 0, semicolons = 0, tabs = 0;
            bool inQuotes = false;
            foreach (char c in text)
            {
                if (c == '"') inQuotes = !inQuotes;
                else if (!inQuotes)
                {
                    if (c == '\n' || c == '\r') break;   // only the first line
                    if (c == ',') commas++;
                    else if (c == ';') semicolons++;
                    else if (c == '\t') tabs++;
                }
            }
            if (tabs > commas && tabs >= semicolons) return '\t';
            if (semicolons > commas) return ';';
            return ',';
        }

        // The CSV rules spreadsheets use:
        //  - cells are separated by the delimiter, rows by a line break
        //  - a cell with a comma, quote or line break in it is wrapped in "double quotes"
        //  - a quote inside a quoted cell is written twice: "She said ""hi"""
        private static List<string[]> SplitRecords(string text, char delimiter)
        {
            List<string[]> records = new List<string[]>();
            List<string> cells = new List<string>();
            StringBuilder cell = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }   // "" -> "
                        else inQuotes = false;                                                    // closing quote
                    }
                    else cell.Append(c);   // anything goes inside quotes, including commas and line breaks
                    continue;
                }

                if (c == '"') inQuotes = true;
                else if (c == delimiter) { cells.Add(cell.ToString()); cell.Clear(); }
                else if (c == '\r' || c == '\n')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;   // Windows line break \r\n
                    cells.Add(cell.ToString()); cell.Clear();
                    records.Add(cells.ToArray()); cells.Clear();
                }
                else cell.Append(c);
            }

            // last row (files often don't end with a line break)
            if (cell.Length > 0 || cells.Count > 0)
            {
                cells.Add(cell.ToString());
                records.Add(cells.ToArray());
            }
            return records;
        }

        private static bool IsBlank(string[] cells)
        {
            foreach (string c in cells)
                if (!string.IsNullOrWhiteSpace(c)) return false;
            return true;
        }
    }
}
