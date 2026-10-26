using Azure.Communication.Email;
using Cassandra;
using Horizon.Notifications.Adapters.Outbound.Email;
using Horizon.Notifications.Adapters.Outbound.Persistence;
using Horizon.Notifications.Application.Ports;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using Horizon.Notifications.Adapters.Inbound.Messaging;
using Horizon.Notifications.Application.UseCases;
using Horizon.Notifications.Adapters.Inbound.Http;

var builder = WebApplication.CreateBuilder(args);

// ---------- JWT (solo valida con la llave pública) ----------
var jwt = builder.Configuration.GetSection("Jwt");
var rsa = RSA.Create();
rsa.ImportFromPem(File.ReadAllText(
    Path.Combine(builder.Environment.ContentRootPath, jwt["PublicKeyPath"]!)));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwt["Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new RsaSecurityKey(rsa),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

// ---------- NUEVO: adaptador de salida, Cassandra ----------
builder.Services.Configure<CassandraOptions>(
    builder.Configuration.GetSection(CassandraOptions.Section));

// el Cluster conoce los nodos; se crea UNA vez en toda la app
builder.Services.AddSingleton<ICluster>(sp =>
{
    var options = sp.GetRequiredService<IOptions<CassandraOptions>>().Value;

    return Cluster.Builder()
        .AddContactPoint(options.ContactPoint)
        .WithPort(options.Port)
        .Build();
});

// la Session es la conexión abierta; también UNA sola (es thread-safe)
builder.Services.AddSingleton<Cassandra.ISession>(sp =>
{
    var cluster = sp.GetRequiredService<ICluster>();
    var options = sp.GetRequiredService<IOptions<CassandraOptions>>().Value;

    var session = cluster.Connect();
    CassandraSchema.Ensure(session, options.Keyspace);
    return session;
});

// puerto → adaptador: el core pide INotificationRepository y recibe Cassandra
builder.Services.AddSingleton<INotificationRepository, CassandraNotificationRepository>();
// ---------- NUEVO: adaptador de salida, Email (Azure Communication Services) ----------
builder.Services.AddOptions<EmailOptions>()
    .Bind(builder.Configuration.GetSection(EmailOptions.Section))
    .ValidateDataAnnotations()   // revisa los [Required] y [EmailAddress]
    .ValidateOnStart();          // ...al arrancar, no en el primer correo

// el cliente de Azure se crea UNA vez (igual que BlobServiceClient en Channels)
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<EmailOptions>>().Value;
    return new EmailClient(options.ConnectionString);
});

// puerto → adaptador
builder.Services.AddSingleton<IEmailSender, AzureEmailSender>();
// ---------- Casos de uso (core) ----------
builder.Services.AddScoped<SendChannelConfirmation>();
builder.Services.AddScoped<SendPasswordReset>();
builder.Services.AddScoped<GetMyNotifications>();
builder.Services.AddScoped<MarkAllAsRead>();

// ---------- Adaptador de entrada: RabbitMQ ----------
builder.Services.AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection(RabbitMqOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHostedService<RabbitMqConsumer>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// NUEVO: conecta a Cassandra y crea keyspace/tabla AL ARRANCAR
// (si Cassandra no responde, la app falla de inmediato y no en la primera petición)
app.Services.GetRequiredService<Cassandra.ISession>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapNotificationsEndpoints();

app.Run();