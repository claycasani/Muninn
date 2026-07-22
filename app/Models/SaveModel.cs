using System.Text.Json.Serialization;

namespace Muninn.Models;

public class SaveModel
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("sourceUrl")]
    public string? SourceUrl { get; set; }

    [JsonPropertyName("imageRef")]
    public string? ImageRef { get; set; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "active";

    [JsonPropertyName("manualCategory")]
    public string? ManualCategory { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("analysis")]
    public AnalysisModel? Analysis { get; set; }

    // ── Derived helpers used by the UI ──────────────────────────────

    public bool IsCompleted => Status == "completed";
    public bool IsArchived => Status == "archived";
    public bool IsActive => Status == "active";
    public bool IsImageSave => Type == "image";

    /// Domain extracted from SourceUrl, e.g. "nytimes.com". Empty for image saves.
    public string Domain => Uri.TryCreate(SourceUrl, UriKind.Absolute, out var uri)
        ? uri.Host.Replace("www.", "")
        : SourceUrl ?? string.Empty;

    /// Short label for the card metadata line.
    /// Link saves: the domain ("nytimes.com"). Image saves: "Screenshot".
    public string MetaLabel => IsImageSave ? "Screenshot" : Domain;

    /// First letter of MetaLabel, uppercased — monogram shown when no image is available.
    public string Monogram => string.IsNullOrEmpty(MetaLabel) ? "?" : MetaLabel[0].ToString().ToUpperInvariant();

    /// Google favicon service URL for link saves; null for image saves.
    /// Used as the icon in the favicon/monogram slot on link cards.
    public string? FaviconUrl => !IsImageSave && !string.IsNullOrEmpty(Domain)
        ? $"https://www.google.com/s2/favicons?domain={Uri.EscapeDataString(Domain)}&sz=64"
        : null;

    /// Human-readable relative time, e.g. "2h ago"
    public string RelativeTime
    {
        get
        {
            var diff = DateTime.UtcNow - CreatedAt.ToUniversalTime();
            if (diff.TotalMinutes < 1) return "just now";
            if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            return CreatedAt.ToString("MMM d");
        }
    }

    /// Display title — identifies the saved artifact, without restating why it was saved.
    /// Image saves use "Screenshot" as the final fallback instead of the URL.
    public string DisplayTitle => FirstNonBlank(
            Analysis?.ArtifactTitle,
            Analysis?.ExtractedText?.Split('\n').FirstOrDefault(),
            IsImageSave ? "Screenshot" : SourceUrl,
            IsImageSave ? "Screenshot" : "Untitled");

    /// AI reason line text — null when analysis not yet available
    public string? AiReason => Analysis?.SuggestedAction;

    /// Category for grouping — manual user choice wins over AI analysis.
    public string Category => FirstNonBlank(ManualCategory, Analysis?.Category, "Uncategorized");

    private static string FirstNonBlank(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Length <= 80 ? value : value[..80].TrimEnd() + "…";
        }

        return "Untitled";
    }
}

public class AnalysisModel
{
    [JsonPropertyName("artifactTitle")]
    public string? ArtifactTitle { get; set; }

    [JsonPropertyName("inferredIntent")]
    public string? InferredIntent { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("suggestedAction")]
    public string? SuggestedAction { get; set; }

    [JsonPropertyName("extractedText")]
    public string? ExtractedText { get; set; }

    [JsonPropertyName("confidence")]
    public decimal? Confidence { get; set; }
}
