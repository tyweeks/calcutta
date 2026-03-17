using Calcutta.Models;

namespace Calcutta.Services;

/// <summary>
/// Parses CSV files into strongly-typed model objects.
/// Supports flexible, case-insensitive column names.
/// </summary>
public static class CsvParser
{
    // --- Teams CSV ---

    private static readonly string[] TeamNameColumns   = ["team", "name", "teamname"];
    private static readonly string[] SeedColumns       = ["seed"];
    private static readonly string[] RegionColumns     = ["region", "conf", "conference"];
    private static readonly string[] AdjEmColumns      = ["adjem", "adj. em", "adjustedem", "em", "netrating", "net"];
    private static readonly string[] AdjOColumns       = ["adjo", "adjoff", "adjustedo", "adjoffense", "offense"];
    private static readonly string[] AdjDColumns       = ["adjd", "adjdef", "adjustedd", "adjdefense", "defense"];

    public static List<Team> ParseTeams(string filePath)
    {
        var lines = ReadLines(filePath);
        if (lines.Count < 2)
            throw new InvalidDataException($"Teams file '{filePath}' must have a header row and at least one data row.");

        var headers = ParseCsvRow(lines[0]);
        var colIndex = BuildColumnMap(headers);

        int teamCol   = RequireColumn(colIndex, TeamNameColumns, filePath, "team name");
        int seedCol   = RequireColumn(colIndex, SeedColumns,     filePath, "seed");
        int regionCol = RequireColumn(colIndex, RegionColumns,   filePath, "region");
        int adjEmCol  = RequireColumn(colIndex, AdjEmColumns,    filePath, "AdjEM");

        int adjOCol = FindColumn(colIndex, AdjOColumns);
        int adjDCol = FindColumn(colIndex, AdjDColumns);

        var teams = new List<Team>();
        for (int i = 1; i < lines.Count; i++)
        {
            var row = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(row)) continue;

            var cols = ParseCsvRow(row);
            if (cols.Length <= Math.Max(teamCol, Math.Max(seedCol, Math.Max(regionCol, adjEmCol))))
                throw new InvalidDataException($"Line {i + 1} in '{filePath}' has too few columns.");

            var team = new Team
            {
                Name   = cols[teamCol].Trim(),
                Seed   = ParseInt(cols[seedCol],   i + 1, filePath, "seed"),
                Region = cols[regionCol].Trim(),
                AdjEM  = ParseDouble(cols[adjEmCol], i + 1, filePath, "AdjEM"),
            };

            if (adjOCol >= 0 && adjOCol < cols.Length)
                team.AdjO = ParseDouble(cols[adjOCol], i + 1, filePath, "AdjO");
            if (adjDCol >= 0 && adjDCol < cols.Length)
                team.AdjD = ParseDouble(cols[adjDCol], i + 1, filePath, "AdjD");

            teams.Add(team);
        }

        return teams;
    }

    // --- Payouts CSV ---

    private static readonly string[] RoundColumns   = ["round", "roundnumber", "roundnum"];
    private static readonly string[] RoundNameColumns = ["name", "roundname", "description"];
    private static readonly string[] PayoutColumns  = ["payoutpercent", "payout", "percent", "pct", "%"];

    public static List<RoundPayout> ParsePayouts(string filePath)
    {
        var lines = ReadLines(filePath);
        if (lines.Count < 2)
            throw new InvalidDataException($"Payouts file '{filePath}' must have a header row and at least one data row.");

        var headers = ParseCsvRow(lines[0]);
        var colIndex = BuildColumnMap(headers);

        int roundCol   = RequireColumn(colIndex, RoundColumns,   filePath, "round");
        int payoutCol  = RequireColumn(colIndex, PayoutColumns,  filePath, "payout percent");
        int nameCol    = FindColumn(colIndex, RoundNameColumns);

        var payouts = new List<RoundPayout>();
        for (int i = 1; i < lines.Count; i++)
        {
            var row = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(row)) continue;

            var cols = ParseCsvRow(row);
            if (cols.Length <= Math.Max(roundCol, payoutCol))
                throw new InvalidDataException($"Line {i + 1} in '{filePath}' has too few columns.");

            var payout = new RoundPayout
            {
                Round         = ParseInt(cols[roundCol],    i + 1, filePath, "round"),
                PayoutPercent = ParseDouble(cols[payoutCol], i + 1, filePath, "payout percent"),
                Name          = (nameCol >= 0 && nameCol < cols.Length) ? cols[nameCol].Trim() : $"Round {cols[roundCol].Trim()}",
            };
            payouts.Add(payout);
        }

        return payouts;
    }

    // --- Internal helpers ---

    private static List<string> ReadLines(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: '{filePath}'");
        return [.. File.ReadAllLines(filePath)];
    }

    /// <summary>Parses a single CSV row, respecting quoted fields.</summary>
    internal static string[] ParseCsvRow(string line)
    {
        var fields = new List<string>();
        int i = 0;
        while (i <= line.Length)
        {
            if (i == line.Length)
            {
                fields.Add(string.Empty);
                break;
            }

            if (line[i] == '"')
            {
                i++; // skip opening quote
                var sb = new System.Text.StringBuilder();
                while (i < line.Length)
                {
                    if (line[i] == '"')
                    {
                        i++;
                        if (i < line.Length && line[i] == '"')
                        {
                            sb.Append('"'); // escaped quote
                            i++;
                        }
                        else break; // end of quoted field
                    }
                    else
                    {
                        sb.Append(line[i++]);
                    }
                }
                fields.Add(sb.ToString());
                if (i < line.Length && line[i] == ',') i++;
            }
            else
            {
                int start = i;
                while (i < line.Length && line[i] != ',') i++;
                fields.Add(line[start..i]);
                if (i < line.Length) i++; // skip comma
            }
        }
        return [.. fields];
    }

    private static Dictionary<string, int> BuildColumnMap(string[] headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
        {
            var key = headers[i].Trim().ToLowerInvariant().Replace(" ", "").Replace(".", "").Replace("_", "");
            map.TryAdd(key, i);
        }
        return map;
    }

    private static int RequireColumn(Dictionary<string, int> map, string[] candidates, string filePath, string fieldName)
    {
        int idx = FindColumn(map, candidates);
        if (idx < 0)
            throw new InvalidDataException(
                $"Could not find '{fieldName}' column in '{filePath}'. " +
                $"Expected one of: {string.Join(", ", candidates)}.");
        return idx;
    }

    private static int FindColumn(Dictionary<string, int> map, string[] candidates)
    {
        foreach (var c in candidates)
        {
            var key = c.ToLowerInvariant().Replace(" ", "").Replace(".", "").Replace("_", "");
            if (map.TryGetValue(key, out int idx)) return idx;
        }
        return -1;
    }

    private static int ParseInt(string s, int lineNum, string filePath, string field)
    {
        if (int.TryParse(s.Trim(), out int v)) return v;
        throw new InvalidDataException($"Invalid {field} '{s}' on line {lineNum} of '{filePath}'.");
    }

    private static double ParseDouble(string s, int lineNum, string filePath, string field)
    {
        if (double.TryParse(s.Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double v)) return v;
        throw new InvalidDataException($"Invalid {field} '{s}' on line {lineNum} of '{filePath}'.");
    }
}
