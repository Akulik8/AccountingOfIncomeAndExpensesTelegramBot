using Bot.App.Interfaces;
using Bot.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot.App.Services
{
    public class TransactionService : ITransactionService
    {
        private readonly ITransactionStorage _transactionStorage;

        public TransactionService(ITransactionStorage transactionStorage)
        {
            _transactionStorage = transactionStorage;
        }

        public async Task AddTransactionAsync(Transaction transaction)
        {
            await _transactionStorage.AddTransactionAsync(transaction);
        }

        public async Task DeleteTransactionAsync(Guid id)
        {
            await _transactionStorage.DeleteTransactionAsync(id);
        }

        public async Task<List<Transaction>> GetTransactionsByUserIdAsync(long userId)
        {
            return await _transactionStorage.GetTransactionsByUserIdAsync(userId);
        }

        public async Task UpdateTransactionAsync(Guid id, Transaction transaction)
        {
            await _transactionStorage.UpdateTransactionAsync(id, transaction);
        }       
       
        public async Task<decimal> GetUserBalanceAsync(long userId)
        {
            var transactions = await GetTransactionsByUserIdAsync(userId);
            return transactions.Sum(t => t.Type == TransactionType.Доход ? t.Amount : -t.Amount);
        }

        public async Task<List<Transaction>> GetTransactionsByUserIdInRangeAsync(long userId, DateTime start, DateTime end)
        {
            return await _transactionStorage.GetTransactionsByUserIdInRangeAsync(userId, start, end);
        }

        public async Task<Transaction> GetTransactionByIdAsync(Guid id)
        {
            return await _transactionStorage.GetTransactionByIdAsync(id);
        }
    }
}