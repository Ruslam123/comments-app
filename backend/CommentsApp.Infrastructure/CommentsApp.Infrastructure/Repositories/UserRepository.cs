using Microsoft.EntityFrameworkCore;
using CommentsApp.Core.Entities;
using CommentsApp.Core.Interfaces;
using CommentsApp.Infrastructure.Data;

namespace CommentsApp.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;
    public UserRepository(ApplicationDbContext context) { _context = context; }
    public async Task<User?> GetByEmailAsync(string email) => await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
    public async Task<User> AddUserAsync(User user) { _context.Users.Add(user); await _context.SaveChangesAsync(); return user; }
}
