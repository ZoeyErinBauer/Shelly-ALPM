using System;
using PackageManager.Alpm;

namespace Shelly_UI.Services;

public interface IAlpmEventService
{
    event EventHandler<AlpmQuestionEventArgs>? Question;
    event EventHandler<AlpmPackageOperationEventArgs>? PackageOperation;

    /// <summary>
    /// Raises a Question event. Called by PrivilegedOperationService when parsing CLI stderr.
    /// </summary>
    void RaiseQuestion(AlpmQuestionEventArgs args);

    /// <summary>
    /// Raises a PackageOperation event. Called by PrivilegedOperationService when parsing CLI stderr.
    /// </summary>
    void RaisePackageOperation(AlpmPackageOperationEventArgs args);
}
