using ReactiveUI;

namespace Shelly_UI.Models;

public class MetaPackageModel(
    string id,
    string name,
    string version,
    string description,
    PackageType packageType,
    string summary,
    string repository,
    bool isInstalled) : ReactiveObject
{
    public string Id { get; init; } = id;

    public string Name { get; init; } = name;

    public string Version { get; init; } = version;

    public string Description { get; init; } = description;

    public PackageType PackageType { get; init; } = packageType;

    public string Summary { get; init; } = summary;

    public string Repository { get; init; } = repository;

    public bool IsInstalled { get; init; } = isInstalled;

    private bool _isChecked;

    public bool IsChecked
    {
        get => _isChecked;
        set => this.RaiseAndSetIfChanged(ref _isChecked, value);
    }
}
