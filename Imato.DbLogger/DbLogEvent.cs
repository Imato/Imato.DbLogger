using System;

namespace Imato.DbLogger
{
    public class DbLogEvent
    {
        public DateTime Date { get; set; } = DateTime.Now;
        public byte Level { get; set; } = 1;
        public string? Source { get; set; }
        public string? Message { get; set; }
        public string? Exception { get; set; }
        public string Server { get; set; } = Environment.MachineName;
        public string App { get; set; } = Constants.AppName;
    }
}