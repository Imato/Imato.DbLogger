using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;

namespace Imato.DbLogger
{
    [ProviderAlias("DbLogger")]
    public class DbLoggerProvider : ILoggerProvider
    {
        private static List<DbLogger> loggers = new List<DbLogger>();

        public DbLoggerOptions Options { get; }
        private readonly IConfiguration configuration;

        public DbLoggerProvider(IOptions<DbLoggerOptions> options, IConfiguration configuration)
        {
            Options = options.Value;
            this.configuration = configuration;
        }

        public ILogger CreateLogger(string categoryName)
        {
            var logger = new DbLogger(configuration, Options, categoryName);
            loggers.Add(logger);
            return logger;
        }

        public void Dispose()
        {
            foreach (var logger in loggers)
            {
                logger.Dispose();
            }
        }
    }
}