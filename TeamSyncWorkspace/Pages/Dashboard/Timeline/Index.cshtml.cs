using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TeamSyncWorkspace.Models;
using TeamSyncWorkspace.Services;

namespace TeamSyncWorkspace.Pages.Teams.Timeline
{
    public class IndexModel : PageModel
    {
        private readonly TaskService _taskService;

        public IndexModel(TaskService taskService)
        {
            _taskService = taskService;
        }

        public List<TimelineTask> Tasks { get; private set; }

        [BindProperty]
        public string TaskDescription { get; set; }

        [BindProperty]
        public DateTime DueDate { get; set; }

        [BindProperty]
        public bool IsCompleted { get; set; }

        [BindProperty]
        public int TaskId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string WorkspaceId { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(WorkspaceId))
            {
                return BadRequest("Missing workspaceId");
            }

            DateTime startDate = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek); // Start of the current week
            DateTime endDate = startDate.AddDays(6);

            Tasks = await _taskService.GetTasksForWeekAsync(WorkspaceId, startDate, endDate);
            return Page();
        }

        public async Task<IActionResult> OnPostAddTaskAsync()
        {
            if (string.IsNullOrEmpty(WorkspaceId))
            {
                return BadRequest("Missing workspaceId");
            }

            if (string.IsNullOrWhiteSpace(TaskDescription) || DueDate == default)
            {
                ModelState.AddModelError("", "Task description and due date are required.");
                return Page();
            }

            await _taskService.AddTaskAsync(WorkspaceId, TaskDescription, DueDate);
            return RedirectToPage(new { workspaceId = WorkspaceId });
        }

        public async Task<IActionResult> OnPostUpdateTaskAsync()
        {
            if (TaskId <= 0)
            {
                return BadRequest("Invalid task ID");
            }

            bool success = await _taskService.UpdateTaskAsync(TaskId, TaskDescription, DueDate, IsCompleted);

            if (!success)
            {
                return NotFound("Task not found");
            }

            return RedirectToPage(new { workspaceId = WorkspaceId });
        }

        public async Task<IActionResult> OnPostDeleteTaskAsync()
        {
            if (TaskId <= 0)
            {
                return BadRequest("Invalid task ID");
            }

            bool success = await _taskService.DeleteTaskAsync(TaskId);

            if (!success)
            {
                return NotFound("Task not found");
            }

            return RedirectToPage(new { workspaceId = WorkspaceId });
        }
    }
}
