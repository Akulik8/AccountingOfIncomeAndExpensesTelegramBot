using Bot.App.Interfaces;
using Bot.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot.App.Services
{
    public class UserService : IUserService
    {
        private readonly IUserStorage _userStorage;

        public UserService(IUserStorage userStorage)
        {
            _userStorage = userStorage;
        }


        public async Task<User> GetUserAsync(long userId)
        {
            var user = await _userStorage.GetUserByIdAsync(userId);
            
            return user;
        }

        public async Task AddUserAsync(User user)
        {
            DateTime today = DateTime.Today;
            if (user.Id == null)
            {
                user.Id = new();
            }


            await _userStorage.RegisterUserAsync(user);
        }

        public async Task RemoveUserAsync(long userId)
        {
            await _userStorage.RemoveUserAsync(userId);
        }

        public async Task UpdateUserAsync(long UserId, User user)
        {
            await _userStorage.UpdateUserAsync(UserId, user);
        }

        public async Task<List<User>> GetUsersAsync()
        {
            List<User> users = await _userStorage.GetUsersAsync();
            
            return users;
        }
    }
}
