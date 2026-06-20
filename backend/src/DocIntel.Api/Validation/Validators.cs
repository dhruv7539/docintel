using DocIntel.Api.Dtos;
using FluentValidation;

namespace DocIntel.Api.Validation;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.WorkspaceName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class UploadTextRequestValidator : AbstractValidator<UploadTextRequest>
{
    public UploadTextRequestValidator()
    {
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(400);
        RuleFor(x => x.Content).NotEmpty().MaximumLength(1_000_000);
    }
}

public class QueryRequestValidator : AbstractValidator<QueryRequest>
{
    public QueryRequestValidator()
    {
        RuleFor(x => x.Question).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.TopK).InclusiveBetween(1, 20);
    }
}
