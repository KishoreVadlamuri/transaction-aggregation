using TransactionAggregation.ExternalPublisher;
using TransactionAggregation.ExternalPublisher.Generation;
using TransactionAggregation.ExternalPublisher.Messaging;
using TransactionAggregation.ExternalPublisher.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));
builder.Services.Configure<PublisherOptions>(builder.Configuration.GetSection(PublisherOptions.SectionName));
builder.Services.AddSingleton<JsonFinancialTransactionChunkSource>();
builder.Services.AddSingleton<IFinancialTransactionChunkPublisher, KafkaFinancialTransactionChunkPublisher>();
builder.Services.AddHostedService<FinancialTransactionChunkPublisherWorker>();

var host = builder.Build();

var startupLogger = host.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("TransactionAggregation.ExternalPublisher.Startup");
var kafka = builder.Configuration.GetSection(KafkaOptions.SectionName).Get<KafkaOptions>() ?? new KafkaOptions();
var publisher = builder.Configuration.GetSection(PublisherOptions.SectionName).Get<PublisherOptions>() ?? new PublisherOptions();

startupLogger.LogInformation(
    "Starting ExternalPublisher ({Environment})",
    builder.Environment.EnvironmentName);
startupLogger.LogInformation(
    "Publisher config: Enabled={Enabled}, IntervalSeconds={IntervalSeconds}, ChunkSize={ChunkSize}, DataFilePath={DataFilePath}",
    publisher.Enabled,
    publisher.IntervalSeconds,
    publisher.ChunkSize,
    publisher.DataFilePath);
startupLogger.LogInformation(
    "Kafka config: Enabled={Enabled}, BootstrapServers={BootstrapServers}, Topic={Topic}, ClientId={ClientId}",
    kafka.Enabled,
    kafka.BootstrapServers,
    kafka.Topic,
    kafka.ClientId);

host.Run();