namespace Booking.Api.Domain;

// One reason a booking is not allowed, in plain language that says how to fix it.
public record Violation(string Code, string Message);
