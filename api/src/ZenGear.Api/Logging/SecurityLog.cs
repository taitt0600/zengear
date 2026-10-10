namespace ZenGear.Api.Logging;

/// <summary>
/// Structured security log of sign-in events (R-SEC-11): SUCCESS and REJECTED; not stored in the database (DEC-085).
/// Never pass a token, password or OTP.
/// </summary>
public sealed partial class SecurityLog(ILoggerFactory loggerFactory)
{
    public const string Category = "ZenGear.Security";

    public const string ResultSuccess = "SUCCESS";

    public const string ResultRejected = "REJECTED";

    private readonly ILogger _logger = loggerFactory.CreateLogger(Category);

    public void LoginSucceeded(Guid userId, DateTimeOffset authTime, string? ip, string? userAgent) =>
        LogLoginSucceeded(ResultSuccess, userId, authTime, ip, userAgent);

    /// <param name="userId">Null when the rejected token is not linked to a user.</param>
    public void LoginRejected(Guid? userId, string reason, string? ip, string? userAgent) =>
        LogLoginRejected(ResultRejected, userId, reason, ip, userAgent);

    [LoggerMessage(EventId = 1001, EventName = "LoginSucceeded", Level = LogLevel.Information,
        Message = "Login {Result} for user {UserId}, auth_time {AuthTime}, IP {Ip}, user agent {UserAgent}")]
    private partial void LogLoginSucceeded(string result, Guid userId, DateTimeOffset authTime, string? ip, string? userAgent);

    [LoggerMessage(EventId = 1002, EventName = "LoginRejected", Level = LogLevel.Warning,
        Message = "Login {Result} for user {UserId}: {Reason}, IP {Ip}, user agent {UserAgent}")]
    private partial void LogLoginRejected(string result, Guid? userId, string reason, string? ip, string? userAgent);
}
