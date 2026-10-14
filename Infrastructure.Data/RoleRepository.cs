namespace Infrastructure.SqlServer
{
    public class RoleRepository : IRoleRepository
    {
        private readonly ShopContext _context;
        private readonly IMapper _mapper;

        public RoleRepository(ShopContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }
        public async Task<Entities.Role> GetRoleAsync(int id)
        {
            var role = await _context.Roles.FindAsync(id) ?? throw new ArgumentNullException("Role not valid");
            return _mapper.Map<Entities.Role>(role);
        }
    }
}
