using Booking.Api.Domain;

namespace Booking.Api.Features.Bookings;

// Start and end are ISO-8601 with an offset or Z; the server converts them to UTC.
// OwnerId lets an admin book for someone else; it is ignored for members.
// AddOns are ordered with the booking, for example 6 sandwiches.
public record BookingRequest(
    Guid? ResourceId, DateTimeOffset? Start, DateTimeOffset? End, int? Attendees,
    Guid? OwnerId = null, List<AddOnOrderRequest>? AddOns = null);

// Version is the one the client last read; a different stored version means someone else changed it.
// AddOns: leave it out to keep the current orders (they are checked again), or send the full new list.
public record RescheduleRequest(
    Guid? ResourceId, DateTimeOffset? Start, DateTimeOffset? End, int? Attendees, int? Version,
    List<AddOnOrderRequest>? AddOns = null);

public record AddOnOrderRequest(Guid? AddOnId, int? Quantity);

public record AddOnOrderResponse(Guid Id, Guid AddOnId, string Name, int Quantity, string Status)
{
    public static AddOnOrderResponse From(AddOnOrder o) => new(o.Id, o.AddOnId, o.AddOn.Name, o.Quantity, o.Status.ToString());
}

// Carries the resource, location and time zone so the UI can show local times without extra calls.
public record BookingResponse(
    Guid Id,
    Guid ResourceId,
    string ResourceName,
    string LocationName,
    string TimeZone,
    Guid OwnerId,
    string OwnerName,
    DateTime Start,
    DateTime End,
    string Status,
    int? Attendees,
    int Version,
    DateTime CreatedAt,
    IReadOnlyList<AddOnOrderResponse> AddOns)
{
    // The booking must have been loaded with its Resource (and Location) and Owner,
    // and with AddOnOrders (and their AddOn) where the orders should be shown.
    public static BookingResponse From(Domain.Booking b) => new(
        b.Id, b.ResourceId, b.Resource.Name, b.Resource.Location.Name, b.Resource.Location.TimeZone,
        b.OwnerId, b.Owner.Name, b.Start, b.End, b.Status.ToString(), b.Attendees, b.Version, b.CreatedAt,
        b.AddOnOrders.OrderBy(o => o.AddOn.Name).Select(AddOnOrderResponse.From).ToList());
}

// One row of a resource's day view.
public record ResourceBookingResponse(Guid Id, DateTime Start, DateTime End, string Status, string OwnerName)
{
    public static ResourceBookingResponse From(Domain.Booking b) =>
        new(b.Id, b.Start, b.End, b.Status.ToString(), b.Owner.Name);
}

// A suggestion returned with a rejection: book this resource at this time instead.
public record AlternativeResponse(Guid ResourceId, string ResourceName, DateTime Start, DateTime End);

// Created is false when an Idempotency-Key replay returned the stored booking.
public record CreateBookingResult(BookingResponse Booking, bool Created);
