using FluentValidation.Results;

namespace TakeInitiative.Api.Controllers;


public class JoinCampaignByJoinCodeRequest
{
    public required string JoinCode { get; set; }
}
