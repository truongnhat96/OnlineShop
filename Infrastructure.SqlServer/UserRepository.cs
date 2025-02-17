using Microsoft.EntityFrameworkCore;

namespace Infrastructure.SqlServer
{
    public class UserRepository : IUserRepository
    {
        private readonly ShopContext _context;
        private readonly IMapper _mapper;

        public UserRepository(ShopContext context, IMapper mapper)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task<Entities.User> AddAccountAsync(Entities.User user)
        {
            var entity = _mapper.Map<Entities.User, DataContext.User>(user);
            await _context.Users.AddAsync(entity);
            _context.SaveChanges();
            return user;
        }

        public bool EmailExists(string email)
        {
            return _context.Users.Any(u => u.Email == email);
        }


        public async Task<Entities.User?> GetUserAsync(string usernameORemail)
        {
            var user = await _context.Users.Where(u => u.Username == usernameORemail || u.Email == usernameORemail).FirstOrDefaultAsync();
            return _mapper.Map<DataContext.User?, Entities.User>(user);
        }

        public async Task<Entities.User?> GetUserAsync(int id)
        {
            var userDb = await _context.Users.FindAsync(id);
            return _mapper.Map<DataContext.User?, Entities.User>(userDb);
        }
    }
}
