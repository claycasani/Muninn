namespace Muninn.Services;

/// <summary>
/// Keeps the device-side daily digest reminder in sync with the account settings.
/// The iOS implementation uses local notifications, so it does not require APNs or
/// an Apple Developer Program membership.
/// </summary>
public interface ILocalNotificationService
{
    Task SyncDailyDigestAsync(bool dailyDigestEnabled, bool notificationsEnabled, string digestTime);
}

/// <summary>No-op implementation for platforms without the iOS User Notifications framework.</summary>
public sealed class NoopLocalNotificationService : ILocalNotificationService
{
    public Task SyncDailyDigestAsync(bool dailyDigestEnabled, bool notificationsEnabled, string digestTime)
        => Task.CompletedTask;
}
