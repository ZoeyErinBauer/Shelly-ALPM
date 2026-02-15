namespace PackageManager.Tests.AlpmReferenceTests;

using PackageManager;

public class LibraryIdentificationTests
{
    [Test]
    public void SuccessfullyResolvesAlpmLibrary()
    {
        var isAvailable = NativeResolver.IsLibraryAvailable(Alpm.AlpmReference.LibName);
        Assert.That(isAvailable, Is.True);
    }
    
}