using System;
using System.Collections.Generic;
using UnityEditor.Localization;
using UnityEngine.Localization;

namespace Yang.Dialogue.Editor
{
    internal static class DialogueCsvImportSanitizer
    {
        private static readonly string[] requiredHeaders = { "ID", "Type", "Next", "Message", "Data", "SpeakerTable", "TextTable", "SpeakerKey", "TextKey", "X", "Y" };

        public static bool TrySanitize(DialogueSO asset, string csv, DialogueCsvImportOptions options, out string sanitized, out DialogueCsvImportOptions effectiveOptions, out string summary, out string error)
        {
            sanitized = "";
            summary = "";
            error = "";
            effectiveOptions = CopyOptions(options);
            List<List<string>> source;

            try
            {
                source = CsvUtility.FromCsv(csv, out _);
            }
            catch (FormatException exception)
            {
                error = exception.Message;
                return false;
            }

            if (source.Count == 0 || source[0].Count < requiredHeaders.Length)
            {
                error = "CSV has no complete dialogue header. This cannot be ignored safely.";
                return false;
            }

            for (int i = 0; i < requiredHeaders.Length; i++)
            {
                if (source[0][i] == requiredHeaders[i]) continue;

                error = $"CSV header column {i + 1} must be '{requiredHeaders[i]}'. This cannot be ignored safely.";
                return false;
            }

            string[] identities = { "SpeakerTableGuid", "TextTableGuid", "SpeakerEntryId", "TextEntryId" };

            foreach (string identity in identities)
            {
                if (!source[0].Contains(identity))
                {
                    error = $"CSV must include '{identity}'. Export a CSV using the current format.";
                    return false;
                }
            }

            foreach (string header in source[0])
            {
                if (!header.StartsWith("SpeakerPresent[", StringComparison.Ordinal) && !header.StartsWith("TextPresent[", StringComparison.Ordinal)) continue;

                error = $"CSV column '{header}' is no longer supported. Export a CSV using the current format.";
                return false;
            }

            HashSet<string> locales = new(StringComparer.Ordinal);
            IReadOnlyList<Locale> configuredLocales = LocalizationEditorSettings.GetLocales();

            foreach (Locale locale in configuredLocales) locales.Add(locale.Identifier.Code);

            List<int> includedColumns = new();
            HashSet<string> names = new(StringComparer.Ordinal);
            int ignoredColumns = 0;

            for (int i = 0; i < source[0].Count; i++)
            {
                string name = source[0][i];
                bool supported = i < requiredHeaders.Length || IsSupportedExtraColumn(name, locales);

                if (!supported || !names.Add(name))
                {
                    ignoredColumns++;
                    continue;
                }

                includedColumns.Add(i);
            }

            List<List<string>> rows = new();

            foreach (List<string> sourceRow in source)
            {
                List<string> result = new();

                foreach (int column in includedColumns) result.Add(column < sourceRow.Count ? sourceRow[column] : "");

                rows.Add(result);
            }

            int originalNodes = CountContentNodes(rows);
            int ignoredNodes = 0;
            int ignoredDetails = 0;
            int clearedLinks = 0;
            int clearedFields = 0;

            for (int i = 1; i < source.Count; i++)
            {
                for (int column = source[0].Count; column < source[i].Count; column++)
                {
                    if (!string.IsNullOrEmpty(source[i][column])) clearedFields++;
                }
            }

            int maxPasses = rows.Count * 8 + 25;

            for (int pass = 0; pass < maxPasses; pass++)
            {
                string candidate = ToCsv(rows);
                DialogueCsvImportReview review = new(asset, candidate);
                CopyOptions(effectiveOptions, review.options);
                review.Scan();

                if (review.errorCount == 0)
                {
                    if (originalNodes > 0 && CountContentNodes(rows) == 0)
                    {
                        error = "All dialogue nodes would be discarded. Import was cancelled to protect the existing graph.";
                        return false;
                    }

                    sanitized = candidate;
                    summary = $"Invalid data to ignore: {ignoredNodes} node(s), {ignoredDetails} detail row(s), {clearedLinks} link(s), {clearedFields} field(s), {ignoredColumns} column(s).";
                    return true;
                }

                bool changed = false;
                HashSet<int> removed = new();

                foreach (DialogueCsvIssue issue in review.issues)
                {
                    if (issue.warning) continue;

                    if (issue.row == 0)
                    {
                        error = issue.detail;
                        return false;
                    }

                    if (issue.row < 0)
                    {
                        if (issue.title == "Start node unavailable" || issue.title == "Start ID conflict")
                        {
                            if (!effectiveOptions.keepStart) continue;

                            effectiveOptions.keepStart = false;
                            changed = true;
                            clearedFields++;
                            continue;
                        }

                        if (issue.title == "Invalid table folder" && effectiveOptions.createTables)
                        {
                            effectiveOptions.createTables = false;
                            changed = true;
                            clearedFields++;
                            continue;
                        }

                        error = issue.detail;
                        return false;
                    }

                    if (issue.row >= rows.Count) continue;

                    List<string> row = rows[issue.row];
                    int column = issue.column;

                    if (issue.title == "Unused data for Type" && column >= 0 && column < row.Count)
                    {
                        if (row[column].Length == 0) continue;

                        row[column] = "";
                        clearedFields++;
                        changed = true;
                        continue;
                    }

                    if (issue.title == "Missing connection target")
                    {
                        if (row[2].Length == 0) continue;

                        row[2] = "";
                        clearedLinks++;
                        changed = true;
                        continue;
                    }

                    if (issue.title == "Invalid position")
                    {
                        if (column < 0 || column >= row.Count || row[column].Length == 0) continue;

                        row[column] = "";
                        clearedFields++;
                        changed = true;
                        continue;
                    }

                    // Never drop an invalid GUID or ID and then resolve the same entry by name.
                    // Ignore mode discards the affected localization reference instead.
                    if ((issue.title == "Invalid table GUID" || issue.title == "Missing table GUID" ||
                         issue.title == "Invalid entry ID" || issue.title == "Locale table missing" ||
                         issue.title == "Conflicting translation") &&
                        TryGetLocalizationKind(rows, issue.row, column, out bool invalidSpeaker))
                    {
                        int cleared = ClearLocalization(rows, issue.row, invalidSpeaker);

                        if (issue.title == "Invalid table GUID" || issue.title == "Missing table GUID")
                        {
                            string guidHeader = invalidSpeaker ? "SpeakerTableGuid" : "TextTableGuid";
                            int guidColumn = rows[0].IndexOf(guidHeader);

                            if (guidColumn >= 0 && row[guidColumn].Length > 0)
                            {
                                row[guidColumn] = "";
                                cleared++;
                            }
                        }

                        if (cleared > 0)
                        {
                            clearedFields += cleared;
                            changed = true;
                        }

                        continue;
                    }

                    if (IsLocalizationIssue(issue.title) && TryGetLocalizationKind(rows, issue.row, column, out bool speaker))
                    {
                        int cleared = ClearLocalization(rows, issue.row, speaker);

                        if (cleared > 0)
                        {
                            clearedFields += cleared;
                            changed = true;
                        }

                        continue;
                    }

                    removed.Add(issue.row);
                }

                if (removed.Count > 0)
                {
                    int parent = -1;

                    for (int row = 1; row < rows.Count; row++)
                    {
                        if (DialogueCsvImporter.IsNodeType(rows[row][1].Trim())) parent = row;

                        if (parent >= 0 && removed.Contains(parent)) removed.Add(row);
                    }

                    for (int row = rows.Count - 1; row >= 1; row--)
                    {
                        if (!removed.Contains(row)) continue;

                        if (DialogueCsvImporter.IsNodeType(rows[row][1].Trim())) ignoredNodes++;
                        else ignoredDetails++;

                        rows.RemoveAt(row);
                    }

                    changed = true;
                }

                if (!changed)
                {
                    error = "The remaining CSV errors cannot be safely ignored. Correct the CSV or change the import options.";
                    return false;
                }
            }

            error = "Too many dependent CSV errors to import safely.";
            return false;
        }

