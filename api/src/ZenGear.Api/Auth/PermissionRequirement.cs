using Microsoft.AspNetCore.Authorization;

namespace ZenGear.Api.Auth;

public sealed record PermissionRequirement(string Key) : IAuthorizationRequirement;
