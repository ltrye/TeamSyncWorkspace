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

        public async Task<TimelineTask> AddTaskAsync(string workspaceId, string taskDescription, DateTime dueDate)
        {
            var newTask = new TimelineTask
            {
                WorkspaceId = workspaceId,
                TaskDescription = taskDescription,
                DueDate = dueDate,
                IsCompleted = false
            };

            _context.TimelineTasks.Add(newTask);
            await _context.SaveChangesAsync();

            _logger.LogInformation("New task added: {TaskDescription} for workspace {WorkspaceId} on {DueDate}", taskDescription, workspaceId, dueDate);
            return newTask;
        }

        public async Task<bool> UpdateTaskAsync(int taskId, string taskDescription, DateTime dueDate, bool isCompleted)
        {
            var task = await _context.TimelineTasks.FindAsync(taskId);
            if (task == null) return false;

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
