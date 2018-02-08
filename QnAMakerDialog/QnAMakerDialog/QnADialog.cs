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
using System.Text.RegularExpressions;
using Autofac.Features.Metadata;
using Chronic.Handlers;
using QnAMakerDialog.Models;

namespace QnAMakerDialog
{
    [Serializable]
    public class QnAMakerDialog<T> : IDialog<T>
    {
        private string _subscriptionKey;
        private string _knowledgeBaseId;
        private int _maxAnswers;
        private List<Metadata> _metadataBoost;
        private List<Metadata> _metadataFilter;

        public string SubscriptionKey { get => _subscriptionKey; set => _subscriptionKey = value; }
        public string KnowledgeBaseId { get => _knowledgeBaseId; set => _knowledgeBaseId = value; }
        public int MaxAnswers { get => _maxAnswers; set => _maxAnswers = value; }
        public List<Metadata> MetadataBoost { get => _metadataBoost; set => _metadataBoost = value; }
        public List<Metadata> MetadataFilter { get => _metadataFilter; set => _metadataFilter = value; }

        [NonSerialized]
        protected Dictionary<QnAMakerResponseHandlerAttribute, QnAMakerResponseHandler> HandlerByMaximumScore;

        public virtual async Task StartAsync(IDialogContext context)
        {
            var type = this.GetType();
            var qNaServiceAttribute = type.GetCustomAttributes<QnAMakerServiceAttribute>().FirstOrDefault();

            if (string.IsNullOrEmpty(KnowledgeBaseId) && qNaServiceAttribute != null)
                KnowledgeBaseId = qNaServiceAttribute.KnowledgeBaseId;

            if (string.IsNullOrEmpty(SubscriptionKey) && qNaServiceAttribute != null)
                SubscriptionKey = qNaServiceAttribute.SubscriptionKey;

            if (qNaServiceAttribute != null)
                MaxAnswers = qNaServiceAttribute.MaxAnswers;

            if (string.IsNullOrEmpty(KnowledgeBaseId) || string.IsNullOrEmpty(SubscriptionKey))
            {
                throw new Exception("Valid KnowledgeBaseId and SubscriptionKey not provided. Use QnAMakerServiceAttribute or set fields on QnAMakerDialog");
            }

            context.Wait(MessageReceived);
        }

        protected virtual async Task MessageReceived(IDialogContext context, IAwaitable<IMessageActivity> item)
        {
            var message = await item;
            await HandleMessage(context, message.Text);
        }

