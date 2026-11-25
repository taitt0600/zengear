namespace ZenGear.Data.Entities.Identity;

/// <summary>
/// The yearly counter of employee codes (<c>employee_code_sequences</c>, R-USR-24): the last number issued for a joined
/// year. Mapped for the model and the migration only; the number is taken with one upsert inside the transaction that
/// creates the account (StaffIdentityIssuer), never through this type.
/// </summary>
public class EmployeeCodeSequence
{
    protected EmployeeCodeSequence()
    {
    }

    public short Year { get; private set; }

    public int LastValue { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
