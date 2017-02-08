using System;

namespace LogApi.Models
{
    public struct LogItem
    {
        public Guid id;
        public DateTime createdon;
        public string comment;
        public string createdby;
        public string nodeid;
    }
}