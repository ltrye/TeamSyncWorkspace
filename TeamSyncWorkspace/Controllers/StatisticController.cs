using Microsoft.AspNetCore.Mvc;
using TeamSyncWorkspace.Services;

namespace TeamSyncWorkspace.Controllers
{
    [Route("api/statistics")]
    [ApiController]
    public class StatisticController : ControllerBase
    {
        private readonly StatisticService _statisticService;

        public StatisticController(StatisticService statisticService)
        {
            _statisticService = statisticService;
        }

        // 🟢 API: Lấy số lượng Task theo trạng thái
        [HttpGet("task-status/{workspaceId}")]
        public async Task<IActionResult> GetTaskStatus(string workspaceId, [FromQuery] DateTime startDate)
        {
            var data = await _statisticService.GetTaskStatusAsync(workspaceId, startDate);
            return Ok(data);
        }


        // 🟢 API: Lấy phần trăm công việc của từng thành viên trong Workspace
        [HttpGet("member-tasks/{workspaceId}")]
        public async Task<IActionResult> GetMemberTaskPercentage(string workspaceId, [FromQuery] DateTime startDate)
        {
            var data = await _statisticService.GetMemberTaskPercentageAsync(workspaceId, startDate);
            return Ok(data);
        }

    }
}
