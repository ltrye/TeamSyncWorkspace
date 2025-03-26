using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeamSyncWorkspace.Data;

namespace TeamSyncWorkspace.Controllers
{
    [Route("api/tasks")]
    [ApiController]
    public class TimelineTaskController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TimelineTaskController> _logger;

        public TimelineTaskController(AppDbContext context, ILogger<TimelineTaskController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetTasks([FromQuery] string workspaceId, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            _logger.LogInformation("Fetching tasks for workspaceId: {WorkspaceId}, StartDate: {StartDate}, EndDate: {EndDate}",
                                  workspaceId, startDate, endDate);

            if (string.IsNullOrEmpty(workspaceId))
            {
                return BadRequest("Missing workspaceId");
            }

            var tasks = await _context.TimelineTasks
                .Where(t => t.WorkspaceId == workspaceId && t.DueDate >= startDate && t.DueDate <= endDate)
                .OrderBy(t => t.DueDate)
                .ToListAsync();

            return Ok(tasks);
        }
    }
}
