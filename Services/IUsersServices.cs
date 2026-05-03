using DTOs;

namespace Services
{
    public interface IUsersServices
    {
        Task DeleteUser(int id);
        Task<List<UserProfileDTO>> GetAllUsers();
        Task<UserProfileDTO> GetUserById(int id);
        Task<AuthResultDTO> LoginUser(UserLoginDTO userToLog);
        Task<AuthResultDTO> RegisterUser(UserRegisterDTO userToRegister);
        Task<UserProfileDTO> UpdateUser(UserUpdateDTO userToUpdate, int id);
    }
}
