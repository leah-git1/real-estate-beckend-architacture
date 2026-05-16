using Entities;
using Repository;
using BCrypt.Net;

namespace Services
{
    public class PasswordService : IPasswordService
    {
        public CheckPassword checkStrengthPassword(string password)
        {
            var result = Zxcvbn.Core.EvaluatePassword(password);
            int strength = result.Score;
            CheckPassword pass = new CheckPassword();
            pass.password = password;
            pass.strength = strength;
            return pass;
        }

        public string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        public bool VerifyPassword(string enteredPassword, string hash)
        {
            return BCrypt.Net.BCrypt.Verify(enteredPassword, hash);
        }
    }
}