using System;
using System.Collections.Generic;
using System.IO;

namespace SlackPOC
{
    class Program
    {
        static void Main(string[] args)
        {
            // obtain your token from https://api.slack.com/custom-integrations/legacy-tokens
            string token = "<YOUR TOKEN>";     // NOTE: remove from public after replacing

#if DEBUG
            // for testing purposes, "token.txt" have to be in .gitignore
            if (File.Exists("token.txt"))
                token = File.ReadAllText("token.txt").Trim();
#endif

            var slackManager = new SlackManager(token);

            // New messages handler
            slackManager.OnNewMessage += m => { Console.WriteLine("New message: " + m); };

            slackManager.Connect();
            if (!slackManager.IsConnected)
            {
                SlackManager.Log("Not connected. Exit");
                return;
            }
            
            string testChannelName = "testchannel3";

            slackManager.CreateChannel(testChannelName, new List<string>() { });
            
            var users = slackManager.GetUsers();
            Console.WriteLine($"@@@Users in workspace:\r\n" + string.Join("\r\n", users));

            // add/remove user to channel
            if (users.Count >= 2)
            {
                string testUser = users[1];
                slackManager.AddUserToChannel(testChannelName, testUser);
            }
            else
                Console.WriteLine($"No users in channel #{testChannelName} to add/remove");

            // Test send/get messages for 'general' channel            
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
