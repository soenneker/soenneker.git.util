using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Git.Util;

public sealed partial class GitUtil
{
    public async ValueTask<string> GetUpdateCommitMessage( string directory, string message, CancellationToken cancellationToken = default)
    {
        var details = new List<string>();
        var versions = new SortedSet<string>(StringComparer.Ordinal);
        var paths = new List<string>();
        foreach (string path in Directory.EnumerateFiles(directory))
        {
            string name = Path.GetFileName(path);
            if (name.StartsWith("openapi", StringComparison.OrdinalIgnoreCase) || name.StartsWith("swagger", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("api.github.com.json", StringComparison.OrdinalIgnoreCase) || name.Equals("api_v2.json", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("fastly.json", StringComparison.OrdinalIgnoreCase) || name.Equals("merged.json", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("spec3.json", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("schema", StringComparison.OrdinalIgnoreCase) || name.Equals("fixed.json", StringComparison.OrdinalIgnoreCase))
                paths.Add(path);
        }
        paths.Sort(StringComparer.Ordinal);

        foreach (string path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            string hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
            details.Add($"{Path.GetFileName(path)}: SHA256 {hash}");
            if (!Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
                continue;

            stream.Position = 0;
            try
            {
                using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (document.RootElement.ValueKind == JsonValueKind.Object &&
                    document.RootElement.TryGetProperty("info", out JsonElement info) && info.ValueKind == JsonValueKind.Object &&
                    info.TryGetProperty("version", out JsonElement version) && version.ValueKind == JsonValueKind.String)
                {
                    string? value = version.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                        versions.Add(value.Replace('\r', ' ').Replace('\n', ' ').Trim());
                }
            }
            catch (JsonException)
            {
                // A non-JSON schema still has a useful source fingerprint.
            }
        }

        string subject = versions.Count == 0 ? message : $"{message} ({string.Join(", ", versions)})";
        return details.Count == 0 ? subject : $"{subject}\n\n{string.Join("\n", details)}";
    }
}
