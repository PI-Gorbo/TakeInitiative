using Microsoft.AspNetCore.Authorization;

namespace TakeInitiative.Api.Bootstrap;

public class RequireUserToExistInDatabaseAuthorizationRequirement : IAuthorizationRequirement { }
