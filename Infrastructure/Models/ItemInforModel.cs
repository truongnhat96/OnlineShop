using Entities;

namespace Infrastructure.Models
{
    public class ItemInforModel
    {
        public List<ItemInfor> ItemInfors { get; set; } = [];
        public string AccountName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public int ProductId { get; set; } = 0;
        public int Id { get; set; } = 0;

    }
}
