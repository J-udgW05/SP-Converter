using System;

namespace SPConverter.Services;

public class NotificationRequest
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string TargetFolder { get; set; } = string.Empty;
    public bool IsSuccess { get; set; } = true;
}

public static class NotificationService
{
    public static event Action<NotificationRequest>? OnShowNotification;

    public static void Show(string title, string message, string targetFolder, bool isSuccess = true)
    {
        OnShowNotification?.Invoke(new NotificationRequest
        {
            Title = title,
            Message = message,
            TargetFolder = targetFolder,
            IsSuccess = isSuccess
        });
    }
}
