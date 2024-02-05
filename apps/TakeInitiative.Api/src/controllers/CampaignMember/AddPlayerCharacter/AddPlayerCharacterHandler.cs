using FastEndpoints;
using Marten;
using TakeInitiative.Api.Models;

namespace TakeInitiative.Api.Controllers;
public record AddPlayerCharacterRequest
{
    public required Guid MemberId { get; set; }
	public required PlayerCharacterDTO PlayerCharacter {get; set;}
}

public class PostAddPlayerCharacter(IDocumentStore Store) : Endpoint<AddPlayerCharacterRequest, CampaignMember> {
	public override void Configure()
	{
		Post("/api/campaign-member/add-character");
	}

	public override Task HandleAsync(AddPlayerCharacterRequest req, CancellationToken ct)
	{
		// Get the user's id.
		// var updateResult = await Store.Try(async session => {
		// 	var campaignMember = 
		// });
		return base.HandleAsync(req, ct);
	}
}


// using CSharpFunctionalExtensions;
// using MediatR;

// namespace TakeInitiative.Data.Commands;

// public class AddPlayerCharacterHandler : IRequestHandler<AddPlayerCharacterRequest, Result>
// {
//     public Task<Result> Handle(AddPlayerCharacterRequest request, CancellationToken cancellationToken)
//     {
//         throw new NotImplementedException();
//         /*
//                     .Ensure(() => this.CurrentUserMemberInfo != null, "MemberInfo cannot be null.")
//             .Ensure(() => character != null, "Player character cannot be null.")
//             .Ensure(() => !this.CurrentUserMemberInfo.Characters.Select(x => x.Name).Contains(character.Name), "You already have a character with that name.")
//             .Tap(() =>
//             {
//                 this.MemberInfoUpdateBuilder.WithUpdate(c => c.Characters, c => c.Characters.AddImmutable(character));
//                 if (this.CurrentUserMemberInfo != null && this.CurrentUserMemberInfo.Characters.Count() == 1)
//                 {
//                     this.MemberInfoUpdateBuilder.WithUpdate(c => c.CurrentCharacterId, c => character.Id);
//                 }
//             }).Bind(async () => await this.MemberInfoUpdateBuilder.SaveChanges(this.Store))
//         */
//     }
// }