using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;
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

    public async Task<bool> UploadFileAsync(IFormFile file, int folderId)
    {
        if (file == null || file.Length == 0)
            return false; // Invalid file

        // Generate unique file name
        var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
        var uploadPath = Path.Combine(_environment.WebRootPath, "uploads", uniqueFileName);

        // Ensure "uploads" directory exists
        var directory = Path.Combine(_environment.WebRootPath, "uploads");
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
            FilePath = $"/uploads/{uniqueFileName}", // Store relative path
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
