using ShortUrl.Application.ShortLinks.Commands.CreateShortLink;
using ShortUrl.Application.Validators;

namespace ShortUrl.Application.UnitTests.Validators;

public class ExpirationValidationTests
{
    private readonly CreateShortLinkCommandValidator _validator = new();
    private const string ValidTargetUrl = "https://learn.microsoft.com/dotnet";

    [Fact]
    public void Validate_WhenExpiresAtUtcIsNull_ShouldSucceed()
    {
        var command = new CreateShortLinkCommand(ValidTargetUrl, ExpiresAtUtc: null);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(365)]
    public void Validate_WhenExpiresAtUtcIsInTheFuture_ShouldSucceed(int daysInFuture)
    {
        var futureDate = DateTime.UtcNow.AddDays(daysInFuture);
        var command = new CreateShortLinkCommand(ValidTargetUrl, ExpiresAtUtc: futureDate);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WhenExpiresAtUtcIsToday_ShouldSucceedBecauseEndOfDayIsFuture()
    {
        var today = DateTime.UtcNow.Date;
        var command = new CreateShortLinkCommand(ValidTargetUrl, ExpiresAtUtc: today);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(365)]
    public void Validate_WhenExpiresAtUtcIsInThePast_ShouldFailWithPortugueseErrorMessage(int daysInPast)
    {
        var pastDate = DateTime.UtcNow.AddDays(-daysInPast);
        var command = new CreateShortLinkCommand(ValidTargetUrl, ExpiresAtUtc: pastDate);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateShortLinkCommand.ExpiresAtUtc) &&
            e.ErrorMessage.Contains("A data de expiração deve ser uma data futura em UTC.", StringComparison.Ordinal));
    }
}

