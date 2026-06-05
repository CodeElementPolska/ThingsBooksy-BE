using Xunit;

namespace ThingsBooksy.Modules.Calendar.IntegrationTests;

[CollectionDefinition(nameof(IntegrationTestCollection))]
public class IntegrationTestCollection : ICollectionFixture<CalendarWebAppFactory>
{
}
