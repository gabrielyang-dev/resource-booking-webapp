using Booking.Api.Features.Health;

namespace Booking.UnitTests;

public class HealthResponseTests
{
    [Fact]
    public void Healthy_response_carries_api_and_database_status()
    {
        var response = new HealthResponse("healthy", "connected");

        Assert.Equal("healthy", response.Api);
        Assert.Equal("connected", response.Database);
    }
}
