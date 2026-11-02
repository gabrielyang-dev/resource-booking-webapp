namespace Booking.IntegrationTests;

// Inherit from this: the database is emptied before every test.
[Collection(ApiCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected ApiFactory Factory { get; }

    protected IntegrationTestBase(ApiFactory factory)
    {
        Factory = factory;
    }

    public Task InitializeAsync() => TestDatabase.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}
