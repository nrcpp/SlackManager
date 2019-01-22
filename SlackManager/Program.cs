﻿using System;

namespace SlackPOC
{
    class Program
    {
        static void Main(string[] args)
        {
            // obtain your token from https://api.slack.com/custom-integrations/legacy-tokens
            string token = "<YOUR TOKEN>";     // NOTE: remove from public after replacing
            token = "xoxp-529125341204-528691463697-529296771073-7f114697cdfd5787e69fb5e99ef09598";

            var slackManager = new SlackManager(token);
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

            Console.WriteLine("Press Enter to exit...");
            Console.ReadLine();
        }
    }
}
