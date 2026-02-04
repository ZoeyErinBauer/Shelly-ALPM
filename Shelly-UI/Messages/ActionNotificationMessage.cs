namespace Shelly_UI.Messages;

public class ActionNotificationMessage
{
    public string Message { get; }
    public bool IsSuccess { get; }

    public ActionNotificationMessage(string message, bool isSuccess = true)
    {
        Message = message;
        IsSuccess = isSuccess;
    }
}
