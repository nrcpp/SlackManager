using Siemplify.Common.ExternalChannels.DataModel;
using SlackAPI;
using SlackAPI.WebSocketMessages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Siemplify.Common
{
    public static class Utils
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

        #region Channel Message

        public static ChannelMessage ToChannelMessage(this NewMessage message)
        {
            var result = new ChannelMessage();

            result.Time = message.ts;
            result.User = message.user;
            result.Username = message.username;
            result.Text = message.text;
            result.ChannelId = message.channel;
            result.IsStarred = false;

            return result;
        }

        public static ChannelMessage ToChannelMessage(this SlackAPI.Message message)
        {
            var result = new ChannelMessage();

            result.Time = message.ts;
            result.User = message.user;
            result.Username = message.username;
            result.Text = message.text;
            result.ChannelId = message.channel;
            result.IsStarred = message.is_starred;

            return result;
        }

        public static string AsString(this ChannelMessage m) => $"[{m.Time}]: @{m.User} ({m.Username}): {m.Text}";        

        #endregion
    }


}
