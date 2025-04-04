using Entities;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using UseCase.Business_Logic;
using UseCase.CachingSupport;
using UseCase.UnitOfWork;

namespace UseCase.Caching
{
    public class CachableCartManage : ICartManage
    {
        private readonly ICartItemUnitOfWork _unitOfWork;
        private readonly IDistributedCache _cache;
        private readonly DistributedCacheEntryOptions _cacheEntryOptions;
        private readonly CachableCartSupportOption _cacheOption;
        
        public CachableCartManage(ICartItemUnitOfWork unitOfWork, IDistributedCache cache, CachableCartSupportOption cacheOption)
        {
            _unitOfWork = unitOfWork;
            _cache = cache;
            _cacheOption = cacheOption;
            _cacheEntryOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _cacheOption.CacheLifeTime
            };
        }

        public async Task AddCartItemAsync(int productId, int userId, int quantity)
        {
            var currentCartItem = await _unitOfWork.CartItemRepository.GetCartItemAsync(userId, productId);
            var product = await _unitOfWork.ProductRepository.GetProductAsync(productId);
            int quantitySelected = product.Quantity - quantity >= 0 ? quantity : product.Quantity;
            if (currentCartItem != null)
            {
                if (currentCartItem.Quantity + quantitySelected > product.Quantity)
                {
                    currentCartItem.Quantity = product.Quantity;
                }
                else
                {
                    currentCartItem.Quantity += quantitySelected;
                }
                await _unitOfWork.CartItemRepository.UpdateCartAsync(currentCartItem);
            }
            else
            {
                await _unitOfWork.CartItemRepository.AddCartAsync(new CartItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    UserId = userId,
                    Quantity = quantitySelected
                });
            }
            var cachekey = _cacheOption.CacheKey;
            var cart = await _unitOfWork.CartItemRepository.GetCartItemsAsync(userId);
            await _cache.SetStringAsync(cachekey, JsonSerializer.Serialize(cart), _cacheEntryOptions);
        }

        public async Task<IEnumerable<CartItem>> GetCartItemsAsync(int userId)
        {
            return await _unitOfWork.CartItemRepository.GetCartItemsAsync(userId);
        }

        public async Task<Product> GetProductInCartAfterDiscountAsync(int productId, string coupon)
        {
            var product = await _unitOfWork.ProductRepository.GetProductAsync(productId);
            var discount = await _unitOfWork.DiscountRepository.GetDiscountAsync(productId);
            if (discount != null && discount.Coupon == coupon)
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

        public async Task<bool> IsCouponUsed(int userId, int discountId)
        {
            var discountInfor = await _unitOfWork.DiscountUsageRepository.GetUsageAsync(userId, discountId);
            if(discountInfor == null)
            {
                return true;
            }
            throw new("Coupon is used");
        }

        public async Task OrderProcessingAsync(int userId, int discountId)
        {
            var cartItem = await _unitOfWork.CartItemRepository.GetCartItemsAsync(userId);
            foreach (var item in cartItem)
            {
                await _unitOfWork.ProductRepository.UpdateQuantityAsync(item.ProductId, item.Quantity);
                await _unitOfWork.CartItemRepository.RemoveCartAsync(item.ProductId, userId);
            }
            if (discountId != 0)
            {
                await _unitOfWork.DiscountUsageRepository.AddUsageAsync(new DiscountUsage
                {
                    DiscountId = discountId,
                    UserId = userId,
                    Id = Guid.NewGuid()
                });
            }
        }

        public async Task RemoveCartItemAsync(int productId, int userId)
        {
            await _unitOfWork.CartItemRepository.RemoveCartAsync(productId, userId);
            var cachekey = _cacheOption.CacheKey;
            var cart = await _unitOfWork.CartItemRepository.GetCartItemsAsync(userId);
            await _cache.SetStringAsync(cachekey, JsonSerializer.Serialize(cart), _cacheEntryOptions);
        }

        public async Task UpdateCartItemAsync(int productId, int userId, int quantity)
        {
            var cartItem = await _unitOfWork.CartItemRepository.GetCartItemAsync(userId, productId) ?? throw new("NULL");
            Guid id = cartItem.Id;
            var product = await _unitOfWork.ProductRepository.GetProductAsync(productId);
            int quantitySelected = product.Quantity - quantity >= 0 ? quantity : product.Quantity;
            await _unitOfWork.CartItemRepository.UpdateCartAsync(new CartItem
            {
                Id = id,
                ProductId = productId,
                UserId = userId,
                Quantity = quantitySelected
            });
            var cachekey = _cacheOption.CacheKey;
            var cart = await _unitOfWork.CartItemRepository.GetCartItemsAsync(userId);
            await _cache.SetStringAsync(cachekey, JsonSerializer.Serialize(cart), _cacheEntryOptions);
        }
    }
}
