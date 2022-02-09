using System;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace consumer
{
    class Program
    {
        private static ILoggerFactory LoggerFactory;
        private static readonly ILogger<Program> logger;

        static Program()
        {
            LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create((builder) =>
            {
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddLog4Net();
            });
            logger = LoggerFactory.CreateLogger<Program>();
        }

        static void Main(string[] args)
        {
            string bootstrapServer = GetValueOrDefault("KAFKA_BOOTSTRAP_SERVER", "localhost:9092");
            string topic = GetValueOrDefault("KAFKA_TOPIC", "topic-test");
            string groupId = GetValueOrDefault("KAFKA_GROUP_ID", "transaction-group");


            ConsumerConfig consumerConfig = new ConsumerConfig();
            consumerConfig.BootstrapServers = bootstrapServer;
            consumerConfig.GroupId = groupId;
            consumerConfig.IsolationLevel = IsolationLevel.ReadCommitted;

            TimeSpan timeout = TimeSpan.FromSeconds(10);
            ConsumerBuilder<string, string> consumerBuilder = new ConsumerBuilder<string, string>(consumerConfig);
            consumerBuilder.SetErrorHandler((p, e) => logger.LogError($"{e.Code}-{e.Reason}"));
            consumerBuilder.SetLogHandler((p, e) => logger.LogInformation($"{e.Name}-{e.Message}"));

            using (var consumer = consumerBuilder.Build())
            {
                Console.CancelKeyPress += (o, e) =>
                {
                    consumer.Unsubscribe();
                    consumer.Close();
                    consumer.Dispose();
                    logger.LogInformation($"Consumer closed");
                };

                logger.LogInformation($"Consumer subscribe topic {topic}");
                consumer.Subscribe(topic);

                while (true)
                {
                    try
                    {
                        var record = consumer.Consume(TimeSpan.FromMilliseconds(100));
                        if (record != null)
                        {
                            logger.LogDebug($"Message : {record.Message.Key} - {record.Message.Value}" +
                                            $" | Metadata : {record.TopicPartition} - {record.Offset}");
                        }
                    }
                    catch (Exception e)
                    {
                        logger.LogInformation($"Consumer aborted - {e.Message}");
                    }
                }
            }
        }

        static string GetValueOrDefault(string envVar, string @default)
        {
            return Environment.GetEnvironmentVariable(envVar) ?? @default;
        }
    }
}