using FluentValidation.Results;

namespace TakeInitiative.Data.Commands;


public class JoinCampaignByJoinCodeRequest
{
    public required string JoinCode { get; set; }
}
