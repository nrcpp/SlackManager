using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SlackAPI;
using SlackAPI.WebSocketMessages;

namespace SlackPOC
{
    public class InSync : IDisposable
    {
        private readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(15);

        private readonly ManualResetEventSlim waiter;
        private readonly string message;

        public InSync([CallerMemberName] string message = null)
        {
            this.message = message;
            this.waiter = new ManualResetEventSlim();
        }

        public void Proceed()
        {
            this.waiter.Set();
        }

        public void Dispose()
        {
            if (!this.waiter.Wait(Debugger.IsAttached ? Timeout.InfiniteTimeSpan : this.WaitTimeout))
                Console.WriteLine($"Took too long to do '{this.message}'");
        }
    }

    public class ChannelMessage
    {
        public DateTime Time { get; set; }
        public string User { get; set; }
        public string Username { get; set; }
        public string Text { get; set; }
        public string ChannelId { get; }
        public bool IsStarred { get; set; }

        public string AsString { get; set; } = "";


        public ChannelMessage() { }
        public ChannelMessage(NewMessage message)
        {
            Time = message.ts;
            User = message.user;
            Username = message.username;
            Text = message.text;
            ChannelId = message.channel;
            IsStarred = false;
        }

        public ChannelMessage(SlackAPI.Message message)
        {
            Time = message.ts;
            User = message.user;
            Username = message.username;
            Text = message.text;
            ChannelId = message.channel;
            IsStarred = message.is_starred;
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(AsString))
                return AsString;
            
