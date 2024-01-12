using System;

namespace Imato.DbLogger
{
    public class DbLogEvent
    {
        public DateTime Date { get; set; } = DateTime.Now;
        public byte Level { get; set; } = 1;
        public string Source { get; set; } = null!;
        public string Message { get; set; } = null!;
        public string Server => Environment.MachineName;
        public string App => Constants.AppName;
    }
}