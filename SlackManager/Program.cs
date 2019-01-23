using System;
using System.Collections.Generic;

namespace SlackPOC
{
    class Program
    {
        static void Main(string[] args)
        {
            // obtain your token from https://api.slack.com/custom-integrations/legacy-tokens
            string token = "<YOUR TOKEN>";     // NOTE: remove from public after replacing
            
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
            string testChannelName = "testchannel3";

            slackManager.CreateChannel(testChannelName, allUsers);            

            var users = slackManager.GetUsers("d");
            Console.WriteLine($"@@@Users in workspace:\r\n" + string.Join("\r\n", users));

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
