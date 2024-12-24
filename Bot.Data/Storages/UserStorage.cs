using Bot.App.Interfaces;
using Bot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Bot.Data.Storages
{
    public class UserStorage : IUserStorage
    {
        private readonly BotDbContext _botDbContext;

        public UserStorage(BotDbContext botDbContext) 
        { 
            _botDbContext = botDbContext;
        }

        public async Task<User> GetUserByChatIdAsync(long chatId)
        {
            User? user = await _botDbContext.Users
                .FirstOrDefaultAsync(c => c.ChatId == chatId);

            if (user != null)
                return user;

            return new User();
        }

        public async Task<User> GetUserByIdAsync(long id)
        {
            User? user = await _botDbContext.Users
                 .FirstOrDefaultAsync(c => c.Id == id);

            if (user != null)
                return user;

            return new User();
        }

        public async Task<List<User>> GetUsersAsync()
        {
            var clients = await _botDbContext.Users.ToListAsync();
           
            return clients;
        }

        public async Task RegisterUserAsync(User user)
        {
            await _botDbContext.Users.AddAsync(user);
            await _botDbContext.SaveChangesAsync();
        }

        public async Task RemoveUserAsync(long id)
        {
            var user = await _botDbContext.Users
                           .FirstOrDefaultAsync(u => u.Id == id);

            if (user != null)
            {
                _botDbContext.Users.Remove(user);
                await _botDbContext.SaveChangesAsync();
            }
        }

        public async Task UpdateUserAsync(long userId, User newUser)
        {
            var user = await _botDbContext.Users
                   .FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null)
            {
                user.TelegramNickname = newUser.TelegramNickname;
                user.FirstName = newUser.FirstName;
                user.LastName = newUser.LastName;
                user.Created = newUser.Created;
                user.Currency = newUser.Currency;
                
                await _botDbContext.SaveChangesAsync();
            }
        }
    }
}
