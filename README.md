
# Financial Analytics Processor

## Overview

The **Financial Analytics Processor** is a robust solution designed for efficiently processing financial transactions. Built with **.NET 8**, **Entity Framework Core**, **Hangfire**, and modern libraries, it ensures scalability, reliability, and maintainability.

---

## Prerequisites

Before running the application locally, ensure the following requirements are met:

1. **Branch**: Check out the `develop` branch.
   ```bash
   git checkout develop
   ```

2. **SQL Server**: Install and run a local instance of SQL Server.

3. **.NET 8 SDK**: Confirm that the .NET 8 SDK is installed.
   ```bash
   dotnet --version
   ```

---

## Running the Application Locally

### 1. Clone the Repository

```bash
git clone <repository-url>
cd FinancialAnalyticsProcessor
git checkout develop
```

### 2. Configure the Connection String

Update the `appsettings.json` file with your SQL Server connection string:
```json
"ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\mssqllocaldb;Database=FinancialAnalyticsDB;Trusted_Connection=True;"
}
```

### 3. Apply Migrations

Execute the following commands to set up the database schema:
```bash
Add-Migration InitialMigration -StartupProject FinancialAnalyticsProcessor -Project FinancialAnalyticsProcessor.Infrastructure
Update-Database -StartupProject FinancialAnalyticsProcessor -Project FinancialAnalyticsProcessor.Infrastructure
```

### 4. Run the Application

Start the application using the command:
```bash
dotnet run --project FinancialAnalyticsProcessor
```

### 5. Access the Hangfire Dashboard

Once the application is running, open the Hangfire Dashboard in your browser:
```
http://localhost:5000/hangfire
```

---

## Job Processing Details

### Recurring Job

The application schedules a recurring job using Hangfire, with the schedule defined in the `appsettings.json` file:
```json
"JobSchedule": {
  "CronExpression": "*/3 * * * *",
  "InputFilePath": "Data/Input/input.csv",
  "OutputFilePath": "Data/Output/output.json"
}
```

This configuration ensures the job runs **every 3 minutes** to process transactions from a CSV file.

### Validation and Error Handling

- The job validates transaction data using **Fluent Validation**. For example, the `Description` field must:
  - Not be empty.
  - Not exceed 255 characters.
- If a validation error occurs:
  - The job reprocesses the CSV file, updating invalid entries with a default description (e.g., `"New Description added after failure"`).
  - If validation cannot be resolved, the error is logged in:
    ```
    \FinancialAnalyticsProcessor\Data\ErrorsInTheProcessing\ErrorsInTheProcessing.txt
    ```

### Resilience

A **Polly-based resilience policy** is implemented to handle transient errors during job execution. Concurrency is restricted with a **30-minute semaphore lock** to avoid overlapping job executions:
```csharp
[DisableConcurrentExecution(timeoutInSeconds: 1800)]
```

---

## Useful Commands

### Entity Framework Core

- Add a new migration:
  ```bash
  Add-Migration MigrationName -StartupProject FinancialAnalyticsProcessor -Project FinancialAnalyticsProcessor.Infrastructure
  ```
- Apply migrations:
  ```bash
  Update-Database -StartupProject FinancialAnalyticsProcessor -Project FinancialAnalyticsProcessor.Infrastructure
  ```

### Hangfire Database Management

#### View Job Executions
Run the following query in SQL Server Management Studio to view successfully processed jobs:
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

#### Reset the Hangfire Database
To clear the Hangfire database, execute:
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

## Additional Notes

- The `input.csv` file must be placed in:
  ```
  \FinancialAnalyticsProcessor\Data\Input\input.csv
  ```
- Logs detailing the job execution are displayed in the console during runtime.
- For further assistance, refer to the project documentation or contact the development team.

---
