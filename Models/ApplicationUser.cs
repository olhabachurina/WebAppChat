using Microsoft.AspNetCore.Identity;

namespace WebAppChat.Models
{
    public class ApplicationUser : IdentityUser
    {
        public DateTime CreatedAt { get; set; }
        public ICollection<ChatMessage> ChatMessages { get; set; }
       
    }
}