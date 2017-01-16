## QnAMakerDialog

NuGet package: https://www.nuget.org/packages/QnAMakerDialog/

The QnAMakerDialog allows you to easily integrate a bot built on the Bot Framework with the QnA Maker Service, part of the Microsoft Cognitive Services suite.

The QnAMakerDialog allows you to take the incoming message text from the bot, send it to your published QnA Maker service and send the answer sent back from the service to the bot user as a reply.

The most straightforward implementation is to simply create a new QnAMakerDialog and specify your QnA Maker subscription key and knowledgebase ID (provided to you when you publish your service at QnAMaker.ai).
Once you have done this messages will be sent to the QnA service and the answers recieved from the service will be sent to the user.

For when no match is found a default message, "Sorry, I cannot find an answer to your question." is sent to the user. You can override the NoMatchHandler method to send a customised reponse.

You can also provide more granular responses for when the QnA Maker returns an answer, but is not confident in the answer (indicated using the score returned in the response between 0 and 100 with the higher the score indicating higher confidence).
To do this you define a custom hanlder in your dialog and decorate it with a QnAMakerResponseHandler attribute, specifying the maximum score that the handler should respond to.

A sample project using the QnAMakerDialog is included in the repository.

Below is an example of a class inheriting from QnAMakerDialog and the minimal implementation:

```cs

    [Serializable]
    [QnAMakerService("YOUR_SUBSCRIPTION_KEY", "YOUR_KNOWLEDGE_BASE_ID")]
    public class QnADialog : QnAMakerDialog<object>
    {
    }

```

Below is an example with a customised method for when a match is not found and also a hanlder for when the QnA Maker service indicates a lower confidence in the match (using the score sent back in the QnA Maker service response).
In this case the custom handler will respond to answers where the confidence score is below 50, with any obove 50 being hanlded in the default way.

```cs

   [Serializable]
    [QnAMakerService("YOUR_SUBSCRIPTION_KEY", "YOUR_KNOWLEDGE_BASE_ID")]
    public class QnADialog : QnAMakerDialog<object>
    {
        public override async Task NoMatchHandler(IDialogContext context, string originalQueryText)
        {
            await context.PostAsync($"Sorry, I couldn't find an answer for '{originalQueryText}'.");
            context.Wait(MessageReceived);
        }

        [QnAMakerResponseHandler(50)]
        public async Task LowScoreHandler(IDialogContext context, string originalQueryText, QnAMakerResult result)
        {
            await context.PostAsync($"I found an answer that might help...{result.Answer}.");
            context.Wait(MessageReceived);
        }
    }

```