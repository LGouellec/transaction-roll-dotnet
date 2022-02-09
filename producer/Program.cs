using System;
using System.Net.Http.Headers;
using System.Threading;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace producer
{
    class Program
    {
        private static ILoggerFactory LoggerFactory;
        private static readonly ILogger<Program> logger;
        private static readonly int NUMBER_RETRY_KAFKA_OPS = 10;
        private static readonly TimeSpan TIMEOUT_KAFKA_OPS = TimeSpan.FromSeconds(10);

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
            string transactionId = GetValueOrDefault("KAFKA_TRANSACTION_ID", "test-transaction");
            int backoff = Int32.Parse(GetValueOrDefault("KAFKA_WAIT_BEFORE_COMMIT_TRANSACTION", "2000"));

            ProducerConfig producerConfig = new ProducerConfig();
            producerConfig.BootstrapServers = bootstrapServer;
            producerConfig.TransactionalId = transactionId;
            producerConfig.EnableIdempotence = true;
            producerConfig.MaxInFlight = 5;
            producerConfig.Debug = "broker,topic,msg,eos,protocol";

            Random rd = new Random();
            ProducerBuilder<string, string> producerBuilder = new ProducerBuilder<string, string>(producerConfig);
            producerBuilder.SetErrorHandler((p, e) => logger.LogError($"{e.Code}-{e.Reason}"));
            producerBuilder.SetLogHandler((p, e) => logger.LogInformation($"{e.Name}-{e.Message}"));

            using (var producer = producerBuilder.Build())
            {
                Console.CancelKeyPress += (o, e) =>
                {
                    producer.AbortTransaction();
                    logger.LogInformation($"Transaction aborted");
                };

                logger.LogInformation($"Producer initialize transaction mode in pending");
                producer.InitTransactions(TIMEOUT_KAFKA_OPS);
                logger.LogInformation($"Producer initialized transaction mode");

                producer.BeginTransaction();
                logger.LogInformation($"Producer began one transaction");

                while (true)
                {
                    try
                    {
                        for (var j = 0; j < 10; ++j)
                        {
                            Message<string, string> msg = new Message<string, string>();
                            msg.Key = $"key-{rd.Next(100)}";
                            msg.Value = $"value-{rd.Next(100)}";
                            producer.Produce(topic, msg, report =>
                            {
                                logger.LogDebug($"Delivery : {report.Error.Code.ToString()} " +
                                                $"- {report.Error.Reason} | Message : {report.Message.Key} - {report.Message.Value}" +
                                                $" | Metadata : {report.TopicPartition} - {report.Offset}");
                            });
                        }

                        Thread.Sleep(backoff);

                        producer.Flush();
                        MaybeRetry<KafkaRetriableException>(() => producer.CommitTransaction(TIMEOUT_KAFKA_OPS),
                            NUMBER_RETRY_KAFKA_OPS);
                        logger.LogInformation("Transaction committed");
                        producer.BeginTransaction();
                        logger.LogInformation($"Producer began one transaction");
                    }
                    catch (KafkaTxnRequiresAbortException e)
                    {
                        try
                        {
                            MaybeRetry<KafkaRetriableException>(() => producer.AbortTransaction(TIMEOUT_KAFKA_OPS),
                                NUMBER_RETRY_KAFKA_OPS);
                            logger.LogInformation(
                                $"Transaction aborted - {e.Message}. A new transaction will be create soon.");
                        }
                        catch (KafkaException ebis)
                        {
                            logger.LogError($"Error kafka none retriable during transaction aborted: {ebis.Message}");
                            logger.LogInformation("Producer will be close !");
                            break;
                        }
                    }
                    catch (KafkaException e)
                    {
                        logger.LogError($"Error kafka none retriable : {e.Message}");
                        logger.LogInformation("Producer will be close !");
                        break;
                    } 
                }
            }
        }

        static string GetValueOrDefault(string envVar, string @default)
        {
            return Environment.GetEnvironmentVariable(envVar) ?? @default;
        }

        static void MaybeRetry<Te>(Action action, int numberRetry) where Te : Exception
        {
            Te exception = null;
            for (int i = 0; i < numberRetry; ++i)
            {
                try
                {
                    action();
                    exception = null;
                    break;
                }
                catch (Te e)
                {
                    exception = e;
                }
            }
            
            if(exception != null)
                throw exception;
        }
    }
}