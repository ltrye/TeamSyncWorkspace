using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.IdentityModel.Tokens;
using System.Threading.Tasks;
using TeamSyncWorkspace.Models;
using TeamSyncWorkspace.Services;

namespace TeamSyncWorkspace.Pages.Teams.Timeline
{
    public class IndexModel : PageModel
    {
        private readonly TaskService _taskService;
        private readonly NotificationService _notificationService;

        public IndexModel(TaskService taskService, NotificationService notificationService)
        {
            _taskService = taskService;
            _notificationService = notificationService;
        }

        public List<TimelineTask> Tasks { get; private set; }

        [BindProperty]
        public string TaskDescription { get; set; }

        [BindProperty]
        public DateTime DueDate { get; set; }

        [BindProperty]
        public bool IsCompleted { get; set; } = false;

        [BindProperty]
        public int TaskId { get; set; }
        [BindProperty]
        public int? AssignedId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string WorkspaceId { get; set; }
        [BindProperty]
        public List<ApplicationUser> Users { get; set; } = new();
        [BindProperty]
        public int UserId { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(WorkspaceId))
            {
                return BadRequest("Missing workspaceId");
            }

            DateTime startDate = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek); // Start of the current week
            DateTime endDate = startDate.AddDays(6);

            Tasks = await _taskService.GetTasksForWeekAsync(WorkspaceId, startDate, endDate);
            foreach (var task in Tasks)
            {
                Console.WriteLine($"TaskID: {task.TaskId}, AssignedId: {task.AssignedId}");
            }
            Users = await _taskService.GetUsersByWorkspaceIdAsync(WorkspaceId);
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
            if (AssignedId == 0)
            {
                AssignedId = null;
            }

            

            await _taskService.AddTaskAsync(WorkspaceId, AssignedId, TaskDescription, DueDate);
            if (AssignedId != null)
            {
                string title = IsCompleted ? "Done Task" : "Assign Task";
                string message = IsCompleted
                    ? $"You have done the task {TaskDescription}"
                    : $"You have been assigned the task {TaskDescription}";

                await _notificationService.CreateNotificationAsync((int)AssignedId, title, message);
            }
            return RedirectToPage(new { workspaceId = WorkspaceId });
        }


        public async Task<IActionResult> OnPostUpdateTaskAsync()
        {
            if (TaskId <= 0)
            {
                return BadRequest("Invalid task ID");
            }

            bool success = await _taskService.UpdateTaskAsync(TaskId,AssignedId, TaskDescription, DueDate, IsCompleted);
            if (IsCompleted && AssignedId != null)
            {
                _notificationService.CreateNotificationAsync((int)AssignedId, "Done Task", "You have done the task" + TaskDescription);
            }
            if (!IsCompleted && AssignedId != null)
            {
                _notificationService.CreateNotificationAsync((int)AssignedId, "Assign Task", "You have been assigned the task" + TaskDescription);
            }
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
