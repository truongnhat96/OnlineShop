using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.SqlServer.DataContext
{
    public class DiscountUsage
    {
        [Key]
        public Guid Id { get; set; }
        public int UserId { get; set; }
        public int DiscountId { get; set; }
        public virtual Discount? Discount { get; set; }
        public virtual User? User { get; set; }
    }
}
