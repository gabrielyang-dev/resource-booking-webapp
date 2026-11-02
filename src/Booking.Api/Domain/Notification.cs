namespace Booking.Api.Domain;

// A message for one user, shown under the bell in the app.
public class Notification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
