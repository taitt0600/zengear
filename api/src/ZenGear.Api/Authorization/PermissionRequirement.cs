using Microsoft.AspNetCore.Authorization;

namespace ZenGear.Api.Authorization;

public sealed record PermissionRequirement(string Key) : IAuthorizationRequirement;
