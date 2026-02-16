using System;
using PackageManager.Alpm;

namespace Shelly_UI.Services;

public class AlpmEventService : IAlpmEventService
{
    public event EventHandler<AlpmQuestionEventArgs>? Question;
    public event EventHandler<AlpmPackageOperationEventArgs>? PackageOperation;

    public void RaiseQuestion(AlpmQuestionEventArgs args)
    {
        Question?.Invoke(this, args);
    }

    public void RaisePackageOperation(AlpmPackageOperationEventArgs args)
    {
        PackageOperation?.Invoke(this, args);
    }
}
