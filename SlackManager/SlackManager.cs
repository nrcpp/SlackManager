using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Siemplify.Common.ExternalChannels.DataModel;
using SlackAPI;
using SlackPOC;

namespace Siemplify.Common.ExternalChannels
{    
    public class SlackManager : SlackAPI.SlackTaskClient
    {
        private bool _isConnected;
        SlackSocketClient _socketClient;
        private List<ChannelMessage> _newMessages = new List<ChannelMessage>();


        public bool IsConnected => _isConnected;

        private string _token;

        public string BotName { get; set; } = nameof(SlackManager);

        public event Action<ChannelMessage> OnNewMessage;


        public SlackManager(string token, string botName = nameof(SlackManager))
            : base(token)
        {
            _token = token;
            BotName = botName;            
        }


        #region Socket Client

        private void ConnectSocketClient()
        {
            if (_socketClient != null) return;
            _socketClient = CreateClient(_token);            

            if (_socketClient == null) return;

            _socketClient.OnMessageReceived += msg =>
            {
                var channelMsg = msg.ToChannelMessage();

                _newMessages.Add(channelMsg);
                OnNewMessage?.Invoke(channelMsg);
            };
        }

        private SlackSocketClient CreateClient(string authToken, IWebProxy proxySettings = null, [CallerMemberName] string caller = null)
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

                    //Console.WriteLine($"[CreateClient]: {x.ok}");
                    syncClient.Proceed();
                    if (!x.ok)
                    {
                        // If connect fails, socket connect callback is not called
                        syncClientSocket.Proceed();
                        syncClientSocketHello.Proceed();
                    }
                }, () =>
                {
                    //Console.WriteLine("[CreateClient]: Socket Connected");
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

        /// <summary>
        /// Connects to Slack and could connect through socket to handle real-time events
        /// </summary>
        /// <param name="connectSocket">If true, will connect via socket as well to handle new messages</param>
        public void Connect(bool connectSocket = true)
        {
            _userDirectChannelIds.Clear();

            var loginResponse = base.ConnectAsync().Result;
            _isConnected = loginResponse.ok;

            Log(loginResponse);

            if (_isConnected && connectSocket) ConnectSocketClient();
        }


        public void SendMessage(string channelName, string message)
        {
            var channel = GetChannelByName(channelName);
            if (channel == null) return;
            
            var response = base.PostMessageAsync(channelId: channel.id, text: message,
                                                     botName: BotName).Result;
            Log(response);            
        }


        Dictionary<string, string> _userDirectChannelIds = new Dictionary<string, string>();

        public void SendMessageToUser(string userName, string message)
        {
            string userChannelId = "";
            if (!_userDirectChannelIds.TryGetValue(userName, out userChannelId))
            {
                var user = GetUserListAsync().Result.members.FirstOrDefault(u => u.name == userName);
                if (user == null)
                {
                    Log($"{userName} - user not found");
                    return;
                }

                var joinResponse = JoinDirectMessageChannelAsync(user.id).Result;
                if (!Log(joinResponse)) return;

                userChannelId = joinResponse.channel.id;
                _userDirectChannelIds.Add(userName, joinResponse.channel.id);
            }


            var response = base.PostMessageAsync(channelId: userChannelId, text: message,
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
            
            result = messages.Select(m => m.ToChannelMessage()).Reverse().ToList();

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

            //_isConnected = false;       // re-connect on next call to obtain actual info. Note, this may cause reaching request limit 

            return true;
        }


        // Undocumented Slack API call. See https://github.com/ErikKalkoken/slackApiDoc/blob/master/channels.delete.md
        public bool CloseChannel(string channelName)
        {
            if (!EnsureConnected()) return false;

            var channel = GetChannelListAsync().Result.channels.FirstOrDefault(ch => ch.name == channelName);
            if (channel == null)
            {
                Log($"{channelName} - channel not found");
                return false;
            }


            var channelParam = new Tuple<string, string>("channel", channel.id);
            var response = APIRequestWithTokenAsync<DeleteChannelResponse>(channelParam).Result;

            if (!Log(response)) return false;            

            return true;
        }


        private void InviteOrRemoveUser<T>(Channel channel, string userName, [CallerMemberName] string caller = null) where T : Response
        {
            var channelParam = new Tuple<string, string>("channel", channel.id);
            var userObj = Users.FirstOrDefault(u => u.name == userName);
            if (userObj == null)
            {
                Log("User not found - " + userName, caller);
                return;
            }
            if (userObj.IsSlackBot)
            {
                Log("User is SlackBot - " + userName, caller);
                return;
            }            

            var userParam = new Tuple<string, string>("user", userObj.id);

            var response = APIRequestWithTokenAsync<T>(channelParam, userParam).Result;

            Log(response, caller);
        }

        private void InviteUser(Channel channel, string userName, [CallerMemberName] string caller = null) =>
            InviteOrRemoveUser<InviteChannelResponse>(channel, userName, caller);

        private void RemoveUser(Channel channel, string userName, [CallerMemberName] string caller = null) =>
            InviteOrRemoveUser<RemoveChannelResponse>(channel, userName, caller);
        

        public bool AddUserToChannel(string channelName, string userName)
        {            
            if (!EnsureConnected()) return false;

            var channel = Channels.FirstOrDefault(ch => ch.name == channelName);
            if (channel == null)
            {
                Log("Channel not found - " + channelName);
                return false;
            }

            InviteUser(channel, userName);

            return true;
        }


        /// <summary>
        /// https://api.slack.com/methods/channels.kick - see for restrictions
        /// https://get.slack.help/hc/en-us/articles/201898668-Remove-someone-from-a-channel - see whom may be kicked
        /// </summary>
        /// <param name="channelName"></param>
        /// <param name="userName"></param>
        /// <returns></returns>
        public bool RemoveUserFromChannel(string channelName, string userName)
        {            
            if (!EnsureConnected()) return false;

            var channel = Channels.FirstOrDefault(ch => ch.name == channelName);
            if (channel == null)
            {
                Log("Channel not found - " + channelName);
                return false;
            }

            RemoveUser(channel, userName);

            return true;
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

    [RequestPath("channels.invite")]
    public class InviteChannelResponse : Response
    {
        public Channel channel;
    }

    [RequestPath("channels.kick")]
    public class RemoveChannelResponse : Response
    {        
    }

    [RequestPath("channels.join")]
    public class JoinChannelResponse : Response
    {
        public Channel channel;
    }

    [RequestPath("channels.create")]
    public class CreateChannelResponse : Response
    {
        public Channel channel;
    }


    // NOTE: this is undocumented method. Use 'channels.archive' instead
    [RequestPath("channels.delete")]
    public class DeleteChannelResponse : Response
    {        
    }
}
