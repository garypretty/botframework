# Additional Bot Framework dialogs

This project is intended to become a selection of additional dialogs, extensions and more for the Bot Framework.

## QnAMakerDialog

NuGet package: https://www.nuget.org/packages/QnAMakerDialog/

The QnAMakerDialog allows you to easily integrate a bot built on the Bot Framework with the QnA Maker Service, part of the Microsoft Cognitive Services suite.

Full details and further documentation -> [QnAMakerDialog](https://github.com/garypretty/botframework/tree/master/QnAMakerDialog) 

## BestMatchDialog

NuGet package: https://www.nuget.org/packages/BestMatchDialog/

The BestMatch dialog allows you to take the incoming message text from the bot and match it against 1 or more lists of strings. e.g. "hi", "hey there", "hello there".  The dialog will take the incoming message and find the Best Match in the list of strings, according the a threshold that you set (0.5 by default). For example, if the incoming message was "hello", it would match be a match (matched with "hello there"), but "greetings" would not match at all.

Full details and further documentation -> [BestMatchDialog](https://github.com/garypretty/botframework/tree/master/BestMatchDialog) 
