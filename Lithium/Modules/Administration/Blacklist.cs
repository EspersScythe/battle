using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Lithium.Discord.Contexts;
using Lithium.Discord.Preconditions;
using Lithium.Models;

namespace Lithium.Modules.Administration
{
    [RequireRole.RequireAdmin]
    public class Blacklist : Base
    {
        [Command("Blacklist")]
        [Summary("blacklist")]
        [Remarks("displays the blacklist")]
        public async Task B()
        {
            var pages = new List<PaginatedMessage.Page>();
            var sb = new StringBuilder();
            foreach (var blacklistw in Context.Server.Antispam.Blacklist.BlacklistWordSet)
            {
                if (sb.ToString().Length >= 800)
                {
                    pages.Add(new PaginatedMessage.Page
                    {
                        Description = sb.ToString()
                    });
                    sb.Clear();
                }

                sb.Append("**Word(s)**\n" +
                          $"{string.Join("\n", blacklistw.WordList)}\n" +
                          "**Response**\n" +
                          $"{blacklistw.BlacklistResponse ?? Context.Server.Antispam.Blacklist.DefaultBlacklistMessage}\n\n");
            }

            pages.Add(new PaginatedMessage.Page
            {
                Description = sb.ToString()
            });
            var pager = new PaginatedMessage
            {
                Title = "Blacklisted Messages :stop_button: to remove",
                Pages = pages
            };

            await PagedReplyAsync(pager, new ReactionList
            {
                Forward = true,
                Backward = true,
                Trash = true
            });
        }

        [Command("blacklist formathelp")]
        [Summary("blacklist formathelp")]
        [Remarks("help with adding multiple phrases or words to the blacklist at once")]
        public async Task FormatHelp()
        {
            await ReplyAsync(new EmbedBuilder
            {
                Description = "__**Sentences and Multiple Words**__\n" +
                              "To add a sentence to the blacklist, use the add command like so:\n" +
                              $"`{Config.Load().DefaultPrefix}blacklist add this_is_a_sentence <response>`" +
                              "You may leave the response empty to use the default blacklist message as your response\n" +
                              "To add multiple words at once to the blacklist separate words using commas like so:\n" +
                              $"`{Config.Load().DefaultPrefix}blacklist add word1,word2,sentence_1,word3,sentence2 <response>`\n" +
                              "Note that this will also work for the blacklist delete command\n\n" +
                              "**__Responses__**\n" +
                              "You can add custom text into the response by using the following custom tags:\n" +
                              "{user} - the user's username\n" +
                              "{user.mention} - @ the user\n" +
                              "{guild} - the guild's name\n" +
                              "{channel} - the current channel name\n" +
                              "{channel.mention} - #channel mention"
            }.Build());
        }

        [Command("blacklist add")]
        [Summary("blacklist add <word> <response>")]
        [Remarks("adds a word to the blacklist, leave response blank to use the default message, use the same response for different blacklisted words to be grouped. Also separate sentences like so: hi_there_person for the keyword")]
        public async Task Ab(string keyword, [Remainder] string response = null)
        {
            keyword = keyword.Replace("_", " ");
            var keywords = keyword.Split(',').Select(x => x.ToLower()).ToList();
            if (!Context.Server.Antispam.Blacklist.BlacklistWordSet.Any(x => x.WordList.Contains(keyword)))
            {
                var blacklistunit =
                    Context.Server.Antispam.Blacklist.BlacklistWordSet.FirstOrDefault(
                        x => x.BlacklistResponse == response);
                if (blacklistunit != null)
                {
                    blacklistunit.WordList.AddRange(keywords);
                    await Context.Message.DeleteAsync();
                    await ReplyAsync("Added to the Blacklist");
                }
                else
                {
                    blacklistunit = new GuildModel.Guild.antispams.blacklist.BlacklistWords
                    {
                        WordList = keywords,
                        BlacklistResponse = response
                    };
                    Context.Server.Antispam.Blacklist.BlacklistWordSet.Add(blacklistunit);
                    await Context.Message.DeleteAsync();
                    await ReplyAsync("Added to the Blacklist");
                }
            }
            else
            {
                await Context.Message.DeleteAsync();
                await ReplyAsync("Keyword is already in the blacklist");
                return;
            }

            Context.Server.Save();
        }

        [Command("blacklist del")]
        [Summary("blacklist del <word>")]
        [Remarks("removes a word from the blacklist")]
        public async Task Db(string initkeyword)
        {
            initkeyword = initkeyword.Replace("_", " ");
            var keywords = initkeyword.Split(',').Select(x => x.ToLower()).ToList();
            foreach (var keyword in keywords)
            {
                var blacklistunit = Context.Server.Antispam.Blacklist.BlacklistWordSet.FirstOrDefault(x => x.WordList.Contains(keyword.ToLower()));
                if (blacklistunit != null)
                {
                    blacklistunit.WordList.Remove(keyword.ToLower());
                    if (blacklistunit.WordList.Count == 0)
                    {
                        Context.Server.Antispam.Blacklist.BlacklistWordSet.Remove(blacklistunit);
                    }

                    await ReplyAsync($"{keyword} is has been removed from the blacklist");
                }
                else
                {
                    await ReplyAsync($"{keyword} is not in the blacklist");
                }
            }

            Context.Server.Save();
        }

        [Command("blacklist clear")]
        [Summary("blacklist clear")]
        [Remarks("clears the blacklist")]
        public async Task Clear()
        {
            Context.Server.Antispam.Blacklist.BlacklistWordSet =
                new List<GuildModel.Guild.antispams.blacklist.BlacklistWords>();
            Context.Server.Save();

            await ReplyAsync("The blacklist has been cleared.");
        }

        [Command("blacklist defaultmessage")]
        [Summary("blacklist defaultmessage <message>")]
        [Remarks("set the default blacklist message")]
        public async Task BlMessage([Remainder] string blmess = "")
        {
            Context.Server.Antispam.Blacklist.DefaultBlacklistMessage = blmess;
            Context.Server.Save();

            await ReplyAsync("The default blacklist message is now:\n" +
                             $"{Context.Server.Antispam.Blacklist.DefaultBlacklistMessage}");
        }
    }
}