using CSharpFunctionalExtensions;
using FluentAssertions;

namespace TakeInitiative.Api.Tests.Integration.Features.User;
public class SignInTests(WebAppWithDatabaseFixture fixture) : IClassFixture<WebAppWithDatabaseFixture>
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

    [Fact]
    public async Task CreateAnAccount()
    {
        var result = await fixture.SignUp(new()
        {
            Username = "Testing",
            Email = "testing@gmail.com",
            Password = "Testing!99",
        })
        .Bind(cookie =>
        {
            fixture.AlbaHost.BeforeEach(ctx => ctx.Request.Headers.Cookie = cookie);
            return fixture.GetUser();
        });
        result.Should().Succeed();
    }
}