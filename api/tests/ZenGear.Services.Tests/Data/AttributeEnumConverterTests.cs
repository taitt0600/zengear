using System.Text.Json;
using System.Text.Json.Serialization;
using Shouldly;
using ZenGear.Data.Common;
using ZenGear.Data.Entities.Identity.Enums;

namespace ZenGear.Services.Tests.Data;

public class AttributeEnumConverterTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

    [TestCase(UserStatus.Active, "ACTIVE")]
    [TestCase(UserStatus.Disabled, "DISABLED")]
    [TestCase(UserStatus.Anonymized, "ANONYMIZED")]
    public void Stored_value_is_the_JsonStringEnumMemberName(UserStatus status, string stored)
    {
        var converter = new AttributeEnumConverter<UserStatus>();

        converter.ConvertToProvider(status).ShouldBe(stored);
        converter.ConvertFromProvider(stored).ShouldBe(status);
    }

    [Test]
    public void Json_uses_the_same_value_as_the_database()
    {
        JsonSerializer.Serialize(UserStatus.Disabled, JsonOptions).ShouldBe("\"DISABLED\"");
        JsonSerializer.Deserialize<UserStatus>("\"ANONYMIZED\"", JsonOptions).ShouldBe(UserStatus.Anonymized);
    }

    [Test]
    public void Unknown_stored_value_throws_naming_the_enum_and_the_value()
    {
        var converter = new AttributeEnumConverter<UserStatus>();

        var ex = Should.Throw<InvalidOperationException>(() => converter.ConvertFromProvider("LOCKED"));

        ex.Message.ShouldContain(nameof(UserStatus));
        ex.Message.ShouldContain("LOCKED");
    }

    [Test]
    public void Member_without_the_attribute_is_rejected()
    {
        Should.Throw<InvalidOperationException>(() => new AttributeEnumConverter<MissingAttribute>())
            .Message.ShouldContain(nameof(MissingAttribute.NoName));
    }

    [Test]
    public void Members_with_the_same_value_are_rejected()
    {
        var ex = Should.Throw<InvalidOperationException>(() => new AttributeEnumConverter<SameValue>());

        ex.Message.ShouldContain(nameof(SameValue.Original));
        ex.Message.ShouldContain(nameof(SameValue.Alias));
    }

    [Test]
    public void Members_with_the_same_stored_value_are_rejected()
    {
        var ex = Should.Throw<InvalidOperationException>(() => new AttributeEnumConverter<SameStoredValue>());

        ex.Message.ShouldContain(nameof(SameStoredValue.First));
        ex.Message.ShouldContain(nameof(SameStoredValue.Second));
        ex.Message.ShouldContain("SAME");
    }

    public enum MissingAttribute
    {
        [JsonStringEnumMemberName("NAMED")]
        Named,
        NoName,
    }

    public enum SameValue
    {
        [JsonStringEnumMemberName("ORIGINAL")]
        Original = 1,
        [JsonStringEnumMemberName("ALIAS")]
        Alias = 1,
    }

    public enum SameStoredValue
    {
        [JsonStringEnumMemberName("SAME")]
        First,
        [JsonStringEnumMemberName("SAME")]
        Second,
    }
}
