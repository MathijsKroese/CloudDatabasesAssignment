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

namespace CloudDatabases
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

            if (content != null)
                await SendToSlack(content);

            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            string responseMessage = string.IsNullOrEmpty(name)
                ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a more personalized response  ."
                : $"Hello, {name}. This HTTP triggered function executed successfully.";

            return new OkObjectResult(responseMessage);
        }

        private static async Task<string> SendToSlack(JToken token)
        {
            string message = BuildMessage(token);

            string slackWebhook = "https://hooks.slack.com/services/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";

            using (HttpClient client = new HttpClient())
            {
                var payload = new StringContent(JsonConvert.SerializeObject(new { text = message }), Encoding.UTF8, "application/json");

                var response = await client.PostAsync(slackWebhook, payload);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to send message to Slack: {response.StatusCode}");
                }

                return "Slack has been notified!";
            }
        }

        private static string BuildMessage(JToken token)
        {
            string username = "";
            string repository = "";
            string timestamp = "";
            string message = "";

            foreach (var property in token.Children<JProperty>())
            {
                if (string.IsNullOrEmpty(property.Name))
                    continue;

                string currentName = property.Name.ToString();
                switch (currentName)
                {
                    case "pusher":
                        username = property.Value["name"].ToString();
                        break;

                    case "head_commit":
                        timestamp = property.Value["timestamp"].ToString();
                        message = property.Value["message"].ToString();
                        break;

                    case "repository":
                        repository = property.Value["name"].ToString();
                        break;

                    default:
                        break;
                }

                if (property.Value.Type == JTokenType.Object)
                    BuildMessage(property.Value);
            }
            return $"A commit has been pushed to [ {repository} ] by [ {username} ] on [ {timestamp} ] with the following message: \n[ {message} ]";
        }
    }
}
