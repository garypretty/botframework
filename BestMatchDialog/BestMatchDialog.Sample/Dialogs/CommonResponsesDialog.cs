using System;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using BestMatchDialog.Sample.Properties;

namespace BestMatchDialog.Sample.Dialogs
{
    [Serializable]
    public class CommonResponsesDialog : BestMatchDialog<object>
    {
        [BestMatch(new string[] { "Hi", "Hi There", "Hello there", "Hey", "Hello",
            "Hey there", "Greetings", "Good morning", "Good afternoon", "Good evening", "Good day" },
            threshold: 0.5, ignoreCase: false, ignoreNonAlphaNumericCharacters: false)]
        public async Task HandleGreeting(IDialogContext context, string messageText)
        {
            await context.PostAsync("Well hello there. What can I do for you today?");
            context.Wait(MessageReceived);
        }

        [BestMatch(typeof(BestMatchDialogResources), nameof(BestMatchDialogResources.CommonResponsesDialog_StatusRequest))]
        public async Task HandleStatusRequest(IDialogContext context, string messageText)
        {
            await context.PostAsync("I am great.");
            context.Wait(MessageReceived);
        }

        [BestMatch(new string[] { "bye", "bye bye", "got to go",
            "see you later", "laters", "adios" })]
        public async Task HandleGoodbye(IDialogContext context, string messageText)
        {
            await context.PostAsync("Bye. Looking forward to our next awesome conversation already.");
            context.Wait(MessageReceived);
        }

        [BestMatch("thank you|thanks|much appreciated|thanks very much|thanking you", listDelimiter: '|')]
        public async Task HandleThanks(IDialogContext context, string messageText)
        {
            await context.PostAsync("You're welcome.");
            context.Wait(MessageReceived);
        }

        public override async Task NoMatchHandler(IDialogContext context, string messageText)
        {
            await context.PostAsync($"I’m not sure what you want when you say '{messageText}'.");
            context.Wait(MessageReceived);
        }
    }
}