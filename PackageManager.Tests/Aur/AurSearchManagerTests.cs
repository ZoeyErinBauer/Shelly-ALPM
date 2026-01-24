using System.Net.Http;
using System.Threading.Tasks;
using PackageManager.Aur;
using NUnit.Framework;

namespace PackageManager.Tests.Aur;

[TestFixture]
public class AurSearchManagerTests
{
    private AurSearchManager _manager;
    private HttpClient _httpClient;

    [SetUp]
    public void SetUp()
    {
        _httpClient = new HttpClient();
        _manager = new AurSearchManager(_httpClient);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient.Dispose();
    }

    [Test]
    public async Task SearchAsync_ShouldReturnResults()
    {
        // Act
        var response = await _manager.SearchAsync("visual-studio-code-bin");

        // Assert
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Type, Is.EqualTo("search"));
        Assert.That(response.Results, Is.Not.Null);
    }

    [Test]
    public async Task GetInfoAsync_ShouldReturnDetailedResults()
    {
        // Act
        var response = await _manager.GetInfoAsync(["visual-studio-code-bin", "google-chrome"]);

        // Assert
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Type, Is.EqualTo("multiinfo"));
        Assert.That(response.Results, Is.Not.Null);
        Assert.That(response.Results.Count, Is.GreaterThanOrEqualTo(1));
    }
}
