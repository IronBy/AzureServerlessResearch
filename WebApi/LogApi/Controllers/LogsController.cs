using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using LogApi.Models;

namespace LogApi.Controllers
{
    [Authorize]
    public class LogsController : ApiController
    {
        [HttpPost]
        [AllowAnonymous]
        public async Task<LogResult> AddLog([FromBody]LogRequest logRequest)
        {
            Trace.WriteLine("Api starts processing a request.");
            var stopWatch = Stopwatch.StartNew();
            List<LogItem> result;
            
            try
            {
                var str = ConfigurationManager.ConnectionStrings["mobileconnection"].ConnectionString;
                using (SqlConnection connection = new SqlConnection(str))
                {
                    connection.Open();
                    await AddDbLogItem(logRequest?.Comment ?? string.Empty, connection);
                    result = await ReadLogItems(connection);
                }
            }
            catch (Exception e)
            {
                Trace.TraceError(e.Message);
                Trace.TraceError(e.StackTrace);
                throw new HttpResponseException(
                    Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Something went wrong"));
            }

            return new LogResult
            {
                Elapsed = (int)stopWatch.ElapsedMilliseconds,
                Items = result
            };
        }

        private async Task AddDbLogItem(string comment, SqlConnection connection)
        {
            var sqlText = "INSERT INTO ActionLog (id, nodeid, createdon, comment, createdby) "
                + $"values (NewID(), '{Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")}', "
                + "getutcdate(), "
                + $"'{comment}', "
                + "'WebApi')";
            using (SqlCommand cmd = new SqlCommand(sqlText, connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private static async Task<List<LogItem>> ReadLogItems(SqlConnection connection)
        {
            var maxItemsCount = 10;
            var result = new List<LogItem>(maxItemsCount);
            using (SqlCommand cmd = new SqlCommand($"select top {maxItemsCount} * from ActionLog order by createdon desc", connection))
            {
                using (var reader = await cmd.ExecuteReaderAsync(CommandBehavior.Default))
                {
                    while (reader.Read())
                    {
                        result.Add(new LogItem
                        {
                            id = (Guid)reader["id"],
                            createdon = (DateTime)reader["createdOn"],
                            comment = (string)reader["comment"],
                            createdby = (string)reader["createdby"],
                            nodeid = (string)reader["nodeid"]
                        });
                    }
                }
            }
            return result;
        }

        // GET api/logs/5
        [AllowAnonymous]
        public string Get(string id)
        {
            return $"value '{id}'";
        }
    }
}