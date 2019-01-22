# SlackManager

SlackManager class is an extension of SlackAPI .NET wrapper. It contains such methods:
```c#
// Connects to Slack. 
public void Connect()        

// Send message to channel as a bot
public void SendMessage(string channelName, string message)
        
// Get messages history
public List<string> GetMessages(string channelName)
public List<string> GetMessages(string channelName, DateTime from)

// NOTE: Slack API won't return message.id field. So this method
// won't work for now.
// See https://api.slack.com/methods/channels.history Response section
public List<string> GetMessages(string channelName, long messageId)
        
// Creates new channel and invites users from list.
// If channel has incorrect name of already exists then error will be logged
public void CreateChannel(string channelName, List<string> channelUsers)
        
// Returns all users from workspace
public List<string> GetUsers()

// Returns all users with a name started with 'userPrefix'
public List<string> GetUsers(string userPrefix)        
```

----

## Notes

1. All Slack API response results will be logged to Console. Such as OK or reason of error.
2. For most of method calls there is a check to ensure connection to Slack endpoint. If there is no connection, then it connects again. So `Connect()` method call is optional in most cases. 
3. SlackManager.GetMessages(long messageId) isn't work because Slack API does not return message.id. See https://api.slack.com/methods/channels.history *Response* section

## Example Code

```c#
using System;

namespace SlackPOC
{
    class Program
    {
        static void Main(string[] args)
        {
            // obtain your token from https://api.slack.com/custom-integrations/legacy-tokens
            string token = "xoxp-529125341204-529125341956-529297732770-3b0a7551daa157d35d66a833b4fe7b05";

            var slackManager = new SlackManager(token);
            slackManager.Connect();
            if (!slackManager.IsConnected)
            {
                SlackManager.Log("Not connected. Exit");
                return;
            }

            var allUsers = slackManager.GetUsers();
            string testChannelName = "testchannel2";

            slackManager.CreateChannel(testChannelName, allUsers);            

            var users = slackManager.GetUsers("d");
            Console.WriteLine($"@@@Users in workspace:\r\n" + string.Join("\r\n", users));

            // Test send/get messages for 'general' channel            
            slackManager.SendMessage(testChannelName, "Hello! This is a test message from `SlackManager`");

            var messages = slackManager.GetMessages(testChannelName, DateTime.Today);
            Console.WriteLine($"Messages from #{testChannelName}:\r\n" + string.Join("\r\n", messages));
        }
    }
}
```