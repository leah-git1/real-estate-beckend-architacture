using DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace WebApiShop.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUsersServices _iUsersServices;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IUsersServices iUsersServices, ILogger<UsersController> logger)
        {
            _iUsersServices = iUsersServices;
            _logger = logger;
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<List<UserProfileDTO>>> GetAllUsers()
        {
            List<UserProfileDTO> users = await _iUsersServices.GetAllUsers();
            if (users == null)
            {
                _logger.LogWarning("Users were not found");
                return new List<UserProfileDTO>();
            }
            return Ok(users);
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<UserProfileDTO>> GetUserById(int id)
        {
            UserProfileDTO user = await _iUsersServices.GetUserById(id);
            if (user == null)
            {
                _logger.LogWarning("User with ID {id} was not found", id);
                return NotFound();
            }
            return Ok(user);
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<ActionResult<UserProfileDTO>> RegisterUser(UserRegisterDTO user)
        {
            try
            {
                AuthResultDTO result = await _iUsersServices.RegisterUser(user);
                SetJwtCookie(result.Token);
                _logger.LogInformation("User registered successfully: ID: {Id}, Email: {Email}", result.User.UserId, user.Email);
                return CreatedAtAction(nameof(GetAllUsers), new { id = result.User.UserId }, result.User);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration failed for user email: {Email}", user.Email);
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<UserProfileDTO>> LoginUser(UserLoginDTO userToLog)
        {
            AuthResultDTO result = await _iUsersServices.LoginUser(userToLog);
            if (result == null)
            {
                _logger.LogInformation("Login failed for email: {Email}", userToLog.Email);
                return BadRequest(new { message = "Login failed" });
            }

            SetJwtCookie(result.Token);
            _logger.LogInformation("User login successfully: Name: {FullName}, Email: {Email}", result.User.FullName, userToLog.Email);
            return Ok(result.User);
        }

        [HttpPost("logout")]
        [Authorize]
        public ActionResult Logout()
        {
            Response.Cookies.Delete("jwt");
            return NoContent();
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult> UpdateUser(int id, UserUpdateDTO userToUpdate)
        {
            try
            {
                UserProfileDTO user = await _iUsersServices.UpdateUser(userToUpdate, id);
                if (user == null)
                {
                    _logger.LogWarning("Update failed: User with ID {id} not found or invalid data", id);
                    return BadRequest(new { message = "Update failed" });
                }
                _logger.LogInformation("User with ID {id} updated successfully", id);
                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Update failed for user ID: {id}", id);
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> DeleteUser(int id)
        {
            UserProfileDTO user = await _iUsersServices.GetUserById(id);
            if (user == null)
            {
                _logger.LogWarning("Delete failed: Attempted to delete non-existent user with ID {id}", id);
                return NotFound();
            }
            await _iUsersServices.DeleteUser(id);
            _logger.LogInformation("User with ID {id} was deleted from the system", id);
            return NoContent();
        }

        private void SetJwtCookie(string token)
        {
            Response.Cookies.Append("jwt", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddHours(8)
            });
        }
    }
}
