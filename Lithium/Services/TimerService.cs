using System;
using System.Linq;
using System.Threading;
using Discord;
using Discord.WebSocket;
using DiscordBotsList.Api;
using Lithium.Handlers;
using Lithium.Models;

namespace Lithium.Services
{
    public class TimerService
    {
        public static DateTime LastFireTime = DateTime.MinValue;
        public static int FirePreiod = 10;
        private readonly Timer _timer;


        public TimerService(DiscordSocketClient client)
        {
            _timer = new Timer(async _ =>
                {
                    try
                    {
                        var rnd = new Random();
                        switch (rnd.Next(0, 6))
                        {
                            case 0:
                                await client.SetGameAsync($"{Config.Load().DefaultPrefix}help // {client.Guilds.Count} Guilds!");
                                break;
                            case 1:
                                await client.SetGameAsync($"{Config.Load().DefaultPrefix}help // {client.Guilds.Sum(x => x.MemberCount)} Users!");
                                break;
                            case 2:
                                await client.SetGameAsync($"{Config.Load().DefaultPrefix}help // {Config.Load().SupportServer}");
                                break;
                            case 3:
                                await client.SetGameAsync($"{Config.Load().DefaultPrefix}help // Making Pancakes!");
                                break;
                            case 4:
                                await client.SetGameAsync($"{Config.Load().DefaultPrefix}help // Banning Spammers");
                                break;
                            case 5:
                                await client.SetGameAsync($"{Config.Load().DefaultPrefix}help // AutoModerating!");
                                break;
                        }
                    }
                    catch
                    {
                        //
                    }

                    try
                    {
                        foreach (var guild in client.Guilds)
                        {
                            TimerLoops.checkbans(guild);
                            TimerLoops.checkmutes(guild);
                            TimerLoops.checkwarns(guild);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.LogMessage(e.ToString(), LogSeverity.Error);
                    }

                    try
                    {
                        var token = Config.Load().DBLToken;
                        if (token != null)
                        {
                            var DblApi = new AuthDiscordBotListApi(client.CurrentUser.Id, Config.Load().DBLToken);
                            var me = await DblApi.GetMeAsync();
                            await me.UpdateStatsAsync(client.Guilds.Count);
                        }
                    }
                    catch
                    {
                        //
                    }

                    try
                    {
                        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                    }
                    catch
                    {
                        //
                    }

                    LastFireTime = DateTime.UtcNow;
                },
                null, TimeSpan.Zero, TimeSpan.FromMinutes(FirePreiod));
        }

        public void Stop()
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void Restart()
        {
            _timer.Change(TimeSpan.FromMinutes(0), TimeSpan.FromMinutes(FirePreiod));
        }

        public void ChangeRate(int newperiod = 10)
        {
            FirePreiod = newperiod;
            _timer.Change(TimeSpan.FromMinutes(0), TimeSpan.FromMinutes(FirePreiod));
        }
    }

    public class TimerLoops
    {
        public static async void checkbans(IGuild guild)
        {
            try
            {
                var guildobj = DatabaseHandler.GetGuild(guild.Id);
                if (guildobj?.ModerationSetup.Bans.Any() != true) return;
                if (!guildobj.ModerationSetup.Bans.Any(x => x.Expires && x.ExpiryDate < DateTime.UtcNow)) return;
                var bans = (await guild.GetBansAsync()).ToList();
                foreach (var ban in guildobj.ModerationSetup.Bans.Where(x => x.Expires && x.ExpiryDate < DateTime.UtcNow).ToList())
                {
                    var gban = bans.FirstOrDefault(x => x.User.Id == ban.userID);
                    if (gban == null) continue;
                    try
                    {
                        guildobj.ModerationSetup.Bans.Remove(ban);
                        await guild.RemoveBanAsync(ban.userID);
                        await guildobj.ModLog(new EmbedBuilder
                        {
                            Title = "Ban Auto Removed - Expired",
                            Description = $"User: {ban.username}\n" +
                                          $"UserID: {ban.userID}\n" +
                                          $"Mod: {ban.modname} [{ban.modID}]\n" +
                                          "Reason:\n" +
                                          $"{ban.reason}",
                            Color = Color.DarkGreen
                        }, guild);
                    }
                    catch
                    {
                        //
                    }
                }

                guildobj.Save();
            }
            catch (Exception e)
            {
                Logger.LogMessage(e.ToString(), LogSeverity.Error);
            }
        }

        public static async void checkwarns(IGuild guild)
        {
            try
            {
                var guildobj = DatabaseHandler.GetGuild(guild.Id);
                if (guildobj?.ModerationSetup.Warns.Any() != true) return;
                if (!guildobj.ModerationSetup.Settings.WarnExpiry) return;
                if (!guildobj.ModerationSetup.Warns.Any(x => x.Expiry < DateTime.UtcNow)) return;
                foreach (var warn in guildobj.ModerationSetup.Warns.Where(x => x.Expiry < DateTime.UtcNow).ToList())
                {
                    try
                    {
                        if ((guild as SocketGuild).GetUser(warn.userID) == null) continue;
                        guildobj.ModerationSetup.Warns.Remove(warn);
                        await guildobj.ModLog(new EmbedBuilder
                        {
                            Title = "Warning Auto Removed - Expired",
                            Description = $"User: {warn.username}\n" +
                                          $"UserID: {warn.userID}\n" +
                                          $"Mod: {warn.modname} [{warn.modID}]\n" +
                                          "Reason:\n" +
                                          $"{warn.reason}",
                            Color = Color.DarkGreen
                        }, guild);
                    }
                    catch
                    {
                        //
                    }
                }

                guildobj.Save();
            }
            catch (Exception e)
            {
                Logger.LogMessage(e.ToString(), LogSeverity.Error);
            }
        }

        public static async void checkmutes(IGuild guild)
        {
            var guildobj = DatabaseHandler.GetGuild(guild.Id);
            if (guildobj?.ModerationSetup.Mutes.MutedUsers.Any() != true) return;
            var mutedrole = guild.Roles.FirstOrDefault(x => x.Id == guildobj.ModerationSetup.Mutes.mutedrole);
            if (mutedrole == null) return;
            foreach (var mute in guildobj.ModerationSetup.Mutes.MutedUsers.ToList())
            {
                try
                {
                    if (!mute.expires) continue;
                    var removemute = false;
                    var muteduser = await guild.GetUserAsync(mute.userid);
                    if (muteduser == null)
                    {
                        removemute = true;
                    }
                    else
                    {
                        if (mute.expiry < DateTime.UtcNow)
                        {
                            removemute = true;
                            if (muteduser.RoleIds.Contains(mutedrole.Id))
                            {
                                await muteduser.RemoveRoleAsync(mutedrole);
                            }
                        }
                    }

                    if (removemute)
                    {
                        guildobj.ModerationSetup.Mutes.MutedUsers.Remove(mute);
                        await guildobj.ModLog(new EmbedBuilder
                        {
                            Title = "Mute Auto Removed - Expired",
                            Description = $"User: {muteduser?.Username}\n" +
                                          $"UserID: {muteduser?.Id}",
                            Color = Color.DarkGreen
                        }, guild);
                    }
                }
                catch (Exception e)
                {
                    Logger.LogMessage(e.ToString(), LogSeverity.Error);
                }
            }

            guildobj.Save();
        }
    }
}