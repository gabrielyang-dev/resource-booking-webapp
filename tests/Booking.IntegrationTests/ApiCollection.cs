namespace Booking.IntegrationTests;

// All integration tests share one database, so they share one collection:
// xUnit runs the tests in a collection one at a time.
[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}
