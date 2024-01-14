### Imato.DbLogger

Log into table using bulk insert

Add logger settings into appsettings.json with your DB:
- connection string (USER_NAME - env variable)
- table
- table columns (order matters)
 
```json
"DbLogger": {
      "Options": {
        "ConnectionString": "Data Source=localhost;Initial Catalog=Test;Persist Security Info=True;User ID={USER_NAME};Password={USER_PASSWORD}",
        "Table": "logs",
        "Columns": "App,Date,Level,Message,Server,Source",
        "BatchSizeRows": 100
      }
    }
```

Configure logger into Startup.cs

```csharp
Host.CreateDefaultBuilder(args).ConfigureDbLogger();
```