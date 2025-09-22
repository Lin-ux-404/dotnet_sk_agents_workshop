using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;
using Azure.Monitor.OpenTelemetry.Exporter;
using AgentsDemoSK;
using AgentsDemoSK.Agents;
using AgentsDemoSK.Tracing;

var builder = WebApplication.CreateBuilder(args);

// Add configuration
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Get settings
Settings settings = new Settings();
var corsOrigins = builder.Configuration.GetSection("AppSettings:CorsOrigins").Get<string[]>() ?? new[] { "http://localhost:3000", "http://localhost:3001" };

// Add services to the container
builder.Services.AddSingleton<Settings>(settings);
builder.Services.AddControllers();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Healthcare Agent API",
        Version = "v1",
        Description = "API for interacting with healthcare agents for insurance inquiries and administrative tasks"
    });
});

// Add Application Insights telemetry
builder.Services.AddApplicationInsightsTelemetry();

// Configure Semantic Kernel
builder.Services.AddSingleton<Kernel>(provider =>
{
    Console.WriteLine("Creating kernel...");
    var kernelBuilder = Kernel.CreateBuilder();

    // Add Azure OpenAI chat completion service
    kernelBuilder.Services.AddAzureOpenAIChatCompletion(
        deploymentName: settings.AzureOpenAI.ModelDeploymentName,
        endpoint: settings.AzureOpenAI.Endpoint,
        apiKey: settings.AzureOpenAI.ApiKey,
        serviceId: "chat-completion",
        apiVersion: settings.AzureOpenAI.ApiVersion);

    return kernelBuilder.Build();
});

// Configure logging and tracing
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.AddApplicationInsights();
    
    // Add OpenTelemetry logging with Azure Monitor
    logging.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(
            ResourceBuilder.CreateDefault()
                .AddService("AgentsDemoSK"));
            
        // Use Azure Monitor Log Exporter
        options.AddAzureMonitorLogExporter(o => 
        {
            o.ConnectionString = builder.Configuration.GetSection("ApplicationInsights:ConnectionString").Value;
        });
    });
});

// Configure OpenTelemetry tracing with Azure Monitor
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => 
    {
        tracing
            .AddSource("AgentsDemoSK.GenAI") // Add our custom source
            .SetResourceBuilder(
                ResourceBuilder.CreateDefault()
                    .AddService("AgentsDemoSK"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddAzureMonitorTraceExporter(o => 
            {
                o.ConnectionString = builder.Configuration.GetSection("ApplicationInsights:ConnectionString").Value;
            });
    });

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<GenAITracer>();

// Configure Agents - simplified structure
builder.Services.AddSingleton<IAgentFactory, AgentFactory>();
builder.Services.AddSingleton<AgentPlugin>(provider =>
{
    var factory = provider.GetRequiredService<IAgentFactory>();
    return factory.CreateAgentPlugin();
});
builder.Services.AddSingleton<OrchestratorAgent>(provider =>
{
    var factory = provider.GetRequiredService<IAgentFactory>();
    return factory.CreateOrchestrator();
});

// Build the application
var app = builder.Build();

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Enable Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Healthcare Agent API v1"));

// Enable routing and CORS
app.UseRouting();
app.UseCors();

// Map controllers
app.MapControllers();

// Add root endpoint
app.MapGet("/", () => "Healthcare Agent API is running. Visit /swagger for API documentation.");

Console.WriteLine("Starting Healthcare Agent API...");
app.Run();
