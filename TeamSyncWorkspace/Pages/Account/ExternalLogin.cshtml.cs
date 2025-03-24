using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TeamSyncWorkspace.Models;
using TeamSyncWorkspace.Services;

namespace TeamSyncWorkspace.Pages.Account
{
    public class ExternalLoginModel : PageModel
    {
        private readonly AccountService _accountService;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly ILogger<ExternalLoginModel> _logger;

        public ExternalLoginModel(
            AccountService accountService,
            IUserStore<ApplicationUser> userStore,
            ILogger<ExternalLoginModel> logger)
        {
            _accountService = accountService;
            _userStore = userStore;
            _logger = logger;
        }

        [BindProperty]
        public InputModel? Input { get; set; }

        public string? ProviderDisplayName { get; set; }

        public string? ReturnUrl { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string? Email { get; set; }

            [Required]
            [Display(Name = "First Name")]
            public string? FirstName { get; set; }

            [Required]
            [Display(Name = "Last Name")]
            public string? LastName { get; set; }
        }

        public IActionResult OnGet() => RedirectToPage("./Login");


        // Redirect to the external login provider.
        public IActionResult OnPost(string provider, string? returnUrl = null)
        {
            // Request a redirect to the external login provider.
            returnUrl ??= Url.Content("~/");

            var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl, flowName = "GeneralOAuthFlow" })!;
            // Console.WriteLine("ExternalLoginModel.OnPost: redirectUrl = " + redirectUrl);
            var properties = _accountService.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return new ChallengeResult(provider);
        }


        // Handle callback from the external login provider
        public async Task<IActionResult> OnGetCallbackAsync(string returnUrl = null, string remoteError = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");
            if (remoteError != null)
            {
                ErrorMessage = $"Error from external provider: {remoteError}";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            var info = await _accountService.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Error loading external login information.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            // Sign in the user with this external login provider if the user already has a login.
            var result = await _accountService.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
            if (result.Succeeded)
            {
                _logger.LogInformation("{Name} logged in with {LoginProvider} provider.", info.Principal.Identity.Name, info.LoginProvider);
                return LocalRedirect(returnUrl);
            }
            if (result.IsLockedOut)
            {
                return RedirectToPage("./Lockout");
            }
            else
            {
                // If the user does not have an account, then ask the user to create an account.
                ReturnUrl = returnUrl;
                ProviderDisplayName = info.ProviderDisplayName!;
                if (info.Principal.HasClaim(c => c.Type == ClaimTypes.Email))
                {
                    Input = new InputModel
                    {
                        Email = info.Principal.FindFirstValue(ClaimTypes.Email)!,
                        FirstName = info.Principal.FindFirstValue(ClaimTypes.GivenName)!,
                        LastName = info.Principal.FindFirstValue(ClaimTypes.Surname)!
                    };
                }
                return Page();
            }
        }

        public async Task<IActionResult> OnPostConfirmationAsync(string returnUrl = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");
            // Get the information about the user from the external login provider
            var info = await _accountService.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Error loading external login information during confirmation.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            if (ModelState.IsValid)
            {
                var (result, user) = await _accountService.RegisterUserAsync(
                    Input.Email,
                    Guid.NewGuid().ToString(), // Generate a random password for external users
                    Input.FirstName,
                    Input.LastName);

                if (result.Succeeded)
                {
                    result = await _accountService.AddLoginAsync(user, new UserLoginInfo(info.LoginProvider, info.ProviderKey, info.ProviderDisplayName));
                    if (result.Succeeded)
                    {
                        _logger.LogInformation("User created an account using {Name} provider.", info.LoginProvider);
                        await _accountService.SignInAsync(user, isPersistent: false);
                        return LocalRedirect(returnUrl);
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            ProviderDisplayName = info.ProviderDisplayName;
            ReturnUrl = returnUrl;
            return Page();
        }
    }
}