namespace Services
{
    public interface IKafkaProducerService
    {
        Task SendMessageAsync<T>(string topic, string key, T message);
        Task SendOrderNotificationAsync(int orderId, string orderStatus, decimal totalAmount, int userId, string userEmail);
    }
}
