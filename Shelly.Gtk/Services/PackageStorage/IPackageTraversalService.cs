using Shelly.Gtk.UiModels.PackageManagerObjects;

namespace Shelly.Gtk.Services.PackageStorage;

public interface IPackageTraversalService
{
   List<AlpmPackageDto> FetchFullDepdencyPackageInfomation(string rootPackageName);

   List<AlpmPackageDto> FetchFullPackageDependsOnList(string rootPackageName);
}