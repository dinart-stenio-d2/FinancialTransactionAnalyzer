using AutoMapper;
using FinancialAnalyticsProcessor.Application.Services;
using FinancialAnalyticsProcessor.Configurations;
using FinancialAnalyticsProcessor.Domain.Interfaces;
using FinancialAnalyticsProcessor.Domain.Interfaces.ApplicationServices;
using FinancialAnalyticsProcessor.Domain.Interfaces.Repositories;
using FinancialAnalyticsProcessor.Domain.Validations;
using FinancialAnalyticsProcessor.FaultResiliencePolicies;
using FinancialAnalyticsProcessor.Infrastructure.Data;
using FinancialAnalyticsProcessor.Infrastructure.Repositories.Generic;
using FinancialAnalyticsProcessor.Infrastructure.Services;
using FinancialAnalyticsProcessor.Mappings;
using FinancialAnalyticsProcessor.Worker.Jobs;
using FluentValidation;
using FluentValidation.AspNetCore;
using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Serilog;
using Serilog.Events;

//TO EXECUTE THE JOB DISCOMENT
//var builder = Host.CreateDefaultBuilder(args)
//    .UseSerilog((context, services, configuration) =>
//    {
//        // Configures Serilog to log to the console
//        configuration
//            .MinimumLevel.Debug()
//            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
//            .Enrich.FromLogContext()
//            .WriteTo.Console();
//    })
//    .ConfigureServices((context, services) =>
//    {
//        // DbContext Configuration - Scoped for proper lifecycle
//        services.AddDbContext<TransactionDbContext>(options =>
//        {
//            options.UseSqlServer(context.Configuration.GetConnectionString("DefaultConnection"));
//            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
//        }, ServiceLifetime.Scoped);

//        // FluentValidation Configuration
//        services.AddValidatorsFromAssemblyContaining<TransactionValidator>();
//        services.AddValidatorsFromAssemblyContaining<TransactionListValidator>();
//        services.AddFluentValidationAutoValidation();
//        services.AddFluentValidationClientsideAdapters();

//        // AutoMapper Configuration
//        services.AddAutoMapper(cfg =>
//        {
//            cfg.AddProfile<TransactionMappingProfile>();
//        });

//        // Cron Job Configuration
//        services.Configure<JobScheduleConfig>(context.Configuration.GetSection("JobSchedule"));

//        // Ensure the Hangfire database exists
//        var hangfireConnectionString = context.Configuration.GetConnectionString("HangfireConnection");
//        EnsureDatabaseExists(hangfireConnectionString);

//        // Hangfire Configuration
//        services.AddHangfire(config =>
//            config.UseSqlServerStorage(hangfireConnectionString));
//        services.AddHangfireServer();

//        // Service Registration
//        services.AddTransient(typeof(IRepository<>), typeof(Repository<>));
//        services.AddScoped<ITransactionProcessor, TransactionProcessor>();
//        services.AddScoped<ICsvTransactionLoader, CsvTransactionLoader>();
//        services.AddTransient<TransactionJob>();

//        // Add Polly Retry Policy with Scoped Service Resolution
//        services.AddScoped<AsyncRetryPolicy>(provider =>
//        {
//            var csvTransactionLoader = provider.GetRequiredService<ICsvTransactionLoader>();
//            var logger = provider.GetRequiredService<ILogger<TransactionJob>>();

//            return PollyPolicy.CreateRetryPolicy(csvTransactionLoader, logger);
//        });
//    })
//    .ConfigureWebHostDefaults(webBuilder =>
//    {
//        webBuilder.Configure(app =>
//        {
//            // Configure Hangfire Dashboard
//            app.UseHangfireDashboard("/hangfire");

//            // Retrieve Cron Job Schedule from IOptions
//            var jobConfig = app.ApplicationServices.GetRequiredService<IOptions<JobScheduleConfig>>().Value;

//            // Fallback to default cron expression if not configured
//            var cronExpression = jobConfig?.CronExpression ?? "*/3 * * * *";

//            // Schedule the recurring job
//            RecurringJob.AddOrUpdate<TransactionJob>(
//                "process-transactions",
//                job => job.ExecuteAsync(jobConfig.InputFilePath, jobConfig.OutputFilePath),
//                cronExpression
//            );
//        });
//    });

//await builder.Build().RunAsync();

//void EnsureDatabaseExists(string connectionString)
//{
//    try
//    {
//        // Parse the connection string to extract the database name
//        var builder = new SqlConnectionStringBuilder(connectionString);
//        var databaseName = builder.InitialCatalog;

//        // Set InitialCatalog to "master" to connect to the server itself
//        builder.InitialCatalog = "master";

//        using var connection = new SqlConnection(builder.ConnectionString);
//        connection.Open();

//        // Check if the database exists and create it if it doesn't
//        var commandText = $"IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'{databaseName}') CREATE DATABASE [{databaseName}]";
//        using var command = new SqlCommand(commandText, connection);
//        command.ExecuteNonQuery();
//    }
//    catch (Exception ex)
//    {
//        Console.WriteLine($"An error occurred while ensuring the Hangfire database exists: {ex.Message}");
//        throw;
//    }
//}

//TO TEST LOCALLY DISCOMENT
var builder = Host.CreateDefaultBuilder(args)
    .UseSerilog((context, services, configuration) =>
    {
        // Configures Serilog to log to the console
        configuration
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console();
    })
    .ConfigureServices((context, services) =>
    {
        // Configure DbContext
        services.AddDbContext<TransactionDbContext>(options =>
        {
            options.UseSqlServer(context.Configuration.GetConnectionString("DefaultConnection"));
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        }, ServiceLifetime.Transient);

        // FluentValidation Configuration
        services.AddValidatorsFromAssemblyContaining<TransactionValidator>();
        services.AddValidatorsFromAssemblyContaining<TransactionListValidator>();
        services.AddFluentValidationAutoValidation();
        services.AddFluentValidationClientsideAdapters();

        // AutoMapper Configuration
        services.AddAutoMapper(cfg =>
        {
            cfg.AddProfile<TransactionMappingProfile>();
        });

        // Add configuration for the Job
        services.Configure<JobScheduleConfig>(context.Configuration.GetSection("JobSchedule"));

        // Service Registration
        services.AddTransient(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<ITransactionProcessor, TransactionProcessor>();
        services.AddScoped<ICsvTransactionLoader, CsvTransactionLoader>();
        services.AddTransient<TransactionJob>();

        // Add Polly retry policy

        services.AddSingleton(provider =>
        {

            using (var scope = provider.CreateScope())
            {
                var scopedProvider = scope.ServiceProvider;

                var csvTransactionLoader = scopedProvider.GetRequiredService<ICsvTransactionLoader>();
                var logger = provider.GetRequiredService<ILogger<TransactionJob>>();


                return PollyPolicy.CreateRetryPolicy(csvTransactionLoader, logger);
            }
        });
    });

var host = builder.Build();

// Use a scoped service resolution only where necessary
using (var scope = host.Services.CreateScope())
{
    var serviceProvider = scope.ServiceProvider;

    // Ensure services are retrieved properly
    var jobConfig = serviceProvider.GetRequiredService<IOptions<JobScheduleConfig>>().Value;
    var job = serviceProvider.GetRequiredService<TransactionJob>();

    // Log and execute the job
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Executing the job for testing...");

    try
    {
        await job.ExecuteAsync(jobConfig.InputFilePath, jobConfig.OutputFilePath);
        logger.LogInformation("Job executed successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while executing the job.");
    }
}

await host.RunAsync();
