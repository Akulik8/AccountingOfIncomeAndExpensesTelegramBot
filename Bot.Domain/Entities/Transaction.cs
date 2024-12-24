using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot.Domain.Entities
{
    public class Transaction
    {
        public Guid Id { get; set; }

        public long UserId { get; set; }

        public string? Category { get; set; }

        public decimal Amount { get; set; }

        public TransactionType Type { get; set; }

        public DateTime Date { get; set; } = DateTime.Now.ToUniversalTime();

        public string? Description { get; set; }

        public int? AmountMessageId { get; set; }
    }
}
