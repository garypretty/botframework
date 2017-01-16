// 
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// 
// Gary Pretty Github:
// https://github.com/GaryPretty
// 
// Code derived from existing dialogs within the Microsoft Bot Framework
// https://github.com/Microsoft/BotBuilder
// 
// MIT License:
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using System.Net;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Microsoft.Bot.Builder.Internals.Fibers;

namespace QnADialog
{
    [Serializable]
    public class QnADialog<T> : IDialog<T>
    {
        QnAMakerService qNaMakerService;

        [NonSerialized]
        protected Dictionary<QnAMakerResponseHandlerAttribute, QnAMakerResponseHandler> HandlerByMaximumScore;

        public virtual async Task StartAsync(IDialogContext context)
        {
            var type = this.GetType();
            var qNaServiceAttribute = type.GetCustomAttributes<QnAMakerServiceAttribute>().FirstOrDefault();
            qNaMakerService = new QnAMakerService(qNaServiceAttribute.SubscriptionKey, qNaServiceAttribute.KnowledgeBaseId);
            context.Wait(MessageReceived);
        }

        protected virtual async Task MessageReceived(IDialogContext context, IAwaitable<IMessageActivity> item)
        {
            var message = await item;
            await HandleMessage(context, message.Text);
        }

        private async Task HandleMessage(IDialogContext context, string queryText)
        {
            var response = await GetQnAMakerResponse(queryText, qNaMakerService.KnowledgeBaseId, qNaMakerService.SubscriptionKey);

            if (HandlerByMaximumScore == null)
            {
                HandlerByMaximumScore =
                    new Dictionary<QnAMakerResponseHandlerAttribute, QnAMakerResponseHandler>(GetHandlersByMaximumScore());
            }

            if (response.Score == 0)
            {
                await NoMatchHandler(context, queryText);
            }
            else
            {
                var applicableHandlers = HandlerByMaximumScore.OrderBy(h => h.Key.MaximumScore).Where(h => h.Key.MaximumScore > response.Score);
                var handler = applicableHandlers.Any() ? applicableHandlers.First().Value : null;

                if (handler != null)
                {
                    await handler.Invoke(context, queryText, response);
                }
                else
                {
                    await DefaultMatchHandler(context, queryText, response);
                }
            }
        }

        private async Task<QnAMakerResult> GetQnAMakerResponse(string query, string knowledgeBaseId, string subscriptionKey)
        {
            string responseString = string.Empty;

            var knowledgebaseId = knowledgeBaseId; // Use knowledge base id created.
            var qnamakerSubscriptionKey = subscriptionKey; //Use subscription key assigned to you.

            //Build the URI
            Uri qnamakerUriBase = new Uri("https://westus.api.cognitive.microsoft.com/qnamaker/v1.0");
            var builder = new UriBuilder($"{qnamakerUriBase}/knowledgebases/{knowledgebaseId}/generateAnswer");

            //Add the question as part of the body
            var postBody = $"{{\"question\": \"{query}\"}}";

            //Send the POST request
            using (WebClient client = new WebClient())
            {
                //Set the encoding to UTF8
                client.Encoding = System.Text.Encoding.UTF8;

                //Add the subscription key header
                client.Headers.Add("Ocp-Apim-Subscription-Key", qnamakerSubscriptionKey);
                client.Headers.Add("Content-Type", "application/json");
                responseString = client.UploadString(builder.Uri, postBody);
            }

            //De-serialize the response
            QnAMakerResult response;
            try
            {
                response = JsonConvert.DeserializeObject<QnAMakerResult>(responseString);
                return response;
            }
            catch
            {
                throw new Exception("Unable to deserialize QnA Maker response string.");
            }
        }

        public virtual async Task DefaultMatchHandler(IDialogContext context, string originalQueryText, QnAMakerResult result)
        {
            await context.PostAsync(result.Answer);
            context.Wait(MessageReceived);
        }

        public virtual async Task NoMatchHandler(IDialogContext context, string originalQueryText)
        {
            throw new Exception("No QnA match found and NoMatchHandler method not overridden");
        }

        protected virtual IDictionary<QnAMakerResponseHandlerAttribute, QnAMakerResponseHandler> GetHandlersByMaximumScore()
        {
            return EnumerateHandlers(this).ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        internal static IEnumerable<KeyValuePair<QnAMakerResponseHandlerAttribute, QnAMakerResponseHandler>> EnumerateHandlers(object dialog)
        {
            var type = dialog.GetType();
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (var method in methods)
            {
                var qNaResponseHandlerAttributes = method.GetCustomAttributes<QnAMakerResponseHandlerAttribute>(inherit: true).ToArray();
                Delegate created = null;
                try
                {
                    created = Delegate.CreateDelegate(typeof(QnAMakerResponseHandler), dialog, method, throwOnBindFailure: false);
                }
                catch (ArgumentException)
                {
                    // "Cannot bind to the target method because its signature or security transparency is not compatible with that of the delegate type."
                    // https://github.com/Microsoft/BotBuilder/issues/634
                    // https://github.com/Microsoft/BotBuilder/issues/435
                }

                var qNaResponseHanlder = (QnAMakerResponseHandler)created;
                if (qNaResponseHanlder != null)
                {
                    foreach (var qNaResponseAttribute in qNaResponseHandlerAttributes)
                    {
                        if (qNaResponseAttribute != null && qNaResponseHandlerAttributes.Any())
                            yield return new KeyValuePair<QnAMakerResponseHandlerAttribute, QnAMakerResponseHandler>(qNaResponseAttribute, qNaResponseHanlder);
                    }
                }
            }
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class QnAMakerResponseHandlerAttribute : Attribute
    {
        public readonly double MaximumScore;

        public QnAMakerResponseHandlerAttribute(double maximumScore)
        {
            MaximumScore = maximumScore;
        }
    }

    public interface IQnAMakerService
    {
        /// <summary>
        /// The LUIS subscription key.
        /// </summary>
        string SubscriptionKey { get; }

        /// <summary>
        /// The base Uri for accessing LUIS.
        /// </summary>
        string KnowledgeBaseId { get; }
    }

    [Serializable]
    public class QnAMakerService : IQnAMakerService
    {
        public QnAMakerService(string subscriptionId, string knowledgeBaseId)
        {
            SubscriptionKey = subscriptionId;
            KnowledgeBaseId = knowledgeBaseId;
        }

        public string KnowledgeBaseId { get; set; }
        public string SubscriptionKey { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
    [Serializable]
    public class QnAMakerServiceAttribute : Attribute, IQnAMakerService
    {
        private readonly string subscriptionKey;
        public string SubscriptionKey => subscriptionKey;

        private readonly string knowledgeBaseId;
        public string KnowledgeBaseId => knowledgeBaseId;

        public QnAMakerServiceAttribute(string subscriptionKey, string knowledgeBaseId)
        {
            SetField.NotNull(out this.subscriptionKey, nameof(subscriptionKey), subscriptionKey);
            SetField.NotNull(out this.knowledgeBaseId, nameof(knowledgeBaseId), knowledgeBaseId);
        }
    }

    public delegate Task QnAMakerResponseHandler(IDialogContext context, string originalQueryText, QnAMakerResult result);
}