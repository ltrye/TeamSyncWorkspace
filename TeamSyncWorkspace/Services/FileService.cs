using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TeamSyncWorkspace.Data;
using TeamSyncWorkspace.Models;
using File = TeamSyncWorkspace.Models.File;

public class FileService
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public FileService(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }
    public async Task<string> UploadProfilePictureAsync(IFormFile file, string userId)
    {
        if (file == null || file.Length == 0)
            return null; // Invalid file

        // Check if it's an image file
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(fileExtension))
            return null; // Not an allowed image type

        // Generate unique file name
        var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";

        // Create profile pictures directory
        var profileDirectory = Path.Combine(Path.GetTempPath(), "uploads", "profile");
        if (!Directory.Exists(profileDirectory))
        {
            Directory.CreateDirectory(profileDirectory);
        }

        var uploadPath = Path.Combine(profileDirectory, uniqueFileName);

        // Save file to server
        using (var fileStream = new FileStream(uploadPath, FileMode.Create))
        {
            await file.CopyToAsync(fileStream);
        }

        // Return the relative path to the file
        return $"/files/profile/{uniqueFileName}";
    }
    public async Task<bool> UploadFileAsync(IFormFile file, int folderId)
    {


        if (file == null || file.Length == 0)
            return false; // Invalid file

        Console.WriteLine($"[INFO] Uploading file: {file.FileName} to folder ID: {folderId}");

        // Generate unique file name
        var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
        var uploadPath = Path.Combine(Path.GetTempPath(), "uploads", uniqueFileName);

        // Ensure "uploads" directory exists
        var directory = Path.Combine(Path.GetTempPath(), "uploads");
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Save file to server
        using (var fileStream = new FileStream(uploadPath, FileMode.Create))
        {
            await file.CopyToAsync(fileStream);
        }

        // Save file metadata to the database
        var fileEntity = new File
        {
            FolderId = folderId,
            FileName = file.FileName,
            FilePath = $"/files/{uniqueFileName}", // Store relative path
            UploadedDate = DateTime.UtcNow
        };

        _context.Files.Add(fileEntity);
        await _context.SaveChangesAsync();

        return true;
    }

    //Get File in folder
    public async Task<List<File>> GetFilesAsync(int folderId)
    {
        return await _context.Files
            .Where(f => f.FolderId == folderId)
            .ToListAsync();
    }

    public async Task<bool> DeleteFileAsync(int fileId)
    {
        Console.WriteLine($"[INFO] Deleting file with ID: {fileId}");
        try
        {
            var file = await _context.Files.FindAsync(fileId);
            if (file == null)
            {
                Console.WriteLine($"[ERROR] File with ID {fileId} not found.");
                return false;
            }

            _context.Files.Remove(file);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Exception in DeleteFileAsync: {ex.Message}");
            return false;
        }
    }

    //For delete file in folder
    public async Task DeleteFilesByFolderIdAsync(int folderId)
    {
        var files = await _context.Files.Where(f => f.FolderId == folderId).ToListAsync();

        if (files.Any())
        {
            _context.Files.RemoveRange(files);
            await _context.SaveChangesAsync();
        }
    }
}
