using Microsoft.EntityFrameworkCore;
using TeamSyncWorkspace.Data;
using TeamSyncWorkspace.Models;

namespace TeamSyncWorkspace.Services
{
    public class FolderService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<FolderService> _logger;
        private readonly FileService _fileService;

        public FolderService(AppDbContext context, ILogger<FolderService> logger, FileService fileService)
        {
            _context = context;
            _logger = logger;
            _fileService = fileService;
        }

        //Check root folder, if not
        //then create it, if exists then 
        //use it, nhu singleton
        //*Root folder la nhu kieu google drive, hay myPC: Folder goc chua nhg file va folder khac
        public async Task<Folder> EnsureRootFolderAsync(string workspaceId, int createdByUserId)
        {
            var rootFolder = await _context.Folders
                .FirstOrDefaultAsync(f => f.WorkspaceId == workspaceId && f.ParentFolderId == null);

            if (rootFolder == null)
            {
                rootFolder = new Folder
                {
                    FolderName = "Root",
                    WorkspaceId = workspaceId,
                    ParentFolderId = null,
                    CreatedByUserId = createdByUserId,
                    CreatedDate = DateTime.UtcNow
                };

                _context.Folders.Add(rootFolder);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created root folder for workspace {WorkspaceId}", workspaceId);
            }

            return rootFolder;
        }

        public async Task<List<Folder>> GetSubFoldersAsync(int parentFolderId)
        {
            return await _context.Folders
                .Where(f => f.ParentFolderId == parentFolderId) // Get only subfolders
                .OrderBy(f => f.FolderName) // Sort alphabetically
                .ToListAsync();
        }


        //Create a sub folder
        public async Task<Folder> CreateSubFolderAsync(string workspaceId, string folderName, int parentFolderId, int createdByUserId)
        {
            var parentFolder = await _context.Folders.FindAsync(parentFolderId);
            if (parentFolder == null || parentFolder.WorkspaceId != workspaceId)
            {
                throw new InvalidOperationException("Invalid parent folder");
            }

            var newFolder = new Folder
            {
                FolderName = folderName,
                WorkspaceId = workspaceId,
                ParentFolderId = parentFolderId,
                CreatedByUserId = createdByUserId,
                CreatedDate = DateTime.UtcNow
            };

            _context.Folders.Add(newFolder);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created subfolder '{FolderName}' under parent folder {ParentFolderId}", folderName, parentFolderId);

            return newFolder;
        }

        //Get all folders
        public async Task<List<Folder>> GetFoldersAsync(string workspaceId)
        {
            return await _context.Folders
                .Where(f => f.WorkspaceId == workspaceId)
                .Include(f => f.ChildFolders)
                .ToListAsync();
        }

        //Get folder by id
        public async Task<Folder> GetFolderAsync(int folderId)
        {
            return await _context.Folders
                .Include(f => f.ParentFolder) // Include ParentFolder to navigate back
                .FirstOrDefaultAsync(f => f.FolderId == folderId);
        }

        public async Task<bool> DeleteFolderAsync(int folderId)
        {
            var folder = await _context.Folders
                .Include(f => f.ChildFolders) // Load subfolders
                .FirstOrDefaultAsync(f => f.FolderId == folderId);

            if (folder == null)
            {
                return false; // Folder not found
            }

            // Delete all files in this folder
            await _fileService.DeleteFilesByFolderIdAsync(folderId);

            // Recursively delete subfolders
            foreach (var child in folder.ChildFolders.ToList())
            {
                await DeleteFolderAsync(child.FolderId);
            }

            // Delete the folder itself
            _context.Folders.Remove(folder);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}
