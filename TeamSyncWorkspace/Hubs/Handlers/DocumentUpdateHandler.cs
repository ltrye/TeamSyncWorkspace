using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using TeamSyncWorkspace.Services;
using TeamSyncWorkspace.Services.ColabDocServices;

namespace TeamSyncWorkspace.Hubs.Handlers
{
    public class DocumentUpdateHandler
    {
        private readonly TempDocumentManager _documentManager;
        private readonly ILogger<DocumentUpdateHandler> _logger;

        public DocumentUpdateHandler(
            TempDocumentManager documentManager,
            ILogger<DocumentUpdateHandler> logger)
        {
            _documentManager = documentManager;
            _logger = logger;
        }

        public async Task HandleUpdateDocument(
            IHubCallerClients clients,
            int documentId,
            int userId,
            string content,
            object delta

            )
        {
            string documentKey = $"document_{documentId}";

            try
            {
                // Get the current document from cache
                var tempDoc = _documentManager.GetDocument(documentId);

                Console.WriteLine("TempDoc: " + tempDoc.ToString());
                Console.WriteLine("TempDoc Content: " + tempDoc.Content.ToString());


                if (delta == null)
                {
                    _logger.LogWarning("Delta is null for document {DocumentId} by user {UserId}", documentId, userId);
                    return;
                }

                Console.WriteLine(delta.ToString());
                // Cast to dynamic to easily access properties
                ChangeDelta deltaObj = JsonSerializer.Deserialize<ChangeDelta>(delta.ToString());

                Console.WriteLine(deltaObj.SuffixLength);
                Console.WriteLine(deltaObj.PrefixLength);

                int prefixLength = deltaObj.PrefixLength;
                int suffixLength = deltaObj.SuffixLength;
                string removed = deltaObj.Removed;
                string added = deltaObj.Added;

                // Calculate where changes should be applied
                string currentContent = tempDoc.Content ?? string.Empty;

                Console.WriteLine("Current Content: " + currentContent + " | Length: " + currentContent.Length);
                Console.WriteLine($"Delta values - Prefix: {prefixLength}, Suffix: {suffixLength}, Removed: '{removed ?? "null"}', Added: '{added ?? "null"}'");

                // Ensure all values are initialized
                removed = removed ?? string.Empty;
                added = added ?? string.Empty;

                // Apply delta change with safe bounds checking
                try
                {
                    // Ensure we don't go out of bounds with our indices
                    prefixLength = Math.Min(prefixLength, currentContent.Length);
                    int midStartIndex = prefixLength + removed.Length;
                    int midLength = currentContent.Length - (midStartIndex + suffixLength);

                    // Defensive programming to prevent errors
                    if (midLength < 0) midLength = 0;
                    if (midStartIndex > currentContent.Length) midStartIndex = currentContent.Length;

                    string newContent;
                    if (currentContent.Length == 0)
                    {
                        // If document is empty, just use the added text
                        newContent = added;
                    }
                    else
                    {
                        // Build the content in parts to avoid index errors
                        newContent = string.Concat(
                            currentContent.Substring(0, prefixLength),
                            added
                        );

                        // Add the middle portion if needed
                        if (midStartIndex < currentContent.Length && midLength > 0)
                        {
                            newContent = string.Concat(
                                newContent,
                                currentContent.Substring(midStartIndex, midLength)
                            );
                        }

                        // Add the suffix portion if needed
                        if (suffixLength > 0 && currentContent.Length - suffixLength >= 0)
                        {
                            newContent = string.Concat(
                                newContent,
                                currentContent.Substring(currentContent.Length - suffixLength)
                            );
                        }
                    }

                    Console.WriteLine($"New content length: {newContent?.Length ?? 0}");

                    // Update the temporary document in cache
                    bool inCache = await _documentManager.UpdateDocumentAsync(documentId, newContent);

                    Console.WriteLine("UserId: " + userId);
                    // Broadcast changes to all other clients
                    // await clients.OthersInGroup(documentKey).SendAsync("ReceiveDocumentUpdate", userId, content);
                    await clients.OthersInGroup(documentKey).SendAsync("ReceiveDocumentUpdate", userId, delta);

                    if (inCache)
                    {
                        _logger.LogInformation("Document {DocumentId} updated by user {UserId} in temporary cache", documentId, userId);
                    }
                    else
                    {
                        _logger.LogWarning("Document {DocumentId} updated directly in database by user {UserId}", documentId, userId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while updating document {DocumentId}", documentId);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while updating document {DocumentId}", documentId);
                throw;
            }
        }
    }

    public class ChangeDelta
    {
        [JsonPropertyName("prefixLength")]
        public int PrefixLength { get; set; }
        [JsonPropertyName("suffixLength")]
        public int SuffixLength { get; set; }
        [JsonPropertyName("removed")]
        public string Removed { get; set; }
        [JsonPropertyName("added")]
        public string Added { get; set; }
    }
}