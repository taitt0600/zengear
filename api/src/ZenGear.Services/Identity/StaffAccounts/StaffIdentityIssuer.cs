using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using ZenGear.Data;
using ZenGear.Services.Common;

namespace ZenGear.Services.Identity.StaffAccounts;

/// <summary>
/// Issues the identifiers of a new staff account (R-USR-24): takes the next number of the joined year from
/// <c>employee_code_sequences</c> and builds the employee code, the username and the company email. One place for every
/// caller (STF-01, the bootstrap-admin command of DEC-156), so the rule is written once.
/// </summary>
public sealed class StaffIdentityIssuer(ZenGearDbContext db, IOptions<StaffIdentityOptions> options, IClock clock)
{
    /// <summary>
    /// Must run inside the transaction that creates the account: the upsert locks the year's row until that transaction
    /// ends, so concurrent creations of one year queue and never share a number, and a rollback issues no number.
    /// </summary>
    /// <param name="name">A name the validator accepted (<see cref="StaffCreateRequestValidator.IdentityFailures"/>).</param>
    /// <param name="joinedDate">A joined date the validator accepted: its year within 2000–2099 (DEC-156 (4)).</param>
    /// <exception cref="BusinessRuleException">The 9,999 numbers of the year are issued (R-USR-24); nothing changed.</exception>
    public async Task<StaffIdentity> IssueAsync(string name, DateOnly joinedDate, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("R-USR-24: the employee code is taken in the transaction that creates the account.");
        }

        var year = (short)joinedDate.Year;
        var now = clock.UtcNow;
        List<int> numbers;
        try
        {
            // ToListAsync, not SingleAsync: an INSERT … RETURNING cannot be composed over.
            numbers = await db.Database
                .SqlQuery<int>(
                    $"""
                     INSERT INTO employee_code_sequences (year, last_value, created_at, updated_at)
                     VALUES ({year}, 1, {now}, {now})
                     ON CONFLICT (year) DO UPDATE
                     SET last_value = employee_code_sequences.last_value + 1, updated_at = {now}
                     RETURNING last_value AS "Value"
                     """)
                .ToListAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex is
            { SqlState: PostgresErrorCodes.CheckViolation, ConstraintName: "ck_employee_code_sequences_last_value" })
        {
            throw new BusinessRuleException("R-USR-24", "employee-codes-exhausted");
        }

        return StaffIdentityGenerator.Generate(name.Trim(), year, numbers.Single(), options.Value.EmailDomain);
    }
}
