using AutoMapper;
using DTOs;
using Entities;
using Microsoft.Extensions.Logging;
using Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _iOrderRepository;
        private readonly IProductRepository _iProductRepository;
        private readonly IProductService _iProductService;
        private readonly IUsersRepository _iUsersRepository;
        private readonly IKafkaProducerService _kafkaProducerService;
        private readonly IMapper _mapper;   
        private readonly ILogger<OrderService> _logger;

        public OrderService(IOrderRepository iOrderRepository, IProductRepository iProductRepository, IProductService iProductService, IUsersRepository iUsersRepository, IKafkaProducerService kafkaProducerService, IMapper mapper, ILogger<OrderService> logger)
        {
            this._iOrderRepository = iOrderRepository;
            this._iProductRepository = iProductRepository;
            this._iProductService = iProductService;
            this._iUsersRepository = iUsersRepository;
            this._kafkaProducerService = kafkaProducerService;
            this._mapper = mapper;
            this._logger = logger;
        }

        public async Task<OrderDTO> GetOrderById(int id)
        {
            Order order = await _iOrderRepository.GetOrderById(id);
            OrderDTO orderDTO = _mapper.Map<Order, OrderDTO>(order);
            return orderDTO;
        }

        public async Task<List<OrderHistoryDTO>> GetOrdersByUserId(int userId)
        {
            List<Order> orders = await _iOrderRepository.GetOrdersByUserId(userId);
            return _mapper.Map<List<Order>, List<OrderHistoryDTO>>(orders);
        }

        public async Task<List<OrderHistoryAdminDTO>> GetAllOrders()
        {
            List<Order> orders = await _iOrderRepository.GetAllOrders();
            return _mapper.Map<List<Order>, List<OrderHistoryAdminDTO>>(orders);
        }

        public async Task<OrderDTO> AddOrder(OrderCreateDTO createOrder)
        {
            User user = await _iUsersRepository.GetUserById(createOrder.UserId);
            if (user == null)
                throw new Exception("UserNotFound");

            Order order = new Order
            {
                UserId = createOrder.UserId,
                OrderItems = new List<OrderItem>()
            };

            decimal calculatedTotal = 0;

            foreach (OrderItemCreateDTO item in createOrder.OrderItems)
            {
                Product product = await _iProductRepository.GetProductById(item.ProductId);
                if (product == null)
                    throw new Exception("ProductNotFound");

                if (product.TransactionType == "Sale" || product.TransactionType == "מכירה")
                    throw new Exception("Sale items are not available for online booking");

                if (!item.StartDate.HasValue || !item.EndDate.HasValue)
                    throw new Exception("InvalidDates");

                if (item.StartDate < DateTime.UtcNow.Date)
                    throw new Exception("StartDateInPast");

                if (item.EndDate <= item.StartDate)
                    throw new Exception("EndDateBeforeStart");

                bool isAvailable = await _iProductService
                    .CheckAvailability(item.ProductId, item.StartDate, item.EndDate);

                if (!isAvailable)
                    throw new Exception("ProductUnavailable");

                int days = (int)(item.EndDate.Value - item.StartDate.Value).TotalDays;
                decimal itemTotal = days * product.Price;
                calculatedTotal += itemTotal;

                OrderItem newItem = new OrderItem
                {
                    ProductId = item.ProductId,
                    PriceAtPurchase = product.Price,
                    StartDate = item.StartDate,
                    EndDate = item.EndDate
                };

                order.OrderItems.Add(newItem);
            }

            if (createOrder.ExpectedTotalAmount != calculatedTotal)
            {
                _logger.LogWarning("Order sum mismatch for UserId {UserId}. Expected: {ExpectedTotalAmount}, Actual: {CalculatedTotal}.",
                    createOrder.UserId, createOrder.ExpectedTotalAmount, calculatedTotal);

                throw new Exception($"Order sum mismatch. Expected: {createOrder.ExpectedTotalAmount}, Actual: {calculatedTotal}");
            }

            order.TotalAmount = calculatedTotal;
            order.OrderDate = DateTime.UtcNow;

            Order orderTbl = await _iOrderRepository.AddOrder(order);

            // Send order notification to Kafka
            try
            {
                await _kafkaProducerService.SendOrderNotificationAsync(
                    orderTbl.OrderId,
                    orderTbl.Status ?? "Pending",
                    orderTbl.TotalAmount,
                    user.UserId,
                    user.Email);

                _logger.LogInformation($"Order notification sent to Kafka for OrderId: {orderTbl.OrderId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send Kafka notification for OrderId: {orderTbl.OrderId}. Order was created successfully.");
            }

            return _mapper.Map<OrderDTO>(orderTbl);
        }

        public async Task<OrderDTO> UpdateOrderStatus(int orderId, OrderStatusUpdateDTO dto)
        {
            string status = dto.Status;
            Order order = await _iOrderRepository.UpdateOrderStatus(orderId, status);
            return _mapper.Map<Order, OrderDTO>(order);
        }

        public async Task<bool> OrderDelivered(int orderId)
        {
            return await _iOrderRepository.OrderDelivered(orderId);
        }

        public async Task<OccupiedDatesResponseDTO> GetOccupiedDatesForProduct(int productId, int month, int year)
        {
            var ranges = await _iOrderRepository.GetProductOccupiedRanges(productId, month, year);

            var occupiedDatesSet = new HashSet<DateTime>();

            foreach (var range in ranges)
            {
                for (var date = range.Start; date <= range.End; date = date.AddDays(1))
                {
                    if (date.Month == month && date.Year == year)
                    {
                        occupiedDatesSet.Add(date.Date);
                    }
                }
            }

            var formattedDates = occupiedDatesSet
                .OrderBy(d => d)
                .Select(d => d.ToString("yyyy-MM-dd"))
                .ToList();

            return new OccupiedDatesResponseDTO(
                ProductId: productId,
                Month: month,
                Year: year,
                OccupiedDates: formattedDates
            );
        }
    }
}
