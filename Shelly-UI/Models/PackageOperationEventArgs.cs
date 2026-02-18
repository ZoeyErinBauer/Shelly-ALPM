using System;

namespace Shelly_UI.Models;

public class PackageOperationEventArgs(OperationType operationType, string? packageName) : EventArgs
{
    public OperationType OperationType { get; } = operationType;
    public string? PackageName { get; } = packageName;
}