        private static bool IsSupportedExtraColumn(string name, HashSet<string> locales)
        {
            if (name == "SpeakerTableGuid" || name == "TextTableGuid" || name == "SpeakerEntryId" || name == "TextEntryId") return true;

            string[] prefixes = { "Speaker[", "Text[" };

            foreach (string prefix in prefixes)
            {
                if (!name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith("]", StringComparison.Ordinal)) continue;

                string code = name.Substring(prefix.Length, name.Length - prefix.Length - 1);
                return locales.Contains(code);
            }

            return false;
        }

        private static bool IsLocalizationIssue(string title)
        {
            return title == "Missing table name" || title == "Missing Localization table" || title == "Missing table GUID reference" || title == "Missing Localization entry" ||
                   title == "Missing Localization entry ID" || title == "Missing key name" ||
                   title == "Entry ID mismatch" || title == "Key name conflict" || title == "Conflicting entry keys" || title == "Conflicting entry IDs";
        }

        private static bool TryGetLocalizationKind(List<List<string>> rows, int rowIndex, int column, out bool speaker)
        {
            speaker = false;

            if (column == 5 || column == 7)
            {
                speaker = true;
                return true;
            }

            if (column == 6 || column == 8) return true;

            if (column < 0 || column >= rows[0].Count) return false;

            string header = rows[0][column];

            if (header.StartsWith("Speaker", StringComparison.Ordinal))
            {
                speaker = true;
                return true;
            }

            return header.StartsWith("Text", StringComparison.Ordinal);
        }

