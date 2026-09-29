namespace ZenGear.Data.Entities.Identity;

/// <summary>
/// A single grantable action (<c>permissions</c>). The keys are defined in code and synchronized
/// at startup (DEC-095); nothing else writes this table.
/// </summary>
public class Permission
{
    private Permission()
    {
    }

    public Permission(string key, string? description)
    {
        Key = key;
        Description = description;
    }

    public Guid Id { get; private set; }

    /// <summary><c>resource.action</c>, e.g. <c>order.confirm</c>.</summary>
    public string Key { get; private set; } = null!;

    public string? Description { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Describe(string? description, DateTimeOffset now)
    {
        if (Description == description)
        {
            return;
        }

        Description = description;
        UpdatedAt = now;
    }
}
