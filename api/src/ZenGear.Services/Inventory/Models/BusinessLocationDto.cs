using System.Text.Json.Serialization;

namespace ZenGear.Services.Inventory.Models;

/// <summary>The business location as the Admin edits it (UC-070).</summary>
/// <param name="SpecialHours">The stored holiday schedule; empty when there is none. Dates before today are dropped at the next update.</param>
/// <param name="Version"><c>business_locations.xmin</c>, sent as the <c>ETag</c> header and never in the body.</param>
public sealed record BusinessLocationDto(
    Guid Id,
    string Name,
    string Phone,
    string? Email,
    string AddressDetail,
    int ProvinceId,
    string ProvinceName,
    int WardId,
    string WardName,
    IReadOnlyList<RegularHoursPeriodDto> RegularHours,
    IReadOnlyList<SpecialHoursPeriodDto> SpecialHours,
    DateTimeOffset UpdatedAt,
    [property: JsonIgnore] uint Version);
