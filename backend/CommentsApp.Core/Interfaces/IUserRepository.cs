using CommentsApp.Core.Entities;

namespace CommentsApp.Core.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User> AddUserAsync(User user);
}
