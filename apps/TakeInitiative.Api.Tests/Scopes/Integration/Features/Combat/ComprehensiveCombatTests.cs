namespace TakeInitiative.Api.Tests.Integration;
public class ComprehensiveCombatTests(WebAppWithDatabaseFixture fixture) : IClassFixture<WebAppWithDatabaseFixture>
{
    [Fact]
    public async Task AttemptToRetrieveContentButUnauthenticated()
    {

        await fixture.AlbaHost.Scenario(_ =>
        {
            _.Get.Url("/api/user");
            _.StatusCodeShouldBe(401);
        });
    }
}