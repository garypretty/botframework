# Additional Bot Framework dialogs

This project is intended to become a selection of additional dialogs, extensions and more for the Bot Framework.  Right now there is a single additional Dialog, the BestMatch Dialog.

## BestMatchDialog

The BestMatch dialog allows you to take the incoming message text from the bot and match it against 1 or more lists of strings. e.g. "hi", "hey there", "hello there".  The dialog will take the incoming message and find the Best Match in the list of strings, according the a threshold that you set (0.5 by default). For example, if the incoming message was "hello", it would match be a match (matched with "hello there"), but "greetings" would not match at all.

Each list of strings to match against is paired with a method handler so that you can repsond appropriately based on the incoming message. As well as being able to set the matching threshold, you can also choose if matching should ignore case (case ignored by default) and if non alphanumeric characters should be ignored (also ignored by default).

For when no match is found you can override the NoMatchFound method to send an appropriate response to the user.

A sample project using the BestMatchDialog is included in the repository.

Below is an example of a class inheriting from BestMatchDialog:

```cs

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

        [BestMatch(new string[] { "bye", "bye bye", "got to go",
            "see you later", "laters", "adios" })]
        public async Task HandleGoodbye(IDialogContext context)
        {
            await context.PostAsync("Bye. Looking forward to our next awesome conversation already.");
            context.Wait(MessageReceived);
        }

        public override async Task NoMatchHandler(IDialogContext context)
        {
            await context.PostAsync("I’m not sure what you want.");
            context.Wait(MessageReceived);
        }

```

You can use a BestMatch Dialog as your root dialog, but you can also use it as a child dialog, to handle common responses for example.  When calling as a child dialog, instead of calling content.Wait, you can call context.Done to pass back to the parent dialog. An example of the class implemented as a child dialog, plus some code calling a BestMatch dialog from a LUIS child dialog is shown below. NOTE: I am using the NoMatchFound override here to set the result of the dialog to false and then I check the result and act appropriately in the resume method ran when the dialog returns.


```cs

    [Serializable]
    public class CommonResponsesDialog : BestMatchDialog<object>
    {
        [BestMatch(new string[] { "Hi", "Hi There", "Hello there", "Hey", "Hello",
            "Hey there", "Greetings", "Good morning", "Good afternoon", "Good evening", "Good day" },
            threshold: 0.5, ignoreCase: false, ignoreNonAlphaNumericCharacters: false)]
        public async Task HandleGreeting(IDialogContext context)
        {
            await context.PostAsync("Well hello there. What can I do for you today?");
            context.Done(true);
        }

        [BestMatch(new string[] { "bye", "bye bye", "got to go",
            "see you later", "laters", "adios" })]
        public async Task HandleGoodbye(IDialogContext context)
        {
            await context.PostAsync("Bye. Looking forward to our next awesome conversation already.");
            context.Done(true);
        }
        
        public override async Task NoMatchHandler(IDialogContext context)
        {
            context.Done(false);
        }

```

and to call the dialog (here it is being called from a LUIS dialog)....

```cs
        [LuisIntent("")]
        public async Task None(IDialogContext context, LuisResult result)
        {
            var dialog = new CommonResponsesDialog(result.Query);
            context.Call(dialog, AfterCommonResponseHandled);
        }

        private async Task AfterCommonResponseHandled(IDialogContext context, IAwaitable<bool> result)
        {
            var messageHandled = await result;

            if (!messageHandled)
            {
                await context.PostAsync("I’m not sure what you want");
            }

            context.Wait(MessageReceived);
        }
```