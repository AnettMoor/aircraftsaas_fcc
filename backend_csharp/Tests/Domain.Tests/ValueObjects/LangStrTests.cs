using Base.Domain;
using FluentAssertions;

namespace Domain.Tests.ValueObjects;

public class LangStrTests
{
    [Fact]
    public void Constructor_WithString_SetsDefaultCulture()
    {
        // Arrange & Act
        var langStr = new LangStr("Hello");

        // Assert
        langStr.ToString().Should().Be("Hello");
    }

    [Fact]
    public void Constructor_WithCultureAndValue_SetsSpecificCulture()
    {
        // Arrange & Act
        var langStr = new LangStr("Tere", "et");

        // Assert
        langStr.Translate("et").Should().Be("Tere");
    }

    [Fact]
    public void ImplicitConversion_FromString_CreatesLangStr()
    {
        // Arrange & Act
        LangStr langStr = "Test value";

        // Assert
        langStr.Should().NotBeNull();
        langStr.ToString().Should().Be("Test value");
    }

    [Fact]
    public void ImplicitConversion_ToString_ReturnsDefaultCulture()
    {
        // Arrange
        var langStr = new LangStr("Test value");

        // Act
        string result = langStr;

        // Assert
        result.Should().Be("Test value");
    }

    [Fact]
    public void SetTranslation_AddsNewCulture()
    {
        // Arrange
        var langStr = new LangStr("Hello");

        // Act
        langStr.SetTranslation("Tere", "et");

        // Assert
        langStr.Translate("et").Should().Be("Tere");
        langStr.ToString().Should().Be("Hello"); // default unchanged
    }

    [Fact]
    public void SetTranslation_OverwritesExistingCulture()
    {
        // Arrange
        var langStr = new LangStr("Hello");
        langStr.SetTranslation("Old", "et");

        // Act
        langStr.SetTranslation("New", "et");

        // Assert
        langStr.Translate("et").Should().Be("New");
    }

    [Fact]
    public void Translate_NonExistentCulture_ReturnsFallback()
    {
        // Arrange
        var langStr = new LangStr("Fallback");

        // Act
        var result = langStr.Translate("fr");

        // Assert — should return some value (default/fallback behavior)
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ToString_EmptyLangStr_ReturnsFallbackMarker()
    {
        // Arrange
        var langStr = new LangStr();

        // Act
        var result = langStr.ToString();

        // Assert — LangStr returns "????" as fallback for missing translations
        result.Should().Be("????");
    }

    [Fact]
    public void MultipleTranslations_AllStoredCorrectly()
    {
        // Arrange
        var langStr = new LangStr("Hello");

        // Act
        langStr.SetTranslation("Tere", "et");
        langStr.SetTranslation("Hallo", "de");
        langStr.SetTranslation("Bonjour", "fr");

        // Assert
        langStr.Translate("et").Should().Be("Tere");
        langStr.Translate("de").Should().Be("Hallo");
        langStr.Translate("fr").Should().Be("Bonjour");
    }

    [Fact]
    public void ImplicitConversion_NullString_CreatesEmptyLangStr()
    {
        // Arrange & Act
        LangStr langStr = (string?)null!;

        // Assert
        langStr.Should().NotBeNull();
    }
}
