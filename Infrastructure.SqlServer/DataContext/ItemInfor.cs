using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.SqlServer.DataContext
{
    public class ItemInfor
    {
        [Key]
        public int Id { get; set; }
        [StringLength(100)]
        public string? AccountName { get; set; }
        [StringLength(100)]
        public string? Password { get; set; }
        [StringLength(100)]
        public string? Key { get; set; }
        public int ProductId { get; set; }
        public virtual Product? Product { get; set; }
    }
}
