using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TeamSyncWorkspace.Models;
using TeamSyncWorkspace.Services;

namespace TeamSyncWorkspace.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly AccountService _accountService;
        private readonly ILogger<RegisterModel> _logger;

        public RegisterModel(
            AccountService accountService,
            ILogger<RegisterModel> logger)
        {
            _accountService = accountService;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; } = "http://localhost:5055/Account/ExternalLogin";

        public class InputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string? Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string? Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string? ConfirmPassword { get; set; }

            [Required]
            [Display(Name = "First Name")]
            public string? FirstName { get; set; }

            [Required]
            [Display(Name = "Last Name")]
            public string? LastName { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {

        }

        public async Task<IActionResult> OnPostAsync()
        {


            if (ModelState.IsValid)
            {
                var (result, user) = await _accountService.RegisterUserAsync(
                    Input.Email,
                    Input.Password,
                    Input.FirstName,
                    Input.LastName);

                if (result.Succeeded)
                {
                    await _accountService.SignInAsync(user, isPersistent: false);
                    return RedirectToPage("/Teams/Index");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }
    }
}