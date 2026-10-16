using System.Text.RegularExpressions;
using FluentValidation;
using ShortUrl.Application.ShortLinks.Commands.ResolveShortLink;

namespace ShortUrl.Application.Validators;

/// <summary>
/// Validador FluentValidation para <see cref="ResolveShortLinkCommand"/> validando o formato Base62 do código curto.
/// </summary>
public sealed partial class ResolveShortLinkCommandValidator : AbstractValidator<ResolveShortLinkCommand>
{
    [GeneratedRegex("^[0-9A-Za-z]{1,7}$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex Base62CodeRegex();

    public ResolveShortLinkCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("O código do link curto é obrigatório.")
            .Matches(Base62CodeRegex())
            .WithMessage("O código do link curto deve conter entre 1 e 7 caracteres alfanuméricos em Base62.");
    }
}

