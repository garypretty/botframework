using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Bot.Builder.Dialogs;

namespace BestMatchDialog.Sample.Dialogs
{
    [Serializable]
    public class CommonResponsesDialog : BestMatchDialog<object>
    {
        [BestMatch(new string[] { "Hi", "Hi There", "Hello there", "Hey", "Hello",
            "Hey there", "Greetings", "Good morning", "Good afternoon", "Good evening", "Good day" },
            threshold: 0.5, ignoreCase: false, ignoreNonAlphaNumericCharacters: false)]
        public async Task HandleGreeting(IDialogContext context)
        {
            await context.PostAsync("Well hello there. What can I do for you today?");
            context.Wait(MessageReceived);
        }

        [BestMatch(new string[] { "how goes it", "how do", "hows it going", "how are you",
            "how do you feel", "whats up", "sup", "hows things" })]
        public async Task HandleStatusRequest(IDialogContext context)
        {
            await context.PostAsync("I am great.");
            context.Wait(MessageReceived);
        }

        [BestMatch(new string[] { "bye", "bye bye", "got to go",
            "see you later", "laters", "adios" })]
        public async Task HandleGoodbye(IDialogContext context)
        {
            await context.PostAsync("Bye. Looking forward to our next awesome conversation already.");
            context.Wait(MessageReceived);
        }

        [BestMatch(new string[] { "thank you", "thanks", "much appreciated", "thanks very much", "thanking you" })]
        public async Task HandleThanks(IDialogContext context)
        {
            await context.PostAsync("You're welcome.");
            context.Wait(MessageReceived);
        }

        public override async Task NoMatchHandler(IDialogContext context)
        {
            await context.PostAsync("I’m not sure what you want.");
            context.Wait(MessageReceived);
        }
    }
}