using SlackAPI.WebSocketMessages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SlackPOC
{
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


}
