using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebAppChat.Data;
using WebAppChat.Models;

namespace WebAppChat
{
    public class ChatHub : Hub
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ChatHub> _logger;

        private static HashSet<string> ConnectedUsers = new HashSet<string>();

        public ChatHub(UserManager<ApplicationUser> userManager, ApplicationDbContext context, ILogger<ChatHub> logger)
        {
            _userManager = userManager;
            _context = context;
            _logger = logger;
        }
        public async Task<string> GetUserId(string username)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);
            if (user != null)
            {
                return user.Id; 
            }
            throw new HubException("User not found.");
        }
        public async Task UserJoined(string username)
        {
            if (!string.IsNullOrEmpty(username) && !ConnectedUsers.Contains(username))
            {
                _logger.LogInformation("User {Username} joined the chat.", username);
                ConnectedUsers.Add(username);
                await Clients.All.SendAsync("UserJoined", username, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            else
            {
                _logger.LogWarning("User {Username} tried to join but is already in the chat or username is null/empty.", username);
            }
        }

        public async Task UserLeft(string username)
        {
            if (!string.IsNullOrEmpty(username) && ConnectedUsers.Contains(username))
            {
                _logger.LogInformation("User {Username} left the chat.", username);
                ConnectedUsers.Remove(username);
                await Clients.All.SendAsync("UserLeft", username, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            else
            {
                _logger.LogWarning("User {Username} tried to leave but is not in the chat or username is null/empty.", username);
            }
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                var user = await _userManager.GetUserAsync(Context.User);
                if (user != null)
                {
                    _logger.LogInformation("User {Username} connected.", user.UserName);
                    await UserJoined(user.UserName);
                }
                else
                {
                    _logger.LogWarning("An unauthenticated user tried to connect.");
                }
                await base.OnConnectedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during OnConnectedAsync.");
                throw;
            }
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            try
            {
                var user = await _userManager.GetUserAsync(Context.User);
                if (user != null)
                {
                    _logger.LogInformation("User {Username} disconnected.", user.UserName);
                    ConnectedUsers.Remove(user.UserName); // Удаляем пользователя из списка подключенных
                    await UserLeft(user.UserName);
                }
                else
                {
                    _logger.LogWarning("An unauthenticated user disconnected.");
                }
                await base.OnDisconnectedAsync(exception);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during OnDisconnectedAsync.");
                throw;
            }
        }
        public async Task SendMessage(string message)
        {
            try
            {
                var user = await _userManager.GetUserAsync(Context.User);
                if (user == null)
                {
                    _logger.LogWarning("Unauthenticated user tried to send a message.");
                    await Clients.Caller.SendAsync("Error", "User is not authenticated.");
                    return;
                }

                var chatMessage = new ChatMessage
                {
                    UserId = user.Id,
                    Content = message,
                    Timestamp = DateTime.UtcNow
                };

                _context.ChatMessages.Add(chatMessage);
                await _context.SaveChangesAsync();

                var insertedMessageId = chatMessage.Id;
                await Clients.All.SendAsync("ReceiveMessage", user.UserName, message, chatMessage.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"), insertedMessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while sending a message.");
                await Clients.Caller.SendAsync("Error", "An error occurred while sending the message.");
            }
        }

        public async Task GetChatHistory(string username, int pageNumber = 1, int pageSize = 100, DateTime? startDate = null, DateTime? endDate = null, string keyword = null)
        {
            _logger.LogInformation("GetChatHistory invoked with parameters: {Username}, {PageNumber}, {PageSize}, {StartDate}, {EndDate}, {Keyword}",
                                   username, pageNumber, pageSize, startDate, endDate, keyword);

            // Инициализация переменной query
            var query = _context.ChatMessages.AsQueryable();

            // Если задано имя пользователя, ищем пользователя и фильтруем по его Id
            if (!string.IsNullOrEmpty(username))
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);
                if (user == null)
                {
                    _logger.LogWarning("User with username {Username} not found.", username);
                    await Clients.Caller.SendAsync("ReceiveChatHistory", new List<object>());
                    return;
                }
                query = query.Where(m => m.UserId == user.Id);
            }

            // Фильтрация по дате
            if (startDate.HasValue)
            {
                query = query.Where(m => m.Timestamp >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(m => m.Timestamp <= endDate.Value);
            }

            // Фильтрация по ключевому слову
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(m => m.Content.Contains(keyword));
            }

            // Пагинация и сортировка
            var messages = await query
                .OrderByDescending(m => m.Timestamp)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new
                {
                    m.Id,
                    m.Content,
                    m.Timestamp,
                    UserId = m.UserId
                })
                .ToListAsync();

            // Возвращаем результат клиенту
            await Clients.Caller.SendAsync("ReceiveChatHistory", messages);
        }
    }
}