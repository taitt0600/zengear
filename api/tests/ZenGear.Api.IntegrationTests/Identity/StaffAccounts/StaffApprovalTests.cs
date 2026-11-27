using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Shouldly;
using ZenGear.Api.IntegrationTests.Infrastructure;
using ZenGear.Services.Identity.Access.AccessCache;
using ZenGear.Services.Identity.Permissions;

namespace ZenGear.Api.IntegrationTests.Identity.StaffAccounts;

/// <summary>
/// PENDING staff accounts (STF-09): creation without <c>staff.approve</c>, POST /api/v1/admin/staff/{id}/approve,
/// /reject and /withdraw with <c>If-Match</c>, against PostgreSQL with Firebase Authentication faked.
/// </summary>
public class StaffApprovalTests : IntegrationTestBase
{
    private const string Path = "/api/v1/admin/staff";
    private const string AdminUid = "uid-admin";
    private const string ItSupportUid = "uid-itsupport";
    private const string OtherCreatorUid = "uid-other-creator";
    private const string ShiftLeadUid = "uid-shift-lead";
    private const string ReaderUid = "uid-reader";

    /// <summary>09:00 on 2026-11-26 in Asia/Ho_Chi_Minh.</summary>
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 2, 0, 0, TimeSpan.Zero);

    private Guid _itSupportRole;
    private Guid _cashierRole;
    private Guid _warehouseRole;
    private Guid _itSupportId;
    private Guid _adminId;

    private FakeAuthProvider Auth => Factory.Auth;

    [SetUp]
    public async Task SeedAsync()
    {
        Auth.Reset();
        Factory.Time.AdjustTime(Now);

        var adminRole = await TestRoles.InsertAsync("Admin", isSystemRole: true);
        _itSupportRole = await TestRoles.InsertAsync("IT Support");
        var shiftLeadRole = await TestRoles.InsertAsync("Shift Lead");
        var readerRole = await TestRoles.InsertAsync("Reader");
        _cashierRole = await TestRoles.InsertAsync("Cashier");
        _warehouseRole = await TestRoles.InsertAsync("Warehouse Keeper");

        foreach (var key in new[]
                 {
                     PermissionKeys.StaffCreate, PermissionKeys.StaffRead, PermissionKeys.StaffDisable,
                     PermissionKeys.StaffApprove,
                 })
        {
            await TestRoles.GrantAsync(adminRole, key);
        }

        await TestRoles.GrantAsync(_itSupportRole, PermissionKeys.StaffCreate);
        await TestRoles.GrantAsync(_itSupportRole, PermissionKeys.StaffRead);
        await TestRoles.GrantAsync(_itSupportRole, PermissionKeys.StaffDisable);
        await TestRoles.ManageAsync(_itSupportRole, _cashierRole, _warehouseRole);
        // The shift lead approves, and manages the Cashier only (R-SEC-12): a Warehouse Keeper is outside its set.
        await TestRoles.GrantAsync(shiftLeadRole, PermissionKeys.StaffApprove);
        await TestRoles.GrantAsync(shiftLeadRole, PermissionKeys.StaffRead);
        await TestRoles.ManageAsync(shiftLeadRole, _cashierRole);
        await TestRoles.GrantAsync(readerRole, PermissionKeys.StaffRead);

        _adminId = await TestUsers.InsertAsync(AdminUid);
        await TestRoles.AssignAsync(_adminId, adminRole);
        _itSupportId = await TestUsers.InsertAsync(ItSupportUid);
        await TestRoles.AssignAsync(_itSupportId, _itSupportRole);
        await TestRoles.AssignAsync(await TestUsers.InsertAsync(OtherCreatorUid), _itSupportRole);
        await TestRoles.AssignAsync(await TestUsers.InsertAsync(ShiftLeadUid), shiftLeadRole);
        await TestRoles.AssignAsync(await TestUsers.InsertAsync(ReaderUid), readerRole);
    }

    // ---- Creation (R-USR-17) ----

    [Test]
    public async Task R_USR_17_an_account_created_without_staff_approve_is_pending_without_a_firebase_account()
    {
        var response = await PostAsync(ItSupportUid, Path, new { name = "Võ Thị Lan", joinedDate = "2026-11-02", roleIds = new[] { _cashierRole } });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await JsonAsync(response);
        body.GetProperty("status").GetString().ShouldBe("PENDING");
        body.GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();
        Auth.Accounts.ShouldBeEmpty();
        var id = body.GetProperty("id").GetGuid();
        (await RowAsync(id)).ShouldBe(new AccountRow("PENDING", null, _itSupportId));
    }

    [Test]
    public async Task R_USR_17_an_account_created_by_a_holder_of_staff_approve_is_active_at_once()
    {
        var response = await PostAsync(AdminUid, Path, new { name = "Nguyễn Văn An", joinedDate = "2026-11-02", roleIds = new[] { _cashierRole } });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await JsonAsync(response);
        body.GetProperty("status").GetString().ShouldBe("ACTIVE");
        Auth.Accounts.Count.ShouldBe(1);
    }

    [Test]
    public async Task R_USR_17_a_pending_account_keeps_its_email_reserved()
    {
        var id = await CreatePendingAsync(_cashierRole);

        var error = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
            "INSERT INTO users (firebase_uid, name, email) SELECT 'uid-other', 'Người khác', email FROM users WHERE id = @id", id));

        error.ConstraintName.ShouldBe("ix_users_email");
    }

    // ---- Approve ----

    [Test]
    public async Task R_USR_17_approve_activates_the_account_with_a_firebase_account_and_the_default_password()
    {
        var id = await CreatePendingAsync(_cashierRole);
        var (etag, account) = await GetWithETagAsync(AdminUid, id);

        var response = await ActAsync(AdminUid, id, "approve", etag);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await JsonAsync(response);
        body.GetProperty("status").GetString().ShouldBe("ACTIVE");
        body.GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();
        body.GetProperty("canApprove").ValueKind.ShouldBe(JsonValueKind.Null);
        response.Headers.ETag.ShouldNotBeNull().Tag.ShouldNotBe(etag);
        var email = account.GetProperty("email").GetString()!;
        var firebase = Auth.Find(email).ShouldNotBeNull();
        firebase.Password.ShouldBe(FakeAuthProvider.DefaultPassword);
        (await RowAsync(id)).ShouldBe(new AccountRow("ACTIVE", firebase.Uid, _itSupportId));
    }

    [Test]
    public async Task R_USR_17_the_creator_who_gained_staff_approve_cannot_approve_or_reject_their_own_request()
    {
        var id = await CreatePendingAsync(_cashierRole);
        // A holder of staff.approve creates ACTIVE accounts, so this case arises only when the creator gains it later.
        await TestRoles.GrantAsync(_itSupportRole, PermissionKeys.StaffApprove);
        await Factory.AccessCache().BumpAsync([new AccessBump(AccessBumpKind.Role, _itSupportRole)]);
        var (etag, _) = await GetWithETagAsync(AdminUid, id);

        await (await ActAsync(ItSupportUid, id, "approve", etag))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/own-account-request-not-approvable", "R-USR-17");
        await (await ActAsync(ItSupportUid, id, "reject", etag))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/own-account-request-not-approvable", "R-USR-17");
        (await RowAsync(id)).Status.ShouldBe("PENDING");
        Auth.Accounts.ShouldBeEmpty();
    }

    [Test]
    public async Task R_SEC_12_approve_and_reject_refuse_an_account_with_a_role_outside_the_approver_scope()
    {
        var id = await CreatePendingAsync(_warehouseRole);
        var (etag, _) = await GetWithETagAsync(AdminUid, id);

        await (await ActAsync(ShiftLeadUid, id, "approve", etag))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/account-out-of-scope", "R-SEC-12");
        await (await ActAsync(ShiftLeadUid, id, "reject", etag))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/account-out-of-scope", "R-SEC-12");
        (await RowAsync(id)).Status.ShouldBe("PENDING");
    }

    [Test]
    public async Task R_SEC_12_an_approver_approves_an_account_within_scope()
    {
        var id = await CreatePendingAsync(_cashierRole);
        var (etag, account) = await GetWithETagAsync(ShiftLeadUid, id);
        // Not the Admin: the flag comes from the scope of R-SEC-12 alone (DEC-138).
        account.GetProperty("canApprove").GetBoolean().ShouldBeTrue();

        (await ActAsync(ShiftLeadUid, id, "approve", etag)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task R_USR_17_approve_reject_and_withdraw_refuse_an_account_that_is_not_pending()
    {
        var id = await CreatePendingAsync(_cashierRole);
        var (etag, _) = await GetWithETagAsync(AdminUid, id);
        (await ActAsync(AdminUid, id, "approve", etag)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var (active, _) = await GetWithETagAsync(AdminUid, id);

        foreach (var (uid, action) in new[] { (AdminUid, "approve"), (AdminUid, "reject"), (ItSupportUid, "withdraw") })
        {
            await (await ActAsync(uid, id, action, active))
                .ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/account-not-pending", "R-USR-17");
        }

        (await RowAsync(id)).Status.ShouldBe("ACTIVE");
    }

    [Test]
    public async Task R_USR_17_approve_refuses_an_email_another_firebase_account_holds_and_keeps_the_account_pending()
    {
        var id = await CreatePendingAsync(_cashierRole);
        var (etag, account) = await GetWithETagAsync(AdminUid, id);
        var other = Auth.Add(account.GetProperty("email").GetString()!);

        await (await ActAsync(AdminUid, id, "approve", etag))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/auth-account-exists", null);

        (await RowAsync(id)).ShouldBe(new AccountRow("PENDING", null, _itSupportId));
        Auth.Accounts.Single().Uid.ShouldBe(other.Uid);
        Auth.Deleted.ShouldBeEmpty();
    }

    [Test]
    public async Task R_USR_17_a_failure_after_the_firebase_account_is_created_deletes_it_and_keeps_the_account_pending()
    {
        var id = await CreatePendingAsync(_cashierRole);
        var (etag, _) = await GetWithETagAsync(AdminUid, id);
        string? createdUid = null;
        Auth.AfterCreate = created =>
        {
            createdUid = created.Uid;
            throw new InvalidOperationException("Firebase failed after writing");
        };

        (await ActAsync(AdminUid, id, "approve", etag)).StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        Auth.AfterCreate = null;

        Auth.Deleted.ShouldBe([createdUid.ShouldNotBeNull()]);
        Auth.Accounts.ShouldBeEmpty();
        (await RowAsync(id)).ShouldBe(new AccountRow("PENDING", null, _itSupportId));
    }

    // ---- If-Match (DEC-140 (2)) ----

    [Test]
    public async Task R_USR_17_approve_reject_and_withdraw_without_if_match_get_428_and_change_nothing()
    {
        var id = await CreatePendingAsync(_cashierRole);

        foreach (var (uid, action) in new[] { (AdminUid, "approve"), (AdminUid, "reject"), (ItSupportUid, "withdraw") })
        {
            await (await ActAsync(uid, id, action, ifMatch: null))
                .ShouldBeProblemAsync((HttpStatusCode)428, "/problems/if-match-required", null);
        }

        (await RowAsync(id)).Status.ShouldBe("PENDING");
    }

    [TestCase("\"1\"")]
    [TestCase("*")]
    [TestCase("W/\"1\"")]
    public async Task R_USR_17_approve_reject_and_withdraw_with_another_version_get_412_and_change_nothing(string ifMatch)
    {
        var id = await CreatePendingAsync(_cashierRole);

        foreach (var (uid, action) in new[] { (AdminUid, "approve"), (AdminUid, "reject"), (ItSupportUid, "withdraw") })
        {
            await (await ActAsync(uid, id, action, ifMatch))
                .ShouldBeProblemAsync(HttpStatusCode.PreconditionFailed, "/problems/precondition-failed", null);
        }

        (await RowAsync(id)).Status.ShouldBe("PENDING");
        Auth.Accounts.ShouldBeEmpty();
    }

    [Test]
    public async Task R_USR_17_an_account_changed_since_it_was_read_gets_412()
    {
        var id = await CreatePendingAsync(_cashierRole);
        var (etag, _) = await GetWithETagAsync(AdminUid, id);
        await ExecuteAsync("UPDATE users SET name = 'Võ Thị Lan B', updated_at = now() WHERE id = @id", id);

        await (await ActAsync(AdminUid, id, "approve", etag))
            .ShouldBeProblemAsync(HttpStatusCode.PreconditionFailed, "/problems/precondition-failed", null);
        (await RowAsync(id)).Status.ShouldBe("PENDING");
    }

    // ---- Reject and withdraw ----

    [Test]
    public async Task R_USR_17_reject_deletes_the_pending_account_with_its_profile_and_roles()
    {
        var id = await CreatePendingAsync(_cashierRole);
        var (etag, _) = await GetWithETagAsync(AdminUid, id);

        var response = await ActAsync(AdminUid, id, "reject", etag);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await CountRowsAsync(id)).ShouldBe(0);
        (await SendAsync(AdminUid, HttpMethod.Get, $"{Path}/{id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task R_USR_17_the_creator_withdraws_their_own_pending_account()
    {
        var id = await CreatePendingAsync(_cashierRole);
        var (etag, _) = await GetWithETagAsync(ItSupportUid, id);

        (await ActAsync(ItSupportUid, id, "withdraw", etag)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await CountRowsAsync(id)).ShouldBe(0);
    }

    [TestCase(AdminUid, "reject")]
    [TestCase(ItSupportUid, "withdraw")]
    public async Task R_USR_17_reject_and_withdraw_leave_the_audit_rows_of_the_deleted_account(string uid, string action)
    {
        var id = await CreatePendingAsync(_cashierRole);
        var (etag, _) = await GetWithETagAsync(uid, id);
        var actorId = uid == AdminUid ? _adminId : _itSupportId;

        (await ActAsync(uid, id, action, etag)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // R-AUD-01: one DELETE row per row of the account, written by the actor in the transaction of the delete.
        var rows = await DeleteAuditRowsAsync(id);
        rows.Select(r => r.TableName).Order().ShouldBe(["staff_profiles", "user_roles", "users"]);
        rows.ShouldAllBe(r => r.ChangedBy == actorId && r.OldValues != null);
        rows.Select(r => r.TransactionId).Distinct().ShouldHaveSingleItem();
        rows.Single(r => r.TableName == "user_roles").OldValues.ShouldNotBeNull().ShouldContain(_cashierRole.ToString());
    }

    [Test]
    public async Task R_USR_17_another_holder_of_staff_create_cannot_withdraw_the_account()
    {
        var id = await CreatePendingAsync(_cashierRole);
        var (etag, _) = await GetWithETagAsync(AdminUid, id);

        await (await ActAsync(OtherCreatorUid, id, "withdraw", etag))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/not-account-creator", "R-USR-17");
        await (await ActAsync(AdminUid, id, "withdraw", etag))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/not-account-creator", "R-USR-17");
        (await RowAsync(id)).Status.ShouldBe("PENDING");
    }

    [Test]
    public async Task R_USR_17_the_three_actions_need_their_permission()
    {
        var id = await CreatePendingAsync(_cashierRole);
        var (etag, _) = await GetWithETagAsync(AdminUid, id);

        await (await ActAsync(ItSupportUid, id, "approve", etag)).ShouldBeForbiddenProblemAsync("/problems/permission-denied", "R-SEC-03");
        await (await ActAsync(ItSupportUid, id, "reject", etag)).ShouldBeForbiddenProblemAsync("/problems/permission-denied", "R-SEC-03");
        await (await ActAsync(ShiftLeadUid, id, "withdraw", etag)).ShouldBeForbiddenProblemAsync("/problems/permission-denied", "R-SEC-03");
        (await RowAsync(id)).Status.ShouldBe("PENDING");
    }

    [Test]
    public async Task R_USR_17_the_three_actions_answer_404_for_an_unknown_id_or_a_customer()
    {
        var customer = await TestUsers.InsertAsync("uid-customer", isStaff: false);

        foreach (var target in new[] { Guid.NewGuid(), customer })
        {
            foreach (var (uid, action) in new[] { (AdminUid, "approve"), (AdminUid, "reject"), (ItSupportUid, "withdraw") })
            {
                (await ActAsync(uid, target, action, "\"1\"")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
            }
        }
    }

    [Test]
    public async Task R_USR_17_approve_and_reject_at_once_run_one_after_the_other()
    {
        var id = await CreatePendingAsync(_cashierRole);
        var (etag, _) = await GetWithETagAsync(AdminUid, id);

        var results = await Task.WhenAll(ActAsync(AdminUid, id, "approve", etag), ActAsync(AdminUid, id, "reject", etag));

        var statuses = results.Select(r => r.StatusCode).ToList();
        statuses.Count(s => s is HttpStatusCode.OK or HttpStatusCode.NoContent).ShouldBe(1);
        statuses.Count(s => s is HttpStatusCode.Conflict or HttpStatusCode.NotFound).ShouldBe(1);
        // Whichever won, no Firebase account is left without its user row.
        var row = await CountRowsAsync(id) == 0 ? null : await RowAsync(id);
        Auth.Accounts.Count.ShouldBe(row is { Status: "ACTIVE" } ? 1 : 0);
    }

    // ---- Disable and restore (R-USR-17) ----

    [Test]
    public async Task R_USR_17_a_pending_account_is_neither_disabled_nor_restored()
    {
        var id = await CreatePendingAsync(_cashierRole);

        await (await SendAsync(AdminUid, HttpMethod.Post, $"{Path}/{id}/disable", new { }))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/account-pending", "R-USR-17");
        await (await SendAsync(AdminUid, HttpMethod.Post, $"{Path}/{id}/restore"))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/account-pending", "R-USR-17");
        (await RowAsync(id)).Status.ShouldBe("PENDING");
    }

    // ---- The flags of the account and of the list (DEC-137 (1), DEC-138, DEC-140) ----

    [Test]
    public async Task R_USR_17_the_account_says_who_may_approve_or_withdraw_it_and_who_created_it()
    {
        var id = await CreatePendingAsync(_warehouseRole);

        var admin = await GetJsonAsync(AdminUid, id);
        admin.GetProperty("canApprove").GetBoolean().ShouldBeTrue();
        admin.GetProperty("canWithdraw").GetBoolean().ShouldBeFalse();
        admin.GetProperty("createdBy").GetProperty("id").GetGuid().ShouldBe(_itSupportId);
        admin.GetProperty("createdBy").GetProperty("name").GetString().ShouldBe("Nguyễn Văn A");
        admin.GetProperty("createdAt").GetDateTimeOffset().ShouldBe(Now);

        var creator = await GetJsonAsync(ItSupportUid, id);
        creator.GetProperty("canApprove").GetBoolean().ShouldBeFalse();
        creator.GetProperty("canWithdraw").GetBoolean().ShouldBeTrue();

        (await GetJsonAsync(ShiftLeadUid, id)).GetProperty("canApprove").GetBoolean().ShouldBeFalse();
        (await GetJsonAsync(ReaderUid, id)).GetProperty("canApprove").GetBoolean().ShouldBeFalse();
        (await GetJsonAsync(OtherCreatorUid, id)).GetProperty("canWithdraw").GetBoolean().ShouldBeFalse();
    }

    [Test]
    public async Task R_USR_17_an_active_account_has_no_approval_fields()
    {
        var response = await PostAsync(AdminUid, Path, new { name = "Nguyễn Văn An", joinedDate = "2026-11-02", roleIds = new[] { _cashierRole } });
        var id = (await JsonAsync(response)).GetProperty("id").GetGuid();

        var account = await GetJsonAsync(AdminUid, id);

        foreach (var field in new[] { "canApprove", "canWithdraw", "createdBy", "createdAt" })
        {
            account.GetProperty(field).ValueKind.ShouldBe(JsonValueKind.Null);
        }
    }

    [Test]
    public async Task R_USR_17_the_creator_is_left_out_once_anonymized()
    {
        var id = await CreatePendingAsync(_cashierRole);
        await ExecuteAsync("UPDATE users SET status = 'ANONYMIZED' WHERE id = @id", _itSupportId);

        (await GetJsonAsync(AdminUid, id)).GetProperty("createdBy").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task R_USR_17_the_list_filters_pending_accounts_and_carries_their_flags_and_version()
    {
        var pending = await CreatePendingAsync(_cashierRole);
        var (etag, _) = await GetWithETagAsync(AdminUid, pending);

        var page = await JsonAsync(await SendAsync(AdminUid, HttpMethod.Get, $"{Path}?status=PENDING"));

        var item = page.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("id").GetGuid().ShouldBe(pending);
        item.GetProperty("status").GetString().ShouldBe("PENDING");
        item.GetProperty("canApprove").GetBoolean().ShouldBeTrue();
        item.GetProperty("canWithdraw").GetBoolean().ShouldBeFalse();
        item.GetProperty("createdBy").GetProperty("id").GetGuid().ShouldBe(_itSupportId);
        item.GetProperty("createdAt").GetDateTimeOffset().ShouldBe(Now);
        item.GetProperty("version").GetString().ShouldBe(etag);

        var creatorItem = (await JsonAsync(await SendAsync(ItSupportUid, HttpMethod.Get, $"{Path}?status=PENDING")))
            .GetProperty("items").EnumerateArray().Single();
        creatorItem.GetProperty("canApprove").GetBoolean().ShouldBeFalse();
        creatorItem.GetProperty("canWithdraw").GetBoolean().ShouldBeTrue();
    }

    [Test]
    public async Task R_USR_17_list_items_of_other_statuses_have_no_approval_fields()
    {
        await CreatePendingAsync(_cashierRole);

        var page = await JsonAsync(await SendAsync(AdminUid, HttpMethod.Get, $"{Path}?status=ACTIVE"));

        foreach (var item in page.GetProperty("items").EnumerateArray())
        {
            foreach (var field in new[] { "canApprove", "canWithdraw", "createdBy", "createdAt", "version" })
            {
                item.GetProperty(field).ValueKind.ShouldBe(JsonValueKind.Null);
            }
        }
    }

    // ---- Helpers ----

    private async Task<Guid> CreatePendingAsync(Guid roleId)
    {
        var response = await PostAsync(ItSupportUid, Path, new { name = "Võ Thị Lan", joinedDate = "2026-11-02", roleIds = new[] { roleId } });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await JsonAsync(response);
        body.GetProperty("status").GetString().ShouldBe("PENDING");
        return body.GetProperty("id").GetGuid();
    }

    private async Task<(string ETag, JsonElement Account)> GetWithETagAsync(string uid, Guid id)
    {
        var response = await SendAsync(uid, HttpMethod.Get, $"{Path}/{id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (response.Headers.ETag.ShouldNotBeNull().Tag, await JsonAsync(response));
    }

    private async Task<JsonElement> GetJsonAsync(string uid, Guid id) => (await GetWithETagAsync(uid, id)).Account;

    private Task<HttpResponseMessage> PostAsync(string uid, string path, object body) =>
        SendAsync(uid, HttpMethod.Post, path, body);

    private Task<HttpResponseMessage> ActAsync(string uid, Guid id, string action, string? ifMatch) =>
        SendAsync(uid, HttpMethod.Post, $"{Path}/{id}/{action}", body: null, ifMatch);

    private async Task<HttpResponseMessage> SendAsync(string uid, HttpMethod method, string path, object? body = null,
        string? ifMatch = null)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.EmulatorToken(uid));
        return await Client.SendAsync(request);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private sealed record AccountRow(string Status, string? FirebaseUid, Guid? CreatedBy);

    private static async Task<AccountRow> RowAsync(Guid id)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT u.status, u.firebase_uid, s.created_by FROM users u JOIN staff_profiles s ON s.user_id = u.id WHERE u.id = @id",
            connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).ShouldBeTrue();
        return new AccountRow(reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetGuid(2));
    }

    /// <summary>The rows of the account in <c>users</c>, <c>staff_profiles</c> and <c>user_roles</c> together.</summary>
    private static async Task<long> CountRowsAsync(Guid id) =>
        await ScalarAsync("SELECT (SELECT count(*) FROM users WHERE id = @id) + (SELECT count(*) FROM staff_profiles WHERE user_id = @id) + (SELECT count(*) FROM user_roles WHERE user_id = @id)", id);

    private sealed record DeleteAuditRow(string TableName, string? OldValues, long TransactionId, Guid? ChangedBy);

    /// <summary>The DELETE rows of <c>audit_logs</c> whose key names the account (<c>id</c> or <c>user_id</c>).</summary>
    private static async Task<List<DeleteAuditRow>> DeleteAuditRowsAsync(Guid id)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT table_name, old_values::text, transaction_id, changed_by FROM audit_logs
            WHERE action = 'DELETE' AND (record_key ->> 'id' = @id::text OR record_key ->> 'user_id' = @id::text)
            ORDER BY id
            """,
            connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<DeleteAuditRow>();
        while (await reader.ReadAsync())
        {
            rows.Add(new DeleteAuditRow(reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.GetInt64(2), reader.IsDBNull(3) ? null : reader.GetGuid(3)));
        }

        return rows;
    }

    private static async Task<long> ScalarAsync(string sql, Guid id)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task ExecuteAsync(string sql, Guid id)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync();
    }
}
