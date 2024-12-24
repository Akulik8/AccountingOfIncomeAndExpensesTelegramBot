using Bot.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot.App.Interfaces
{
    public interface ITransactionService
    {
        public Task AddTransactionAsync(Transaction transaction);
        public Task DeleteTransactionAsync(Guid id);
        public Task UpdateTransactionAsync(Guid id, Transaction transaction);
        public Task<Transaction> GetTransactionByIdAsync(Guid id);
        public Task<List<Transaction>> GetTransactionsByUserIdAsync(long userId);
        public Task<List<Transaction>> GetTransactionsByUserIdInRangeAsync(long userId, DateTime start, DateTime end);
        public Task<decimal> GetUserBalanceAsync(long userId);
    }
}
