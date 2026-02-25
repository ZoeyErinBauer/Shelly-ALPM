using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using PackageManager.Aur;
using PackageManager.Aur.Models;
using NUnit.Framework;

namespace PackageManager.Tests.Aur;

[TestFixture]
public class AurSearchManagerTests
{
    private AurSearchManager _manager;
    private HttpClient _httpClient;
    private string _testCachePath;

    [SetUp]
    public void SetUp()
    {
        _httpClient = new HttpClient();
        _manager = new AurSearchManager(_httpClient);
        
        var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "shelly");
        _testCachePath = Path.Combine(configPath, "aur-packages.json");
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient.Dispose();
        _manager?.Dispose();
        
        if (File.Exists(_testCachePath + ".bak"))
        {
            File.Move(_testCachePath + ".bak", _testCachePath, true);
        }
        else if (File.Exists(_testCachePath))
        {
            File.Delete(_testCachePath);
        }
    }

    [Test]
    public async Task SearchAsync_ShouldUseCacheIfExists()
    {
        // Arrange
        if (File.Exists(_testCachePath))
        {
            File.Move(_testCachePath, _testCachePath + ".bak", true);
        }
        
        var cachedPackages = new List<AurPackageDto>
        {
            new AurPackageDto { Name = "test-package-123", Description = "A test package" }
        };
        
        Directory.CreateDirectory(Path.GetDirectoryName(_testCachePath)!);
        await File.WriteAllTextAsync(_testCachePath, JsonSerializer.Serialize(cachedPackages, AurJsonContext.Default.ListAurPackageDto));
        
        // Act
        var response = await _manager.SearchAsync("test-package-123");
        
        // Assert
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Results, Is.Not.Null);
        Assert.That(response.Results.Count, Is.EqualTo(1));
        Assert.That(response.Results[0].Name, Is.EqualTo("test-package-123"));
    }

    // [Test]
    // public async Task SearchAsync_ShouldReturnResults()
    // {
    //     // Act
    //     var response = await _manager.SearchAsync("visual-studio-code-bin");
    //
    //     // Assert
    //     Assert.That(response, Is.Not.Null);
    //     Assert.That(response.Type, Is.EqualTo("search"));
    //     Assert.That(response.Results, Is.Not.Null);
    // }

    // [Test]
    // public async Task GetInfoAsync_ShouldReturnDetailedResults()
    // {
    //     // Act
    //     var response = await _manager.GetInfoAsync(["visual-studio-code-bin", "google-chrome"]);
    //
    //     // Assert
    //     Assert.That(response, Is.Not.Null);
    //     Assert.That(response.Type, Is.EqualTo("multiinfo"));
    //     Assert.That(response.Results, Is.Not.Null);
    //     Assert.That(response.Results.Count, Is.GreaterThanOrEqualTo(1));
    // }
}