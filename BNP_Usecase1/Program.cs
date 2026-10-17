using BNP_Usecase1;
using BNP_Usecase1.Hubs;
using BNP_Usecase1.Messaging;
using BNP_Usecase1.Middleware;
using BNB.UsecaseRepository;
using BNB.UsecaseRepository.EF;
using BNB.UsecaseRepository.Interfaces;
using BNB.UsecaseServices;
using BNB.UsecaseServices.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddGlobalExceptionMiddleware();
builder.Services.AddSignalR();
builder.Services.AddSingleton<PaymentNotifier>();

// Active repository: ADO.NET. To use Entity Framework instead, register PaymentEFRepository here.
//builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddDbContext<PaymentDbContext>();
builder.Services.AddScoped<IPaymentRepository,PaymentEFRepository>();
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

// Angular dev server address (appsettings: AngularClient:Url). Used for CORS and for the dev proxy below.
var angularUrl = builder.Configuration["AngularClient:Url"]
    ?? throw new InvalidOperationException("Setting 'AngularClient:Url' not found.");

builder.Services.AddCors(options => options.AddPolicy(APIConstant.AngularDevCorsPolicy, policy =>
    policy.WithOrigins(angularUrl).AllowAnyHeader().WithMethods("GET", "POST").AllowCredentials()));

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

app.UseCors(APIConstant.AngularDevCorsPolicy);

app.UseAuthorization();

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
        spa.UseProxyToSpaDevelopmentServer(angularUrl);
    }
});

app.Run();
