using Microsoft.Maui.Controls;

namespace Muninn.Models;

public class ScreenshotCandidate
{
    public string LocalIdentifier { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public byte[] ThumbnailBytes { get; init; } = [];
    public bool HasThumbnail => ThumbnailBytes.Length > 0;

    public string SourceLabel => "Camera Roll";

    public string RelativeTime
    {
        get
        {
            var diff = DateTime.UtcNow - CreatedAt.ToUniversalTime();
            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes} min ago";
            if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            return CreatedAt.ToString("MMM d");
        }
    }

    public ImageSource ThumbnailSource =>
        ImageSource.FromStream(() => new MemoryStream(ThumbnailBytes));
}
