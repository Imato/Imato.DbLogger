using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Imato.DbLogger.Test
{
    public class BaseTest
    {
        private static IServiceProvider _provider = null!;
        private static IHost _app = null!;

        public BaseTest()
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();
            var builder = Host.CreateDefaultBuilder();
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IConfiguration>(config);
            });
            builder.ConfigureLogging((_, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddDebug();
            })
            .ConfigureDbLogger();

            _app = builder.Build();
            _provider = _app.Services.CreateScope().ServiceProvider;
        }

        public T GetRequiredService<T>() where T : class
        {
            return _provider.GetRequiredService<T>();
        }

        public MethodInfo GetMethod<T>(string name,
                object[]? parameters = null)
        {
            if (parameters?.Length > 0)
            {
                return typeof(T)
                    .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(x => x.Name == name && x.GetParameters().Length == parameters.Length)
                    .FirstOrDefault() ?? throw new NotExistsMethodException<T>(name);
            }

            return typeof(T).GetMethod(name,
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new NotExistsMethodException<T>(name);
        }

        public object? GetField<T>(T obj, string name)
        {
            var field = typeof(T).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(obj);
        }
    }
}