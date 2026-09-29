using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Shouldly;
using ZenGear.Api.Logging;

namespace ZenGear.Api.IntegrationTests.Logging;

public class SensitivePropertyMaskerTests
{
    [Test]
    public void Secret_properties_are_masked_and_others_are_kept()
    {
        var sink = new CollectingSink();
        using (var logger = CreateLogger(sink))
        {
            logger.Information("Sign-in {UserId} {Password} {IdToken} {OtpCode} {ApiKey}", 7, "p@ss", "eyJ...", "123456", "re_123");
        }

        var properties = sink.Events.ShouldHaveSingleItem().Properties;
        properties["UserId"].ToString().ShouldBe("7");
        foreach (var name in new[] { "Password", "IdToken", "OtpCode", "ApiKey" })
        {
            properties[name].ToString().ShouldBe($"\"{SensitivePropertyMasker.Mask}\"");
        }
    }

    [Test]
    public void R_SEC_11_LoginSucceeded_writes_structured_security_event()
    {
        var sink = new CollectingSink();
        var userId = Guid.CreateVersion7();
        var authTime = new DateTimeOffset(2026, 9, 29, 2, 0, 0, TimeSpan.Zero);
        using (var logger = CreateLogger(sink))
        using (var loggerFactory = new SerilogLoggerFactory(logger))
        {
            new SecurityLog(loggerFactory).LoginSucceeded(userId, authTime, "203.0.113.5", "Mozilla/5.0");
        }

        var logEvent = sink.Events.ShouldHaveSingleItem();
        logEvent.Properties["SourceContext"].ToString().ShouldBe($"\"{SecurityLog.Category}\"");
        logEvent.Properties["Result"].ToString().ShouldBe("\"SUCCESS\"");
        logEvent.Properties["UserId"].ToString().ShouldBe(userId.ToString());
        logEvent.Properties["AuthTime"].ShouldBeOfType<ScalarValue>().Value.ShouldBe(authTime);
        logEvent.Properties["Ip"].ToString().ShouldBe("\"203.0.113.5\"");
        logEvent.Properties["UserAgent"].ToString().ShouldBe("\"Mozilla/5.0\"");
    }

    [Test]
    public void R_SEC_11_LoginRejected_allows_unknown_user()
    {
        var sink = new CollectingSink();
        using (var logger = CreateLogger(sink))
        using (var loggerFactory = new SerilogLoggerFactory(logger))
        {
            new SecurityLog(loggerFactory).LoginRejected(null, "SESSION_REVOKED", "203.0.113.5", "Mozilla/5.0");
        }

        var logEvent = sink.Events.ShouldHaveSingleItem();
        logEvent.Level.ShouldBe(LogEventLevel.Warning);
        logEvent.Properties["Result"].ToString().ShouldBe("\"REJECTED\"");
        logEvent.Properties["UserId"].ToString().ShouldBe("null");
        logEvent.Properties["Reason"].ToString().ShouldBe("\"SESSION_REVOKED\"");
    }

    private static Logger CreateLogger(ILogEventSink sink) =>
        new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.With<SensitivePropertyMasker>()
            .WriteTo.Sink(sink)
            .CreateLogger();

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
