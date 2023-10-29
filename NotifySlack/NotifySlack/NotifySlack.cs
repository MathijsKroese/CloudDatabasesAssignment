using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Text;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Windows.Markup;

namespace NotifySlack
{
    public static class NotifySlack
    {
        [FunctionName("NotifySlack")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var content = JObject.Parse(body);

            if (content == null)
                return new BadRequestObjectResult("No body found");

            string slackMessage = "";
            try
            {
                List<string> values = ExtractValues(content);
                slackMessage = $"A commit has been pushed to [ {values[0]} ] by [ {values[1]} ] on [ {values[2]} ] with the following message: \n[ {values[3]} ]";
                await SendToSlack(slackMessage);
                await UpdateDatabase(values);
                return new OkObjectResult("Slack has been notified");
            }
            catch (Exception e)
            { 
                log.LogError(e.Message);
                return new BadRequestObjectResult($"Error: {e.Message}");
            }
        }

        private static async Task<string> SendToSlack(string message)
        {

            using (HttpClient client = new HttpClient())
            {
                string slackWebhook = "https://hooks.slack.com/services/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";
                var payload = new StringContent(JsonConvert.SerializeObject(new { text = message }), Encoding.UTF8, "application/json");
                var response = await client.PostAsync(slackWebhook, payload);

                if (!response.IsSuccessStatusCode)
                    throw new Exception($"Failed to send message to Slack: {response.StatusCode}");

                return "Slack has been notified!";
            }

        }

        private static async Task UpdateDatabase(List<string> values)
        {
            var connectionString = Environment.GetEnvironmentVariable("notify-slack-connection", EnvironmentVariableTarget.Process);
            var responseResult = 0;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var db = connection.Database;
                var query = $"INSERT INTO {db} VALUES('{values[4]}, {values[0]}, {values[1]}, {values[2]}, {values[3]}')";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    responseResult = await command.ExecuteNonQueryAsync();
                }
                connection.Close();
            }
        }

        private static List<string> ExtractValues(JToken token)
        {
            List<string> values = new List<string>();

            foreach (var property in token.Children<JProperty>())
            {
                if (string.IsNullOrEmpty(property.Name))
                    continue;

                string currentName = property.Name.ToString();
                switch (currentName)
                {
                    case "repository":
                        values.Add(property.Value["name"].ToString());
                        break;

                    case "pusher":
                        values.Add(property.Value["name"].ToString());
                        break;

                    case "head_commit":
                        values.Add(property.Value["timestamp"].ToString());
                        values.Add(property.Value["message"].ToString());
                        values.Add(property.Value["id"].ToString());
                        break;

                    default:
                        break;
                }

                if (property.Value.Type == JTokenType.Object)
                    ExtractValues(property.Value);
            }

            return values;
        }
    }
}
