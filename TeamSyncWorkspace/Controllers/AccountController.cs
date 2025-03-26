using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TeamSyncWorkspace.Models;
using TeamSyncWorkspace.Services;

namespace TeamSyncWorkspace.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<AccountController> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
        }

        /// <summary>
        /// Logs out the current user
        /// </summary>
        /// <param name="returnUrl">Optional URL to redirect after logout</param>
        /// <returns>Success status and redirect info</returns>
        [HttpPost("logout")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout(string returnUrl = "/Account/Login")
        {
            // Get the current user for logging
            var user = await _userManager.GetUserAsync(User);

            // Sign out the user
            await _signInManager.SignOutAsync();

            // Log the logout
            if (user != null)
            {
                _logger.LogInformation("User {UserId} logged out.", user.Id);
            }

            // If this is an API call with no redirect expected
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Ok(new { success = true, redirectUrl = returnUrl });
            }

            // Otherwise redirect to the specified URL
            return LocalRedirect(returnUrl ?? "/");
        }

        /// <summary>
        /// Gets information about the current user
        /// </summary>
        /// <returns>User information</returns>
        [HttpGet("current")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            return Ok(new
            {
                id = user.Id,
                email = user.Email,
                firstName = user.FirstName,
                lastName = user.LastName,
                profileImageUrl = user.ProfileImageUrl ?? $"https://ui-avatars.com/api/?name={Uri.EscapeDataString($"{user.FirstName} {user.LastName}")}&size=128&background=4a68bd&color=ffffff"
            });
        }

        /// <summary>
        /// Checks if the current user is authenticated
        /// </summary>
        /// <returns>Authentication status</returns>
        [HttpGet("status")]
        public IActionResult GetAuthStatus()
        {
            return Ok(new { isAuthenticated = User.Identity?.IsAuthenticated ?? false });
        }
    }
}