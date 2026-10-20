namespace Infrastructure.Configuration;

/// <summary>Reads only explicitly requested values. Never exports variables or executes shell syntax.</summary>
public static class LocalEnvironmentFile
{
    public static IReadOnlyDictionary<string, string> Read(string contentRoot, params string[] allowedNames)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var parent = Directory.GetParent(contentRoot)?.FullName;
        var paths = new List<string> { Path.Combine(contentRoot, ".env") };
        if (parent != null && File.Exists(Path.Combine(parent, "OnlineShop.sln")))
            paths.Add(Path.Combine(parent, ".env"));
        foreach (var path in paths.Where(File.Exists))
        foreach (var source in File.ReadLines(path))
        {
            var line = source.Trim();
            if (line.StartsWith("export ", StringComparison.Ordinal)) line = line[7..].TrimStart();
            var equals = line.IndexOf('=');
            if (equals < 0) continue;
            var name = line[..equals].Trim();
            if (!allowedNames.Contains(name, StringComparer.Ordinal) || values.ContainsKey(name)) continue;
            var value = line[(equals + 1)..].Trim();
            if (value.StartsWith('"') || value.StartsWith('\''))
            {
                var closing = value.IndexOf(value[0], 1);
                if (closing < 0) continue;
                var suffix = value[(closing + 1)..].TrimStart();
                if (suffix.Length != 0 && !suffix.StartsWith('#')) continue;
                value = value[1..closing];
            }
            else
            {
                if (value.StartsWith('#')) continue;
                var comment = value.IndexOf(" #", StringComparison.Ordinal);
                if (comment >= 0) value = value[..comment].TrimEnd();
            }
            if (!string.IsNullOrWhiteSpace(value)) values[name] = value;
        }
        return values;
    }
}
