namespace Kyklos.Tests;

public sealed class UnpackedCopiesTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "kyklos-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    [Fact]
    public void AnUnpackedCopyLivesInTheDotNetFolderUnderTheExesName() =>
        Assert.Equal(@"C:\Temp\.net\kyklos", UnpackedCopies.Home(@"C:\Temp\.net\kyklos\aBc123\", @"D:\Apps\kyklos.exe"));

    [Fact]
    public void ARenamedExeHasItsOwnFolder() =>
        Assert.Equal(@"C:\Temp\.net\kyklos (1)", UnpackedCopies.Home(@"C:\Temp\.net\kyklos (1)\aBc123\", @"D:\Downloads\kyklos (1).exe"));

    [Theory]
    // A Debug or folder build runs where its exe is: there's no unpacked copy, and its neighbours are other builds.
    [InlineData(@"C:\src\kyklos\bin\Debug\net10.0-windows10.0.26100.0\win-x64\", @"C:\src\kyklos\bin\Debug\net10.0-windows10.0.26100.0\win-x64\kyklos.exe")]
    // Unpacked somewhere other than %TEMP%\.net (DOTNET_BUNDLE_EXTRACT_BASE_DIR), or under another app's name.
    [InlineData(@"D:\Unpacked\kyklos\aBc123\", @"D:\Apps\kyklos.exe")]
    [InlineData(@"C:\Temp\.net\something-else\aBc123\", @"D:\Apps\kyklos.exe")]
    public void AnythingElseIsLeftAlone(string baseDirectory, string processPath) =>
        Assert.Null(UnpackedCopies.Home(baseDirectory, processPath));

    [Fact]
    public void OnlyOtherCopiesNotWrittenToJustNowAreOld()
    {
        var now = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        (string, DateTime)[] copies =
        [
            (@"C:\Temp\.net\kyklos\running", now.AddDays(-3)),
            (@"C:\Temp\.net\kyklos\lastVersion", now.AddDays(-3)),
            (@"C:\Temp\.net\kyklos\beingUnpacked", now.AddMinutes(-2)),
        ];

        Assert.Equal([@"C:\Temp\.net\kyklos\lastVersion"], UnpackedCopies.Old(@"C:\Temp\.net\kyklos\running\", copies, now));
    }

    [Fact]
    public void DeletesEarlierVersionsAndKeepsTheRunningOne()
    {
        var home = Path.Combine(_temp, ".net", "kyklos");
        var running = Copy(home, "running", DateTime.UtcNow.AddDays(-1));
        var earlier = Copy(home, "earlier", DateTime.UtcNow.AddDays(-30));
        var fresh = Copy(home, "fresh", DateTime.UtcNow);
        var neighbour = Copy(Path.Combine(_temp, ".net"), "another-app", DateTime.UtcNow.AddDays(-30));

        UnpackedCopies.DeleteOld(running + Path.DirectorySeparatorChar, @"D:\Apps\kyklos.exe");

        Assert.True(Directory.Exists(running));
        Assert.False(Directory.Exists(earlier));
        Assert.True(Directory.Exists(fresh));
        Assert.True(Directory.Exists(neighbour));
    }

    [Fact]
    public void ADebugBuildDeletesNothing()
    {
        var bin = Path.Combine(_temp, "bin");
        var build = Copy(bin, "win-x64", DateTime.UtcNow.AddDays(-1));
        var other = Copy(bin, "win-arm64", DateTime.UtcNow.AddDays(-30));

        UnpackedCopies.DeleteOld(build + Path.DirectorySeparatorChar, Path.Combine(build, "kyklos.exe"));

        Assert.True(Directory.Exists(other));
    }

    /// <summary>A stand-in unpacked copy with a file in it, last written at the time given.</summary>
    private static string Copy(string home, string name, DateTime lastWritten)
    {
        var folder = Path.Combine(home, name);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "kyklos.dll"), "");
        Directory.SetLastWriteTimeUtc(folder, lastWritten);
        return folder;
    }
}
