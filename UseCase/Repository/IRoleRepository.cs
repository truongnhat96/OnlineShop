using Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UseCase.Repository
{
    public interface IRoleRepository
    {
        Task<Role> GetRoleAsync(int id);
    }
}
