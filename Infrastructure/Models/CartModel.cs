using Entities;

namespace Infrastructure.Models
{
    public class CartModel
    {
        public int Quantity { get; set; }
        public required Product Product { get; set; }
    }

    public class CartItemModel
    {
        public int Quantity { get; set; }
        public int ProductId { get; set; }
    }

    public class CartUpdateRequest
    {
        public List<CartItemModel> UpdateItem { get; set; } = [];
    }
}
