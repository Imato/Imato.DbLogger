using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Imato.DbLogger
{
    [ProviderAlias("DbLogger")]
    public class DbLoggerProvider : ILoggerProvider
    {
        private static DbLogger logger = null!;

        public DbLoggerOptions? Options { get; }

        public DbLoggerProvider(IOptions<DbLoggerOptions?> options)
        {
            Options = options?.Value;
        }

        public ILogger CreateLogger(string categoryName)
        {
            if (logger == null)
            {
                logger = new DbLogger(Options, categoryName);
            }
            return logger;
        }

        public void Dispose()
        {
            logger.Dispose();
        }
    }
}