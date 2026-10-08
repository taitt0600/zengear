using Npgsql;

namespace ZenGear.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Inserts the geography rows and the one business location with plain SQL: both come from data loaded once per
/// environment (the official list, the go-live seed), never from an endpoint (R-LOC-01).
/// </summary>
public static class TestLocations
{
    public const int HoChiMinhCity = 79;
    public const int HaNoi = 1;

    /// <summary>In <see cref="HoChiMinhCity"/>.</summary>
    public const int SaiGonWard = 26734;

    /// <summary>In <see cref="HoChiMinhCity"/>.</summary>
    public const int BenThanhWard = 26743;

    /// <summary>In <see cref="HoChiMinhCity"/>, no longer active.</summary>
    public const int InactiveWard = 26740;

    /// <summary>In <see cref="HaNoi"/>.</summary>
    public const int HoanKiemWard = 70;

    /// <summary>
    /// Vietnam, two provinces and four wards, then the location in Phường Sài Gòn, open Monday 08:30–21:30 and Sunday
    /// 09:00–18:00, with no holiday schedule.
    /// </summary>
    public static async Task<Guid> InsertAsync()
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(
            $$"""
            INSERT INTO countries (id, country_code, common_name) VALUES (238, 'VN', 'Vietnam');
            INSERT INTO provinces (id, name, code, country_id) VALUES
              ({{HoChiMinhCity}}, 'Thành phố Hồ Chí Minh', '79', 238), ({{HaNoi}}, 'Thành phố Hà Nội', '01', 238);
            INSERT INTO wards (id, name, code, province_id, is_active) VALUES
              ({{SaiGonWard}}, 'Phường Sài Gòn', '26734', {{HoChiMinhCity}}, true),
              ({{BenThanhWard}}, 'Phường Bến Thành', '26743', {{HoChiMinhCity}}, true),
              ({{InactiveWard}}, 'Phường Cũ', '26740', {{HoChiMinhCity}}, false),
              ({{HoanKiemWard}}, 'Phường Hoàn Kiếm', '00070', {{HaNoi}}, true);
            INSERT INTO business_locations (name, phone, email, address_detail, province_id, ward_id, regular_hours)
            VALUES ('ZenGear Sài Gòn', '02838123456', 'cskh@zengear.test', '123 Lê Lợi', {{HoChiMinhCity}}, {{SaiGonWard}},
              '[{"day": 1, "open": "08:30", "close": "21:30"}, {"day": 7, "open": "09:00", "close": "18:00"}]')
            RETURNING id;
            """,
            connection);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    public static async Task<LocationRow> RowAsync(Guid id)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT name, phone, email, address_detail, province_id, ward_id, regular_hours::text, special_hours::text, updated_by
            FROM business_locations WHERE id = @id
            """,
            connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return new LocationRow(reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetString(3), reader.GetInt32(4), reader.GetInt32(5), reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetGuid(8));
    }

    public static async Task<long> CountAsync()
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM business_locations", connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}

/// <summary>A <c>business_locations</c> row as stored; the hours are the jsonb text PostgreSQL returns.</summary>
public sealed record LocationRow(
    string Name,
    string Phone,
    string? Email,
    string AddressDetail,
    int ProvinceId,
    int WardId,
    string RegularHours,
    string? SpecialHours,
    Guid? UpdatedBy);
