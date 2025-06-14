using CSharpFunctionalExtensions;
using Microsoft.Extensions.Primitives;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Combats;
using TakeInitiative.Api.Features.Users;
namespace TakeInitiative.Api.Tests.Integration;

public static class WebAppClientExtensions
{

    private static Task<Result<TResponse>> Delete<TRequest, TResponse>(this IWebAppClient client, TRequest req, string url, int statusCode = 200) where TRequest : class
    => Result.Try(async () =>
        {
            var result = await client.AlbaHost.Scenario(_ =>
            {
                _.Delete.Json(req).ToUrl(url);
                _.StatusCodeShouldBe(statusCode);
            });
            return await result.ReadAsJsonAsync<TResponse>() ?? throw new InvalidCastException($"Could not cast response to type of {typeof(TResponse).Name}");
        });

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
    public static Task<Result<CombatResponse>> PostOpenCombat(this IWebAppClient client, PostStartCombatRequest request)
        => client.Post<PostStartCombatRequest, CombatResponse>(request, "/api/combat/start");
    public static Task<Result<CombatResponse>> PostStagePlannedCharacters(this IWebAppClient client, PostStagePlannedCharactersRequest request)
        => client.Post<PostStagePlannedCharactersRequest, CombatResponse>(request, "/api/combat/stage/planned-character");
    public static Task<Result<CombatResponse>> PostRollCombatInitiative(this IWebAppClient client, PostRollCombatInitiativeRequest request)
        => client.Post<PostRollCombatInitiativeRequest, CombatResponse>(request, "/api/combat/roll-initiative");
    public static Task<Result<CombatResponse>> PostFinishCombat(this IWebAppClient client, PostFinishCombatRequest request)
        => client.Post<PostFinishCombatRequest, CombatResponse>(request, "/api/combat/finish");
    public static Task<Result<CombatResponse>> PostEndTurn(this IWebAppClient client, PostEndTurnRequest request)
        => client.Post<PostEndTurnRequest, CombatResponse>(request, "/api/combat/turn/end");
    public static Task<Result<CombatResponse>> PostStagePlayerCharacters(this IWebAppClient client, PostStagePlayerCharactersRequest request)
        => client.Post<PostStagePlayerCharactersRequest, CombatResponse>(request, "/api/combat/stage/player-character");
    public static Task<Result<CombatResponse>> PostRollStagedCharactersIntoInitiative(this IWebAppClient client, PostRollStagedCharactersIntoInitiativeRequest request)
        => client.Post<PostRollStagedCharactersIntoInitiativeRequest, CombatResponse>(request, "/api/combat/stage/roll");
    public static Task<Result<CombatResponse>> PostStageCharacter(this IWebAppClient client, PostAddStagedCharacterRequest request)
    => client.Post<PostAddStagedCharacterRequest, CombatResponse>(request, "/api/combat/stage/character");
    public static Task<Result<PlannedCombat>> PutPlannedCombatNpc(this IWebAppClient client, PutPlannedCombatNpcRequest request)
        => client.Put<PutPlannedCombatNpcRequest, PlannedCombat>(request, "/api/campaign/planned-combat/stage/npc");
    public static Task<Result<CombatResponse>> PutUpsertStagedCharacter(this IWebAppClient client, PutUpdateStagedCharacterRequest request)
        => client.Put<PutUpdateStagedCharacterRequest, CombatResponse>(request, "/api/combat/stage/character");
    public static Task<Result<CombatResponse>> PutUpdateInitiativeCharacter(this IWebAppClient client, PutUpdateInitiativeCharacterRequest request)
        => client.Put<PutUpdateInitiativeCharacterRequest, CombatResponse>(request, "/api/combat/initiative/character");
    public static Task<Result<CombatResponse>> DeleteInitiativeCharacter(this IWebAppClient client, DeleteInitiativeCharacterRequest request)
        => client.Delete<DeleteInitiativeCharacterRequest, CombatResponse>(request, "/api/combat/initiative/character");

}