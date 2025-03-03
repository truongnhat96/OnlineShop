using Entities;
using UseCase.Business_Logic;
using UseCase.UnitOfWork;

namespace UseCase
{
    public class CartManage : ICartManage
    {
        private readonly ICartItemUnitOfWork _unitOfWork;

        public CartManage(ICartItemUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task AddCartItemAsync(int productId, int userId, int quantity)
        {
            var currentCartItem = await _unitOfWork.CartItemRepository.GetCartItemAsync(userId, productId);
            if (currentCartItem != null)
            {
                currentCartItem.Quantity += quantity;
                await _unitOfWork.CartItemRepository.UpdateCartAsync(currentCartItem);
            }
            else
            {
                await _unitOfWork.CartItemRepository.AddCartAsync(new CartItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    UserId = userId,
                    Quantity = quantity
                });
            }
        }

        public async Task<IEnumerable<CartItem>> GetCartItemsAsync(int userId)
        {
            return await _unitOfWork.CartItemRepository.GetCartItemsAsync(userId);
        }

        public async Task<Product> GetProductInCartAfterDiscountAsync(int productId, string coupon)
        {
            var product = await _unitOfWork.ProductRepository.GetProductAsync(productId);
            var discount = await _unitOfWork.DiscountRepository.GetDiscountAsync(productId);
            if(discount != null && discount.Coupon == coupon)
            {
                product.Price -= product.Price * discount.DiscountPercent / 100;
            }
            else
            {
                throw new("Coupon not exist");
            }
                return product;
        }

        public async Task<Product> GetProductInCartAsync(int productId)
        {
            return await _unitOfWork.ProductRepository.GetProductAsync(productId);
        }

        public async Task RemoveCartItemAsync(int productId)
        {
            await _unitOfWork.CartItemRepository.RemoveCartAsync(productId);
        }

        public async Task UpdateCartItemAsync(int productId, int userId, int quantity)
        {
            Guid id = (await _unitOfWork.CartItemRepository.GetCartItemAsync(userId, productId))?.Id ?? Guid.NewGuid();
            await _unitOfWork.CartItemRepository.UpdateCartAsync(new CartItem
            {
                Id = id,
                ProductId = productId,
                UserId = userId,
                Quantity = quantity
            });
        }
    }
}
