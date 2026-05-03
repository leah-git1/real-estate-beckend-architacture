using DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace WebApiShop.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(IAdminService adminService, ILogger<AdminController> logger)
        {
            _adminService = adminService;
            _logger = logger;
        }

        [HttpGet("users")]
        public async Task<ActionResult<List<UserProfileDTO>>> GetAllUsers()
        {
            List<UserProfileDTO> users = await _adminService.GetAllUsers();
            return Ok(users);
        }

        [HttpGet("products")]
        public async Task<ActionResult<List<ProductDetailsDTO>>> GetAllProducts()
        {
            List<ProductDetailsDTO> products = await _adminService.GetAllProducts();
            return Ok(products);
        }

        [HttpDelete("user/{id}")]
        public async Task<ActionResult> DeleteUser(int id)
        {
            bool isDeleted = await _adminService.DeleteUser(id);
            if (!isDeleted)
            {
                _logger.LogWarning("Delete failed: User with ID {id} not found", id);
                return NotFound();
            }
            _logger.LogInformation("User with ID {id} was deleted by admin", id);
            return NoContent();
        }

        [HttpDelete("product/{id}")]
        public async Task<ActionResult> DeleteProduct(int id)
        {
            bool isDeleted = await _adminService.DeleteProduct(id);
            if (!isDeleted)
            {
                _logger.LogWarning("Delete failed: Product with ID {id} not found", id);
                return NotFound();
            }
            _logger.LogInformation("Product with ID {id} was deleted by admin", id);
            return NoContent();
        }

        [HttpGet("statistics")]
        public async Task<ActionResult<AdminStatisticsDTO>> GetStatistics()
        {
            AdminStatisticsDTO statistics = await _adminService.GetStatistics();
            return Ok(statistics);
        }

        [HttpGet("orders")]
        public async Task<ActionResult<List<OrderAdminDTO>>> GetAllOrders()
        {
            List<OrderAdminDTO> orders = await _adminService.GetAllOrders();
            return Ok(orders);
        }

        [HttpDelete("order/{id}")]
        public async Task<ActionResult> DeleteOrder(int id)
        {
            bool isDeleted = await _adminService.DeleteOrder(id);
            if (!isDeleted)
            {
                _logger.LogWarning("Delete failed: Order with ID {id} not found", id);
                return NotFound();
            }
            _logger.LogInformation("Order with ID {id} was deleted by admin", id);
            return NoContent();
        }

        [HttpGet("inquiries")]
        public async Task<ActionResult<List<AdminInquiryDTO>>> GetAllAdminInquiries()
        {
            _logger.LogInformation("Getting all admin inquiries");
            List<AdminInquiryDTO> inquiries = await _adminService.GetAllAdminInquiries();
            return Ok(inquiries);
        }

        [HttpGet("inquiry/{id}")]
        public async Task<ActionResult<AdminInquiryDTO>> GetAdminInquiryById(int id)
        {
            _logger.LogInformation("Getting admin inquiry with ID: {Id}", id);
            AdminInquiryDTO inquiry = await _adminService.GetAdminInquiryById(id);
            if (inquiry == null)
            {
                _logger.LogWarning("Admin inquiry with ID {Id} was not found", id);
                return NotFound();
            }
            return Ok(inquiry);
        }

        [HttpPost("inquiry")]
        [AllowAnonymous]
        public async Task<ActionResult<AdminInquiryDTO>> AddAdminInquiry(AdminInquiryCreateDTO createDto)
        {
            try
            {
                _logger.LogInformation("Adding new admin inquiry from: {Email}", createDto.Email);
                AdminInquiryDTO inquiry = await _adminService.AddAdminInquiry(createDto);
                _logger.LogInformation("Admin inquiry added successfully with ID: {Id}", inquiry.InquiryId);
                return CreatedAtAction(nameof(GetAdminInquiryById), new { id = inquiry.InquiryId }, inquiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while adding admin inquiry. Inner: {Inner}", ex.InnerException?.Message);
                return BadRequest(new { Message = "שגיאה ביצירת הפנייה", Details = ex.Message, Inner = ex.InnerException?.Message });
            }
        }

        [HttpPut("inquiry/{id}/status")]
        public async Task<ActionResult<AdminInquiryDTO>> UpdateAdminInquiryStatus(int id, AdminInquiryStatusUpdateDTO statusDto)
        {
            _logger.LogInformation("Updating status for admin inquiry ID: {Id}", id);
            AdminInquiryDTO updatedInquiry = await _adminService.UpdateAdminInquiryStatus(id, statusDto);
            if (updatedInquiry == null)
            {
                _logger.LogWarning("Update failed: Admin inquiry with ID {Id} not found", id);
                return NotFound();
            }
            _logger.LogInformation("Admin inquiry with ID {Id} status updated successfully", id);
            return Ok(updatedInquiry);
        }

        [HttpDelete("inquiry/{id}")]
        public async Task<ActionResult> DeleteAdminInquiry(int id)
        {
            try
            {
                _logger.LogInformation("Deleting admin inquiry with ID: {Id}", id);
                await _adminService.DeleteAdminInquiry(id);
                _logger.LogInformation("Admin inquiry with ID {Id} deleted successfully", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting admin inquiry with ID: {Id}", id);
                return BadRequest(new { Message = "שגיאה במחיקת הפנייה", Details = ex.Message });
            }
        }
    }
}
