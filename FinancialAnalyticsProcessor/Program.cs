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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Serilog;
using Serilog.Events;
using System;
using System.Linq;
using System.Threading.Tasks;

public class Program
{
    public static async Task Main(string[] args)
    {
        var useHangfire = args.Contains("--use-hangfire");
        var builder = CreateHostBuilder(args, useHangfire);
        var host = builder.Build();

        using (var scope = host.Services.CreateScope())
        {
            var serviceProvider = scope.ServiceProvider;

            if (useHangfire)
            {
                ConfigureHangfire(serviceProvider);
            }
            else
            {
                await ExecuteJobForTesting(serviceProvider);
            }
        }

        await host.RunAsync();
    }

    /// <summary>
    /// Creates and configures the application host.
    /// </summary>
    private static IHostBuilder CreateHostBuilder(string[] args, bool useHangfire) =>
     Host.CreateDefaultBuilder(args)
         .UseSerilog(ConfigureLogging)
         .ConfigureServices((context, services) =>
         {
             ConfigureDatabase(context, services);
             ConfigureValidation(services);
             ConfigureAutoMapper(services);
             ConfigureJobScheduling(context, services);
             RegisterServices(services);
             ConfigurePolly(services);

             if (useHangfire)
             {
                 ConfigureHangfireServices(context, services);
             }
         })
         .ConfigureWebHostDefaults(webBuilder =>
         {
             webBuilder.Configure(app =>
             {
                 if (useHangfire) // Se o Hangfire estiver ativado
                 {
                     app.UseHangfireDashboard("/hangfire", new DashboardOptions
                     {
                         Authorization = new[] { new Hangfire.Dashboard.LocalRequestsOnlyAuthorizationFilter() }, // Permite acesso apenas localmente
                         IgnoreAntiforgeryToken = true // Evita erros de CSRF em ambiente local
                     });

                     using var scope = app.ApplicationServices.CreateScope();
                     var serviceProvider = scope.ServiceProvider;
                     ConfigureHangfire(serviceProvider); // Inicializa o Hangfire
                 }
             });
         });

    /// <summary>
    /// Configures logging using Serilog.
    /// </summary>
    private static void ConfigureLogging(HostBuilderContext context, IServiceProvider services, LoggerConfiguration configuration)
    {
        configuration
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console();
    }

    /// <summary>
    /// Configures the database context.
    /// </summary>
    private static void ConfigureDatabase(HostBuilderContext context, IServiceCollection services)
    {
        services.AddDbContext<TransactionDbContext>(options =>
        {
            options.UseSqlServer(context.Configuration.GetConnectionString("DefaultConnection"));
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        }, ServiceLifetime.Scoped);
    }

    /// <summary>
    /// Configures FluentValidation.
    /// </summary>
    private static void ConfigureValidation(IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<TransactionValidator>();
        services.AddValidatorsFromAssemblyContaining<TransactionListValidator>();
        services.AddFluentValidationAutoValidation();
        services.AddFluentValidationClientsideAdapters();
    }

    /// <summary>
    /// Configures AutoMapper.
    /// </summary>
    private static void ConfigureAutoMapper(IServiceCollection services)
    {
        services.AddAutoMapper(cfg =>
        {
            cfg.AddProfile<TransactionMappingProfile>();
        });
    }

    /// <summary>
    /// Configures job scheduling.
    /// </summary>
    private static void ConfigureJobScheduling(HostBuilderContext context, IServiceCollection services)
    {
        services.Configure<JobScheduleConfig>(context.Configuration.GetSection("JobSchedule"));
    }

    /// <summary>
    /// Registers application services.
    /// </summary>
    private static void RegisterServices(IServiceCollection services)
    {
        services.AddTransient(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<ITransactionProcessor, TransactionProcessor>();
        services.AddScoped<ICsvTransactionLoader, CsvTransactionLoader>();
        services.AddTransient<TransactionJob>();
    }

    /// <summary>
    /// Configures Polly retry policies.
    /// </summary>
    private static void ConfigurePolly(IServiceCollection services)
    {
        services.AddSingleton(provider =>
        {
            using var scope = provider.CreateScope();
            var scopedProvider = scope.ServiceProvider;
            var csvTransactionLoader = scopedProvider.GetRequiredService<ICsvTransactionLoader>();
            var logger = provider.GetRequiredService<ILogger<TransactionJob>>();

            return PollyPolicy.CreateRetryPolicy(csvTransactionLoader, logger);
        });
    }

    /// <summary>
    /// Configures Hangfire services.
    /// </summary>
    private static void ConfigureHangfireServices(HostBuilderContext context, IServiceCollection services)
    {
        var hangfireConnectionString = context.Configuration.GetConnectionString("HangfireConnection");

        // Garante que o banco do Hangfire existe
        EnsureDatabaseExists(hangfireConnectionString);

        // Registra o Hangfire no projeto
        services.AddHangfire(config =>
            config.UseSqlServerStorage(hangfireConnectionString)); //  CONFIGURA O BANCO DO HANGFIRE

        services.AddHangfireServer(); // ADICIONA O SERVIDOR DO HANGFIRE PARA EXECUÇÃO DE JOBS
    }

    /// <summary>
    /// Configures Hangfire job execution.
    /// </summary>
    private static void ConfigureHangfire(IServiceProvider serviceProvider)
    {
        var jobConfig = serviceProvider.GetRequiredService<IOptions<JobScheduleConfig>>().Value;
        var cronExpression = jobConfig?.CronExpression ?? "*/3 * * * *";

        using var scope = serviceProvider.CreateScope();
        var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>(); // 💡 Correção aqui

        recurringJobManager.AddOrUpdate<TransactionJob>(
            "process-transactions",
            job => job.ExecuteAsync(jobConfig.InputFilePath, jobConfig.OutputFilePath),
            cronExpression
        );
    }

    /// <summary>
    /// Executes the transaction job for testing.
    /// </summary>
    private static async Task ExecuteJobForTesting(IServiceProvider serviceProvider)
    {
        var jobConfig = serviceProvider.GetRequiredService<IOptions<JobScheduleConfig>>().Value;
        var job = serviceProvider.GetRequiredService<TransactionJob>();
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

    /// <summary>
    /// Ensures that the Hangfire database exists.
    /// </summary>
    private static void EnsureDatabaseExists(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            var databaseName = builder.InitialCatalog;
            builder.InitialCatalog = "master";

            using var connection = new SqlConnection(builder.ConnectionString);
            connection.Open();

            var commandText = $"IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'{databaseName}') CREATE DATABASE [{databaseName}]";
            using var command = new SqlCommand(commandText, connection);
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while ensuring the Hangfire database exists: {ex.Message}");
            throw;
        }
    }
}
