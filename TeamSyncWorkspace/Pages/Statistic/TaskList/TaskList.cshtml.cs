using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TeamSyncWorkspace.Models;
using TeamSyncWorkspace.Services;

namespace TeamSyncWorkspace.Pages.TaskList
{
    public class TaskListModel : PageModel
    {
        private readonly StatisticService _statisticService;
        private readonly Data.AppDbContext _context;

        public TaskListModel(StatisticService statisticService, Data.AppDbContext appDbContext)
        {
            _statisticService = statisticService;
            this._context = appDbContext;
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

            task.IsCompleted = IsCompleted;
            task.AssignedId = AssignedId;

            await _context.SaveChangesAsync();

            // Cập nhật giá trị FilterType và FilterValue
            this.FilterType = FilterType;
            this.FilterValue = FilterValue;

            // Lấy lại danh sách task giống như OnGetAsync()
            Users = await _context.Users.ToListAsync();
            Workspace = await _statisticService.GetWorkspaceAsync(task.WorkspaceId);
            if (Workspace == null)
            {
                return NotFound("Workspace not found");
            }

            var allTasks = await _statisticService.GetTasksAsync(task.WorkspaceId);

            if (!string.IsNullOrEmpty(FilterType) && !string.IsNullOrEmpty(FilterValue))
            {
                if (FilterType == "status")
                {
                    bool isCompleted = FilterValue.Equals("Complete");
                    Tasks = allTasks.Where(t => t.IsCompleted == isCompleted).ToList();
                }
                else if (FilterType == "member")
                {
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
                }
                else
                {
                    Tasks = allTasks;
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
            Users = await _context.Users.ToListAsync();
            // Lấy thông tin workspace
            Workspace = await _statisticService.GetWorkspaceAsync(WorkspaceId);
            if (Workspace == null)
            {
                return NotFound("Workspace not found");
            }

            // Lấy danh sách task
            var allTasks = await _statisticService.GetTasksAsync(WorkspaceId);

            if (!string.IsNullOrEmpty(FilterType) && !string.IsNullOrEmpty(FilterValue))
            {
                if (FilterType == "status")
                {
                    bool isCompleted = FilterValue.Equals("Complete");
                    Tasks = allTasks.Where(t => t.IsCompleted == isCompleted).ToList();
                }
                else if (FilterType == "member")
                {
                    if (FilterValue == "Unassigned")
                    {
                        Tasks = allTasks.Where(t => t.AssignedId == null).ToList();
                    }
                    else
                    {
                        // Lấy danh sách Id của người dùng có tên khớp với FilterValue
                        var users = await _context.Users
                            .Where(u => (u.FirstName + " " + u.LastName) == FilterValue)
                            .Select(u => u.Id)
                            .ToListAsync();

                        // Lọc các task mà AssignedId nằm trong danh sách Id của người dùng
                        Tasks = allTasks.Where(t => t.AssignedId.HasValue && users.Contains(t.AssignedId.Value)).ToList();
                    }
                }
                else
                {
                    Tasks = allTasks;
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