        private static int ClearLocalization(List<List<string>> rows, int rowIndex, bool speaker)
        {
            int count = ClearEntry(rows, rowIndex, speaker);

            if (speaker || rows[rowIndex][1].Trim() != "Choice") return count;

            for (int row = rowIndex + 1; row < rows.Count; row++)
            {
                string type = rows[row][1].Trim();

                if (DialogueCsvImporter.IsNodeType(type)) break;

                if (type == "Option") count += ClearEntry(rows, row, false);
            }

            return count;
        }

        private static int ClearEntry(List<List<string>> rows, int rowIndex, bool speaker)
        {
            int cleared = 0;
            int keyColumn = speaker ? 7 : 8;
            string prefix = speaker ? "Speaker" : "Text";
            List<string> header = rows[0];
            List<string> row = rows[rowIndex];

            for (int column = 0; column < header.Count; column++)
            {
                string name = header[column];
                bool isEntryField = column == keyColumn || name == prefix + "EntryId" ||
                                    name.StartsWith(prefix + "[", StringComparison.Ordinal);

                if (!isEntryField || row[column].Length == 0) continue;

                row[column] = "";
                cleared++;
            }

            return cleared;
        }

        private static int CountContentNodes(List<List<string>> rows)
        {
            int count = 0;

            for (int row = 1; row < rows.Count; row++)
            {
                if (DialogueCsvImporter.IsNodeType(rows[row][1].Trim()) && rows[row][1].Trim() != "Start") count++;
            }

            return count;
        }

        private static string ToCsv(List<List<string>> rows)
        {
            List<IReadOnlyList<string>> output = new();

            foreach (List<string> row in rows) output.Add(row);

            return CsvUtility.ToCsv(output);
        }

        private static DialogueCsvImportOptions CopyOptions(DialogueCsvImportOptions options)
        {
            DialogueCsvImportOptions copy = new();
            CopyOptions(options, copy);
            return copy;
        }

        private static void CopyOptions(DialogueCsvImportOptions source, DialogueCsvImportOptions target)
        {
            target.createTables = source.createTables;
            target.createEntries = source.createEntries;
            target.createLocaleTables = source.createLocaleTables;
            target.createNodes = source.createNodes;
            target.approvedNewNodeIds.Clear();

            foreach (string id in source.approvedNewNodeIds) target.approvedNewNodeIds.Add(id);

            target.useDefaultTables = source.useDefaultTables;
            target.keepStart = source.keepStart;
            target.tableFolder = source.tableFolder;
        }
    }
}
