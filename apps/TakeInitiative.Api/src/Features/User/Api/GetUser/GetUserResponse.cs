using CSharpFunctionalExtensions;
using Marten;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Users;

/// <summary>
/// The signed-in user. Their campaigns come from <c>GET /api/campaigns</c>, which reads
/// the Campaign projection; the user document no longer stores membership.
/// </summary>
public class GetUserResponse
{
	public required Guid UserId { get; set; }
	public required string Username { get; set; }
	public required bool ConfirmedEmail { get; set; }

	public static async Task<Result<GetUserResponse, ApiError>> Generate(IDocumentSession session, Guid userId)
	{
		return await Result.Try(
				async () => await session.LoadAsync<ApplicationUser>(userId),
				err => ApiError.DbInteractionFailed(err.Message))
			.Ensure(user => user is not null, ApiError.NotFound("There is no user with the given user id."))
			.Map(user => new GetUserResponse()
			{
				UserId = user!.Id,
				Username = user.UserName!,
				ConfirmedEmail = user.EmailConfirmed,
			});
	}
}
