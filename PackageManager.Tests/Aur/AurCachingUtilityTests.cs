using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using NUnit.Framework;
using PackageManager.Aur;

namespace PackageManager.Tests.Aur;

[TestFixture]
[Ignore("Doesn't need to be ran unless on local")]
public class AurCachingUtilityTests
{
    private string _tempConfigPath;

    [SetUp]
    public void SetUp()
    {
        _tempConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "shelly");
        if (Directory.Exists(_tempConfigPath))
        {
            // Back up or just be careful. For tests, we might want to use a mockable path, 
            // but the current implementation uses Environment.GetFolderPath.
        }
    }

    
    [Test]
    [Ignore("This test makes a real network call and modifies the user's config directory. Run manually if needed.")]
    public void CacheAurPackages_RealCall_ShouldExtractJson()
    {
        // Act
        AurCachingUtility.CacheAurPackages();

        // Assert
        var filePath = Path.Combine(_tempConfigPath, "aur-packages.json");
        Assert.That(File.Exists(filePath), "JSON file was not extracted to the expected path.");
    }
    
}
