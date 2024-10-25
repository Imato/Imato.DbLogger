using System;
using System.Reflection;
using System.Text.Json;

namespace Imato.DbLogger
{
    internal static class Constants
    {
        private static string _appName = "";

        public static JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public static string AppName
        {
            get
            {
                if (string.IsNullOrEmpty(_appName))
                {
                    _appName =
                        AppDomain.CurrentDomain.BaseDirectory
                        + Assembly.GetEntryAssembly().GetName().Name
                        + ":"
                        + Assembly.GetEntryAssembly().GetName().Version.ToString();
                }
                return _appName;
            }

            set { _appName = !string.IsNullOrEmpty(value) ? value : _appName; }
        }
    }
}