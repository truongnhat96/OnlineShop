namespace Infrastructure.SqlServer
{
    public class UserUnitOfWork : IUserUnitOfWork
    {
        private readonly ShopContext _context;
        private readonly IMapper _mapper;

        public UserUnitOfWork(ShopContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;

            UserRepository = new UserRepository(context, mapper);
            RoleRepository = new RoleRepository(context, mapper);
            PostRepository = new PostRepository(context, mapper);
            ReviewRepository = new ReviewRepository(context, mapper);
        }
        public IUserRepository UserRepository { get; }
                 
        public IRoleRepository RoleRepository { get; }
        public IPostRepository PostRepository { get; }

        public IReviewRepository ReviewRepository { get; }

        public async Task SaveAsync()
        {
           await _context.SaveChangesAsync();
        }
    }
}
