using Microsoft.Bot.Connector.DirectLine;
using Microsoft.Bot.Connector.DirectLine.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Media.SpeechRecognition;
using Windows.Media.SpeechSynthesis;
using Windows.System.UserProfile;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace BotChat
{
    public sealed partial class MainPage : Page
    {
        SpeechRecognizer recognizer;

        private static string directLineSecret = "pnGGXAHPZR0.cwA.7Tg.Z_JHCp5GuT_8-w7YHeyv-hZEr5DD-P-zeMgVVfqxPoM";
        private static string botId = "MandoFridgeBotPOC";
        private static string fromUser = "DirectLineSampleClientUser";

        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += OnLoaded;
        }

        async void OnLoaded(object sender, RoutedEventArgs args)
        {
            StartBotConversation();
        }

        private async Task StartBotConversation()
        {
            var client = new DirectLineClient(directLineSecret);
            var conversation = await client.Conversations.NewConversationAsync();

            var topUserLanguage = GlobalizationPreferences.Languages[0];
            var language = new Language(topUserLanguage);
            recognizer = new SpeechRecognizer(language);
            await recognizer.CompileConstraintsAsync();

            string watermark = null;

            while (true)
            {
                recognizer.Timeouts.InitialSilenceTimeout = TimeSpan.FromDays(1);

                var result = await this.recognizer.RecognizeAsync();

                if (result != null && !string.IsNullOrEmpty(result.Text))
                {
                    Message message = new Message
                    {
                        FromProperty = fromUser,
                        Text = result.Text
                    };

                    await client.Conversations.PostMessageAsync(conversation.ConversationId, message);

                    watermark = await ReadBotMessagesAsync(client, conversation.ConversationId, watermark);
                }
            }
        }

        private async Task<string> ReadBotMessagesAsync(DirectLineClient client, string conversationId, string watermark)
        {
            bool messageReceived = false;

            while (!messageReceived)
            {
                var messages = await client.Conversations.GetMessagesAsync(conversationId, watermark);
                watermark = messages?.Watermark;

                var messagesFromBotText = from x in messages.Messages
                                          where x.FromProperty == botId
                                          select x;

                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                async () =>
                {
                    foreach (Message message in messagesFromBotText)
                    {

                        await SynthesiseTextAsync(message.Text);
                    }

                    messageReceived = true;
                });

                var playMediaTask = new TaskCompletionSource<bool>();
                mediaElement.MediaEnded += (o, e) =>
                {
                    playMediaTask.TrySetResult(true);
                };
                await playMediaTask.Task;
            }

            return watermark;
        }

        async Task SynthesiseTextAsync(string text)
        {
            using (var speech = new SpeechSynthesizer())
            {
                speech.Voice = SpeechSynthesizer.AllVoices.First(gender => gender.Gender == VoiceGender.Male);
                string ssml = @"<speak version='1.0' " + "xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-GB'><prosody rate=\"medium\">" + text + "</prosody></speak>";
                SpeechSynthesisStream stream = await speech.SynthesizeSsmlToStreamAsync(ssml);
                mediaElement.SetSource(stream, stream.ContentType);
                mediaElement.Play();
            }
        }
    }
}