            return $"[{Time}]: @{User} ({Username}): {Text}";
        }
    }


    public class Utils
    {
        public static string GetUserById(List<User> Users, string userId, string userName)
        {
            if (string.IsNullOrEmpty(userName))
                userName = Users.FirstOrDefault(u => u.id == userId)?.name ?? userId;
            return userName;
        }


        public static string NormalizeMessage(List<User> Users, string text)
        {
            var dict = Users.ToDictionary(u => u.id, u => u.name);
            foreach (var item in dict)
            {
                text = text.Replace($"<@{item.Key}>", "@" + item.Value);
            }

            return text;
        }

        public static string AsStr(List<User> Users, dynamic m) => 
            $"[{m.ts}]: @{Utils.GetUserById(Users, m.user, m.username)}: {Utils.NormalizeMessage(Users, m.text)}";
    }


    public class SlackManager : SlackAPI.SlackTaskClient
    {
        private bool _isConnected;
        SlackSocketClient _socketClient;
        private List<ChannelMessage> _newMessages = new List<ChannelMessage>();


        public bool IsConnected => _isConnected;
        public string BotName { get; set; } = nameof(SlackManager);

        public event Action<ChannelMessage> OnNewMessage;


        public SlackManager(string token, string botName = nameof(SlackManager))
            : base(token)
        {
            BotName = botName;
            
            _socketClient = CreateClient(token);
            _socketClient.OnMessageReceived += msg =>
            {                
                var channelMsg = new ChannelMessage(msg)
                {
                    AsString = Utils.AsStr(_socketClient.Users, msg)
                };

                _newMessages.Add(channelMsg);
                OnNewMessage?.Invoke(channelMsg);
            };
        }


        #region Socket Client

        private SlackSocketClient CreateClient(string authToken, IWebProxy proxySettings = null)
        {
            SlackSocketClient client;

            LoginResponse loginResponse = null;
            using (var syncClient = new InSync($"{nameof(SlackClient.Connect)} - Connected callback"))
            using (var syncClientSocket = new InSync($"{nameof(SlackClient.Connect)} - SocketConnected callback"))
            using (var syncClientSocketHello = new InSync($"{nameof(SlackClient.Connect)} - SocketConnected hello callback"))
            {
                client = new SlackSocketClient(authToken);
                client.OnHello += () => syncClientSocketHello.Proceed();
                client.Connect(x =>
                {
                    loginResponse = x;

                    Console.WriteLine($"Connected {x.ok}");
                    syncClient.Proceed();
                    if (!x.ok)
                    {
                        // If connect fails, socket connect callback is not called
                        syncClientSocket.Proceed();
                        syncClientSocketHello.Proceed();
                    }
                }, () =>
                {
                    Console.WriteLine("Socket Connected");
                    syncClientSocket.Proceed();
                });
            }

            loginResponse.AssertOk();

            return client;
        }

        #endregion


        #region Helper Methods and Overrides

        private bool EnsureConnected()
        {
            if (_isConnected) return true;

            Connect();

            return _isConnected;
        }

        protected override void Connected(LoginResponse loginDetails)
        {
            base.Connected(loginDetails);

            _isConnected = loginDetails.ok;            
        }


        public static void Log(string msg, [CallerMemberName] string caller = null) =>        
            Console.WriteLine($"[{caller}]: {msg}");

        protected static bool Log(Response response, [CallerMemberName] string caller = null)
        {
            try
            {
                response.AssertOk();
                Log("OK", caller);
                return true;
            }
            catch (Exception ex)
            {
                Log(ex.Message, caller);
                return false;
            }
        }

        public List<string> GetChannelNames()
        {
            if (!EnsureConnected()) return new List<string>();

            return base.Channels.ConvertAll(ch => ch.name);
        }


        private Channel GetChannelByName(string channelName, [CallerMemberName] string caller = null)
        {
            if (!EnsureConnected()) return null;

            var channel = Channels.FirstOrDefault(ch => ch.name == channelName);
            if (channel == null)
                Log($"{channelName} - channel not found", caller);

            return channel;
        }


        #endregion


        #region Core Methods

        public void Connect()
        {
            var loginResponse = base.ConnectAsync().Result;
            _isConnected = loginResponse.ok;

            Log(loginResponse);
        }


        public void SendMessage(string channelName, string message)
        {
            var channel = GetChannelByName(channelName);
            if (channel == null) return;
            
            var response = base.PostMessageAsync(channelId: channel.id, text: message,
                                                     botName: BotName).Result;
            Log(response);            
        }


        public List<ChannelMessage> GetMessages(string channelName) => GetMessages(channelName, from: null, messageId: null);
        public List<ChannelMessage> GetMessages(string channelName, DateTime from) => GetMessages(channelName, from: from, messageId: null);
        public List<ChannelMessage> GetMessages(string channelName, long messageId) => GetMessages(channelName, from: null, messageId: messageId);

        public List<ChannelMessage> GetMessages(string channelName, DateTime? from, long? messageId)
        {
            var result = new List<ChannelMessage>();

            var channel = GetChannelByName(channelName);
            if (channel == null) return result;

            var history = base.GetChannelHistoryAsync(channel).Result;

            if (!Log(history)) return result;

            // just concat channel/user/text into one message
            var messages = history.messages.AsEnumerable();
            if (from != null)
                messages = messages.Where(m => m.ts >= from.Value);

            // NOTE: this filter does not work in SlackAPI
            // See https://api.slack.com/methods/channels.history
            // message.id always 0
            if (messageId != null)
                messages = messages.Where(m => m.id == messageId.Value);
            
            result = messages.Select(m => new ChannelMessage(m) { AsString = Utils.AsStr(Users, m) }).Reverse().ToList();

            return result;
        }

        public List<ChannelMessage> GetNewMessages(List<string> channels)
        {
            List<ChannelMessage> result = new List<ChannelMessage>();
            foreach (var channelName in channels)
            {
                var channelId = Channels.FirstOrDefault(c => c.name == channelName)?.id;
                if (channelId == null) continue;

                var newMsgs = _newMessages.Where(m => m.ChannelId == channelId);
                result.AddRange(newMsgs);

                _newMessages.RemoveAll(m => m.ChannelId == channelId);
            }

            return result;
        }


        public bool CreateChannel(string channelName, List<string> channelUsers)
        {
            if (!EnsureConnected()) return false;

            var channelParam = new Tuple<string, string>("name", channelName);
            var response = APIRequestWithTokenAsync<CreateChannelResponse>(channelParam).Result;

            if (!Log(response)) return false;
            
            foreach (var user in channelUsers)
            {
                InviteUser(response.channel, user);
            }

            _isConnected = false;       // re-connect on next call to obtain actual info

            return true;
        }

        private void InviteUser(Channel channel, string userName)
        {
            var channelParam = new Tuple<string, string>("channel", channel.id);
            var userObj = Users.FirstOrDefault(u => u.name == userName);
            if (userObj == null)
            {
                Log("User not found - " + userName);
                return;
            }
            if (userObj.IsSlackBot)
            {
                Log("User is SlackBot - " + userName);
                return;
            }            

            var userParam = new Tuple<string, string>("user", userObj.id);

            var response = APIRequestWithTokenAsync<InviteChannelResponse>(channelParam, userParam).Result;

            Log(response);
        }

        public List<string> GetUsers(string userPrefix)
        {
            var result = new List<string>();
            if (!EnsureConnected()) return result;

            var response = base.GetUserListAsync().Result;
            if (!Log(response)) return result;

            var users = response.members.AsEnumerable();
            if (userPrefix != null)
                users = users.Where(u => u.name.StartsWith(userPrefix));

            result = users.Select(u => $"{u.name}").ToList();
            return result;
        }

        public List<string> GetUsers() => GetUsers(null);

        #endregion
    }


    [RequestPath("channels.create")]
    public class CreateChannelResponse : Response
    {
        public Channel channel;
    }

    [RequestPath("channels.invite")]
    public class InviteChannelResponse : Response
    {
        public Channel channel;
    }    
}
