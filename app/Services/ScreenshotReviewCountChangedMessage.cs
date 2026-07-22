using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Muninn.Services;

public class ScreenshotReviewCountChangedMessage(int count) : ValueChangedMessage<int>(count);

