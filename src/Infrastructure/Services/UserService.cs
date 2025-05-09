using Core.Entities;
using Infrastructure.Repositories;

namespace Infrastructure.Services
{
    public class UserService
    {
        private readonly UserRepository _repository;

        public UserService(UserRepository repository) => _repository = repository;

        public Task<User?> LoginAsync(string email, string password) => _repository.LoginAsync(email, password);
        public Task<bool> RegisterAsync(User user, string password) => _repository.RegisterAsync(user, password);
    }
}
