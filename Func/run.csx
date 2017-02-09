#r "System.Configuration"
#r "System.Data"

using System;
using System.Collections;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Net;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info("C# HTTP trigger function processed a request.");
    var stopWatch = Stopwatch.StartNew();
    List<LogItem> result;

    try
    {
        var str = ConfigurationManager.ConnectionStrings["mobileconnection"].ConnectionString;
        using (SqlConnection connection = new SqlConnection(str))
        {
            connection.Open();
            await AddDbLogItem(req, connection);
            result = await ReadLogItems(connection);
        }
    }
    catch (Exception e)
    {
        log.Error(e.Message);
        log.Error(e.StackTrace);
        return req.CreateResponse(HttpStatusCode.BadRequest, "Error happened during request processing");
    }

    return req.CreateResponse(
        HttpStatusCode.OK,
        // It's important to return not pure collection but wrapped into an object 
        new 
        { 
            elapsed = stopWatch.ElapsedMilliseconds,
            items = result
        });
}

private static async Task AddDbLogItem(HttpRequestMessage req, SqlConnection connection)
{
    var sqlText = $"INSERT INTO ActionLog (id, nodeid, createdon, comment, createdby) "
        + $"values (NewID(), '{Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")}', "
        + "getutcdate(), "
        + "@comment, "
        + "'azurefunc')";
    using (SqlCommand cmd = new SqlCommand(sqlText, connection))
    {
        var param = cmd.Parameters.Add("@comment", SqlDbType.NVarChar, 500);
        param.Value = await GetParameter(req, "comment");
        await cmd.ExecuteNonQueryAsync();
    }
}

private static async Task<string> GetParameter(HttpRequestMessage req, string paramName)
{
    return GetQeryParameter(req, paramName) ?? await GetBodyParameter(req, paramName);
}

private static string GetQeryParameter(HttpRequestMessage req, string paramName)
{
    return req.GetQueryNameValuePairs()
        .FirstOrDefault(q => string.Compare(q.Key, paramName, true) == 0)
        .Value;
}

private static async Task<string> GetBodyParameter(HttpRequestMessage req, string paramName)
{
    var body = await GetBodyParameters(req);
    return body != null && body.ContainsKey(paramName)
        ? body[paramName]
        : null;
}

private static Dictionary<string, string> bodyParams = null;

private static async Task<Dictionary<string, string>> GetBodyParameters(HttpRequestMessage req)
{
    // We store body parameters in static variable as once we want to read them once
    // Parameters are empty if we read them second time. It's a nature of HttpRequestMessage.Content
    return bodyParams ?? (bodyParams = await ReadBodyParams(req));
}

private static async Task<Dictionary<string, string>> ReadBodyParams(HttpRequestMessage req)
{
    return await req.Content.ReadAsAsync<Dictionary<string, string>>()
        ?? new Dictionary<string, string>(0);
}

private static async Task<List<LogItem>> ReadLogItems(SqlConnection connection)
{
    var maxItemsCount = 10;
    var result = new List<LogItem>(maxItemsCount);
    using (SqlCommand cmd = new SqlCommand($"select top {maxItemsCount} * from ActionLog order by createdon desc", connection))
    {
        using(var reader = await cmd.ExecuteReaderAsync(CommandBehavior.Default))
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

public struct LogItem
{
    public Guid id;
    public DateTime createdon;
    public string comment;
    public string createdby;
    public string nodeid;
}