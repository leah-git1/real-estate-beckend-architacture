using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Services
{
    public class KafkaProducerService : IKafkaProducerService
    {
        private readonly IProducer<string, string> _producer;
        private readonly ILogger<KafkaProducerService> _logger;
        private readonly string _bootstrapServers;

        public KafkaProducerService(IConfiguration configuration, ILogger<KafkaProducerService> logger)
        {
            _logger = logger;
            _bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";

            var config = new ProducerConfig
            {
                BootstrapServers = _bootstrapServers,
                ClientId = "realestate-api-producer",
                Acks = Acks.All,
                MessageMaxBytes = 1000000
            };

            try
            {
                _producer = new ProducerBuilder<string, string>(config)
                    .SetErrorHandler((_, e) => 
                        _logger.LogError($"Producer error: {e.Reason}"))
                    .Build();

                _logger.LogInformation($"Kafka Producer initialized with bootstrap servers: {_bootstrapServers}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Kafka Producer");
                throw;
            }
        }

        public async Task SendMessageAsync<T>(string topic, string key, T message)
        {
            try
            {
                var messageJson = JsonSerializer.Serialize(message);
                var kafkaMessage = new Message<string, string>
                {
                    Key = key,
                    Value = messageJson
                };

                var deliveryReport = await _producer.ProduceAsync(topic, kafkaMessage);

                _logger.LogInformation(
                    $"Message sent to Kafka topic '{topic}' - Key: {key}, " +
                    $"Partition: {deliveryReport.Partition}, Offset: {deliveryReport.Offset}");
            }
            catch (ProduceException<string, string> ex)
            {
                _logger.LogError(ex, $"Failed to send message to Kafka topic '{topic}': {ex.Error.Reason}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error sending message to Kafka topic '{topic}'");
                throw;
            }
        }

        public async Task SendOrderNotificationAsync(int orderId, string orderStatus, decimal totalAmount, int userId, string userEmail)
        {
            try
            {
                var notification = new
                {
                    OrderId = orderId,
                    Status = orderStatus,
                    TotalAmount = totalAmount,
                    UserId = userId,
                    UserEmail = userEmail,
                    CreatedAt = DateTime.UtcNow,
                    MessageType = "OrderCreated"
                };

                await SendMessageAsync("orders", $"order-{orderId}", notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send order notification for OrderId {orderId}");
                throw;
            }
        }

        public void Dispose()
        {
            _producer?.Dispose();
        }
    }
}
