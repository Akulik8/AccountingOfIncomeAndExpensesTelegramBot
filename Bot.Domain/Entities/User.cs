using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot.Domain.Entities
{
    public class User
    {
        public long Id { get; set; }

        public long ChatId { get; set; }

        public string? TelegramNickname { get; set; }

        public string? FirstName { get; set; }
        
        public string? LastName { get; set; }

        public DateTime Created { get; set; } = DateTime.Now.ToUniversalTime();

        public string Currency {  get; set; }

        public virtual ICollection<Transaction> Transactions { get; set; }


        public override bool Equals(object? obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (obj is not User entity)
            {
                return false;
            }

            if (Id != entity.Id)
            {
                return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            var hash = 17;
            hash = hash * 23 + Id.GetHashCode();
            return hash;
        }
    }
}
