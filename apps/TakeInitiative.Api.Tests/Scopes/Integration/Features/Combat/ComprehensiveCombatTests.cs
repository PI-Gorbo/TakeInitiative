namespace TakeInitiative.Api.Tests.Integration;
public class ComprehensiveCombatTests(AuthenticatedWebAppWithDatabaseFixture fixture) : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
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