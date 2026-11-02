using Booking.Api.Features.Catalog;

namespace Booking.UnitTests;

public class CatalogTests
{
    [Theory]
    [InlineData("FK_Resources_Locations_LocationId", "locationId")]
    [InlineData("FK_Resources_ResourceTypes_ResourceTypeId", "resourceTypeId")]
    public void A_foreign_key_violation_names_the_field_it_came_from(string constraint, string field)
    {
        Assert.Equal(field, ResourcesManager.FieldForForeignKey(constraint));
    }

    [Theory]
    [InlineData("Europe/London")]
    [InlineData("UTC")]
    [InlineData("Etc/UTC")]
    [InlineData("America/New_York")]
    [InlineData("Asia/Kolkata")]
    public void Iana_ids_spelled_exactly_are_accepted(string id)
    {
        Assert.True(TimeZoneIds.IsIana(id));
    }

    [Theory]
    [InlineData("europe/london")]
    [InlineData("EUROPE/LONDON")]
    [InlineData("GMT Standard Time")]
    [InlineData("gmt standard time")]
    [InlineData("Pacific Standard Time")]
    [InlineData("pacific standard time")]
    [InlineData("Mars/Olympus")]
    [InlineData("")]
    public void Wrong_case_windows_and_unknown_ids_are_rejected(string id)
    {
        // Windows .NET looks ids up ignoring case depending on what it has already cached, so the same
        // ids are checked several times, in different orders, to prove the answer never changes.
        for (var i = 0; i < 3; i++)
        {
            Assert.False(TimeZoneIds.IsIana(id));
            Assert.True(TimeZoneIds.IsIana("Europe/London"));
        }
    }

    // No other test touches these two ids, so this is the first time the process sees them. Windows .NET
    // then finds a zone whose Id is the lowercase text itself, which a Windows-id check by name misses.
    [Fact]
    public void Lowercase_windows_ids_seen_for_the_first_time_are_rejected()
    {
        Assert.False(TimeZoneIds.IsIana("tokyo standard time"));
        Assert.False(TimeZoneIds.IsIana("w. europe standard time"));
    }
}
