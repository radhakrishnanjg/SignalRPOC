using BNP_Usecase1;
using BNP_Usecase1.Hubs;
using BNP_Usecase1.Messaging;
using BNP_Usecase1.Middleware;
using BNB.UsecaseRepository;
using BNB.UsecaseRepository.Interfaces;
using BNB.UsecaseServices;
using BNB.UsecaseServices.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddGlobalExceptionMiddleware();
builder.Services.AddSignalR();
builder.Services.AddSingleton<PaymentNotifier>();

var connectionString = builder.Configuration.GetConnectionString("PaymentConnection")
    ?? throw new InvalidOperationException("Connection string 'PaymentConnection' not found.");
builder.Services.AddScoped<IPaymentRepository>(_ => new PaymentRepository(connectionString));
builder.Services.AddScoped<IPaymentBusiness, PaymentBusiness>();
builder.Services.AddHostedService<FlatFileBackgroundService>();
builder.Services.AddSingleton<PaymentProducer>();
builder.Services.AddHostedService<KafkaConsumerService>();

// Angular client (ClientApp): served from the build output in production,
// proxied to the Angular dev server (npm start) in development.
builder.Services.AddSpaStaticFiles(options => options.RootPath = "ClientApp/dist/ClientApp/browser");

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// The Angular dev server (port 4200) calls this API directly using the base URL from its environment file.
builder.Services.AddCors(options => options.AddPolicy("AngularDev", policy =>
    policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseGlobalExceptionMiddleware();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseSpaStaticFiles();
}

app.UseRouting();

app.UseCors("AngularDev");

app.UseAuthorization();

// Endpoints must be mapped explicitly here: UseSpa below is terminal, and the implicit
// endpoint middleware would otherwise run after it, so API routes would never match.
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapHub<PaymentHub>("/hubs/payment");
});

app.UseSpa(spa =>
{
    spa.Options.SourcePath = "ClientApp";
    if (app.Environment.IsDevelopment())
    {
        spa.UseProxyToSpaDevelopmentServer("http://localhost:4200");
    }
});

app.Run();
