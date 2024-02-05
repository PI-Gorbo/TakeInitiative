using TakeInitiative.Api.Models;

namespace TakeInitiative.Api.Controllers;

public record PlayerCharacterDTO : ICharacter
{
    public required string Name { get; set; }
    public CharacterHealth? Health { get; set; } = null;
    public required CharacterInitiative Initiative { get; set; }
    public int? ArmorClass { get; set; } = null;
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