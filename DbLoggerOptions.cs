namespace Imato.DbLogger
{
    public class DbLoggerOptions
    {
        public string ConnectionString { get; set; } = "";
        public string Table { get; set; } = "";
        public string Columns { get; set; } = "";
        public int BatchSizeRows { get; set; } = 100;
        public string LogLevel { get; set; } = "Error";
    }
}