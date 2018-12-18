using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBotsList.Api;
using Lithium.Discord.Contexts;
using Lithium.Models;

namespace Lithium.Modules.BotOwner
{
    [RequireOwner]
    public class OwnerCommands : Base
    {
        [Command("GetInvite")]
        [Summary("GetInvite <ID>")]
        [Remarks("Generate an invite for the specified server")]
        public async Task GetInvite(ulong ID)
        {
            if (Context.Socket.Client.GetGuild(ID) is SocketGuild Guild)
            {
                string inviteURL = null;
                foreach (var channel in Guild.TextChannels)
                {
                    try
                    {
                        var inv = await channel.CreateInviteAsync();
                        inviteURL = inv.Url;
                        break;
                    }
                    catch
                    {
                        //
                    }
                    
                }

                if (inviteURL == null)
                {
                    var invites = await Guild.GetInvitesAsync();
                    inviteURL = invites.FirstOrDefault()?.Url;
                }

                await ReplyAsync(inviteURL);
            }
            else
            {
                throw new Exception("Unknown Guild ID");
            }
        }

        [Command("UpdateServers")]
        [Summary("UpdateServers")]
        [Remarks("Update the Bot's Server Count on DiscordBots.org")]
        public async Task UpdateCount()
        {
            var token = Config.Load().DBLToken;
            if (token == null)
            {
                return;
            }

            var DblApi = new AuthDiscordBotListApi(Context.Client.CurrentUser.Id, Config.Load().DBLToken);
            var me = await DblApi.GetMeAsync();
            await me.UpdateStatsAsync(Context.Socket.Client.Guilds.Count);
        }

        [Command("SetGame")]
        [Summary("SetGame <game>")]
        [Remarks("Set the bot's Current Game.")]
        public async Task Setgame([Remainder] string game = null)
        {
            if (game == null)
            {
                await ReplyAsync("Please specify a game");
            }
            else
            {
                try
                {
                    await Context.Socket.Client.SetGameAsync(game);
                    await ReplyAsync($"{Context.Client.CurrentUser.Username}'s game has been set to:\n" +
                                     $"{game}");
                }
                catch (Exception e)
                {
                    await ReplyAsync($"{e.Message}\n" +
                                     "Unable to set the game");
                }
            }
        }

        [Command("Stats")]
        [Summary("Stats")]
        [Remarks("Display Bot Statistics")]
        public async Task BotStats()
        {
            var embed = new EmbedBuilder();

            var heap = Math.Round(GC.GetTotalMemory(true) / (1024.0 * 1024.0), 2).ToString(CultureInfo.InvariantCulture);
            var uptime = (DateTime.Now - Process.GetCurrentProcess().StartTime).ToString(@"dd\.hh\:mm\:ss");

            embed.AddField($"{Context.Client.CurrentUser.Username} Statistics",
                $"Servers: {Context.Socket.Client.Guilds.Count}\n" +
                $"Users: {Context.Socket.Client.Guilds.Select(x => x.Users.Count).Sum()}\n" +
                $"Unique Users: {Context.Socket.Client.Guilds.SelectMany(x => x.Users.Select(y => y.Id)).Distinct().Count()}\n" +
                $"Server Channels: {Context.Socket.Client.Guilds.Select(x => x.Channels.Count).Sum()}\n" +
                $"DM Channels: {Context.Socket.Client.DMChannels.Count}\n\n" +
                $"Uptime: {uptime}\n" +
                $"Heap Size: {heap}\n" +
                $"Discord Version: {DiscordConfig.Version}");

            await ReplyAsync("", false, embed.Build());
        }

        [Group("Tokens")]
        public class Tokens : Base
        {
            [Command("Perspective")]
            [Summary("Perspective <token>")]
            [Remarks("Set the toxicity token")]
            public async Task SetPerspectiveToken([Remainder] string token = null)
            {
                var cfg = Config.Load();
                cfg.ToxicityToken = token;
                cfg.Save();

                await ReplyAsync("Success Token Set (or reset if nothing was supplied)\n" +
                                 "NOTE, This requires a bot restart");
            }

            [Command("DBL")]
            [Summary("DBL <token>")]
            [Remarks("Set the DiscordBotsList token")]
            public async Task SetDBL([Remainder] string token = null)
            {
                var cfg = Config.Load();
                cfg.DBLToken = token;
                cfg.Save();

                await ReplyAsync("Success Token Set (or reset if nothing was supplied)");
            }
        }
    }
}