
# Financial Analytics Processor

## Overview

The **Financial Analytics Processor** is a robust solution designed to efficiently process financial transactions. Built with modern technologies such as **.NET 8**, **Entity Framework Core**, and **Hangfire**, the application ensures scalability, reliability, and maintainability. It includes a **Worker** (background service) to execute recurring jobs, providing efficient data processing capabilities.

---

## Prerequisites

Before running the application locally, ensure your environment meets the following requirements:

1. **Branch**: Switch to the `develop` branch.
   ```bash
   git checkout develop
   ```

2. **SQL Server**: Install and run a local instance of SQL Server.

3. **.NET 8 SDK**: Verify that .NET 8 SDK is installed.
   ```bash
   dotnet --version
   ```

---

## Running the Application Locally

### 1. Clone the Repository

Clone the repository and navigate to the project directory:
```bash
git clone <repository-url>
cd FinancialAnalyticsProcessor
git checkout develop
```

### 2. Configure the Connection String

Update the `appsettings.json` file in the project root with your SQL Server connection string:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\mssqllocaldb;Database=FinancialAnalyticsDB;Trusted_Connection=True;"
}
```

### 3. Apply Database Migrations

Set up the database schema by running the following commands:
```bash
Add-Migration InitialMigration -StartupProject FinancialAnalyticsProcessor -Project FinancialAnalyticsProcessor.Infrastructure
Update-Database -StartupProject FinancialAnalyticsProcessor -Project FinancialAnalyticsProcessor.Infrastructure
```

### 4. Run the Application

Start the application using:
```bash
dotnet run --project FinancialAnalyticsProcessor
```

### 5. Access the Hangfire Dashboard

Once the application is running, navigate to the Hangfire Dashboard in your browser:
```
http://localhost:5000/hangfire
```

---

## Job Execution Details

### Recurring Job Configuration

The application schedules a recurring job using Hangfire, which runs every **3 minutes**. The configuration for this job is defined in the `Program` class as follows:

```csharp
.ConfigureWebHostDefaults(webBuilder =>
{
    webBuilder.Configure(app =>
    {
        app.UseHangfireDashboard("/hangfire");

        var jobConfig = app.ApplicationServices.GetRequiredService<IOptions<JobScheduleConfig>>().Value;
        var cronExpression = jobConfig?.CronExpression ?? "*/3 * * * *";

        RecurringJob.AddOrUpdate<TransactionJob>(
            "process-transactions",
            job => job.ExecuteAsync(jobConfig.InputFilePath, jobConfig.OutputFilePath),
            cronExpression
        );
    });
});
```

The **Cron expression** is configured in `appsettings.json`:
```json
"JobSchedule": {
  "CronExpression": "*/3 * * * *",
  "InputFilePath": "Data/Input/input.csv",
  "OutputFilePath": "Data/Output/output.json"
}
```

This setup ensures the job runs every **3 minutes**, regardless of the hour, day, or week.

---

### CSV File Requirements

The input CSV file must:
- Be named `input.csv`.
- Be stored in the directory:
  ```
  \FinancialAnalyticsProcessor\Data\Input\input.csv
  ```
- This file is excluded from version control (Git) to avoid issues with branch size limits.

---

### Validation and Error Handling

The job validates transaction data using **Fluent Validation**. For example, the `Description` field must:
1. Not be empty.
2. Not exceed 255 characters.

If a validation error occurs:
- The affected CSV line is updated with a default description, such as:
  ```csharp
  var newDescription = "New Description added after failure";
  ```
- The processing restarts after correcting the invalid line.

If a business rule validation cannot be resolved, the transaction is logged in:
```
\FinancialAnalyticsProcessor\Data\ErrorsInTheProcessing\ErrorsInTheProcessing.txt
```

Example log entry:
```
------------------------------------------------------------------------------------------
--------------------- Validation Error Logged at 2024-12-22 12:29:44 ---------------------
Transaction Details:
TransactionId: d90e3ac6-2291-4918-b26e-20ce26e3f229
UserId: 2eb32a09-8303-4b05-8711-59f778eb086d
Date: 05/02/2024 13:08:46 -03:00
Amount: 435.03
Category: Shopping
Description: 
Merchant: Kiehn Inc

Errors:
- Description is required and must not exceed 255 characters.
```

---

## Useful EF Core Commands

### Add a New Migration
```bash
Add-Migration MigrationName -StartupProject FinancialAnalyticsProcessor -Project FinancialAnalyticsProcessor.Infrastructure
```

### Apply Migrations to the Database
```bash
Update-Database -StartupProject FinancialAnalyticsProcessor -Project FinancialAnalyticsProcessor.Infrastructure
```

---

## Hangfire Database Management

### View Job Executions

Run the following query in **SQL Server Management Studio** to view successfully processed jobs:
```sql
SELECT 
    j.Id AS JobId,
    j.StateName AS CurrentState,
    j.CreatedAt AS JobCreatedAt,
    s.CreatedAt AS StateUpdatedAt,
    DATEDIFF(SECOND, j.CreatedAt, s.CreatedAt) AS ProcessingTimeInSeconds,
    CONCAT(
        FLOOR(DATEDIFF(SECOND, j.CreatedAt, s.CreatedAt) / 60), ' minutes and ',
        DATEDIFF(SECOND, j.CreatedAt, s.CreatedAt) % 60, ' seconds'
    ) AS ProcessingTimeFormatted
FROM 
    HangFire.Job j
LEFT JOIN 
    HangFire.State s ON j.StateId = s.Id
WHERE 
    j.StateName = 'Succeeded'
ORDER BY 
    ProcessingTimeInSeconds DESC;
```

### Reset the Hangfire Database
```sql
DELETE FROM [HangFire].[Job];
DELETE FROM [HangFire].[State];
DELETE FROM [HangFire].[Set];
DELETE FROM [HangFire].[Hash];
DELETE FROM [HangFire].[List];
DELETE FROM [HangFire].[Counter];
DELETE FROM [HangFire].[AggregatedCounter];
DELETE FROM [HangFire].[Server];
DELETE FROM [HangFire].[Schema];
```

---

## Observations

- The Hangfire Dashboard is accessible at:
  ```
  http://localhost:5000/hangfire
  ```
- To prevent overlapping job executions, a **30-minute semaphore lock** has been added:
  ```csharp
  [DisableConcurrentExecution(timeoutInSeconds: 1800)]
  ```
- Logs detailing the execution process are displayed in the console during runtime.

For further assistance, refer to the project documentation or contact the development team.

