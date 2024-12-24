using Bot.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot.App.Interfaces
{
    public interface IUserStorage
    {
       public Task RegisterUserAsync(User user);
       public Task<User> GetUserByIdAsync(long id);
       public Task<User> GetUserByChatIdAsync(long chatId);
       public Task UpdateUserAsync(long userId, User user);
       public Task RemoveUserAsync(long id);
       public Task<List<User>> GetUsersAsync();
    }
}
