using FluentValidation;
using SmartDash.Models;

namespace SmartDash.Validators;

public class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required")
            .MinimumLength(3).WithMessage("Username must be at least 3 characters")
            .MaximumLength(50).WithMessage("Username cannot exceed 50 characters")
            .Matches("^[a-zA-Z0-9_.-]+$").WithMessage("Username can only contain letters, numbers, dots, hyphens, and underscores");
        
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters");
    }
}

public class RegisterValidator : AbstractValidator<RegisterRequest>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required")
            .MinimumLength(3).WithMessage("Username must be at least 3 characters")
            .MaximumLength(50).WithMessage("Username cannot exceed 50 characters")
            .Matches("^[a-zA-Z0-9_.-]+$").WithMessage("Username can only contain letters, numbers, dots, hyphens, and underscores");
        
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(100).WithMessage("Email cannot exceed 100 characters");
        
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .MaximumLength(100).WithMessage("Password cannot exceed 100 characters")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one number")
            .Matches(@"[@$!%*?&#]").WithMessage("Password must contain at least one special character (@$!%*?&#)");
        
        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Passwords do not match");
    }
}

public class ModeUpdateValidator : AbstractValidator<ModeUpdateRequest>
{
    public ModeUpdateValidator()
    {
        RuleFor(x => x.Mode)
            .NotEmpty().WithMessage("Mode is required")
            .Must(mode => mode == "simulated" || mode == "live")
            .WithMessage("Mode must be either 'simulated' or 'live'");
    }
}
