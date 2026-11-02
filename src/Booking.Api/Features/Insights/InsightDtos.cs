namespace Booking.Api.Features.Insights;

// Utilisation is a fraction from 0 to 1 (booked hours / open hours).
// CheckedInHours sits next to BookedHours: the gap is time held but not used.
// NoShowHours are bookings released because nobody checked in.
public record ResourceUtilisation(
    Guid ResourceId, string ResourceName, Guid ResourceTypeId, string ResourceTypeName, string LocationName,
    double BookedHours, double CheckedInHours, double NoShowHours, double OpenHours, double Utilisation);

public record TypeUtilisation(
    Guid ResourceTypeId, string Name, double BookedHours, double CheckedInHours, double NoShowHours, double OpenHours, double Utilisation);

// From and To are local dates; To is included.
public record UtilisationResponse(DateOnly From, DateOnly To, IReadOnlyList<ResourceUtilisation> Resources, IReadOnlyList<TypeUtilisation> Types);
