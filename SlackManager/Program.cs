using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Siemplify.Common.ExternalChannels
{
    class Program
    {
        const string testChannelName = "testchannel1";
        static SlackManager slackManager;

        static void TestCreateChannel_AddRemoveUser()
        {
            //slackManager.CreateChannel(testChannelName, new List<string>() { });
            var users = slackManager.GetUsers();

            // add/remove user to channel
            if (users.Count >= 2)
            {
                string testUser = users[1];     // can't invite self - users[0]

                // Uncomment to invite user
                //slackManager.AddUserToChannel(testChannelName, testUser);

                // Uncomment to remove user next time.                 
                //slackManager.RemoveUserFromChannel(testChannelName, testUser);
            }
            else
                Console.WriteLine($"No users in channel #{testChannelName} to add/remove");
        }


        static void Main(string[] args)
        {
            // obtain your token from https://api.slack.com/custom-integrations/legacy-tokens
            string token = "<YOUR TOKEN>";     // NOTE: remove from public after replacing

#if DEBUG
            // for testing purposes, "token.txt" have to be in .gitignore
            if (File.Exists("token.txt"))
                token = File.ReadAllText("token.txt").Trim();
#endif

            slackManager = new SlackManager(token);

            // New messages handler
            slackManager.OnNewMessage += m => { Console.WriteLine("New message: " + Utils.AsString(m)); };

            slackManager.Connect();
            if (!slackManager.IsConnected)
            {
                SlackManager.Log("Not connected. Exit");
                return;
            }
                        
            
            var users = slackManager.GetUsers();
            Console.WriteLine($"@@@Users in workspace:\r\n" + string.Join("\r\n", users));

            // Uncomment to test create channel and add/remove user there.
            // Note there are rate limit for such operations: https://api.slack.com/docs/rate-limits
            TestCreateChannel_AddRemoveUser();

            // Test send/get messages for 'general' channel            
            slackManager.SendMessage(testChannelName, "Hello! This is a test message from `SlackManager`");

            var messages = slackManager.GetMessages(testChannelName, DateTime.Today);
            Console.WriteLine($"Messages from #{testChannelName}:\r\n" + string.Join("\r\n", messages.Select(m => m.AsString())));

            Console.WriteLine("Press Enter to get new messages...");
            Console.ReadLine();

            // get new messages
            messages = slackManager.GetNewMessages(new List<string>() { testChannelName });
            Console.WriteLine("New messages:\r\n" + string.Join("\r\n", messages.ConvertAll(m => m.AsString())));

            Console.WriteLine("\r\nPress Enter to exit...");
            Console.ReadLine();
        }
    }
}
