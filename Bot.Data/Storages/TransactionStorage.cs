using Bot.App.Interfaces;
using Bot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot.Data.Storages
{
    public class TransactionStorage : ITransactionStorage
    {
        private readonly BotDbContext _botDbContext;

        public TransactionStorage(BotDbContext botDbContext) 
        {
            _botDbContext = botDbContext;
        }

        public async Task AddTransactionAsync(Transaction transaction)
        {
            await _botDbContext.Transactions.AddAsync(transaction);
            await _botDbContext.SaveChangesAsync();
        }

        public async Task DeleteTransactionAsync(Guid id)
        {
            var transaction = await _botDbContext.Transactions.FindAsync(id);
            if (transaction != null)
            {
                _botDbContext.Transactions.Remove(transaction);
                await _botDbContext.SaveChangesAsync();
            }
        }

        public async Task<Transaction> GetTransactionByIdAsync(Guid id)
        {
            Transaction? transaction = await _botDbContext.Transactions
                 .FirstOrDefaultAsync(c => c.Id == id);

            if (transaction != null)
                return transaction;

            return new Transaction();
        }

        public async Task<List<Transaction>> GetTransactionsByUserIdAsync(long userId)
        {
           return await _botDbContext.Transactions
                .Where(t => t.UserId == userId)
                .ToListAsync();
        }

        public async Task<List<Transaction>> GetTransactionsByUserIdInRangeAsync(long userId, DateTime start, DateTime end)
        {
            return await _botDbContext.Transactions
              .Where(t => t.UserId == userId)
              .Where(t => t.Date >= start && t.Date <= end)
              .ToListAsync();
        }

        public async Task UpdateTransactionAsync(Guid id, Transaction newTransaction)
        {
            var transaction = await _botDbContext.Transactions
                    .FirstOrDefaultAsync(u => u.Id == id);
            if (transaction != null)
            {
                transaction.Amount = newTransaction.Amount;
                transaction.Type = newTransaction.Type;
                transaction.Date = newTransaction.Date;
                transaction.Description = newTransaction.Description;
                transaction.Category = newTransaction.Category;
                
                await _botDbContext.SaveChangesAsync();
            }
        }
    }
}
