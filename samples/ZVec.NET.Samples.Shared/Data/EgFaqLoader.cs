using System.Text;

namespace ZVec.NET.Samples.Shared.Data;

/// <summary>Loads committed Egyptian Arabic FAQ CSV (T0) under fixtures/rag.</summary>
public static class EgFaqLoader
{
    public const string FileName = "eg_faq_dataset.csv";
    public const string SourceName = "eg_faq_dataset.csv";

    public static string? ResolvePath()
    {
        var path = Path.Combine(SamplePaths.FixturesRoot, "rag", FileName);
        return File.Exists(path) ? path : null;
    }

    public static IReadOnlyList<TextSource> Load()
    {
        var path = ResolvePath();
        if (path is null)
            return [];

        var rows = new List<TextSource>();
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var header = reader.ReadLine();
        if (header is null)
            return [];

        var cols = ParseCsvLine(header);
        var idIdx = IndexOf(cols, "id");
        var sectorIdx = IndexOf(cols, "sector");
        var categoryIdx = IndexOf(cols, "category");
        var questionIdx = IndexOf(cols, "question_ar");
        var answerIdx = IndexOf(cols, "answer_ar");
        if (idIdx < 0 || questionIdx < 0 || answerIdx < 0)
            throw new InvalidOperationException(
                $"'{FileName}' must have columns id, question_ar, answer_ar (optional: sector, category).");

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = ParseCsvLine(line);
            if (fields.Count <= Math.Max(idIdx, Math.Max(questionIdx, answerIdx)))
                continue;

            var id = fields[idIdx].Trim();
            var question = fields[questionIdx].Trim();
            var answer = fields[answerIdx].Trim();
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(question) ||
                string.IsNullOrWhiteSpace(answer))
                continue;

            var sector = sectorIdx >= 0 && sectorIdx < fields.Count ? fields[sectorIdx].Trim() : "";
            var category = categoryIdx >= 0 && categoryIdx < fields.Count ? fields[categoryIdx].Trim() : "";
            var tags = string.Join(';', new[] { sector, category }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (string.IsNullOrWhiteSpace(tags))
                tags = "eg-faq";

            rows.Add(new TextSource(id, question, answer, SourceName, tags));
        }

        return rows;
    }

    private static int IndexOf(IReadOnlyList<string> cols, string name)
    {
        for (var i = 0; i < cols.Count; i++)
        {
            if (cols[i].Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    internal static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(ch);
                }
            }
            else if (ch == '"')
            {
                inQuotes = true;
            }
            else if (ch == ',')
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(ch);
            }
        }

        fields.Add(sb.ToString());
        return fields;
    }
}
