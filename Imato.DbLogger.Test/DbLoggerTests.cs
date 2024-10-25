using Microsoft.Extensions.Logging;

namespace Imato.DbLogger.Test
{
    public class DbLoggerTests : BaseTest
    {
        public DbLogger dbLogger;
        private readonly ILogger<DbLoggerTests> logger;

        public DbLoggerTests()
        {
            dbLogger = GetRequiredService<DbLogger>();
            logger = GetRequiredService<ILoggerFactory>().CreateLogger<DbLoggerTests>();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            dbLogger?.Dispose();
        }

        [Test]
        public async Task SaveLogsTest()
        {
            await dbLogger.ClearAsync();

            foreach (var i in Enumerable.Range(0, 100))
            {
                logger.LogInformation("Log entry {0}", [i]);
            }
            await Task.Delay(15000);
            logger.LogInformation("Save logs");

            var logs = await dbLogger.GetLastEventsAsync();
            Assert.That(logs.Count(), Is.GreaterThanOrEqualTo(100));
        }
    }
}