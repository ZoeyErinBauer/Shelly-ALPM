using System.Collections.Generic;
using System.Threading.Tasks;
using PackageManager.Aur.Models;

namespace PackageManager.Aur;

public interface IAurPackageManager
{
    Task Initialize(bool root = false);

    Task<List<AurPackageDto>> GetInstalledPackages();
    Task<List<AurPackageDto>> SearchPackages(string query);
    
    Task<List<AurUpdateDto>> GetPackagesNeedingUpdate();
    
    Task UpdatePackages(List<string> packageNames);
    
    Task InstallPackages(List<string> packageNames);
    
    Task RemovePackages(List<string> packageNames);
    
}