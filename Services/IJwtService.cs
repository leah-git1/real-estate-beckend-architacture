using Entities;

namespace Services
{
    public interface IJwtService
    {
        string GenerateToken(User user);
    }
}
