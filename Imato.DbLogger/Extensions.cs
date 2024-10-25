using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Data;

namespace Imato.DbLogger
{
    public static class Extensions
    {
        public static ILoggingBuilder AddDbLoggerConfig(
            this ILoggingBuilder builder,
            Action<DbLoggerOptions> configure)
        {
            builder.Services.AddSingleton<ILoggerProvider, DbLoggerProvider>();
            builder.Services.AddSingleton<ILogger, DbLogger>();
            builder.Services.AddSingleton<DbLogger>();
            builder.Services.Configure(configure);
            SqlMapper.AddTypeMap(typeof(LogLevel), DbType.String);
            var t = new DbLogger(new DbLoggerOptions(), "");
            return builder;
        }

        public static IHostBuilder ConfigureDbLogger(this IHostBuilder builder)
        {
            builder.ConfigureLogging((context, logging) =>
                logging.AddDbLoggerConfig(options =>
                {
                    context.Configuration
                        .GetSection("Logging")
                        .GetSection("DbLogger")
                        .GetSection("Options")
                        .Bind(options);
                })
            );
            return builder;
        }

        public static string GetFormatedConnectionString(this IConfiguration configuration, string name)
        {
            var cs = configuration.GetConnectionString(name)
                ?? throw new ArgumentException($"Unknown connection string name {name}");
            return AppEnvironment.GetVariables(cs);
        }
    }
}