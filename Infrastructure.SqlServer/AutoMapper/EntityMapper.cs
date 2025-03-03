using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.SqlServer.AutoMapper
{
    public class EntityMapper : Profile
    {
        public EntityMapper()
        {
            CreateMap<Entities.Category, DataContext.Category>().ReverseMap();
            CreateMap<Entities.Product, DataContext.Product>().ReverseMap();
            CreateMap<Entities.Post, DataContext.Post>().ReverseMap();
            CreateMap<Entities.Role, DataContext.Role>().ReverseMap();
            CreateMap<Entities.User, DataContext.User>().ReverseMap();
            CreateMap<Entities.ItemInfor, DataContext.ItemInfor>().ReverseMap();
            CreateMap<Entities.CartItem, DataContext.CartItem>().ReverseMap();
            CreateMap<Entities.Review, DataContext.Review>().ReverseMap();
            CreateMap<Entities.Discount, DataContext.Discount>().ReverseMap();
            CreateMap<Entities.DiscountUsage, DataContext.DiscountUsage>().ReverseMap();
        }
    }
}
