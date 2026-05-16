using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using System.Text.Json;

// Setup Serilog logging
var logger = new LoggerFactory()
    .AddSerilog(new LoggerConfiguration()
        .MinimumLevel.Information()
        .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File("logs/kafka-consumer-.txt", rollingInterval: RollingInterval.Day)
        .CreateLogger())
    .CreateLogger("KafkaConsumer");

logger.LogInformation("=== Real Estate Kafka Consumer Started ===");

var bootstrapServers = "localhost:9092";
var topic = "orders";
var groupId = "realestate-consumer-group";

var consumerConfig = new ConsumerConfig
{
    BootstrapServers = bootstrapServers,
    GroupId = groupId,
    AutoOffsetReset = AutoOffsetReset.Earliest,
    EnableAutoCommit = true,
    StatisticsIntervalMs = 5000
};

try
{
    using (var consumer = new ConsumerBuilder<string, string>(consumerConfig)
        .SetErrorHandler((_, e) =>
        {
            logger.LogError($"Consumer error: {e.Reason}");
        })
        .SetStatisticsHandler((_, json) =>
        {
            var stats = JsonDocument.Parse(json);
            logger.LogInformation($"Consumer stats - Consumer lag: {json}");
        })
        .Build())
    {
        consumer.Subscribe(topic);
        logger.LogInformation($"Subscribed to topic: {topic}");

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var consumeResult = consumer.Consume(cts.Token);

                if (consumeResult != null)
                {
                    try
                    {
                        logger.LogInformation(
                            $"\n=== Order Message Received ===\n" +
                            $"Topic: {consumeResult.Topic}\n" +
                            $"Partition: {consumeResult.Partition}\n" +
                            $"Offset: {consumeResult.Offset}\n" +
                            $"Key: {consumeResult.Message.Key}\n" +
                            $"Message: {consumeResult.Message.Value}\n" +
                            $"Timestamp: {consumeResult.Message.Timestamp.UtcDateTime:yyyy-MM-dd HH:mm:ss}\n" +
                            $"====================================\n");

                        // Parse and log order details
                        try
                        {
                            var orderData = JsonSerializer.Deserialize<dynamic>(consumeResult.Message.Value);
                            logger.LogInformation($"Order Data: {JsonSerializer.Serialize(orderData, new JsonSerializerOptions { WriteIndented = true })}");
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning($"Could not parse order data: {ex.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error processing message");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Consumer cancelled");
        }
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "Fatal error in Kafka Consumer");
}

logger.LogInformation("=== Real Estate Kafka Consumer Stopped ===");
