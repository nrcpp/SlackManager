# SlackManager

_Last updated 23th Jan 2019. Check SlackManager.cs for most actual info. And Program.cs for example of usage of SlackManager._

SlackManager class is an extension of SlackAPI .NET wrapper. It contains such methods:

```
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
// If channel has incorrect name or already exists then error will be logged
public void CreateChannel(string channelName, List<string> channelUsers)
        
// Returns all users from workspace
public List<string> GetUsers()

// Returns all users with a name started with 'userPrefix'
public List<string> GetUsers(string userPrefix)        
```



## Notes

1. Slack API requires token which you can obtain from https://api.slack.com/custom-integrations/legacy-tokens. Then you could paste it on object creation `new SlackManager(token)`.
2. All Slack API response results will be logged to Console. Such as OK or reason of error.
3. For most of method calls there is a check to ensure connection to Slack endpoint. If there is no connection, then it connects again. So `Connect()` method call is optional in most cases. 
4. SlackManager.GetMessages(long messageId) isn't work because Slack API does not return message.id. See https://api.slack.com/methods/channels.history *Response* section
5. You could set proxy settings and bot name within constructor `SlackManager(token, "Slack Manager", proxySettings)`.
6. **NOTE:** that you can't share your token with other users in public, including github or other public links. Then Slack will revoke such token.



## Example Code


            using System;
            
            namespace SlackPOC
            {
                class Program
                {
                    static void Main(string[] args)
                    {
                        // obtain your token from https://api.slack.com/custom-integrations/legacy-tokens
                        string token = "<Your Slack API Token>";

                        var slackManager = new SlackManager(token);
                        // New messages handler
                        slackManager.OnNewMessage += m => { Console.WriteLine("New message: " + m); };
                        slackManager.Connect();
                        if (!slackManager.IsConnected)
                        {
                            SlackManager.Log("Not connected. Exit");
                            return;
                        }

                        var allUsers = slackManager.GetUsers();
                        string testChannelName = "testchannel2";

                        slackManager.CreateChannel(testChannelName, allUsers);            

                        var users = slackManager.GetUsers();
                        Console.WriteLine($"@@@Users in workspace:\r\n" + string.Join("\r\n", users));

                        // Test send/get messages for test channel            
                        slackManager.SendMessage(testChannelName, "Hello! This is a test message from `SlackManager`");

                        var messages = slackManager.GetMessages(testChannelName, DateTime.Today);
                        Console.WriteLine($"Messages from #{testChannelName}:\r\n" + string.Join("\r\n", messages));
                        
                        Console.WriteLine("Press Enter to get new messages...");
                        Console.ReadLine();

                        // get new messages
                        messages = slackManager.GetNewMessages(new List<string>() { testChannelName });
                        Console.WriteLine("New messages:\r\n" + string.Join("\r\n", messages.ConvertAll<string>(m => m.AsString)));

                        Console.WriteLine("\r\nPress Enter to exit...");
                        Console.ReadLine();
                    }
                }
            }

