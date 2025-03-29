using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TeamSyncWorkspace.Data;
using TeamSyncWorkspace.Models;

namespace TeamSyncWorkspace.Services
{
    public class TaskService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TaskService> _logger;

        public TaskService(AppDbContext context, ILogger<TaskService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<TimelineTask>> GetTasksForWeekAsync(string workspaceId, DateTime startDate, DateTime endDate)
        {
            return await _context.TimelineTasks
                .Where(t => t.WorkspaceId == workspaceId && t.DueDate >= startDate && t.DueDate <= endDate)
                .OrderBy(t => t.DueDate)
                .ToListAsync();
        }
        public async Task<List<ApplicationUser>> GetUsersByWorkspaceIdAsync(string workspaceId)
        {
            try
            {
                var teamId = await _context.Workspaces
                    .Where(w => w.WorkspaceId == workspaceId)
                    .Select(w => w.TeamId)
                    .FirstOrDefaultAsync();

                if (teamId == 0)
                {
                    _logger.LogWarning($"Workspace ID {workspaceId} không có team.");
                    return new List<ApplicationUser>();
                }

                var users = await _context.TeamMembers
                    .Where(tm => tm.TeamId == teamId)
                    .Select(tm => tm.User) // Join với bảng ApplicationUser
                    .ToListAsync();

                return users;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy user của workspace {workspaceId}: {ex.Message}");
                return new List<ApplicationUser>();
            }
        }

        public async Task<TimelineTask> AddTaskAsync(string workspaceId, int userId, string taskDescription, DateTime dueDate)
        {
            var newTask = new TimelineTask
            {
                WorkspaceId = workspaceId,
                AssignedId = userId, // Thêm UserId vào task
                TaskDescription = taskDescription,
                DueDate = dueDate,
                IsCompleted = false
            };

            _context.TimelineTasks.Add(newTask);
            await _context.SaveChangesAsync();

            _logger.LogInformation("New task added: {TaskDescription} for workspace {WorkspaceId}, assigned to user {UserId} on {DueDate}",
                                    taskDescription, workspaceId, userId, dueDate);

            return newTask;
        }


        public async Task<bool> UpdateTaskAsync(int taskId,int? AssignedID, string taskDescription, DateTime dueDate, bool isCompleted)
        {
            var task = await _context.TimelineTasks.FindAsync(taskId);
            if (task == null) return false;
            task.AssignedId = AssignedID;
            task.TaskDescription = taskDescription;
            task.DueDate = dueDate;
            task.IsCompleted = isCompleted;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Task {TaskId} updated successfully", taskId);
            return true;
        }

        public async Task<bool> DeleteTaskAsync(int taskId)
        {
            var task = await _context.TimelineTasks.FindAsync(taskId);
            if (task == null) return false;

            _context.TimelineTasks.Remove(task);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Task {TaskId} deleted successfully", taskId);
            return true;
        }
    }
}
