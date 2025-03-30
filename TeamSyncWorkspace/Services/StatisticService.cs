using TeamSyncWorkspace.Models;
using Microsoft.EntityFrameworkCore;
using TeamSyncWorkspace.Data;

namespace TeamSyncWorkspace.Services
{
    public class StatisticService
    {
        private readonly AppDbContext _context;

        public StatisticService(AppDbContext context)
        {
            _context = context;
        }

        // 📌 Lấy số lượng Task theo trạng thái
        public async Task<List<object>> GetTaskStatusAsync(string workspaceId, DateTime startDate)
        {
            DateTime endDate = startDate.AddDays(6); // Ngày cuối tuần

            var taskData = await _context.TimelineTasks
                .Where(t => t.WorkspaceId == workspaceId && t.DueDate >= startDate && t.DueDate <= endDate)
                .GroupBy(t => t.IsCompleted)
                .Select(g => new
                {
                    label = g.Key ? "Complete" : "Pending",
                    count = g.Count()
                }).ToListAsync();

           

            return taskData.Cast<object>().ToList();
        }

        public async Task<Workspace> GetWorkspaceAsync(string id)
        {
            

            var workspace = await _context.Workspaces
                .AsNoTracking() // Tối ưu nếu chỉ đọc dữ liệu
                .FirstOrDefaultAsync(i => i.WorkspaceId == id);

            return workspace;
        }


        // 📌 Tính phần trăm công việc của từng thành viên
        public async Task<List<object>> GetMemberTaskPercentageAsync(string workspaceId, DateTime startDate)
        {
            DateTime endDate = startDate.AddDays(6); // Ngày cuối của tuần

            // Lấy danh sách công việc có AssignedId trong khoảng thời gian tuần
            var memberTasks = await _context.TimelineTasks
                .Where(t => t.WorkspaceId == workspaceId && t.AssignedId != null && t.DueDate >= startDate && t.DueDate <= endDate)
                .GroupBy(t => t.AssignedId)
                .Select(g => new
                {
                    name = _context.Users
                        .Where(u => u.Id == g.Key)
                        .Select(u => u.FirstName + " " + u.LastName)
                        .FirstOrDefault(),
                    tasks = g.Count()
                }).ToListAsync();

            // Tổng số công việc trong tuần
            int totalAssignedTasks = memberTasks.Sum(m => m.tasks);
            int totalTasks = await _context.TimelineTasks.CountAsync(t => t.WorkspaceId == workspaceId && t.DueDate >= startDate && t.DueDate <= endDate);
            int unassignedTasks = totalTasks - totalAssignedTasks;

            // Tính phần trăm công việc của từng thành viên
            var result = memberTasks.Select(m => new
            {
                name = m.name,
                percentage = totalTasks == 0 ? 0 : ((double)m.tasks / totalTasks * 100)
            }).ToList();

            // Nếu có công việc chưa được giao, thêm vào danh sách
            if (unassignedTasks > 0)
            {
                result.Add(new
                {
                    name = "Unassigned",
                    percentage = totalTasks == 0 ? 0 : ((double)unassignedTasks / totalTasks * 100)
                });
            }

            return result.Cast<object>().ToList();
        }

        public async Task<List<TimelineTask>> GetTasksAsync(string workspaceId)
        {
            return await _context.TimelineTasks
                .Where(t => t.WorkspaceId == workspaceId)
                .ToListAsync();
        }

       

    }
}
