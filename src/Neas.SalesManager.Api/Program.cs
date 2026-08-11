// src/Neas.SalesManager.Api/Program.cs
using Neas.SalesManager.Api.Data;
using Neas.SalesManager.Api.Middleware;
using Serilog;
using Serilog.Sinks.Elasticsearch;

var builder = WebApplication.CreateBuilder(args);

// 1. Serilog Observability Configuration
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

// 2. Framework Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 3. Dependency Injection (Scoped for DB context lifetime)
builder.Services.AddScoped<IDistrictRepository, DistrictRepository>();

var app = builder.Build();

// 4. Custom Middleware (Positioned early in pipeline)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 5. Swagger Configuration (Enabled in all environments for demo visibility)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Neas Sales Manager API v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();