using System.Globalization;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZenGear.Api.Attributes;
using ZenGear.Api.ExceptionHandling;
using ZenGear.Api.Extensions;
using ZenGear.Services.Common;

namespace ZenGear.Api.IntegrationTests.Infrastructure;

/// <summary>Endpoints that exist only in the test host (added as an application part by the factory).</summary>
[ApiController]
[Route("test")]
public sealed class TestEndpointsController : ControllerBase
{
    [HttpGet("business-rule")]
    [AllowAnonymous]
    public IActionResult BusinessRule() =>
        throw new BusinessRuleException("R-INV-02", "stock-not-available", "Stock not available", "SKU L-1 has 1 unit available, 2 requested.");

    [HttpGet("validation")]
    [AllowAnonymous]
    public IActionResult Validation() =>
        throw new ValidationException([new ValidationFailure("Items[0].UnitPrice", "Unit price must be greater than 0.")]);

    [HttpGet("validation-rule")]
    [AllowAnonymous]
    public IActionResult ValidationRule() =>
        throw new ValidationException([
            new ValidationFailure("NewPassword", "Too short.") { ErrorCode = "R-USR-11" },
            new ValidationFailure("NewPassword", "Too common.") { ErrorCode = "R-USR-11" },
        ]);

    [HttpGet("validation-mixed-rules")]
    [AllowAnonymous]
    public IActionResult ValidationMixedRules() =>
        throw new ValidationException([
            new ValidationFailure("NewPassword", "Too short.") { ErrorCode = "R-USR-11" },
            new ValidationFailure("Name", "Name is required.") { ErrorCode = "NotEmptyValidator" },
        ]);

    public const string BadRequestRuleId = "R-TST-01";

    /// <summary>A 400 of model binding (<c>?number=x</c>) on an action that names its rule.</summary>
    [HttpGet("bad-request-rule/binding")]
    [AllowAnonymous]
    [BadRequestRule(BadRequestRuleId)]
    public IActionResult BadRequestRuleBinding([FromQuery] int number) => Ok(new { number });

    /// <summary>A thrown validation 400 whose failures name no rule, on an action that names its rule.</summary>
    [HttpGet("bad-request-rule/validation")]
    [AllowAnonymous]
    [BadRequestRule(BadRequestRuleId)]
    public IActionResult BadRequestRuleValidation() =>
        throw new ValidationException([new ValidationFailure("Name", "Name is required.")]);

    /// <summary>A validation 400 that already names another rule, on an action that names its rule.</summary>
    [HttpGet("bad-request-rule/own-rule")]
    [AllowAnonymous]
    [BadRequestRule(BadRequestRuleId)]
    public IActionResult BadRequestRuleOwnRule() =>
        throw new ValidationException([new ValidationFailure("NewPassword", "Too short.") { ErrorCode = "R-USR-11" }]);

    /// <summary>A 400 the action returns itself, on an action that names its rule: not a model-binding 400.</summary>
    [HttpGet("bad-request-rule/returned")]
    [AllowAnonymous]
    [BadRequestRule(BadRequestRuleId)]
    public IActionResult BadRequestRuleReturned() => BadRequest();

    /// <summary>
    /// The title and detail <see cref="ProblemTexts"/> gives a problem type in the culture of the request, and the
    /// formatting culture of the request.
    /// </summary>
    [HttpGet("problem-texts/{problemType}")]
    [AllowAnonymous]
    public IActionResult ProblemTextsOf(string problemType, [FromServices] ProblemTexts texts) =>
        Ok(new { title = texts.Title(problemType), detail = texts.Detail(problemType), culture = CultureInfo.CurrentCulture.Name });

    [HttpPut("concurrency")]
    [AllowAnonymous]
    public IActionResult Concurrency() =>
        throw new DbUpdateConcurrencyException("The row was changed.");

    [HttpGet("unexpected")]
    [AllowAnonymous]
    public IActionResult Unexpected() =>
        throw new InvalidOperationException("Internal detail that must not leak.");

    /// <summary>Protected by the fallback policy: no attribute needed.</summary>
    [HttpGet("me")]
    public IActionResult Me() => Ok(new { uid = User.FindFirst("sub")?.Value });

    /// <summary>Protected by the default policy (a bare <c>[Authorize]</c>).</summary>
    [HttpGet("authorize")]
    [Authorize]
    public IActionResult AuthorizeDefault() => Ok();

    [HttpGet("anonymous")]
    [AllowAnonymous]
    public IActionResult Anonymous() => Ok();

    public const string TestPermission = "test.read";

    /// <summary>Requires <see cref="TestPermission"/>; returns its scope for the caller.</summary>
    [HttpGet("permission")]
    [RequirePermission(TestPermission)]
    public IActionResult Permission() => Ok(new { scope = HttpContext.GetUserAccess()!.ScopeOf(TestPermission)?.ToString() });
}
