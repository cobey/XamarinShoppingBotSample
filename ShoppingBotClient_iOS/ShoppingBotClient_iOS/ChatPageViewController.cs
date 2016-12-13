using System;
using UIKit;
using Foundation;
using JSQMessagesViewController;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Text;
using System.Collections;

namespace XamarinChat
{
    public class User
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
    }

    public class ChatPageViewController : MessagesViewController
    {
		
        MessagesBubbleImage outgoingBubbleImageData, incomingBubbleImageData;
        List<Message> messages = new List<Message>();
        int messageCount = 0;
        private HttpClient _client;
        private Conversation _lastConversation;
        string DirectLineKey = "[DIRECT LINE KEY]";

		//Tracking of which user said what
        User sender = new User { Id = "2CC8343", DisplayName = "You" };
        User friend = new User { Id = "BADB229", DisplayName = "Xamarin Bot" };

		//Holds the entire message history for a given session
        MessageSet ms = new MessageSet();


        public override async void ViewDidLoad()
        {
            base.ViewDidLoad();

			//CollectionView.BackgroundColor = new UIColor(red:0.39f, green:0.33f, blue:0.50f, alpha:1.0f);
			CollectionView.BackgroundColor = new UIColor(red: 0.04f, green: 0.11f, blue: 0.32f, alpha: 1.0f);
            Title = "Xamarin Shopping Bot";

			//instantiate an HTTPClient, and set properties to our DirectLine bot
            _client = new HttpClient();
            _client.BaseAddress = new Uri("https://directline.botframework.com/api/conversations/");
            _client.DefaultRequestHeaders.Accept.Clear();
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("BotConnector",
                DirectLineKey);
            var response = await _client.GetAsync("/api/tokens/");
            if (response.IsSuccessStatusCode)
            {
                var conversation = new Conversation();
                HttpContent contentPost = new StringContent(JsonConvert.SerializeObject(conversation), Encoding.UTF8,
                    "application/json");
                response = await _client.PostAsync("/api/conversations/", contentPost);
                if (response.IsSuccessStatusCode)
                {
                    var conversationInfo = await response.Content.ReadAsStringAsync();
                    _lastConversation = JsonConvert.DeserializeObject<Conversation>(conversationInfo);
                }

            }

            // You must set your senderId and display name
            SenderId = sender.Id;
            SenderDisplayName = sender.DisplayName;

            // These MessagesBubbleImages will be used in the GetMessageBubbleImageData override
            var bubbleFactory = new MessagesBubbleImageFactory();
            outgoingBubbleImageData = bubbleFactory.CreateOutgoingMessagesBubbleImage(UIColorExtensions.MessageBubbleLightGrayColor);
			//incomingBubbleImageData = bubbleFactory.CreateIncomingMessagesBubbleImage(new UIColor(red: 0.31f, green: 0.00f, blue: 0.28f, alpha: 1.0f));
			incomingBubbleImageData = bubbleFactory.CreateIncomingMessagesBubbleImage(new UIColor(red: 0.51f, green: 0.18f, blue: 0.51f, alpha: 1.0f));
            // Remove the AccessoryButton as we will not be sending pics
            InputToolbar.ContentView.LeftBarButtonItem = null;


            // Remove the Avatars
            CollectionView.CollectionViewLayout.IncomingAvatarViewSize = CoreGraphics.CGSize.Empty;
            CollectionView.CollectionViewLayout.OutgoingAvatarViewSize = CoreGraphics.CGSize.Empty;

            // Load some messagees to start
            messages.Add(new Message(friend.Id, friend.DisplayName, NSDate.DistantPast, "Welcome to the Xamarin Shop! How may I help you?"));
            FinishReceivingMessage(true);
        }

        public override UICollectionViewCell GetCell(UICollectionView collectionView, NSIndexPath indexPath)
        {
            var cell = base.GetCell(collectionView, indexPath) as MessagesCollectionViewCell;

            // Override GetCell to make modifications to the cell
            // In this case darken the text for the sender
            var message = messages[indexPath.Row];
            if (message.SenderId == SenderId)
                cell.TextView.TextColor = UIColor.Black;

            return cell;
        }

        public override nint GetItemsCount(UICollectionView collectionView, nint section)
        {
            return messages.Count;
        }

        public override IMessageData GetMessageData(MessagesCollectionView collectionView, NSIndexPath indexPath)
        {
            return messages[indexPath.Row];
        }

        public override IMessageBubbleImageDataSource GetMessageBubbleImageData(MessagesCollectionView collectionView, NSIndexPath indexPath)
        {
            var message = messages[indexPath.Row];
            if (message.SenderId == SenderId)
                return outgoingBubbleImageData;
            return incomingBubbleImageData;

        }

        public override IMessageAvatarImageDataSource GetAvatarImageData(MessagesCollectionView collectionView, NSIndexPath indexPath)
        {
            return null;
        }

        public override async void PressedSendButton(UIButton button, string text, string senderId, string senderDisplayName, NSDate date)
        {
			//Clear the text and play a send sound
            InputToolbar.ContentView.TextView.Text = "";
            InputToolbar.ContentView.RightBarButtonItem.Enabled = false;
            SystemSoundPlayer.PlayMessageSentSound();

			//set message details and add to the message queue
            var message = new Message("2CC8343", "You", NSDate.Now, text);
            messages.Add(message);
            FinishReceivingMessage(true);

			//Show typing indicator to add to the natual feel of the bot
            ShowTypingIndicator = true;

			//send message to bot and await the message set
            ms = await SendMessage(text);

			//iterate through our message set, and print new messasges from the bot
            while (ms.messages.Length > messageCount)
            {
                if (ms.messages[messageCount].from == "XamarinBot")
                {

                    ScrollToBottom(true);

                    SystemSoundPlayer.PlayMessageReceivedSound();

                    var messageBot = new Message(friend.Id, friend.DisplayName, NSDate.Now, ms.messages[messageCount].text);
                    messages.Add(messageBot);

                    FinishReceivingMessage(true);
                    InputToolbar.ContentView.RightBarButtonItem.Enabled = true;

                }

                messageCount++;

            }
        }


        public async Task<MessageSet> SendMessage(string messageText)
        {
            try
            {

                var messageToSend = new BotMessage() { text = messageText, conversationId = _lastConversation.conversationId };
                var contentPost = new StringContent(JsonConvert.SerializeObject(messageToSend), Encoding.UTF8, "application/json");
                var conversationUrl = "https://directline.botframework.com/api/conversations/" + _lastConversation.conversationId + "/messages/";


                var response = await _client.PostAsync(conversationUrl, contentPost);
                var messageInfo = await response.Content.ReadAsStringAsync();

                var messagesReceived = await _client.GetAsync(conversationUrl);
                var messagesReceivedData = await messagesReceived.Content.ReadAsStringAsync();

                var messages = JsonConvert.DeserializeObject<MessageSet>(messagesReceivedData);

                return messages;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

    }
    public class MessageSet
    {

        public BotMessage[] messages { get; set; }
        public string watermark { get; set; }
        public string eTag { get; set; }

    }

    public class BotMessage
    {
        public string id { get; set; }
        public string conversationId { get; set; }
        public DateTime created { get; set; }
        public string from { get; set; }
        public string text { get; set; }
        public string channelData { get; set; }
        public string[] images { get; set; }
        public Attachment[] attachments { get; set; }
        public string eTag { get; set; }
    }

    public class Attachment
    {
        public string url { get; set; }
        public string contentType { get; set; }
    }

    public class Conversation
    {
        public string conversationId { get; set; }
        public string token { get; set; }
        public string eTag { get; set; }
    }

}

