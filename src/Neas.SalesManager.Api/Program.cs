using Neas.SalesManager.Api.Data;
using Neas.SalesManager.Api.Middleware;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(); // Resolved after installing Swashbuckle.AspNetCore

// Dependency Injection
builder.Services.AddScoped<IDistrictRepository, DistrictRepository>();

var app = builder.Build();

// Global Exception Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();   // Resolved
    app.UseSwaggerUI(); // Resolved
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();