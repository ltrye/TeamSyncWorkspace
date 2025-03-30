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
        public async Task<List<object>> GetTaskStatusAsync(string workspaceId)
        {
            var tasks = await _context.TimelineTasks
                .Where(t => t.WorkspaceId == workspaceId)
                .GroupBy(t => t.IsCompleted)
                .Select(g => new
                {
                    label = g.Key ? "Complete" : "Pending",
                    value = g.Count()
                }).ToListAsync();

            return tasks.Cast<object>().ToList();
        }
        public async Task<Workspace> GetWorkspaceAsync(string id)
        {
            

            var workspace = await _context.Workspaces
                .AsNoTracking() // Tối ưu nếu chỉ đọc dữ liệu
                .FirstOrDefaultAsync(i => i.WorkspaceId == id);

            return workspace;
        }


        // 📌 Tính phần trăm công việc của từng thành viên
        public async Task<List<object>> GetMemberTaskPercentageAsync(string workspaceId)
        {
            // Lấy danh sách công việc có AssignedId
            var memberTasks = await _context.TimelineTasks
                .Where(t => t.WorkspaceId == workspaceId && t.AssignedId != null)
                .GroupBy(t => t.AssignedId)
                .Select(g => new
                {
                    name = _context.Users
                        .Where(u => u.Id == g.Key)
                        .Select(u => u.FirstName + " " + u.LastName)
                        .FirstOrDefault(),
                    tasks = g.Count()
                }).ToListAsync();

            // Tính tổng số công việc
            int totalAssignedTasks = memberTasks.Sum(m => m.tasks);
            int totalTasks = await _context.TimelineTasks.CountAsync(t => t.WorkspaceId == workspaceId);
            int unassignedTasks = totalTasks - totalAssignedTasks;

            // Tạo danh sách kết quả
            var result = memberTasks.Select(m => new
            {
                name = m.name,
                percentage = totalTasks == 0 ? 0 : ((double)m.tasks / totalTasks * 100)
            }).ToList();

            // Thêm "Unassigned" vào danh sách nếu có công việc chưa được nhận
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
