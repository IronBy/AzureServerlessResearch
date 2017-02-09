using System.Collections.Generic;

namespace LogApi.Models
{
    public class LogResult
    {
        public int Elapsed { get; set; }
        public IEnumerable<LogItem> Items { get; set; }
    }
}