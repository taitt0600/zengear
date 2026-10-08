using FluentValidation;
using ZenGear.Services.Common.Models;

namespace ZenGear.Services.Common.Validators;

/// <summary>Input of <see cref="PageRequest"/> (400).</summary>
public sealed class PageRequestValidator : AbstractValidator<PageRequest>
{
    public PageRequestValidator()
    {
        RuleFor(r => r.Page).InclusiveBetween(1, PageRequest.MaxPage);
        RuleFor(r => r.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize);
    }
}
