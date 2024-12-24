using Bot.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot.App.Interfaces
{
    public interface IUserService
    {
        public Task<User> GetUserAsync(long userId);

        public Task AddUserAsync(User user);

        public Task RemoveUserAsync(long userId);

        public Task UpdateUserAsync(long UserId, User user);

        public Task<List<User>> GetUsersAsync();
    }
}
