using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NotifySlack
{
    public static class RetrieveLogs
    {
        [FunctionName("RetrieveLogs")]
        public static async Task<ContentResult> Run(
    [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
    ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            var storedLogs = await RetrieveFromDb();

            // Generate the HTML content.
            var htmlContent = @"
    <!DOCTYPE html>
    <html>
    <head>
        <title>Github Commit Logs</title>
    </head>
    <body>
        <h1>Github Commit Logs</h1>
        <table>
            <thead>
                <tr>
                    <th>Commit ID</th>
                    <th>Repository</th>
                    <th>Username</th>
                    <th>Timestamp</th>
                    <th>Message</th>
                </tr>
            </thead>
            <tbody>";

            foreach (var storedLog in storedLogs)
            {
                htmlContent += @"
            <tr>
                <td>" + storedLog.CommitId + @"</td>
                <td>" + storedLog.Repository + @"</td>
                <td>" + storedLog.Username + @"</td>
                <td>" + storedLog.Timestamp + @"</td>
                <td>" + storedLog.Message + @"</td>
            </tr>";
            }

            htmlContent += @"
        </tbody>
    </table>
    </body>
    </html>";

            return new ContentResult
            {
                Content = htmlContent,
                ContentType = "text/html"
            };
        }


        private static async Task<IEnumerable<StoredLog>> RetrieveFromDb()
        {
            var connectionString = Environment.GetEnvironmentVariable("notify-slack-connection", EnvironmentVariableTarget.Process);

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var db = connection.Database;
                var query = $"SELECT * FROM StoredLogs";

                using (var cmd = new SqlCommand(query, connection))
                {
                    var reader = await cmd.ExecuteReaderAsync();

                    var columnNames = new List<string>();

                    for (int i = 0; i < reader.FieldCount; i++)
                        columnNames.Add(reader.GetName(i));

                    var results = new List<StoredLog>();

                    while (await reader.ReadAsync())
                    {
                        // Create a new StoredLog instance.
                        List<string> currentValues = new List<string>();

                        for (int i = 0; i < columnNames.Count; i++)
                            currentValues.Add(reader[columnNames[i]].ToString());


                        StoredLog result = new StoredLog()
                        {
                            CommitId = currentValues[0],
                            Username = currentValues[1],
                            Timestamp = currentValues[2],
                            Message = currentValues[3],
                            Repository = currentValues[4]
                        };

                        results.Add(result);
                    }

                    return results;
                }
            }

        }


        public class StoredLog
        {
            public string CommitId { get; set; }
            public string Repository { get; set; }
            public string Username { get; set; }
            public string Timestamp { get; set; }
            public string Message { get; set; }

        }
    }
}
