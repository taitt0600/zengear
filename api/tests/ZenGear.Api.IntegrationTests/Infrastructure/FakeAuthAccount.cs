namespace ZenGear.Api.IntegrationTests.Infrastructure;

/// <param name="Password">Null for an account created without a password.</param>
public sealed record FakeAuthAccount(string Uid, string Email, string? Password);
