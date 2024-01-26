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
using System.Threading;
using System.Linq;

namespace Imato.DbLogger
{
    public class DbLogger : ILogger, IDisposable
    {
        private readonly string category;
        private readonly int saveDelay = 15_000;

        private static int batchSize;
        private static string? sqlTable;
        private static string[]? sqlColumns;
        private static DateTime lastSave = DateTime.Now;
        private static readonly ConcurrentQueue<DbLogEvent> queue = new ConcurrentQueue<DbLogEvent>();
        private static IDbConnection connection = null!;
        private static SemaphoreSlim semaphore = new SemaphoreSlim(1);
        private static bool active;

        public DbLogger(IOptions<DbLoggerOptions?> options, string category = "")
            : this(options?.Value, category)
        {
        }

        public DbLogger(DbLoggerOptions? options, string category = "")
        {
            var assembly = Assembly.GetEntryAssembly().GetName().Name;
            category = category.Replace($"{assembly}.", "");
            this.category = $"{assembly}: {category}";
            Initilize(options);
        }

        private void Initilize(DbLoggerOptions? options)
        {
            semaphore.Wait();
            if (!active)
            {
                if (options != null
                && !string.IsNullOrEmpty(options?.ConnectionString)
                && !string.IsNullOrEmpty(options?.Table)
                && !string.IsNullOrEmpty(options?.Columns))
                {
                    var context = new DbContext(options.ConnectionString);
                    connection = context.Connection();
                    sqlTable = options.Table;
                    sqlColumns = options.Columns.Split(",");
                    batchSize = options.BatchSizeRows;
                    CheckColumns(context, sqlTable, sqlColumns);
                    active = true;
                }
            }
            semaphore.Release();
        }

        private void CheckColumns(DbContext context, string table, string[] columns)
        {
            var tc = context.GetColumnsAsync(table).Result;
            var notExits = "";
            foreach (var column in columns)
            {
                if (!tc.Contains(column))
                {
                    notExits += column + ";";
                }
            }
            if (notExits.Length > 0)
            {
                throw new ArgumentException($"Columns {notExits} not exist in DB table {table}");
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

                log.Exception = exception?.ToString();
                log.Message = formatter(state, exception) ?? state?.ToString();

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
                    try
                    {
                        await SaveAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(ex.ToString());
                    }
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

        public void Dispose()
        {
            try
            {
                SaveAsync().Wait();
                connection.Dispose();
            }
            catch { }
        }
    }
}