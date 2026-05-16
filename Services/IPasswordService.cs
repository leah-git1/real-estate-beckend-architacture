using Entities;

namespace Services
{
    public interface IPasswordService
    {
        CheckPassword checkStrengthPassword(string password);
        string HashPassword(string password);
        bool VerifyPassword(string enteredPassword, string hash);
    }
}