## QnAMakerDialog (updated to work with QnA Maker v3 API)

NuGet package: https://www.nuget.org/packages/QnAMakerDialog/
QnA Maker v3 API: https://westus.dev.cognitive.microsoft.com/docs/services/597029932bcd590e74b648fb/operations/597037798a8bb5031800bf5b 

The QnAMakerDialog allows you to easily integrate a bot built on the Bot Framework with the QnA Maker Service, part of the Microsoft Cognitive Services suite.

The QnAMakerDialog allows you to take the incoming message text from the bot, send it to your published QnA Maker service, get the top answers from your knowledgebase and send the top answer sent back from the service to the bot user as a reply automatically, or create your own handlers if you want more control - such as presenting the user with more than one of the answers found.

The latest version of QnAMakerDialog now works with v3 of the QnA Maker API, allowing you to get multiple answers back from the service and use metadata to filter or boost answers within your knowledgebase. 

The most straightforward implementation is to simply create a new QnAMakerDialog and specify your QnA Maker subscription key and knowledgebase ID (provided to you when you publish your service at QnAMaker.ai).
Once you have done this messages will be sent to the QnA service and the top answers recieved from the service will be sent to the user.

For when no match is found a default message, "Sorry, I cannot find an answer to your question." is sent to the user. 

Other features;

* Override the default answer and no answer handlers (see code samples below)
* Handling different confidence levels returned from the QnAMaker service (see code samples below)
* Change the number of answers you get back from the service (see code samples below)
* Use metadata to filter or boost answers returned by the service (see code samples below)
* Adding rich attachments such as video or files to your answers (see explanation and code samples further down)
* Using the dialog as a root dialog or forwarding the message and calling the dialog as a child dialog

Implementation details and code samples for each can be found below.

A sample project using the QnAMakerDialog is included in the repository.

### Minimal implementation

Below is an example of a class inheriting from QnAMakerDialog and the minimal implementation when it is used as the root dialog within a bot:

```cs

    [Serializable]
    [QnAMakerService("YOUR_SUBSCRIPTION_KEY", "YOUR_KNOWLEDGE_BASE_ID")]
    public class QnADialog : QnAMakerDialog<object>
    {
    }

```

### Example usage as the root dialog with handlers for different confidence levels (use as a child dialog below)

You can also provide more granular responses for when the QnA Maker returns at least 1 answer, but the confidence for the top answer is lower (indicated using the score returned in the response between 0 and 100 with the higher the score indicating higher confidence).
To do this you define a custom hanlder in your dialog and decorate it with a QnAMakerResponseHandler attribute, specifying the maximum score that the handler should respond to. 

Below is an example with a customised method for when a match is not found and also a hanlder for when the QnA Maker service indicates a lower confidence in the match (using the score sent back in the QnA Maker service response). In this case the custom handler will respond to answers where the confidence score is below 50, with any obove 50 being hanlded in the default way.

```cs

   [Serializable]
    [QnAMakerService("YOUR_SUBSCRIPTION_KEY", "YOUR_KNOWLEDGE_BASE_ID", MaxAnswers = 10)]
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
            messageActivity.Text = $"I found {result.Answers.Length} answer(s) that might help..."
            messageActivity.Text += "here is the first, which returned a score of {result.Answers.First().Score}...{result.Answers.First().Answer}";
            
            context.Wait(MessageReceived);
        }
    }

```

### Altering the maximum number of answers returned by the QnA Maker Service

By default the dialog will request a maximum of 3 answers, returned as an array of answers on the QnAMakerResult object, when you query the QnAMaker Service.  You can override this number and set it to another value by passing an additional parameter to the QnAMakerService attribute, as shown below.

```cs

    [Serializable]
    [QnAMakerService("YOUR_SUBSCRIPTION_KEY", "YOUR_KNOWLEDGE_BASE_ID", MaxAnswers = 10)]
    public class QnADialog : QnAMakerDialog<object>
    {
    }

```

### Example of forwarding a message to the QnAMakerDialog as a child dialog

Below is an example of using a QnAMakerDialog as a child dialog. For example, falling back to QnAMakerDialog from a LUIS dialog if no intents match.
In this case you must override the DefaultMatchHandler and call context.Done to pass control back to the parent dialog. You need to do this in all of 
your other handlers too, including NoMatchHandler.

