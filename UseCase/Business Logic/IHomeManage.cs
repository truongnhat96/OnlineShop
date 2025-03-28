using Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UseCase.Business_Logic
{
    public interface IHomeManage
    {
        Task<IEnumerable<Product>> FindProductAsync(string keyword);
    }
}
