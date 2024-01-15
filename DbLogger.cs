using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;
using System.Reflection;
using System.Data;
using Imato.Dapper.DbContext;
using System.Collections.Generic;
using Dapper;

namespace Imato.DbLogger
{
    public class DbLogger : ILogger, IDisposable
    {
        private readonly string category;
        private readonly string? sqlTable;
        private readonly string[]? sqlColumns;
        private readonly int batchSize;
        private readonly int saveDelay = 15_000;
        private readonly bool active;

        private static DateTime lastSave = DateTime.Now;
        private static readonly ConcurrentQueue<DbLogEvent> queue = new ConcurrentQueue<DbLogEvent>();
        private static IDbConnection connection = null!;

        public DbLogger(IOptions<DbLoggerOptions?> options, string category = "")
            : this(options?.Value, category)
        {
        }

        public DbLogger(DbLoggerOptions? options, string category = "")
        {
            var assembly = Assembly.GetEntryAssembly().GetName().Name;
            category = category.Replace($"{assembly}.", "");
            this.category = $"{assembly}: {category}";
            if (options != null
                && !string.IsNullOrEmpty(options?.ConnectionString)
                && !string.IsNullOrEmpty(options?.Table)
                && !string.IsNullOrEmpty(options?.Columns))
            {
                if (connection == null)
                {
                    connection = DbContext.GetConnection(connectionString: options.ConnectionString,
                    dataBase: "",
                    user: "",
                    password: "");
                }
                sqlTable = options.Table;
                sqlColumns = options.Columns.Split(",");
                batchSize = options.BatchSizeRows;
                active = true;
            }
        }

        public async Task DeleteAsync()
        {
            if (active)
            {
                await connection.ExecuteAsync($"delete from {sqlTable}");
            }
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public async void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (active && IsEnabled(logLevel))
            {
                var log = new DbLogEvent
                {
                    Source = category
                };

                log.Message = exception?.ToString() ?? formatter(state, exception) ?? state?.ToString() ?? "Empty";

                switch (logLevel)
                {
                    case LogLevel.Trace:

                    case LogLevel.Debug:
                        log.Level = 0;
                        break;

                    case LogLevel.Information:
                        log.Level = 1;
                        break;

                    case LogLevel.Warning:
                        log.Level = 2;
                        break;

                    case LogLevel.Error:

                    case LogLevel.Critical:
                        log.Level = 3;
                        break;

                    case LogLevel.None:
                        log.Level = 0;
                        break;
                }

                queue.Enqueue(log);

                if (queue.Count > batchSize
                    || ((DateTime.Now - lastSave).TotalMilliseconds > saveDelay && queue.Count > 0))
                {
                    await SaveAsync();
                }
            }
        }

        public async Task SaveAsync()
        {
            lastSave = DateTime.Now;

            if (!active)
            {
                return;
            }

            try
            {
                var count = queue.Count;
                var logs = new List<DbLogEvent>(count);
                while (queue.TryDequeue(out var log) && logs.Count <= count)
                {
                    logs.Add(log);
                }

                await connection.BulkInsertAsync(data: logs,
                    tableName: sqlTable,
                    columns: sqlColumns,
                    batchSize: count,
                    skipFieldsCheck: true);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
            }
        }

        public void Dispose()
        {
            SaveAsync().Wait();
            connection.Dispose();
        }
    }
}