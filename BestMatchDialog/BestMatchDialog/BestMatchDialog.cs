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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Chronic;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;

namespace BestMatchDialog
{
    [Serializable]
    public class BestMatchDialog<T> : IDialog<T>
    {
        [NonSerialized]
        protected Dictionary<BestMatchAttribute, BestMatchHandler> HandlerByBestMatchLists;

        protected string InitialMessage;

        public virtual async Task StartAsync(IDialogContext context)
        {
            if (!string.IsNullOrEmpty(InitialMessage))
            {
                await HandleMessage(context, InitialMessage);
            }
            else
            {
                context.Wait(MessageReceived);
            }
        }

        protected virtual async Task MessageReceived(IDialogContext context, IAwaitable<IMessageActivity> item)
        {
            var message = await item;
            var messageText = await GetBestMatchQueryTextAsync(context, message);
            await HandleMessage(context, messageText);
        }

        private async Task HandleMessage(IDialogContext context, string messageText)
        {
            if (HandlerByBestMatchLists == null)
            {
                HandlerByBestMatchLists =
                    new Dictionary<BestMatchAttribute, BestMatchHandler>(GetHandlersByBestMatchLists());
            }

            BestMatchHandler handler = null;

            double bestMatchedScore = 0;

            foreach (var handlerByBestMatchList in HandlerByBestMatchLists)
            {
                var match = FindBestMatch(handlerByBestMatchList.Key.BestMatchList,
                    messageText,
                    handlerByBestMatchList.Key.Threshold,
                    handlerByBestMatchList.Key.IgnoreCase,
                    handlerByBestMatchList.Key.IgnoreNonAlphanumericCharacters);

                if (match?.Score > bestMatchedScore)
                {
                    bestMatchedScore = match.Score;
                    handler = handlerByBestMatchList.Value;
                }
            }

            if (handler != null)
            {
                await handler(context, messageText);
            }
            else
            {
                await NoMatchHandler(context, messageText);
            }
        }

        protected virtual Task<string> GetBestMatchQueryTextAsync(IDialogContext context, IMessageActivity message)
        {
            return Task.FromResult(message.Text);
        }

        protected virtual IDictionary<BestMatchAttribute, BestMatchHandler> GetHandlersByBestMatchLists()
        {
            return EnumerateHandlers(this).ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        private static StringMatch FindBestMatch(IEnumerable<string> choices, string utterance, double threshold = 0.5, bool ignoreCase = true, bool ignoreNonAlphanumeric = true)
        {
            StringMatch bestMatch = null;
            var matches = FindAllMatches(choices, utterance, threshold, ignoreCase, ignoreNonAlphanumeric);
            foreach (var match in matches)
            {
                if (bestMatch == null || match.Score > bestMatch.Score)
                {
                    bestMatch = match;
                }
            }
            return bestMatch;
        }

        private static IEnumerable<StringMatch> FindAllMatches(IEnumerable<string> choices, string utterance, double threshold = 0.6, bool ignoreCase = true, bool ignoreNonAlphanumeric = true)
        {
            var matches = new List<StringMatch>();

            var choicesList = choices as IList<string> ?? choices.ToList();

            if (!choicesList.Any())
                return matches;

            var utteranceToCheck = ignoreNonAlphanumeric 
                ? utterance.ReplaceAll(@"[^A-Za-z0-9 ]", string.Empty).Trim() 
                : utterance;

            var tokens = utterance.Split(' ');

            foreach (var choice in choicesList)
            {
                double score = 0;
                var choiceValue = choice.Trim();
                if (ignoreNonAlphanumeric)
                    choiceValue.ReplaceAll(@"[^A-Za-z0-9 ]", string.Empty); 

                if (choiceValue.IndexOf(utteranceToCheck, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) >= 0)
                {
                    score = utteranceToCheck.Length / choiceValue.Length;
                }
                else if (utteranceToCheck.IndexOf(choiceValue) >= 0)
                {
                    score = Math.Min(0.5 + (choiceValue.Length / utteranceToCheck.Length), 0.9);
                }
                else
                {
                    foreach (var token in tokens)
                    {
                        var matched = string.Empty;

                        if (choiceValue.IndexOf(token, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) >= 0)
                        {
                            matched += token;
                        }

                        score = matched.Length / choiceValue.Length;
                    }
                }

                if (score >= threshold)
                {
                    matches.Add(new StringMatch { Choice = choiceValue, Score = score });
                }
            }

            return matches;
        }

        internal static IEnumerable<KeyValuePair<BestMatchAttribute, BestMatchHandler>> EnumerateHandlers(object dialog)
        {
            var type = dialog.GetType();
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (var method in methods)
            {
                var bestMatchListAttributes = method.GetCustomAttributes<BestMatchAttribute>(inherit: true).ToArray();
                Delegate created = null;
                try
                {
                    created = Delegate.CreateDelegate(typeof(BestMatchHandler), dialog, method, throwOnBindFailure: false);
                }
                catch (ArgumentException)
                {
                    // "Cannot bind to the target method because its signature or security transparency is not compatible with that of the delegate type."
                    // https://github.com/Microsoft/BotBuilder/issues/634
                    // https://github.com/Microsoft/BotBuilder/issues/435
                }

                var bestMatchHandler = (BestMatchHandler)created;
                if (bestMatchHandler != null)
                {
                    foreach (var bestMatchListAttribute in bestMatchListAttributes)
                    {
                        if (bestMatchListAttribute != null && bestMatchListAttributes.Any())
                            yield return new KeyValuePair<BestMatchAttribute, BestMatchHandler>(bestMatchListAttribute, bestMatchHandler);
                    }
                }
            }
        }

        public virtual async Task NoMatchHandler(IDialogContext context, string messageText)
        {
            throw new Exception("No best match found and NoMatchHandler method not overridden");
        }

        internal class StringMatch
        {
            public string Choice { get; set; }
            public double Score { get; set; }
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class BestMatchAttribute : Attribute
    {
        public readonly string[] BestMatchList;
        public readonly bool IgnoreCase;
        public readonly bool IgnoreNonAlphanumericCharacters;
        public readonly double Threshold;

        public BestMatchAttribute(string[] bestMatchList, double threshold = 0.5, bool ignoreCase = true, bool ignoreNonAlphaNumericCharacters = true)
        {
            BestMatchList = bestMatchList;
            IgnoreCase = ignoreCase;
            IgnoreNonAlphanumericCharacters = ignoreNonAlphaNumericCharacters;
            Threshold = threshold;
        }
    }

    public delegate Task BestMatchHandler(IDialogContext context, string messageText);
}