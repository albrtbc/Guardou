using Guardou.Domain.Files;

namespace Guardou.UnitTests.Domain;

public sealed class ItemNameTests
{
    [Theory]
    [InlineData("report.txt")]
    [InlineData(".env")]
    [InlineData("my files")]
    [InlineData("relatório 2026")]
    public void AcceptsNames(string name)
    {
        Assert.True(ItemName.IsValid(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("a\b")]
    [InlineData(" padded")]
    [InlineData("padded ")]
    [InlineData("tab\there")]
    public void RefusesNames(string name)
    {
        Assert.False(ItemName.IsValid(name));
    }

    [Fact]
    public void NormalizesNamesToNfc()
    {
        Assert.Equal("relat\u00f3rio", ItemName.TryNormalize("relato\u0301rio"));
        Assert.Null(ItemName.TryNormalize("broken\ud800"));
    }

    [Fact]
    public void RefusesLongNames()
    {
        Assert.False(ItemName.IsValid(new string('a', ItemName.MaximumLength + 1)));
    }

    [Theory]
    [InlineData("report.txt", "report (2).txt")]
    [InlineData("archive.tar.gz", "archive.tar (2).gz")]
    [InlineData("folder", "folder (2)")]
    [InlineData(".env", ".env (2)")]
    public void NumbersNames(string name, string expected)
    {
        Assert.Equal(expected, ItemName.Numbered(name, 2));
    }
}
