using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Api.Controllers;
internal class Program
{
	private static void Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);

		// Add services to the container.
		// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
		builder.Services.AddEndpointsApiExplorer();
		builder.Services.AddSwaggerGen();
		builder.Services.AddHealthChecks();
		builder.Services.AddFastEndpoints();
		builder.Services.AddSignalR();

		// Custom Injection
		builder.AddMartenDB();
		builder.AddSerilog();
		builder.AddIdentityAuthenticationAndAuthorization();
		builder.AddOptionObjects();

		// Cors
		var cors = (builder.Configuration.GetValue<string>("CORS") ?? throw new MissingMemberException("Missing configuration for value 'CORS'."))
						.Split(";").ToArray();

		builder.Services.AddCors(opts => opts.AddPolicy("ApiCORS", corsBuilder => corsBuilder
				.WithOrigins(cors)
				.AllowAnyHeader()
				.AllowAnyMethod()
				.AllowCredentials()
			));
			
		var app = builder.Build();

		// Configure the HTTP request pipeline.
		if (app.Environment.IsDevelopment())
		{
			app.UseSwagger();
			app.UseSwaggerUI();
		}

		// Map SignalR Hubs
		app.MapHub<CombatHub>("/combatHub");

		app.UseCors("ApiCORS")
			.UseFastEndpoints()
			.UseAuthentication()
			.UseAuthorization();

		app.UseHealthChecks("/healthz");
		app.Run();
	}
}