        private async Task HandleMessage(IDialogContext context, string queryText)
        {
            var response = await GetQnAMakerResponse(queryText, KnowledgeBaseId, SubscriptionKey);

            if (HandlerByMaximumScore == null)
            {
                HandlerByMaximumScore =
                    new Dictionary<QnAMakerResponseHandlerAttribute, QnAMakerResponseHandler>(GetHandlersByMaximumScore());
            }

            if (response.Answers.Any() && response.Answers.First().QnaId == -1)
            {
                await NoMatchHandler(context, queryText);
            }
            else
            {
                var applicableHandlers = HandlerByMaximumScore.OrderBy(h => h.Key.MaximumScore).Where(h => h.Key.MaximumScore > response.Answers.First().Score);
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
            string responseString;

            var knowledgebaseId = knowledgeBaseId; // Use knowledge base id created.
            var qnamakerSubscriptionKey = subscriptionKey; //Use subscription key assigned to you.

            //Build the URI
            var qnamakerUriBase = new Uri("https://westus.api.cognitive.microsoft.com/qnamaker/v3.0");
            var builder = new UriBuilder($"{qnamakerUriBase}/knowledgebases/{knowledgebaseId}/generateAnswer");

            //Add the question as part of the body
            var request = new QnAMakerRequest()
            {
                Question = query,
                Top = MaxAnswers,
                UserId = "QnAMakerDialog"
            };

            request.MetadataBoost = MetadataBoost?.ToArray() ?? new Metadata[] { };
            request.StrictFilters = MetadataFilter?.ToArray() ?? new Metadata[] { };

            var postBody = JsonConvert.SerializeObject(request);

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
            try
            {
                var response = JsonConvert.DeserializeObject<QnAMakerResult>(responseString);
                return response;
            }
            catch
            {
                throw new Exception("Unable to deserialize QnA Maker response string.");
            }
        }

        public virtual async Task DefaultMatchHandler(IDialogContext context, string originalQueryText, QnAMakerResult result)
        {
            var messageActivity = ProcessResultAndCreateMessageActivity(context, ref result);
            messageActivity.Text = result.Answers.First().Answer;
            await context.PostAsync(messageActivity);
            context.Wait(MessageReceived);
        }

        public virtual async Task NoMatchHandler(IDialogContext context, string originalQueryText)
        {
            throw new Exception("Sorry, I cannot find an answer to your question.");
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

        protected static IMessageActivity ProcessResultAndCreateMessageActivity(IDialogContext context, ref QnAMakerResult result)
        {
            var message = context.MakeMessage();

            var attachmentsItemRegex = new Regex("((&lt;attachment){1}((?:\\s+)|(?:(contentType=&quot;[\\w\\/-]+&quot;))(?:\\s+)|(?:(contentUrl=&quot;[\\w:/.=?-]+&quot;))(?:\\s+)|(?:(name=&quot;[\\w\\s&?\\-.@%$!£\\(\\)]+&quot;))(?:\\s+)|(?:(thumbnailUrl=&quot;[\\w:/.=?-]+&quot;))(?:\\s+))+(/&gt;))", RegexOptions.IgnoreCase);
            var matches = attachmentsItemRegex.Matches(result.Answers.First().Answer);

            foreach (var attachmentMatch in matches)
            {
                result.Answers.First().Answer = result.Answers.First().Answer.Replace(attachmentMatch.ToString(), string.Empty);

                var match = attachmentsItemRegex.Match(attachmentMatch.ToString());
                string contentType = string.Empty;
                string name = string.Empty;
                string contentUrl = string.Empty;
                string thumbnailUrl = string.Empty;

                foreach (var group in match.Groups)
                {
                    if(group.ToString().ToLower().Contains("contenttype="))
                    {
                        contentType = group.ToString().ToLower().Replace(@"contenttype=&quot;", string.Empty).Replace("&quot;",string.Empty);
                    }
                    if (group.ToString().ToLower().Contains("contenturl="))
                    {
                        contentUrl = group.ToString().ToLower().Replace(@"contenturl=&quot;", string.Empty).Replace("&quot;", string.Empty);
                    }
                    if (group.ToString().ToLower().Contains("name="))
                    {
                        name = group.ToString().ToLower().Replace(@"name=&quot;", string.Empty).Replace("&quot;", string.Empty);
                    }
                    if (group.ToString().ToLower().Contains("thumbnailurl="))
                    {
                        thumbnailUrl = group.ToString().ToLower().Replace(@"thumbnailurl=&quot;", string.Empty).Replace("&quot;", string.Empty);
                    }
                }

                var attachment = new Attachment(contentType, contentUrl, name: !string.IsNullOrEmpty(name) ? name : null, thumbnailUrl: !string.IsNullOrEmpty(thumbnailUrl) ? thumbnailUrl : null);
                message.Attachments.Add(attachment);
            }

            return message;
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

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
    [Serializable]
    public class QnAMakerServiceAttribute : Attribute
    {
        public string SubscriptionKey { get; set; }
        public string KnowledgeBaseId { get; set; }
        public int MaxAnswers { get; set; }
        public List<Metadata> MetadataBoost { get; set; }
        public List<Metadata> MetadataFilter { get; set; }

        public QnAMakerServiceAttribute(string subscriptionKey, string knowledgeBaseId, int maxAnswers = 5)
        {
            MaxAnswers = maxAnswers;
            SubscriptionKey = subscriptionKey;
            KnowledgeBaseId = knowledgeBaseId;
        }
    }

    public delegate Task QnAMakerResponseHandler(IDialogContext context, string originalQueryText, QnAMakerResult result);
}