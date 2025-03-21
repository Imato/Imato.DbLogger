﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Imato.Dapper.DbContext;
using Imato.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Imato.DbLogger
{
    public class DbLogger : ILogger, IDisposable
    {
        private readonly string category;
        private readonly int saveDelay = 15_000;

        private static int batchSize;
        private static string? sqlTable;
        private static Dictionary<string, string> mappings = new Dictionary<string, string>();
        private static DateTime lastSave = DateTime.Now;
        private static readonly ConcurrentQueue<DbLogEvent> queue = new ConcurrentQueue<DbLogEvent>();
        private static IDbConnection connection = null!;
        private static SemaphoreSlim semaphore = new SemaphoreSlim(1);
        private static bool active;
        private static LogLevel logLevel = LogLevel.Error;

        public DbLogger(IConfiguration configuration, IOptions<DbLoggerOptions> options, string category = "")
            : this(configuration, options?.Value, category)
        {
        }

        public DbLogger(IConfiguration configuration, DbLoggerOptions options, string category = "")
        {
            var assembly = Assembly.GetEntryAssembly().GetName().Name;
            category = category.Replace($"{assembly}.", "");
            this.category = $"{assembly}: {category}";
            Initilize(options, configuration);
        }

        private void Initilize(DbLoggerOptions options, IConfiguration configuration)
        {
            semaphore.Wait();
            if (sqlTable == null
                    && !active
                    && options != null
                    && !string.IsNullOrEmpty(options?.ConnectionString)
                    && !string.IsNullOrEmpty(options?.Table)
                    && !string.IsNullOrEmpty(options?.Columns))
            {
                var context = new DbContext(configuration, null, options.ConnectionString);
                connection = context.Connection();
                sqlTable = options.Table;
                var cl = options.Columns.Split(",");
                var fields = Objects.GetFieldNames<DbLogEvent>().ToArray();
                for (int i = 0; i < fields.Length; i++)
                {
                    if (i < cl.Length)
                    {
                        mappings.Add(fields[i], cl[i]);
                    }
                }
                batchSize = options.BatchSizeRows;
                CheckColumns(context, sqlTable, mappings.Values);
                if (Enum.TryParse(typeof(LogLevel), options.LogLevel, out var ll))
                {
                    logLevel = (LogLevel)ll;
                }

                active = true;
            }

            sqlTable ??= "";
            semaphore.Release();
        }

        private void CheckColumns(DbContext context, string table, IEnumerable<string> columns)
        {
            var t = context.GetColumnsAsync(table).Result;
            var tc = t
                .Where(x => !x.IsComputed && !x.IsIdentity)
                .Select(x => x.Name)
                .ToArray();
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

        public bool IsEnabled(LogLevel level)
        {
            return level >= logLevel;
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
                        log.Level = 0;
                        break;

                    case LogLevel.Debug:
                        log.Level = 1;
                        break;

                    case LogLevel.Information:
                        log.Level = 2;
                        break;

                    case LogLevel.Warning:
                        log.Level = 3;
                        break;

                    case LogLevel.Error:
                        log.Level = 4;
                        break;

                    case LogLevel.Critical:
                        log.Level = 5;
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
                mappings: mappings,
                batchSize: count);
        }

        public async Task<IEnumerable<DbLogEvent>> GetLastEventsAsync(DateTime? lastDate = null)
        {
            lastDate ??= DateTime.Now.AddMinutes(-5);
            return await connection.QueryAsync<DbLogEvent>(
                $"select * from {sqlTable} where {mappings["Date"]} > @lastDate",
                new { lastDate });
        }

        public async Task ClearAsync()
        {
            await connection.ExecuteAsync($"truncate table {sqlTable}");
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