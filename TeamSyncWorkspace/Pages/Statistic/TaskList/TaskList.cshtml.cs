using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TeamSyncWorkspace.Models;
using TeamSyncWorkspace.Services;

namespace TeamSyncWorkspace.Pages.TaskList
{
    public class TaskListModel : PageModel
    {
        private readonly TaskService _taskService;
        private readonly StatisticService _statisticService;
        private readonly Data.AppDbContext _context;
        private readonly NotificationService _notificationService;
        public TaskListModel(StatisticService statisticService, Data.AppDbContext appDbContext, NotificationService notificationService, TaskService taskService)
        {
            _statisticService = statisticService;
            this._context = appDbContext;
            this._notificationService = notificationService;
            _taskService = taskService;
        }

        [BindProperty(SupportsGet = true)]
        public string WorkspaceId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string FilterType { get; set; } // "status" hoặc "member"

        [BindProperty(SupportsGet = true)]
        public string FilterValue { get; set; } // "Complete", "Pending", hoặc AssignedId

        public int? AssignedId { get; set; }
        public Workspace Workspace { get; set; }
        public List<ApplicationUser> Users { get; set; }
        public List<TimelineTask> Tasks { get; set; }
        public async Task<IActionResult> OnPostUpdateTaskAsync(int TaskId, bool IsCompleted, int? AssignedId, string FilterType, string FilterValue)
        {
            var task = await _context.TimelineTasks.FindAsync(TaskId);
            if (task == null)
            {
                return NotFound();
            }
            // Cập nhật trạng thái hoàn thành và người được giao
            task.IsCompleted = IsCompleted;
            task.AssignedId = AssignedId;
            if (AssignedId != null)
            {
                string title = IsCompleted ? "Done Task" : "Assign Task";
                string message = IsCompleted
                    ? $"You have done the task {task.TaskDescription}"
                    : $"You have been assigned the task {task.TaskDescription}";

                await _notificationService.CreateNotificationAsync((int)AssignedId, title, message);
            }



            await _context.SaveChangesAsync();

            // Cập nhật giá trị FilterType và FilterValue để duy trì bộ lọc
            this.FilterType = FilterType;
            this.FilterValue = FilterValue;

            // Lấy danh sách người dùng
            Users = await _taskService.GetUsersByWorkspaceIdAsync(WorkspaceId); 

            // Lấy thông tin workspace
            Workspace = await _statisticService.GetWorkspaceAsync(task.WorkspaceId);
            if (Workspace == null)
            {
                return NotFound("Workspace not found");
            }

            // Lấy danh sách tất cả các task trong workspace
            var allTasks = await _statisticService.GetTasksAsync(task.WorkspaceId);

            // Áp dụng bộ lọc nếu có
            if (!string.IsNullOrEmpty(FilterType) && !string.IsNullOrEmpty(FilterValue))
            {
                switch (FilterType)
                {
                    case "status":
                        bool isCompleted = FilterValue.Equals("Complete");
                        Tasks = allTasks.Where(t => t.IsCompleted == isCompleted).ToList();
                        break;

                    case "member":
                        if (FilterValue == "Unassigned")
                        {
                            Tasks = allTasks.Where(t => t.AssignedId == null).ToList();
                        }
                        else
                        {
                            var users = await _context.Users
                                .Where(u => (u.FirstName + " " + u.LastName) == FilterValue)
                                .Select(u => u.Id)
                                .ToListAsync();

                            Tasks = allTasks.Where(t => t.AssignedId.HasValue && users.Contains(t.AssignedId.Value)).ToList();
                        }
                        break;

                    case "day":
                        if (DateTime.TryParse(FilterValue, out DateTime filterDate))
                        {
                            Tasks = allTasks.Where(t => t.DueDate.Date == filterDate.Date).ToList();
                        }
                        break;

                    default:
                        Tasks = allTasks;
                        break;
                }
            }
            else
            {
                Tasks = allTasks;
            }

            return Page();
        }


        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(WorkspaceId))
            {
                return BadRequest("Missing workspaceId");
            }

            // Lấy danh sách người dùng
            Users = await _taskService.GetUsersByWorkspaceIdAsync(WorkspaceId);

            // Lấy thông tin workspace
            Workspace = await _statisticService.GetWorkspaceAsync(WorkspaceId);
            if (Workspace == null)
            {
                return NotFound("Workspace not found");
            }

            // Lấy danh sách tất cả các task trong workspace
            var allTasks = await _statisticService.GetTasksAsync(WorkspaceId);

            // Áp dụng bộ lọc nếu có
            if (!string.IsNullOrEmpty(FilterType) && !string.IsNullOrEmpty(FilterValue))
            {
                switch (FilterType)
                {
                    case "status":
                        bool isCompleted = FilterValue.Equals("Complete", StringComparison.OrdinalIgnoreCase);
                        Tasks = allTasks.Where(t => t.IsCompleted == isCompleted).ToList();
                        break;

                    case "member":
                        if (FilterValue == "Unassigned")
                        {
                            Tasks = allTasks.Where(t => t.AssignedId == null).ToList();
                        }
                        else
                        {
                            var users = await _context.Users
                                .Where(u => (u.FirstName + " " + u.LastName) == FilterValue)
                                .Select(u => u.Id)
                                .ToListAsync();

                            Tasks = allTasks.Where(t => t.AssignedId.HasValue && users.Contains(t.AssignedId.Value)).ToList();
                        }
                        break;

                    case "day":
                        if (DateTime.TryParse(FilterValue, out DateTime filterDate))
                        {
                            Tasks = allTasks.Where(t => t.DueDate.Date == filterDate.Date).ToList();
                        }
                        break;

                    default:
                        Tasks = allTasks;
                        break;
                }
            }
            else
            {
                Tasks = allTasks;
            }

            return Page();
        }

    }
}
