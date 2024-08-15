using CSharpFunctionalExtensions;
using Microsoft.Extensions.Primitives;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Combats;
using TakeInitiative.Api.Features.Users;
namespace TakeInitiative.Api.Tests.Integration;

public static class WebAppClientExtensions
{
    private static Task<Result<TResponse>> Post<TRequest, TResponse>(this IWebAppClient client, TRequest req, string url, int statusCode = 200) where TRequest : class
        => Result.Try(async () =>
            {
                var result = await client.AlbaHost.Scenario(_ =>
                {
                    _.Post.Json(req).ToUrl(url);
                    _.StatusCodeShouldBe(statusCode);
                });
                return await result.ReadAsJsonAsync<TResponse>() ?? throw new InvalidCastException($"Could not cast response to type of {typeof(TResponse).Name}");
            });

    private static Task<Result<TResponse>> Put<TRequest, TResponse>(this IWebAppClient client, TRequest req, string url, int statusCode = 200) where TRequest : class
        => Result.Try(async () =>
            {
                var result = await client.AlbaHost.Scenario(_ =>
                {
                    _.Put.Json(req).ToUrl(url);
                    _.StatusCodeShouldBe(statusCode);
                });
                return await result.ReadAsJsonAsync<TResponse>() ?? throw new InvalidCastException($"Could not cast response to type of {typeof(TResponse).Name}");
            });

    public static Task<Result<GetUserResponse>> GetUser(this IWebAppClient client)
        => Result.Try(async () =>
            {
                var result = await client.AlbaHost.Scenario(_ =>
                {
                    _.Get.Url("/api/user");
                    _.StatusCodeShouldBe(200);
                });

                return await result.ReadAsJsonAsync<GetUserResponse>() ?? throw new InvalidCastException($"Could not cast response to type of {typeof(GetUserResponse).Name}");
            });

    public static Task<Result<StringValues>> SignUp(this IWebAppClient client, PostSignUpRequest request)
        => Result.Try(async () =>
            {
                var result = await client.AlbaHost.Scenario(_ =>
                {
                    _.Post.Json(request).ToUrl("/api/signup");
                    _.StatusCodeShouldBe(200);
                });
                var cookie = result.Context.Response.Headers.SetCookie;
                return cookie;
            });

    public static Task<Result<Campaign>> PostCreateCampaign(this IWebAppClient client, PostCreateCampaignRequest request)
        => client.Post<PostCreateCampaignRequest, Campaign>(request, "/api/campaign");

    public static Task<Result<PlannedCombat>> PostPlannedCombat(this IWebAppClient client, PostPlannedCombatRequest request)
        => client.Post<PostPlannedCombatRequest, PlannedCombat>(request, "/api/combat/planned");

    public static Task<Result<PlannedCombat>> PostPlannedCombatNpc(this IWebAppClient client, PostPlannedCombatNpcRequest request)
        => client.Post<PostPlannedCombatNpcRequest, PlannedCombat>(request, "/api/campaign/planned-combat/stage/npc");

    public static Task<Result<PlannedCombat>> PostPlannedCombatStage(this IWebAppClient client, PostPlannedCombatStageRequest request)
        => client.Post<PostPlannedCombatStageRequest, PlannedCombat>(request, "/api/campaign/planned-combat/stage");

    public static Task<Result<CombatResponse>> PostOpenCombat(this IWebAppClient client, PostOpenCombatRequest request)
         => client.Post<PostOpenCombatRequest, CombatResponse>(request, "/api/combat/open");

    public static Task<Result<CombatResponse>> PostStagePlannedCharacters(this IWebAppClient client, PutStagePlannedCharactersRequest request)
         => client.Post<PutStagePlannedCharactersRequest, CombatResponse>(request, "/api/combat/stage/planned-character");

    public static Task<Result<CombatResponse>> PostStartCombat(this IWebAppClient client, PostStartCombatRequest request)
         => client.Post<PostStartCombatRequest, CombatResponse>(request, "/api/combat/start");

    public static Task<Result<CombatResponse>> PostEndTurn(this IWebAppClient client, PostEndTurnRequest request)
     => client.Post<PostEndTurnRequest, CombatResponse>(request, "/api/combat/turn/end");

    public static Task<Result<PlannedCombat>> PutPlannedCombatNpc(this IWebAppClient client, PutPlannedCombatNpcRequest request)
        => client.Put<PutPlannedCombatNpcRequest, PlannedCombat>(request, "/api/campaign/planned-combat/stage/npc");

    public static Task<Result<CombatResponse>> PutUpsertStagedCharacter(this IWebAppClient client, PutUpsertStagedCharacterRequest request)
        => client.Put<PutUpsertStagedCharacterRequest, CombatResponse>(request, "/api/combat/stage/character");

    public static Task<Result<CombatResponse>> PutUpdateInitiativeCharacter(this IWebAppClient client, PutUpdateInitiativeCharacterRequest request)
    => client.Put<PutUpdateInitiativeCharacterRequest, CombatResponse>(request, "/api/combat/initiative/character");

}