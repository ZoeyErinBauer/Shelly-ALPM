using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PackageManager.Alpm;

namespace PackageManager.ChaoticAur;

/// <summary>
/// Manages interactions with the chaotic aur stored inside the shelly.conf
/// This is created by the cli and managed by that.
/// It is functionally a second standard library 
/// </summary>
public interface IChaoticAurManager : IDisposable
{
    Task<List<AlpmPackageDto>> GetInstalledPackages();
    

}