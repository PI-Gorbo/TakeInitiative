using CSharpFunctionalExtensions;

namespace TakeInitiative.Data.Commands;
public class CreateCampaignRequest 
{
    public required string CampaignName { get; set; }
    /*
      var campaign = new Campaign()
            {
                Id = Guid.NewGuid(),
                OwnerId = this.User.Id,
                CampaignName = request.CampaignName
            };

            return await CanCreateCampaignWithName(campaign.CampaignName)
                .Bind(async () => await DbFactory.Try(async (dbContext) =>
                    {
                        dbContext.Add(campaign);
                        await dbContext.SaveChangesAsync();
                    }
                ))
    */
}