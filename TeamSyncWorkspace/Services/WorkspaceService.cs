using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TeamSyncWorkspace.Data;
using TeamSyncWorkspace.Models;

namespace TeamSyncWorkspace.Services
{
    public class WorkspaceService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<WorkspaceService> _logger;

        public WorkspaceService(
            AppDbContext context,
            ILogger<WorkspaceService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get a workspace by its ID
        /// </summary>
        public async Task<Workspace> GetWorkspaceByIdAsync(string workspaceId)
        {
            return await _context.Workspaces
                .Include(w => w.Team)
                .FirstOrDefaultAsync(w => w.WorkspaceId == workspaceId);
        }

        /// <summary>
        /// Get workspace for a specific team
        /// </summary>
        public async Task<Workspace> GetWorkspaceForTeamAsync(int teamId)
        {
            return await _context.Workspaces
                .FirstOrDefaultAsync(w => w.TeamId == teamId);
        }

        /// <summary>
        /// Create a new workspace
        /// </summary>
        public async Task<(bool success, string message, Workspace workspace)> CreateWorkspaceAsync(
            int teamId, string name, string description = null)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(name))
            {
                return (false, "Workspace name cannot be empty.", null);
            }

            // Check if team exists
            var teamExists = await _context.Teams.AnyAsync(t => t.TeamId == teamId);
            if (!teamExists)
            {
                return (false, "Team not found.", null);
            }

            // Check if team already has a workspace
            var existingWorkspace = await _context.Workspaces
                .FirstOrDefaultAsync(w => w.TeamId == teamId);

            if (existingWorkspace != null)
            {
                return (false, "Team already has a workspace.", null);
            }

            // Create new workspace
            var workspace = new Workspace
            {
                WorkspaceId = Guid.NewGuid().ToString(),
                TeamId = teamId,
                WorkspaceName = name,
                Description = description,
                CreatedDate = DateTime.Now
            };

            try
            {
                _context.Workspaces.Add(workspace);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created workspace {WorkspaceId} for team {TeamId}",
                    workspace.WorkspaceId, teamId);

                return (true, "Workspace created successfully.", workspace);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating workspace for team {TeamId}", teamId);
                return (false, "An error occurred while creating the workspace.", null);
            }
        }

        /// <summary>
        /// Update an existing workspace
        /// </summary>
        public async Task<(bool success, string message)> UpdateWorkspaceAsync(
            string workspaceId, string name, string description = null)
        {
            var workspace = await _context.Workspaces.FindAsync(workspaceId);
            if (workspace == null)
            {
                return (false, "Workspace not found.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return (false, "Workspace name cannot be empty.");
            }

            try
            {
                workspace.WorkspaceName = name;
                workspace.Description = description;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated workspace {WorkspaceId}", workspaceId);
                return (true, "Workspace updated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating workspace {WorkspaceId}", workspaceId);
                return (false, "An error occurred while updating the workspace.");
            }
        }

        /// <summary>
        /// Get the list of documents for a workspace
        /// </summary>
        public async Task<List<CollabDoc>> GetWorkspaceDocumentsAsync(string workspaceId)
        {
            return await _context.CollabDocs
                .Include(d => d.CreatedByUser)
                .Where(d => d.WorkspaceId == workspaceId)
                .OrderByDescending(d => d.LastModifiedDate)
                .ToListAsync();
        }

        /// <summary>
        /// Get workspace activity summary
        /// </summary>
        public async Task<WorkspaceSummary> GetWorkspaceSummaryAsync(string workspaceId)
        {
            var workspace = await GetWorkspaceByIdAsync(workspaceId);
            if (workspace == null)
            {
                return null;
            }

            int documentsCount = await _context.CollabDocs
                .CountAsync(d => d.WorkspaceId == workspaceId);

            int recentActivitiesCount = await _context.DocActions
                .Where(a => a.CollabDoc.WorkspaceId == workspaceId &&
                           a.Timestamp > DateTime.UtcNow.AddDays(-7))
                .CountAsync();

            int pendingTasksCount = await _context.TimelineTasks
                .CountAsync(t => t.WorkspaceId == workspaceId && !t.IsCompleted);

            return new WorkspaceSummary
            {
                WorkspaceId = workspaceId,
                WorkspaceName = workspace.WorkspaceName,
                DocumentsCount = documentsCount,
                RecentActivitiesCount = recentActivitiesCount,
                PendingTasksCount = pendingTasksCount
            };
        }
    }

    public class WorkspaceSummary
    {
        public string WorkspaceId { get; set; }
        public string WorkspaceName { get; set; }
        public int DocumentsCount { get; set; }
        public int RecentActivitiesCount { get; set; }
        public int PendingTasksCount { get; set; }
    }
}