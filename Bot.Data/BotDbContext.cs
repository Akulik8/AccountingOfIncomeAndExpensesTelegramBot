using System;
using Bot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Bot.Data
{
    public class BotDbContext : DbContext
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<Transaction> Transactions => Set<Transaction>();
  
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            var assembly = Assembly.GetExecutingAssembly();
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql("Host=localhost; Port = 5445; Database = bot_db; Username = postgres; Password = 2616");
        }
    }
}
