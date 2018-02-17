using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using QnAMakerDialog.Models;

namespace QnAMakerDialog.Sample.Dialogs
{
    [Serializable]
    [QnAMakerService("<YOUR_SUBSCRIPTION_KEY>", "<YOUR_KNOWLEDGE_BASE_ID>", MaxAnswers = 10)]
    public class QnADialog : QnAMakerDialog<object>
    {
        /// <summary>
        /// Handler used when the QnAMaker finds no appropriate answer
        /// </summary>
        public override async Task NoMatchHandler(IDialogContext context, string originalQueryText)
        {
            await context.PostAsync($"Sorry, I couldn't find an answer for '{originalQueryText}'.");
            context.Wait(MessageReceived);
        }

        /// <summary>
        /// This is the default handler used if no specific applicable score handlers are found
        /// </summary>
        public override async Task DefaultMatchHandler(IDialogContext context, string originalQueryText, QnAMakerResult result)
        {
            // ProcessResultAndCreateMessageActivity will remove any attachment markup from the results answer
            // and add any attachments to a new message activity with the message activity text set by default
            // to the answer property from the result
            var messageActivity = ProcessResultAndCreateMessageActivity(context, ref result);
            messageActivity.Text = $"I found {result.Answers.Length} answer(s) that might help...here is the first, which returned a score of {result.Answers.First().Score}...{result.Answers.First().Answer}";

            await context.PostAsync(messageActivity);

            context.Wait(MessageReceived);
        }

        /// <summary>
        /// Handler to respond when QnAMakerResult score is a maximum of 0.5
        /// </summary>
        [QnAMakerResponseHandler(0.5)]
        public async Task LowScoreHandler(IDialogContext context, string originalQueryText, QnAMakerResult result)
        {
            var messageActivity = ProcessResultAndCreateMessageActivity(context, ref result);
            messageActivity.Text = $"I found an answer that might help...{result.Answers.First().Answer}.";
            await context.PostAsync(messageActivity);

            context.Wait(MessageReceived);
        }
    }
}