```cs

   [Serializable]
    [QnAMakerService("YOUR_SUBSCRIPTION_KEY", "YOUR_KNOWLEDGE_BASE_ID")]
    public class QnADialog : QnAMakerDialog<true>
    {
        public override async Task NoMatchHandler(IDialogContext context, string originalQueryText)
        {
            await context.PostAsync($"Sorry, I couldn't find an answer for '{originalQueryText}'.");
            context.Done(false);
        }

        public override async Task DefaultMatchHandler(IDialogContext context, string originalQueryText, QnAMakerResult result)
        {
            await context.PostAsync($"I found {result.Answers.Length} answer(s) that might help...{result.Answers.First().Answer}.");
            context.Done(true);
        }
    }

```

Then in your root dialog, in this case a LUIS dialog, you can forward the incoming message to the QnAMakerDialog using context.Forward.
Then in your callback you can determine if an answer was found.

```cs

        [LuisIntent("")]
        public async Task None(IDialogContext context, IAwaitable<IMessageActivity> message, LuisResult result)
        {
            var qnadialog = new QnADialog();
            var messageToForward = await message;
            await context.Forward(qnadialog, AfterQnADialog, messageToForward, CancellationToken.None);
        }

        private async Task AfterQnADialog(IDialogContext context, IAwaitable<bool> result)
        {
            var answerFound = await result;

            // we might want to send a message or take some action if no answer was found (false returned)
            if (!answerFound)
            {
                await context.PostAsync("I’m not sure what you want.");
            }

            context.Wait(MessageReceived);
        }

```

## Boosting and filtering answers using metadata

In v3 of the QnA Maker API, you can add metadata to your QnA items in your knowledge base.  This metadata is just a collection of name / value pairs.  e.g. you might have a piece of metadata to denote a category for a QnA Item (Name, "Category", Value "Category 1").  This metadata can be used to either filter the answers you get back from the service (i.e. only get answers back that have specified name / value metadata pairs), or to boost answers with particular metadata.

*Note: Metadata on knowledgebase items cannot currently be added via the QnAMaker Portal. If you wish to add metadata to your knowledgebase items, you must use the v3 API to populate your knowledgebase or add the metadata manually. You can read more about the v3 API and how to specify metadata at https://westus.dev.cognitive.microsoft.com/docs/services/597029932bcd590e74b648fb/operations/597037798a8bb5031800bf5b.*

When using the QnAMakerDialog, you have the ability to set either filtering or boosting metadata, which will be passed as part of any queries to the QnA Maker Service.  One example of when you might want to do this is if you have many questions in your knowledgebase, but you would only like to check against questions related to a specific product or category.

Below is an example of how you can create your QnA Maker dialog and add a single name / value metadata pair to the MetadataFilter property on the dialog.  This will then cause the dialog to only receieve answers marked with the same name / value metadata.

```cs
        internal static IDialog<object> MakeRoot()
        {
            var qnaDialog = new QnADialog
            {
                MetadataFilter = new List<Metadata>()
                {
                    new Metadata()
                    {
                        Name = "Category",
                        Value = "Moving home"
                    }
                }
            };
            
            return qnaDialog;
        }
```

## Adding rich attachments to responses

QnAMakerDialog now supports adding rich attachments to responses, such as videos, images or files. To do this you need to add attachment markup to your QnA answers within the 
QnAMaker dashboard. For example, if you wanted to return a video with your answer you would add the following to the answer.

```xml
<attachment contentType="video/mp4" contentUrl="http://www.yourdomain.com/video.mp4" name="Your title" thumbnailUrl="http://www.yourdomain.com/thumbnail.png" />
```

By default the dialog will strip the answer of any attachment markup and then add the approproate attachments to the answers. However, if you want to handle attachments in your
own handlers you can use the helper method ProcessResultAndCreateMessageActivity which will generate a message activity with the answer and attachments ready to post back to the user.
 At this point you can override the answer text if you wish on the message activity, as shown below.

```cs

        public override async Task DefaultMatchHandler(IDialogContext context, string originalQueryText, QnAMakerResult result)
        {
            // ProcessResultAndCreateMessageActivity will remove any attachment markup from the results answer
            // and add any attachments to a new message activity with the message activity text set by default
            // to the answer property from the result
            var messageActivity = ProcessResultAndCreateMessageActivity(context, ref result);
            messageActivity.Text = $"I found an answer that might help...{result.Answers.First().Answer}.";

            await context.PostAsync(messageActivity);

            context.Wait(MessageReceived);
        }

````