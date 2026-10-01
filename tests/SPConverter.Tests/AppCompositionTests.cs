using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace SPConverter.Tests;

public class AppCompositionTests
{
    [Fact]
    public void App_ShouldKeepMassConvertViewModelAliveAcrossNavigation()
    {
        string appCode = LoadAppCode();

        appCode.Should().Contain("services.AddSingleton<MassConvertViewModel>()");
        appCode.Should().NotContain("services.AddTransient<MassConvertViewModel>()");
    }

    private static string LoadAppCode()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string appCodePath = Path.GetFullPath(
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "src", "App.xaml.cs"));

        Assert.True(File.Exists(appCodePath), $"Could not find App.xaml.cs at {appCodePath}");

        return File.ReadAllText(appCodePath);
    }
}
