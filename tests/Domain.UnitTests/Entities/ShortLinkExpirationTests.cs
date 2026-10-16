using ShortUrl.Domain.Entities;

namespace ShortUrl.Domain.UnitTests.Entities;

public class ShortLinkExpirationTests
{
    [Fact]
    public void CalculateDefaultExpiration_GivenCreatedAtUtc_ShouldReturnTenYearsLater()
    {
        var createdAtUtc = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

        var expiresAtUtc = ShortLink.CalculateDefaultExpiration(createdAtUtc);

        var expected = createdAtUtc.AddYears(10);
        Assert.Equal(expected, expiresAtUtc);
        Assert.Equal(DateTimeKind.Utc, expiresAtUtc.Kind);
    }

    [Theory]
    [InlineData(2027, 5, 20, 0, 0, 0)]
    [InlineData(2027, 5, 20, 15, 30, 45)]
    [InlineData(2027, 5, 20, 23, 59, 59)]
    public void NormalizeExpirationDate_GivenAnyTimeOfDay_ShouldNormalizeToEndOfDayUtc(
        int year, int month, int day, int hour, int minute, int second)
    {
        var inputDate = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);

        var normalized = ShortLink.NormalizeExpirationDate(inputDate);

        Assert.Equal(DateTimeKind.Utc, normalized.Kind);
        Assert.Equal(year, normalized.Year);
        Assert.Equal(month, normalized.Month);
        Assert.Equal(day, normalized.Day);
        Assert.Equal(23, normalized.Hour);
        Assert.Equal(59, normalized.Minute);
        Assert.Equal(59, normalized.Second);
        Assert.Equal(999, normalized.Millisecond);
        Assert.Equal(9_999_999, normalized.Ticks % TimeSpan.TicksPerSecond);
    }

    [Fact]
    public void NormalizeExpirationDate_GivenNonUtcKind_ShouldConvertAndNormalizeToEndOfDayUtc()
    {
        var localDate = new DateTime(2028, 1, 15, 10, 0, 0, DateTimeKind.Local);
        var expectedUtcDate = localDate.ToUniversalTime().Date;

        var normalized = ShortLink.NormalizeExpirationDate(localDate);

        Assert.Equal(DateTimeKind.Utc, normalized.Kind);
        Assert.Equal(expectedUtcDate.Year, normalized.Year);
        Assert.Equal(expectedUtcDate.Month, normalized.Month);
        Assert.Equal(expectedUtcDate.Day, normalized.Day);
        Assert.Equal(23, normalized.Hour);
        Assert.Equal(59, normalized.Minute);
        Assert.Equal(59, normalized.Second);
    }

    [Fact]
    public void IsExpired_WhenCurrentTimeIsBeforeExpiration_ShouldReturnFalse()
    {
        var link = new ShortLink
        {
            ExpiresAtUtc = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc).AddTicks(9_999_999)
        };

        var nowUtc = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        Assert.False(link.IsExpired(nowUtc));
    }

    [Fact]
    public void IsExpired_WhenCurrentTimeIsExactlyExpiration_ShouldReturnFalse()
    {
        var exactExpiration = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc).AddTicks(9_999_999);
        var link = new ShortLink
        {
            ExpiresAtUtc = exactExpiration
        };

        Assert.False(link.IsExpired(exactExpiration));
    }

    [Fact]
    public void IsExpired_WhenCurrentTimeIsAfterExpiration_ShouldReturnTrue()
    {
        var exactExpiration = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc).AddTicks(9_999_999);
        var link = new ShortLink
        {
            ExpiresAtUtc = exactExpiration
        };

        var justAfterExpiration = exactExpiration.AddTicks(1);

        Assert.True(link.IsExpired(justAfterExpiration));
    }
}

