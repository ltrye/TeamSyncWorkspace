using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TeamSyncWorkspace.Models;
using TeamSyncWorkspace.Services;

namespace TeamSyncWorkspace.Pages.Statistic
{
    public class IndexModel : PageModel
    {


        private readonly StatisticService _statisticService;

        public IndexModel(StatisticService statisticService)
        {
            _statisticService = statisticService;
        }

        [BindProperty(SupportsGet = true)]
        public string WorkspaceId { get; set; }

        public Workspace Workspace { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(WorkspaceId))
            {
                return BadRequest("Missing workspaceId");
            }

            // Lấy thông tin workspace từ StatisticService
            Workspace = await _statisticService.GetWorkspaceAsync(WorkspaceId);
            if (Workspace == null)
            {
                return NotFound("Workspace not found");
            }

            // Tiếp tục xử lý với WorkspaceId nếu cần
            return Page();
        }
    }
}
