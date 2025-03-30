using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeamSyncWorkspace.Models;

namespace TeamSyncWorkspace.Services.ColabDocServices
{
    public class TempDocumentManager
    {
        private readonly IMemoryCache _cache;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TempDocumentManager> _logger;
        private static readonly ConcurrentDictionary<string, Timer> _persistenceTimers = new ConcurrentDictionary<string, Timer>();
        private static readonly TimeSpan _persistInterval = TimeSpan.FromSeconds(10);

        public TempDocumentManager(
            IMemoryCache cache,
            IServiceScopeFactory scopeFactory,
            ILogger<TempDocumentManager> logger)
        {
            _cache = cache;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task<TempDocument> InitializeDocumentAsync(int documentId)
        {
            string cacheKey = GetCacheKey(documentId);
            string documentKey = GetDocumentKey(documentId);

            // Check if already in cache
            if (_cache.TryGetValue(cacheKey, out TempDocument tempDoc))
            {
                Console.WriteLine("Document found in cache");
                return tempDoc;
            }

            // Get from database using a new scope
            using (var scope = _scopeFactory.CreateScope())
            {
                var documentService = scope.ServiceProvider.GetRequiredService<DocumentService>();
                var document = await documentService.GetDocumentByIdAsync(documentId);
                if (document == null)
                {
                    _logger.LogWarning("Document {DocumentId} not found in database", documentId);
                    return null;
                }

                // Create temp document
                tempDoc = new TempDocument
                {
                    DocumentId = documentId,
                    Content = document.Content,
                    LastSavedContent = document.Content, // Add this line
                    LastSaved = DateTime.UtcNow,
                    IsDirty = false
                };

                // Add to cache
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetPriority(CacheItemPriority.High);
                _cache.Set(cacheKey, tempDoc, cacheOptions);
            }

            // Create a timer to periodically persist changes
            Timer timer = new(PersistDocumentCallback, documentId, _persistInterval, _persistInterval);
            _persistenceTimers.TryAdd(documentKey, timer);

            _logger.LogInformation("Created temporary document for {DocumentId}", documentId);
            return tempDoc;
        }

        // Separated callback method for the timer to avoid async void
        private void PersistDocumentCallback(object state)
        {
            int documentId = (int)state;

            // Use Task.Run to safely handle the async operation from a timer callback
            Task.Run(async () =>
            {
                try
                {
                    await PersistDocumentAsync(documentId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in persistence timer callback for document {DocumentId}", documentId);
                }
            });
        }

        public async Task<bool> UpdateDocumentAsync(int documentId, string content)
        {
            string cacheKey = GetCacheKey(documentId);

            // Update document in cache
            if (_cache.TryGetValue(cacheKey, out TempDocument tempDoc))
            {
                tempDoc.Content = content;
                tempDoc.IsDirty = true;
                tempDoc.LastModified = DateTime.UtcNow;

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetPriority(CacheItemPriority.High);
                _cache.Set(cacheKey, tempDoc, cacheOptions);

                _logger.LogInformation("Updated temporary document {DocumentId} in cache", documentId);
                return true;
            }

            // Fallback to direct update if not in cache
            using (var scope = _scopeFactory.CreateScope())
            {
                var documentService = scope.ServiceProvider.GetRequiredService<DocumentService>();
                await documentService.UpdateDocumentContentAsync(documentId, content, 0);
            }

            _logger.LogWarning("Temp document not found in cache for {DocumentId}, updating directly", documentId);
            return false;
        }

        public async Task PersistDocumentAsync(int documentId)
        {
            string cacheKey = GetCacheKey(documentId);

            try
            {
                if (_cache.TryGetValue(cacheKey, out TempDocument tempDoc))
                {
                    // Only save if the document is marked as dirty
                    if (tempDoc.IsDirty)
                    {
                        // Check if document has been modified in the last 10 seconds
                        TimeSpan timeSinceLastModification = DateTime.UtcNow - tempDoc.LastModified;
                        if (timeSinceLastModification < TimeSpan.FromSeconds(10))
                        {
                            // Document is still being actively modified, defer persisting
                            _logger.LogDebug("Deferring persistence for document {DocumentId} as it was modified recently", documentId);
                            return;
                        }

                        // Check if content has actually changed from last saved version
                        if (!tempDoc.HasContentChanged())
                        {
                            _logger.LogDebug("Document {DocumentId} content unchanged, skipping persistence", documentId);

                            // Even though we're not saving, update the dirty flag
                            tempDoc.IsDirty = false;
                            _cache.Set(cacheKey, tempDoc);

                            return;
                        }

                        // Create a new scope to get a fresh DbContext
                        using (var scope = _scopeFactory.CreateScope())
                        {
                            var documentService = scope.ServiceProvider.GetRequiredService<DocumentService>();
                            await documentService.UpdateDocumentContentAsync(documentId, tempDoc.Content, 0);
                        }

                        // Update tracking properties after successful save
                        tempDoc.IsDirty = false;
                        tempDoc.LastSaved = DateTime.UtcNow;
                        tempDoc.LastSavedContent = tempDoc.Content;

                        // Update the cache
                        _cache.Set(cacheKey, tempDoc);

                        _logger.LogInformation("Persisted temporary document {DocumentId} to database", documentId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error persisting document {DocumentId}", documentId);
            }
        }

        public async Task FinalizeDocumentAsync(int documentId)
        {
            string documentKey = GetDocumentKey(documentId);
            string cacheKey = GetCacheKey(documentId);

            try
            {
                // Stop the persistence timer
                if (_persistenceTimers.TryRemove(documentKey, out Timer timer))
                {
                    timer.Dispose();
                }

                // Get the temporary document from cache
                if (_cache.TryGetValue(cacheKey, out TempDocument tempDoc))
                {
                    // Save the final state to the database if it has changes
                    if (tempDoc.IsDirty)
                    {
                        // Create a new scope to get a fresh DbContext
                        using (var scope = _scopeFactory.CreateScope())
                        {
                            var documentService = scope.ServiceProvider.GetRequiredService<DocumentService>();
                            await documentService.UpdateDocumentContentAsync(documentId, tempDoc.Content, 0);
                        }

                        _logger.LogInformation("Final persistence of document {DocumentId} completed", documentId);
                    }

                    // Remove from cache
                    _cache.Remove(cacheKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during final persistence of document {DocumentId}", documentId);
            }
        }

        public TempDocument GetDocument(int documentId)
        {
            string cacheKey = GetCacheKey(documentId);
            _cache.TryGetValue(cacheKey, out TempDocument tempDoc);
            return tempDoc;
        }

        private static string GetCacheKey(int documentId) => $"temp_doc_{documentId}";
        private static string GetDocumentKey(int documentId) => $"document_{documentId}";
    }

    public class TempDocument
    {
        public int DocumentId { get; set; }
        public string Content { get; set; }
        public string LastSavedContent { get; set; }
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
        public DateTime LastSaved { get; set; }
        public bool IsDirty { get; set; }

        // Helper method to check if content has meaningfully changed
        public bool HasContentChanged()
        {
            if (string.IsNullOrEmpty(LastSavedContent) && string.IsNullOrEmpty(Content))
                return false;

            return Content != LastSavedContent;
        }
    }
}