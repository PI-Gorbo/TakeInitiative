// using CSharpFunctionalExtensions;
// using MediatR;

// namespace TakeInitiative.Data.Commands;

// public class RemovePlayerCharacterHandler : IRequestHandler<AddPlayerCharacterRequest, Result>
// {
//     public Task<Result> Handle(AddPlayerCharacterRequest request, CancellationToken cancellationToken)
//     {
//         throw new NotImplementedException();
//         // .Ensure(() => this.CurrentUserMemberInfo != null, "MemberInfo cannot be null.")
//         // .Ensure(() => character != null, "Player character cannot be null.")
//         // .Tap(() =>
//         // {
//         //     this.MemberInfoUpdateBuilder.WithUpdate(c => c.Characters, c => c.Characters.RemoveImmutable(character));
//         //     if (this.CurrentUserMemberInfo.Characters.Count() == 0)
//         //     {
//         //         this.MemberInfoUpdateBuilder.WithUpdate(c => c.CurrentCharacterId, c => null);
//         //     }
//         //     else if (this.CurrentUserMemberInfo.CurrentCharacterId == character.Id)
//         //     {
//         //         this.MemberInfoUpdateBuilder.WithUpdate(c => c.CurrentCharacterId, c => this.CurrentUserMemberInfo.Characters.First().Id);
//         //     }
//         // }).Bind(async () => await this.MemberInfoUpdateBuilder.SaveChanges(this.Store))
//     }
// }