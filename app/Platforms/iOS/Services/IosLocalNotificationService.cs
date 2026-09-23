using Foundation;
using Muninn.Services;
using UserNotifications;

namespace Muninn.Platforms.iOS.Services;

/// <summary>
/// Schedules one repeating, device-local digest reminder.
///
/// Local notifications are owned and delivered by iOS after this request is queued;
/// the app does not need to be running, and no APNs entitlement is involved. The
/// notification intentionally says the digest is ready rather than embedding save
/// content, because the Digest screen remains the source of truth for current saves.
/// </summary>
public sealed class IosLocalNotificationService : ILocalNotificationService
{
    private const string DailyDigestNotificationId = "muninn.daily-digest";
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public async Task SyncDailyDigestAsync(
        bool dailyDigestEnabled,
        bool notificationsEnabled,
        string digestTime)
    {
        await _syncLock.WaitAsync();
        try
        {
            var center = UNUserNotificationCenter.Current;
            center.RemovePendingNotificationRequests(new[] { DailyDigestNotificationId });

            if (!dailyDigestEnabled || !notificationsEnabled)
                return;

            if (!TimeOnly.TryParseExact(
                    digestTime,
                    "HH:mm",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var time))
            {
                Console.WriteLine($"[NOTIFY] Ignoring invalid digest time '{digestTime}'.");
                return;
            }

            var settings = await center.GetNotificationSettingsAsync();
            if (settings.AuthorizationStatus == UNAuthorizationStatus.NotDetermined)
            {
                var authorization = await center.RequestAuthorizationAsync(
                    UNAuthorizationOptions.Alert |
                    UNAuthorizationOptions.Sound);

                if (!authorization.Item1)
                {
                    Console.WriteLine("[NOTIFY] Local notification permission was not granted.");
                    return;
                }
            }
            else if (settings.AuthorizationStatus is UNAuthorizationStatus.Denied)
            {
                Console.WriteLine("[NOTIFY] Local notifications are disabled in iOS Settings.");
                return;
            }

            var content = new UNMutableNotificationContent
            {
                Title = "Your Muninn digest is ready",
                Body = "Take a moment to revisit what you saved.",
                Sound = UNNotificationSound.Default
            };

            var dateComponents = new NSDateComponents
            {
                Hour = time.Hour,
                Minute = time.Minute
            };
            var trigger = UNCalendarNotificationTrigger.CreateTrigger(dateComponents, repeats: true);
            var request = UNNotificationRequest.FromIdentifier(
                DailyDigestNotificationId,
                content,
                trigger);

            await center.AddNotificationRequestAsync(request);
            Console.WriteLine($"[NOTIFY] Scheduled daily digest reminder at {digestTime}.");
        }
        catch (Exception ex)
        {
            // Notification setup must never block login, settings, or app startup.
            Console.WriteLine($"[NOTIFY] Local notification sync failed: {ex.GetType().Name}.");
        }
        finally
        {
            _syncLock.Release();
        }
    }
}
