// src/Neas.SalesManager.Api/Program.cs
using Neas.SalesManager.Api.Data;
using Neas.SalesManager.Api.Middleware;
using Serilog;
using Serilog.Sinks.Elasticsearch;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog with Elasticsearch Sink for Neas Observability
builder.Host.UseSerilog((context, configuration) =>
{
    var elasticUri = context.Configuration["Elasticsearch:Uri"] ?? "http://localhost:9200";

    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Neas.SalesManager.Api")
        .WriteTo.Console()
        .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(elasticUri))
        {
            AutoRegisterTemplate = true,
            IndexFormat = $"neas-salesmanager-api-{context.HostingEnvironment.EnvironmentName.ToLower()}-{DateTime.UtcNow:yyyy.MM}"
        });
});

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Dependency Injection
builder.Services.AddScoped<IDistrictRepository, DistrictRepository>();

var app = builder.Build();

// Global Exception Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();