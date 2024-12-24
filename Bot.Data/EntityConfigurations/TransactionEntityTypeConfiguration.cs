using Bot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot.Data.EntityConfigurations
{
    public class TransactionEntityTypeConfiguration : IEntityTypeConfiguration<Transaction>
    {
        public void Configure(EntityTypeBuilder<Transaction> builder)
        {
            builder.ToTable("transactions");

            builder.HasKey(t => t.Id);

            builder.Property(t => t.UserId).IsRequired();
            builder.Property(t => t.Amount).IsRequired();
            builder.Property(t => t.Type).HasMaxLength(15).IsRequired();
            builder.Property(t => t.Date).IsRequired();
            builder.Property(t => t.Category);
            builder.Property(t => t.Description);
            builder.Property(t => t.AmountMessageId);

            builder.HasOne<User>() 
             .WithMany(u => u.Transactions)
             .HasForeignKey(t => t.UserId); 
        }
    }
}
