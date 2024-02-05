using CSharpFunctionalExtensions;

namespace TakeInitiative.Data.Commands;
public class DeleteCampaignRequest 
{
    public required Guid UserId { get; set; }
    public required Guid CampaignId { get; set; }
}