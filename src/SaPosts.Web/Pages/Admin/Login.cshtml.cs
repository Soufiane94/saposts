using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

using SaPosts.Web.Auth;

namespace SaPosts.Web.Pages.Admin;

[EnableRateLimiting(RateLimitPolicies.Login)]
public sealed class LoginModel(SignInManager<IdentityUser> signInManager, IOptions<IdentityOptions> identityOptions) : PageModel
{
    [BindProperty]
    public LoginInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? LocalRedirect(SafeReturnUrl) : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await signInManager.PasswordSignInAsync(Input.Email, Input.Password, isPersistent: false, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            return LocalRedirect(SafeReturnUrl);
        }

        // Unknown email and wrong password get the same message.
        ErrorMessage = result.IsLockedOut
            ? $"Trop d’échecs de connexion. Réessayez dans {identityOptions.Value.Lockout.DefaultLockoutTimeSpan.TotalMinutes:0} minutes."
            : "Adresse e-mail ou mot de passe incorrect.";
        return Page();
    }

    private string SafeReturnUrl => Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : Url.Page("/Admin/Index")!;
}

public sealed class LoginInput
{
    [Display(Name = "Adresse e-mail")]
    [Required(ErrorMessage = "Saisissez votre adresse e-mail.")]
    [EmailAddress(ErrorMessage = "Saisissez une adresse e-mail valide.")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Mot de passe")]
    [Required(ErrorMessage = "Saisissez votre mot de passe.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}
