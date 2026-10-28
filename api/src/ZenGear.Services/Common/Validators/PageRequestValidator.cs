using FluentValidation;
using ZenGear.Services.Common.Models;

namespace ZenGear.Services.Common.Validators;

/// <summary>Input of <see cref="PageRequest"/> (400); the limits fill <c>{From}</c> and <c>{To}</c> of the texts.</summary>
public sealed class PageRequestValidator : AbstractValidator<PageRequest>
{
    public const string PageOutOfRange = "PAGE_OUT_OF_RANGE";

    public const string PageSizeOutOfRange = "PAGE_SIZE_OUT_OF_RANGE";

    public PageRequestValidator()
    {
        RuleFor(r => r.Page).InclusiveBetween(1, PageRequest.MaxPage).WithErrorCode(PageOutOfRange);
        RuleFor(r => r.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize).WithErrorCode(PageSizeOutOfRange);
    }
}
