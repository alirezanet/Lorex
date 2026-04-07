using Lorex.Core.Models;

namespace Lorex.Core.Serialization;

/// <summary>
/// Minimal YAML parser for lorex skill files.
/// Supports:
///   - Standalone key: value YAML
///   - YAML frontmatter between --- delimiters at the top of a markdown file
///   - tags: val1, val2, val3 (comma-separated → string[])
///   - Blank lines and # comments are ignored
/// </summary>
public static class SimpleYamlParser
{
    // ── Frontmatter ───────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts the YAML string from between --- delimiters at the start of a markdown file.
    /// Returns null if no frontmatter block is present.
    /// </summary>
    public static string? ExtractFrontmatterYaml(string markdownContent)
    {
        var lines = markdownContent.Split('\n');
        if (lines.Length < 2 || lines[0].Trim() != "---")
            return null;

        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
                return string.Join('\n', lines[1..i]).Trim();
        }

        return null; // no closing ---
    }

    /// <summary>Parses skill metadata from YAML frontmatter in a markdown file.</summary>
    public static SkillMetadata ParseSkillMetadataFromMarkdown(string markdownContent)
    {
        var yaml = ExtractFrontmatterYaml(markdownContent)
            ?? throw new InvalidDataException(
                "Markdown file has no YAML frontmatter. Expected --- delimiters at the top of the file.");
        return ParseSkillMetadata(yaml);
    }

    // ── Standalone YAML ───────────────────────────────────────────────────────

    public static SkillMetadata ParseSkillMetadata(string yaml)
    {
        var dict = ParseToDictionary(yaml);

        return new SkillMetadata
        {
            Name = GetRequired(dict, "name"),
            Description = GetRequired(dict, "description"),
            Version = dict.TryGetValue("version", out var v) ? v : "1.0.0",
            Tags = dict.TryGetValue("tags", out var t)
                ? t.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : [],
            Owner = dict.TryGetValue("owner", out var o) ? o : string.Empty,
        };
    }

    public static Dictionary<string, string> ParseToDictionary(string yaml)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines  = yaml.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (line.Length == 0 || line[0] == '#')
                continue;

            var colonIndex = line.IndexOf(':');
            if (colonIndex <= 0)
                continue;

            var key   = line[..colonIndex].Trim();
            var value = line[(colonIndex + 1)..].Trim();

            // Handle YAML block scalars: >- (folded-strip), >  (folded), |- (literal-strip), | (literal)
            // Collect all following indented lines and join them into a single string.
            if (value is ">" or ">-" or "|" or "|-")
            {
                var blockLines = new List<string>();
                while (i + 1 < lines.Length)
                {
                    var next = lines[i + 1];
                    // Block content lines start with whitespace (at least one space/tab)
                    if (next.Length > 0 && (next[0] == ' ' || next[0] == '\t'))
                    {
                        blockLines.Add(next.Trim());
                        i++;
                    }
                    else break;
                }
                value = string.Join(" ", blockLines);
            }

            // Strip surrounding double or single quotes from scalar values
            if (value.Length >= 2
                && ((value[0] == '"'  && value[^1] == '"')
                 || (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            result[key] = value;
        }

        return result;
    }

    private static string GetRequired(Dictionary<string, string> dict, string key)
    {
        if (!dict.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"Required YAML field '{key}' is missing or empty.");
        return value;
    }
}
