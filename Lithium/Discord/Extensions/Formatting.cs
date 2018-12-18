using System.Text.RegularExpressions;
using Discord.WebSocket;
using Lithium.Discord.Contexts;

namespace Lithium.Discord.Extensions
{
    public class Formatting
    {
        public static string DoReplacements(string input, LithiumContext context)
        {
            var result = input;
            if (!string.IsNullOrEmpty(input))
            {
                result = Regex.Replace(input, "{user}", context.User.Username, RegexOptions.IgnoreCase);
                result = Regex.Replace(result, "{user.mention}", context.User.Mention, RegexOptions.IgnoreCase);
                result = Regex.Replace(result, "{guild}", context.Guild.Name, RegexOptions.IgnoreCase);
                result = Regex.Replace(result, "{channel}", context.Channel.Name, RegexOptions.IgnoreCase);
                result = Regex.Replace(result, "{channel.mention}", ((SocketTextChannel) context.Channel).Mention, RegexOptions.IgnoreCase);
            }

            return result;
        }
    }
}