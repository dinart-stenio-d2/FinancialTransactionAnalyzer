# **Financial Analytics Processor - Execution Guide for Visual Studio 2022**

This guide explains how to run the **Financial Analytics Processor** in **Visual Studio 2022**, leveraging the **launchSettings.json** file for easier execution configurations.

---

## **📌 Using launchSettings.json for Execution**

The project uses **launchSettings.json** to manage execution profiles in **Visual Studio 2022**. This file allows for **predefined execution modes**, so you don't need to manually pass command-line arguments.

### **📂 Location of launchSettings.json**
The **launchSettings.json** file is located in:
```
FinancialAnalyticsProcessor/Properties/launchSettings.json
```

### **🔧 Configured Execution Profiles**
This project includes **two execution profiles**:

#### **1️⃣ FinancialAnalyticsProcessor (Local Testing Mode)**
```json
"FinancialAnalyticsProcessor": {
  "commandName": "Project",
  "environmentVariables": {
    "DOTNET_ENVIRONMENT": "Development"
  },
  "dotnetRunMessages": true
}
```
- **Purpose:** Runs the job **once** for local testing (without Hangfire).
- **How to Run:**
  - **Visual Studio**: Select **FinancialAnalyticsProcessor** as the startup profile.
  - **CLI Command**:
    ```bash
    dotnet run --project FinancialAnalyticsProcessor
    ```

---

#### **2️⃣ HangfireExecution (Recurring Jobs with Hangfire)**
```json
"HangfireExecution": {
  "commandName": "Project",
  "commandLineArgs": "--use-hangfire",
  "environmentVariables": {
    "DOTNET_ENVIRONMENT": "Development"
  },
  "dotnetRunMessages": true
}
```
- **Purpose:** Runs the job **recurringly** using **Hangfire**.
- **How to Run:**
  - **Visual Studio**: Select **HangfireExecution** as the startup profile.
  - **CLI Command**:
    ```bash
    dotnet run --project FinancialAnalyticsProcessor -- --use-hangfire
    ```

---

## **📌 Running the Application in Visual Studio 2022**

### **1️⃣ Select Execution Mode**
1. Open **Visual Studio 2022**.
2. In **Solution Explorer**, right-click on the **FinancialAnalyticsProcessor** project.
3. Select **Properties** → **Debug**.
4. Under **Profile**, choose:
   - `FinancialAnalyticsProcessor` (**for local testing**)
   - `HangfireExecution` (**for recurring jobs**)
5. Click **Apply** and **Save**.

### **2️⃣ Run the Application**
- Press **F5** to start the application with debugging.
- OR press **Ctrl + F5** to start without debugging.

---

## **📌 Hangfire Dashboard Access**
If running with `HangfireExecution`, the **Hangfire Dashboard** will be available at:
```
http://localhost:5000/hangfire
```

✅ The dashboard allows you to **monitor scheduled jobs** and **track execution logs**.

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

## **📌 Summary of Execution Methods**
| Execution Mode | Profile Name | Visual Studio Configuration | CLI Command |
|---------------|-------------|----------------------------|-------------|
| **Local Testing Mode** | `FinancialAnalyticsProcessor` | Select `FinancialAnalyticsProcessor` | `dotnet run --project FinancialAnalyticsProcessor` |
| **Hangfire Recurring Jobs** | `HangfireExecution` | Select `HangfireExecution` | `dotnet run --project FinancialAnalyticsProcessor -- --use-hangfire` |

---

## **📌 Final Notes**
- **Use** `FinancialAnalyticsProcessor` **for testing individual executions**.
- **Use** `HangfireExecution` **for automated job scheduling**.
- The **Hangfire Dashboard** can be accessed at:
  ```
  http://localhost:5000/hangfire
  ```
- If the job **does not execute**, check the **logs in Visual Studio's Output Window**.


