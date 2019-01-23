using SlackAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SlackPOC
{
    public class Utils
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
    }


}
