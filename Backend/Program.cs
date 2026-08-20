using Backend.Hubs;
using Backend.Services;
using Backend.Storage;
using StackExchange.Redis;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

var distributedEnabled =
    builder.Configuration.GetValue<bool>(
        "Distributed:Enabled"
    );

var allowedOrigins =
    builder.Configuration
        .GetSection("AllowedOrigins")
        .Get<string[]>()
    ?? [];

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter()
        );
    });

var signalRBuilder =
    builder.Services
        .AddSignalR()
        .AddJsonProtocol(options =>
        {
            options.PayloadSerializerOptions.Converters.Add(
                new JsonStringEnumConverter()
            );
        });

if (distributedEnabled)
{
    var redisConnectionString =
        builder.Configuration["Redis:ConnectionString"]
        ?? throw new InvalidOperationException(
            "Redis connection string is missing."
        );

    var transactionsKey =
        builder.Configuration["Redis:TransactionsKey"]
        ?? throw new InvalidOperationException(
            "Redis transactions key is missing."
        );

    signalRBuilder.AddStackExchangeRedis(
        redisConnectionString
    );

    var redisConfiguration =
    ConfigurationOptions.Parse(redisConnectionString);

    redisConfiguration.AbortOnConnectFail = false;

    builder.Services.AddSingleton<IConnectionMultiplexer>(
        ConnectionMultiplexer.Connect(redisConfiguration)
    );

    builder.Services.AddSingleton<ITransactionStore>(
        serviceProvider =>
        {
            var connection =
                serviceProvider.GetRequiredService<
                    IConnectionMultiplexer
                >();

            return new RedisTransactionStore(
                connection,
                transactionsKey
            );
        }
    );
}
else
{
    builder.Services.AddSingleton<
        ITransactionStore,
        InMemoryTransactionStore
    >();
}

builder.Services.AddScoped<TransactionService>();

builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseCors("Frontend");

app.MapControllers();

app.MapHub<TransactionHub>("/transactionHub");

app.Run();