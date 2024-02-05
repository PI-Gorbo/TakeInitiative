using FastEndpoints;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddFastEndpoints();

// Custom Injection
builder.AddMartenDB();
builder.AddIdentityAuthenticationAndAuthorization();
builder.AddOptionObjects();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseFastEndpoints()
	.UseAuthentication()
	.UseAuthorization();

app.UseHealthChecks("/healthz");
app.Run();

