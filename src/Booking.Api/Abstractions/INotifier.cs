namespace Booking.Api.Abstractions;

// Tells a user something happened (a request to approve, an approval, a rejection).
// The app has an in-app implementation; email or Teams would be another, chosen in Program.cs.
public interface INotifier
{
    // Adds the message to the current unit of work; it is saved with the change that caused it.
    void Notify(Guid userId, string message);
}
