using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace TakeInitiative.Api.Controllers;


public class Test : Endpoint<EmptyRequest, EmptyResponse>
{
	public override void Configure()
	{
		Post("/api/test");
		AuthSchemes(JwtBearerDefaults.AuthenticationScheme);	
		Policies(TakePolicies.ValidUser);
	}
	public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
	{
		await SendOkAsync();
	}
}