﻿using System;

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
            string testChannelName = "testchannel1";

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
