using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TeamSyncWorkspace.Models;
using TeamSyncWorkspace.Services;

namespace TeamSyncWorkspace.Pages.Account
{
    [Authorize]
    public class ProfileModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AccountService _accountService;
        private readonly ILogger<ProfileModel> _logger;

        public ProfileModel(
            UserManager<ApplicationUser> userManager,
            AccountService accountService,
            ILogger<ProfileModel> logger)
        {
            _userManager = userManager;
            _accountService = accountService;
            _logger = logger;
        }

        public ApplicationUser CurrentUser { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string Email { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            CurrentUser = user;

            Input = new InputModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email
            };

            return Page();
        }

        public async Task<IActionResult> OnPostUpdateProfileAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!ModelState.IsValid)
            {
                CurrentUser = user;
                return Page();
            }

            var changesMade = false;

            if (user.FirstName != Input.FirstName)
            {
                user.FirstName = Input.FirstName;
                changesMade = true;
            }

            if (user.LastName != Input.LastName)
            {
                user.LastName = Input.LastName;
                changesMade = true;
            }

            if (changesMade)
            {
                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    CurrentUser = user;
                    return Page();
                }

                StatusMessage = "Your profile has been updated";
            }
            else
            {
                StatusMessage = "No changes were made to your profile";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostLogoutAsync()
        {
            await _accountService.SignOutAsync(HttpContext, IdentityConstants.ApplicationScheme);
            _logger.LogInformation("User logged out.");
            return RedirectToPage("/Account/Login");
        }

        // Add these public methods to check user properties
        public async Task<bool> HasPasswordAsync(ApplicationUser user)
        {
            return await _accountService.HasPasswordAsync(user);
        }

        public async Task<IList<UserLoginInfo>> GetLoginsAsync(ApplicationUser user)
        {
            return await _accountService.GetLoginsAsync(user);
        }
    }
}