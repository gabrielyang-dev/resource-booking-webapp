using Booking.Api.Domain;

namespace Booking.Api.Features.AddOns;

// Times are local "HH:mm" at the add-on's location.
public record AddOnRequest(
    Guid? LocationId, string? Name, int? LeadTimeMinutes, string? AvailableFrom, string? AvailableUntil,
    Guid? FulfillerId, bool? Active);

public record AddOnResponse(
    Guid Id, Guid LocationId, string LocationName, string Name, int LeadTimeMinutes,
    string AvailableFrom, string AvailableUntil, Guid? FulfillerId, bool Active)
{
    // The add-on must have been loaded with its Location.
    public static AddOnResponse From(AddOn a) => new(
        a.Id, a.LocationId, a.Location.Name, a.Name, a.LeadTimeMinutes,
        a.AvailableFrom.ToString("HH:mm"), a.AvailableUntil.ToString("HH:mm"), a.FulfillerId, a.IsActive);
}
