using AutoMapper;
using DTOs;
using Entities;
using Repository;
using System.Collections.Generic;

namespace Services
{
    public class UsersServices : IUsersServices
    {
        private readonly IUsersRepository _iUsersRepository;
        private readonly IPasswordService _iPasswordService;
        private readonly IProductRepository _iProductRepository;
        private readonly IJwtService _jwtService;
        private readonly IMapper _mapper;

        public UsersServices(IUsersRepository iusersRepository, IPasswordService passwordService, IProductRepository iProductRepository, IJwtService jwtService, IMapper mapper)
        {
            _iUsersRepository = iusersRepository;
            _iPasswordService = passwordService;
            _iProductRepository = iProductRepository;
            _jwtService = jwtService;
            _mapper = mapper;
        }

        public async Task<List<UserProfileDTO>> GetAllUsers()
        {
            List<User> users = await _iUsersRepository.GetAllUsers();
            return _mapper.Map<List<User>, List<UserProfileDTO>>(users);
        }

        public async Task<UserProfileDTO> GetUserById(int id)
        {
            User user = await _iUsersRepository.GetUserById(id);
            if (user == null)
                return null;
            return _mapper.Map<User, UserProfileDTO>(user);
        }

        public async Task<AuthResultDTO> RegisterUser(UserRegisterDTO userToRegister)
        {
            var checkPassword = _iPasswordService.checkStrengthPassword(userToRegister.Password);
            if (checkPassword.strength < 2)
                throw new Exception("הסיסמה חלשה מדי. עליה להכיל לפחות 8 תווים ושילוב של אותיות ומספרים.");

            List<User> allUsers = await _iUsersRepository.GetAllUsers();
            foreach (var item in allUsers)
            {
                if (item.Email == userToRegister.Email)
                    throw new Exception("כתובת האימייל כבר קיימת במערכת.");
            }

            User user = _mapper.Map<UserRegisterDTO, User>(userToRegister);
            user = await _iUsersRepository.RegisterUser(user);

            string token = _jwtService.GenerateToken(user);
            UserProfileDTO profile = _mapper.Map<User, UserProfileDTO>(user);
            return new AuthResultDTO(profile, token);
        }

        public async Task<AuthResultDTO> LoginUser(UserLoginDTO userToLog)
        {
            User user = await _iUsersRepository.LoginUser(userToLog.Email, userToLog.Password);
            if (user == null)
                return null;

            string token = _jwtService.GenerateToken(user);
            UserProfileDTO profile = _mapper.Map<User, UserProfileDTO>(user);
            return new AuthResultDTO(profile, token);
        }

        public async Task<UserProfileDTO> UpdateUser(UserUpdateDTO userToUpdate, int id)
        {
            User existingUser = await _iUsersRepository.GetUserById(id);
            if (existingUser == null)
                throw new Exception("משתמש לא נמצא");

            if (userToUpdate.Password != null && userToUpdate.Password != "")
            {
                if (userToUpdate.OldPassword == null || userToUpdate.OldPassword == "")
                    throw new Exception("חובה להזין את הסיסמה הנוכחית");

                if (existingUser.Password != userToUpdate.OldPassword)
                    throw new Exception("הסיסמה הנוכחית שגויה");

                var checkPassword = _iPasswordService.checkStrengthPassword(userToUpdate.Password);
                if (checkPassword.strength < 2)
                    throw new Exception("הסיסמה החדשה חלשה מדי. עליה להכיל לפחות 8 תווים ושילוב של אותיות ומספרים");
            }

            if (userToUpdate.Email != null && userToUpdate.Email != "")
            {
                List<User> allUsers = await _iUsersRepository.GetAllUsers();
                foreach (var item in allUsers)
                {
                    if (item.Email == userToUpdate.Email && item.UserId != id)
                        throw new Exception("כתובת האימייל כבר קיימת במערכת.");
                }
            }

            User user = _mapper.Map<UserUpdateDTO, User>(userToUpdate);
            user.UserId = id;
            user = await _iUsersRepository.UpdateUser(user, id);
            return _mapper.Map<User, UserProfileDTO>(user);
        }

        public async Task DeleteUser(int id)
        {
            List<Product> userProducts = await _iProductRepository.GetProductsByOwnerId(id);
            foreach (Product product in userProducts)
            {
                Product updateDto = new Product { IsAvailable = false };
                await _iProductRepository.UpdateProduct(product.ProductId, updateDto);
            }
            await _iUsersRepository.DeleteUser(id);
        }
    }
}
