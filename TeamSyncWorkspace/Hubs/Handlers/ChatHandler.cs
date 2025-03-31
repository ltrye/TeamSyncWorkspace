using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Azure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TeamSyncWorkspace.Data;
using TeamSyncWorkspace.Models.Documents;

namespace TeamSyncWorkspace.Hubs.Handlers
{
    public class ChatHandler
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ChatHandler> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IHubContext<DocumentHub> _hubContext;

        public ChatHandler(AppDbContext context, ILogger<ChatHandler> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration, IHubContext<DocumentHub> hubContext)
        {
            _context = context;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _hubContext = hubContext;
        }

        public async Task HandleSaveChatMessage(int documentId, int userId, string message)
        {
            try
            {
                var chatMessage = new ChatMessage
                {
                    DocumentId = documentId,
                    UserId = userId,
                    Content = message,
                    SentAt = DateTime.UtcNow
                };

                _context.ChatMessages.Add(chatMessage);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Chat message saved for document {DocumentId} from user {UserId}", documentId, userId);

                // Broadcast the message to all clients in the group
                //await _hubContext.Clients.Group($"document_{documentId}").SendAsync("ReceiveChatMessage", userId, new { Name = "User", Avatar = "avatar_url" }, message, chatMessage.SentAt);

                if (message.StartsWith("@AI "))
                {
                    var prompt = message.Substring(4);
                    var aiResponse = await GetAIResponse(prompt, documentId);
                    if (!string.IsNullOrEmpty(aiResponse))
                    {
                        var aiMessage = new ChatMessage
                        {
                            DocumentId = documentId,
                            UserId = -1, // AI user ID
                            Content = aiResponse,
                            SentAt = DateTime.UtcNow
                        };

                        _context.ChatMessages.Add(aiMessage);
                        await _context.SaveChangesAsync();

                        // Broadcast the AI message to all clients in the group
                        await _hubContext.Clients.Group($"document_{documentId}").SendAsync("ReceiveChatMessage", -1, new { Name = "AI Assistant" }, aiResponse, aiMessage.SentAt);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving chat message for document {DocumentId}", documentId);
                throw;
            }
        }

        public async Task<List<object>> GetChatHistory(int documentId)
        {
            try
            {
                var messages = await _context.ChatMessages
                    .Where(m => m.DocumentId == documentId)
                    .OrderBy(m => m.SentAt)
                    .Select(m => new
                    {
                        m.Id,
                        m.UserId,
                        UserName = m.User.UserName,
                        m.Content,
                        m.SentAt
                    })
                    .ToListAsync();

                return messages.Cast<object>().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chat history for document {DocumentId}", documentId);
                throw;
            }
        }
        public async Task<string?> GetAIResponse(string prompt, int documentId)
        {
            var client = _httpClientFactory.CreateClient();
            var apiKey = _configuration["OpenAI:ApiKey"];
            var apiUrl = _configuration["AI:ApiUrl"];


            var context = _context.CollabDocs.FirstOrDefault(d => d.DocId == documentId)?.Content;
            var requestBody = new
            {
                model = "meta-llama/llama-3.2-3b-instruct:free",
                messages = new[]
                {
            new
            {
                role = "user",
                content = prompt + " đây là context " + context + " nếu câu hỏi ko liên quan tới doc thì không cần quan tâm"
            }
        }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
            {
                Headers =
        {
            { "Authorization", $"Bearer {apiKey}" },
        },
                Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
            };

            var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var aiResponse = JsonSerializer.Deserialize<AIResponse>(responseContent);
                return aiResponse?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
            }

            return null;
        }
        public async Task StreamAIResponse(int documentId, int userId, string prompt)
        {
            var client = _httpClientFactory.CreateClient();
            var apiKey = _configuration["OpenAI:ApiKey"];
            var apiUrl = _configuration["AI:ApiUrl"];

            var requestBody = new
            {
                model = "openai/gpt-4o",
                messages = new[]
                {
            new
            {
                role = "user",
                content = prompt
            }
        },
                stream = true
            };

            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
            {
                Headers =
        {
            { "Authorization", $"Bearer {apiKey}" },
        },
                Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
            };

            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (response.IsSuccessStatusCode)
            {
                var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);
                var buffer = new StringBuilder();

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (!string.IsNullOrEmpty(line) && line.StartsWith("data: "))
                    {
                        var data = line.Substring(6).Trim();
                        if (data == "[DONE]") break;

                        try
                        {
                            var parsed = JsonSerializer.Deserialize<AIResponse>(data);
                            var content = parsed?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;

                            if (!string.IsNullOrEmpty(content))
                            {
                                buffer.Append(content);

                                // Broadcast the AI message to all clients in the group
                                await _hubContext.Clients.Group($"document_{documentId}").SendAsync("ReceiveChatMessage", -1, new { Name = "AI Assistant" }, buffer.ToString(), DateTime.UtcNow);
                            }
                        }
                        catch (JsonException)
                        {
                            // Ignore invalid JSON
                        }
                    }
                }

                // Save the complete response to the database
                var aiMessage = new ChatMessage
                {
                    DocumentId = documentId,
                    UserId = -1, // AI user ID
                    Content = buffer.ToString(),
                    SentAt = DateTime.UtcNow
                };

                _context.ChatMessages.Add(aiMessage);
                await _context.SaveChangesAsync();
            }
        }
    }



    public class AIResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice> Choices { get; set; }
    }

    public class Choice
    {
        [JsonPropertyName("message")]
        public Message Message { get; set; }
    }

    public class Message
    {
        [JsonPropertyName("role")]
        public string Role { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }
    }
}