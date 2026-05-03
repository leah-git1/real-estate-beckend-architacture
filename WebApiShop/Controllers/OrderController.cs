using DTOs;
using Entities;
using MailKit.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;
using System.Collections.Generic;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace WebApiShop.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _iOrderService;
        private readonly ILogger<OrderController> _logger;

        public OrderController(IOrderService iOrderService, ILogger<OrderController> logger)
        {
            _iOrderService = iOrderService;
            _logger = logger;
        }

        // GET api/<OrderController>/5
        [HttpGet("{id}")]
        public async Task<ActionResult<OrderDTO>> GetOrderById(int id)

        {
            OrderDTO order = await _iOrderService.GetOrderById(id);
            if(order == null) 
            {
                _logger.LogWarning("Order with ID {Id} was not found", id);
                return NotFound();
            }
            return order;
        }

        [HttpGet("user/{userId}")]
        public async Task<ActionResult<List<OrderHistoryDTO>>> GetOrdersByUserId(int userId)
        {
            List<OrderHistoryDTO> orders = await _iOrderService.GetOrdersByUserId(userId);
            if (orders == null)
            {
                _logger.LogWarning("Order with userId {userId} were not found", userId);
                return NotFound(); 
            }
            return Ok(orders);
        }

        [HttpGet]
        public async Task<ActionResult<List<OrderHistoryAdminDTO>>> GetAllOrders()
        {
            List<OrderHistoryAdminDTO> orders = await _iOrderService.GetAllOrders();
            if (orders == null)
            {
                _logger.LogWarning("Orders were not found");
                return NotFound(); 
            }
            return Ok(orders);
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<OrderDTO>> AddOrder(OrderCreateDTO order)
        {
            try
            {
                OrderDTO postOrder = await _iOrderService.AddOrder(order);
                _logger.LogInformation("Order added successfully with ID: {Id}", postOrder.OrderId);
                return CreatedAtAction(nameof(GetOrderById), new { id = postOrder.OrderId }, postOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while adding order");
                if (ex.Message == "ProductUnavailable")
                {
                    return Conflict(new { Message = "אחת מהדירות שבחרת כבר תפוסה בתאריכים אלו." });
                }
                return BadRequest(new { Message = "חלה שגיאה בביצוע ההזמנה", Details = ex.Message });
            }
        }

        [HttpPut("{orderId}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> UpdateOrderStatus(int orderId, OrderStatusUpdateDTO statusDto)
        {
            OrderDTO updatedOrder = await _iOrderService.UpdateOrderStatus(orderId, statusDto);
            if (updatedOrder == null)
            {
                _logger.LogWarning("Update failed: Order with ID {OrderId} not found", orderId);
                return BadRequest();
            }
            _logger.LogInformation("Order with ID {OrderId} status updated successfully", orderId);
            return Ok(updatedOrder);
        }


        [HttpPut("{orderId}/delivered")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> OrderDelivered(int orderId)
        {
            bool result = await _iOrderService.OrderDelivered(orderId);
            if (!result)
            {
                _logger.LogWarning("Delivery failed: Order with ID {OrderId} not found", orderId);
                return BadRequest();
            }
            _logger.LogInformation("Order with ID {OrderId} marked as delivered", orderId);
            return NoContent();
        }
        [HttpGet("occupied-dates/{productId}")]
        public async Task<IActionResult> GetOccupiedDates(int productId, [FromQuery] int month, [FromQuery] int year)
        {
            if (month < 1 || month > 12) return BadRequest("חודש לא תקין");

            var result = await _iOrderService.GetOccupiedDatesForProduct(productId, month, year);

            return Ok(result);
        }
    }
}
