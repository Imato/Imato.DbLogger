using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Imato.DbLogger
{
    public static class AppEnvironment
    {
        private static readonly Regex reEnv = new Regex(@"\{(\w+)\}",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Get Environment Variable
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string? GetVariable(string? name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return Environment.GetEnvironmentVariable(name);
        }

        /// <summary>
        /// Set value of Environment Variables in string str
        /// </summary>
        /// <param name="str">Same string {VARIABLE_NAME}</param>
        /// <returns></returns>
        /// <exception cref="ApplicationException"></exception>
        public static string GetVariables(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;

            if (str.Contains("{") && str.Contains("}"))
            {
                var cs = str;
                foreach (var m in reEnv.Matches(cs).ToArray())
                {
                    var name = m.Groups[1].Value;
                    var value = Environment.GetEnvironmentVariable(name);
                    if (value == null)
                    {
                        throw new ApplicationException($"Unknown Environment Variable {name}");
                    }
                    cs = cs.Replace(m.Groups[0].Value, value);
                }
                return cs;
            }

            return str;
        }
    }
}