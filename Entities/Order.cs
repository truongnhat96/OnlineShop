namespace Entities
{
    public class Order
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string OrderCode { get; set; } = string.Empty;
        public long PayOSCode { get; set; }
        public int Status { get; set; } = 0; // 0: Pending, 1: Paid, 2: Cancelled, 3: Failed, 4: Refunded, 5: Partially Refunded
        public int ProductId { get; set; }
        public double Price { get; set; }
        public int Quantity { get; set; }
    }
}
