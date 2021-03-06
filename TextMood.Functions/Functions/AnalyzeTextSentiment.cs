using System;
using System.Net;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;

using Newtonsoft.Json;

using TextMood.Backend.Common;
using TextMood.Shared;

namespace TextMood.Functions
{
    [StorageAccount(QueueNameConstants.AzureWebJobsStorage)]
    public static class AnalyzeTextSentiment
    {
        readonly static Lazy<JsonSerializer> _serializerHolder = new Lazy<JsonSerializer>();

        static JsonSerializer Serializer => _serializerHolder.Value;

        [FunctionName(nameof(AnalyzeTextSentiment))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]HttpRequest req,
            [Queue(QueueNameConstants.TextModelForDatabase)]ICollector<TextMoodModel> textModelForDatabaseCollection,
            [Queue(QueueNameConstants.SendUpdate)]ICollector<TextMoodModel> sendUpdateCollection, TraceWriter log)
        {
            try
            {
                log.Info("Text Message Received");

                log.Info("Parsing Request Body");
                var httpRequestBody = await HttpRequestServices.GetContentAsString(req).ConfigureAwait(false);

                log.Info("Creating New Text Model");
                var textMoodModel = new TextMoodModel(TwilioServices.GetTextMessageBody(httpRequestBody, log));

                log.Info("Retrieving Sentiment Score");
                textMoodModel.SentimentScore = await TextAnalysisServices.GetSentiment(textMoodModel.Text).ConfigureAwait(false) ?? -1;

                log.Info("Adding TextMoodModel to Storage Queue");
                textModelForDatabaseCollection.Add(textMoodModel);
                sendUpdateCollection.Add(textMoodModel);

                var response = $"Text Sentiment: {EmojiServices.GetEmoji(textMoodModel.SentimentScore)}";

                log.Info($"Sending OK Response: {response}");

                return new ContentResult
                {
                    StatusCode = (int)HttpStatusCode.OK,
                    Content = TwilioServices.CreateTwilioResponse(response),
                    ContentType = "application/xml"
                };
            }
            catch(Exception e)
            {
                log.Error(e.Message, e);
                throw;
            }
        }
    }
}
