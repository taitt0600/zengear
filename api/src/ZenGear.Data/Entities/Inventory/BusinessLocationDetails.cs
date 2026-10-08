namespace ZenGear.Data.Entities.Inventory;

/// <summary>What the Admin edits on the business location (<see cref="BusinessLocation.Update"/>, UC-070).</summary>
/// <param name="WardId">Belongs to <paramref name="ProvinceId"/>; the service checks it, the composite FK is the backstop.</param>
/// <param name="SpecialHours">Null or empty: no holiday schedule.</param>
public sealed record BusinessLocationDetails(
    string Name,
    string Phone,
    string? Email,
    string AddressDetail,
    int ProvinceId,
    int WardId,
    IReadOnlyList<RegularHoursPeriod> RegularHours,
    IReadOnlyList<SpecialHoursPeriod>? SpecialHours);
