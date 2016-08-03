using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Chronic;
using Microsoft.Bot.Builder.Luis.Models;

namespace BotFrameworkHelpers
{
    public class EntityRecognizer
    {
        public static EntityRecommendation FindEntity(IEnumerable<EntityRecommendation> entities, string entityType)
        {
            return entities.FirstOrDefault(e => e.Type == entityType);
        }

        public static IList<EntityRecommendation> FindEntities(IEnumerable<EntityRecommendation> entities, string entityType)
        {
            return entities.Where(e => e.Type == entityType).ToList();
        }

        public static void ParseDateTime(IEnumerable<EntityRecommendation> entities, out DateTime? parsedDate)
        {
            parsedDate = ResolveDateTime(entities);
        }

        public static void ParseDateTime(string utterance, out DateTime? parsedDate)
        {
            parsedDate = RecognizeTime(utterance);
        }

        public static void ParseNumber(string utterance, out double parsedNumber)
        {
            Regex numbeRegex = new Regex(@"[+-]?(?:\d+\.?\d*|\d*\.?\d+)");
            var matches = numbeRegex.Matches(utterance);

            if (matches.Count > 0)
            {
                double.TryParse(matches[0].Value, out parsedNumber);
                if (!double.IsNaN(parsedNumber))
                    return;
            }

            parsedNumber = double.NaN;
        }

        public static void ParseNumber(IEnumerable<EntityRecommendation> entities, out double parsedNumber)
        {
            var numberEntities = FindEntities(entities, "builtin.number");

            if (numberEntities != null && numberEntities.Any())
            {
                double.TryParse(numberEntities.First().Entity, out parsedNumber);
                if (!double.IsNaN(parsedNumber))
                    return;
            }

            parsedNumber = double.NaN;
        }

        public static void ParseBoolean(string utterance, out bool? parsedBoolean)
        {
            var boolTrueRegex = new Regex("(?i)^(1|y|yes|yep|sure|ok|true)");
            var boolFalseRegex = new Regex("(?i)^(2|n|no|nope|not|false)");

            var trueMatches = boolTrueRegex.Matches(utterance);
            if (trueMatches.Count > 0)
            {
                parsedBoolean = true;
                return;
            }

            var falseMatches = boolFalseRegex.Matches(utterance);
            if (falseMatches.Count > 0)
            {
                parsedBoolean = true;
                return;
            }

            parsedBoolean = null;
        }

        public static string FindBestMatch(IEnumerable<string> choices, string utterance, double threshold = 0.5, bool ignoreCase = true, bool ignoreNonAlphanumeric = true)
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

            return bestMatch?.Choice;
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

        public static string FindBestMatch(string choices, string utterance, double threshold = 0.5,
            bool ignoreCase = true, bool ignoreNonAlphanumeric = true, string choiceListSeperator = "|")
        {
            var choicesList = ExpandChoices(choices);

            return FindBestMatch(choicesList, utterance, threshold, ignoreCase, ignoreNonAlphanumeric);
        }

        private static IEnumerable<string> ExpandChoices(string choices, char seperator = '|')
        {
            if (string.IsNullOrEmpty(choices))
                return new List<string>();

            return choices.Split(seperator).ToList();
        }

        private static DateTime? ResolveDateTime(IEnumerable<EntityRecommendation> entities)
        {
            DateTime? date = null;
            DateTime? time = null;

            foreach (var entity in entities)
            {
                if (entity.Type.Contains("builtin.datetime") && entity.Resolution.Any())
                {
                    switch (entity.Resolution.First().Key)
                    {
                        case "date":
                            var dateTimeParts = entity.Resolution.First().Value.Split('T');
                            date = ParseLuisDateString(dateTimeParts[0]);

                            if (date.HasValue)
                            {
                                if (!string.IsNullOrEmpty(dateTimeParts[1]))
                                    time = ParseLuisTimeString(dateTimeParts[1]);
                            }
                            break;
                        case "time":
                            time = ParseLuisTimeString(entity.Resolution.First().Key);
                            break;
                    }
                }

                if (date.HasValue)
                {
                    if (time.HasValue)
                    {
                        return date.Value.Date + time.Value.TimeOfDay;
                    }

                    return date.Value;
                }

                if (time.HasValue)
                {
                    return DateTime.Now + time.Value.TimeOfDay;
                }
            }

            return null;
        }

        private static DateTime? ParseLuisDateString(string value)
        {
            int year;
            int month;
            int weekNumber;
            int day;

            string[] dateParts = value.Split('-');

            if (dateParts[0] != "XXXX")
            {
                year = Convert.ToInt16(dateParts[0]);
            }
            else
            {
                year = DateTime.Now.Year;
            }

            if (dateParts[1].Contains("W"))
            {
                if (!dateParts[1].Contains("XX"))
                {
                    weekNumber = Convert.ToInt16(dateParts[1].Substring(1, dateParts[1].Length - 1));
                    return FirstDateOfWeekIso8601(year, weekNumber);
                }
                else
                {
                    month = DateTime.Now.Month;
                }
            }
            else
            {
                month = Convert.ToInt16(dateParts[1]);
            }

            if (dateParts[2] != null && dateParts[2] != "XX")
            {
                day = Convert.ToInt16(dateParts[2]);
            }
            else
            {
                day = 1;
            }

            var dateString = string.Format("{0}-{1}-{2}", year, month, day);
            return DateTime.Parse(dateString);
        }

        private static DateTime? ParseLuisTimeString(string value)
        {
            switch (value)
            {
                case "MO":
                    return DateTime.MinValue.AddHours(10);
                case "MI":
                    return DateTime.MinValue.AddHours(12);
                case "AF":
                    return DateTime.MinValue.AddHours(15);
                case "EV":
                    return DateTime.MinValue.AddHours(18);
                case "NI":
                    return DateTime.MinValue.AddHours(20);
                default:
                    var timeParts = value.Split(':');
                    int hours = 0;
                    int minutes = 0;

                    if (timeParts[0] != null)
                    {
                        int.TryParse(timeParts[0], out hours);
                    }

                    if (timeParts[1] != null)
                    {
                        int.TryParse(timeParts[1], out minutes);
                    }

                    DateTime returnDate = DateTime.MinValue;

                    if (hours > 0)
                        returnDate = returnDate.AddHours(hours);

                    if (minutes > 0)
                        returnDate = returnDate.AddMinutes(minutes);

                    return returnDate;
            }
        }

        private static DateTime? RecognizeTime(string utterance)
        {
            try
            {
                var parser = new Chronic.Parser();
                if (!string.IsNullOrEmpty(utterance))
                {
                    Span parsedObj = parser.Parse(utterance);
                    DateTime? parsedDateTime = parsedObj?.Start;
                    return parsedDateTime;
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static DateTime FirstDateOfWeekIso8601(int year, int weekOfYear)
        {
            DateTime jan1 = new DateTime(year, 1, 1);
            int daysOffset = DayOfWeek.Thursday - jan1.DayOfWeek;

            DateTime firstThursday = jan1.AddDays(daysOffset);
            var cal = CultureInfo.CurrentCulture.Calendar;
            int firstWeek = cal.GetWeekOfYear(firstThursday, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);

            var weekNum = weekOfYear;
            if (firstWeek <= 1)
            {
                weekNum -= 1;
            }
            var result = firstThursday.AddDays(weekNum * 7);
            return result.AddDays(-3);
        }
    }

    internal class StringMatch
    {
        public string Choice { get; set; }
        public double Score { get; set; }
    }
}
