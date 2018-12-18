using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Lithium.Discord.Contexts;
using Lithium.Discord.Extensions;
using Lithium.Models;
using Lithium.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Lithium.Handlers
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly TimerService _timerservice;
        private readonly List<NoSpamGuild> NoSpam = new List<NoSpamGuild>();
        private readonly Perspective.Api ToxicityAPI;

        public List<Delays> AntiSpamMsgDelays = new List<Delays>();

        public List<EventLogDelay> EventLogDelays = new List<EventLogDelay>();

        public IServiceProvider Provider;

        public CommandHandler(IServiceProvider provider)
        {
            Provider = provider;
            _client = Provider.GetService<DiscordSocketClient>();
            _commands = new CommandService();
            _timerservice = new TimerService(_client);
            ToxicityAPI = new Perspective.Api(Config.Load().ToxicityToken);

            _client.MessageReceived += DoCommand;
            _client.JoinedGuild += _client_JoinedGuild;
            _client.Ready += _client_Ready;
        }

        private async Task _client_Ready()
        {
            try
            {
                DatabaseHandler.DatabaseInitialise(_client);
                Log.Information("Database Initialised");
                var application = await _client.GetApplicationInfoAsync();
                Log.Information($"Invite: https://discordapp.com/oauth2/authorize?client_id={application.Id}&scope=bot&permissions=2146958591");
                var dblist = DatabaseHandler.GetFullConfig();
                foreach (var guild in _client.Guilds.Where(g => dblist.All(x => x.GuildID != g.Id)))
                {
                    DatabaseHandler.AddGuild(guild.Id);
                }

                _timerservice.Restart();
            }
            catch (Exception e)
            {
                Logger.LogMessage(e.ToString(), LogSeverity.Error);
            }

            await _client.SetGameAsync($"{Config.Load().DefaultPrefix}help // {_client.Guilds.Sum(x => x.MemberCount)} Users!");
        }

        private async Task _client_JoinedGuild(SocketGuild guild)
        {
            try
            {
                var dblist = DatabaseHandler.GetFullConfig();
                if (dblist.All(x => x.GuildID != guild.Id))
                {
                    foreach (var missingguild in _client.Guilds.Where(g => dblist.All(x => x.GuildID != g.Id)))
                    {
                        DatabaseHandler.AddGuild(missingguild.Id);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogMessage("Joined Guild Setup Error", LogSeverity.Error);
                Logger.LogMessage(e.ToString(), LogSeverity.Error);
            }

            try
            {
                //Ensure that we notify new servers how to use the bot by telling them how to get use th ehelp command.
                var embed = new EmbedBuilder
                {
                    Title = guild.CurrentUser.Username,
                    Description = $"Hi there, I am {guild.CurrentUser.Username}. Type `{Config.Load().DefaultPrefix}help` to see a list of my commands",
                    Color = Color.Blue
                };
                //await guild.DefaultChannel?.SendMessageAsync("", false, embed.Build());
                var defaultchannel = guild.TextChannels?.FirstOrDefault(x => string.Equals(x.Name, "general", StringComparison.CurrentCultureIgnoreCase));
                if (defaultchannel != null)
                {
                    try
                    {
                        await defaultchannel.SendMessageAsync(string.Empty, false, embed.Build());
                    }
                    catch (Exception e)
                    {
                        Logger.LogMessage(e.ToString(), LogSeverity.Error);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogMessage("Joined Guild Notify Error", LogSeverity.Error);
                Logger.LogMessage(e.ToString(), LogSeverity.Error);
            }
        }

        public async Task<bool> AntiSpam(LithiumContext context, List<GuildModel.Guild.antispams.IgnoreRole> exemptcheck)
        {
            var guild = context.Server;
            var detected = false;
            var SpamGuild = NoSpam.FirstOrDefault(x => x.GuildID == ((SocketGuildUser) context.User).Guild.Id);
            if (SpamGuild == null)
            {
                NoSpam.Add(new NoSpamGuild
                {
                    GuildID = context.Guild.Id,
                    Users = new List<NoSpamGuild.NoSpam>
                    {
                        new NoSpamGuild.NoSpam
                        {
                            UserID = context.User.Id,
                            Messages = new List<NoSpamGuild.NoSpam.Msg>
                            {
                                new NoSpamGuild.NoSpam.Msg
                                {
                                    LastMessage = context.Message.Content,
                                    LastMessageDate = DateTime.UtcNow
                                }
                            }
                        }
                    }
                });
            }
            else
            {
                var user = SpamGuild.Users.FirstOrDefault(x => x.UserID == context.User.Id);
                if (user == null)
                {
                    SpamGuild.Users.Add(new NoSpamGuild.NoSpam
                    {
                        UserID = context.User.Id,
                        Messages = new List<NoSpamGuild.NoSpam.Msg>
                        {
                            new NoSpamGuild.NoSpam.Msg
                            {
                                LastMessage = context.Message.Content,
                                LastMessageDate = DateTime.UtcNow
                            }
                        }
                    });
                }
                else
                {
                    user.Messages.Add(new NoSpamGuild.NoSpam.Msg
                    {
                        LastMessage = context.Message.Content,
                        LastMessageDate = DateTime.UtcNow
                    });
                    if (user.Messages.Count >= 2)
                    {
                        var msgs = user.Messages.Where(x => x.LastMessageDate > DateTime.UtcNow - TimeSpan.FromSeconds(10)).ToList();
                        //Here we detect spam based on wether or not a user is sending the same message repeatedly
                        //Or wether they have sent a message more than 3 times in the last 5 seconds
                        if (msgs.GroupBy(n => n.LastMessage.ToLower()).Any(c => c.Count() > 1) || msgs.Count(x => x.LastMessageDate > DateTime.UtcNow - context.Server.Antispam.Antispam.NoSpamTimeout) > context.Server.Antispam.Antispam.NoSpamCount)
                        {
                            detected = true;
                        }
                    }

                    if (user.Messages.Count > 10)
                    {
                        //Filter out messages so that we only keep a log of the most recent ones within the last 30 seconds.
                        var msgs = user.Messages.OrderBy(x => x.LastMessageDate).ToList();
                        msgs.RemoveRange(0, 1);
                        msgs = msgs.Where(x => x.LastMessageDate <= DateTime.UtcNow - TimeSpan.FromSeconds(30)).ToList();
                        user.Messages = msgs;
                    }

                    if (detected)
                    {
                        var BypassAntispam = exemptcheck.Any(x => x.AntiSpam);
                        if (!BypassAntispam)
                        {
                            if (!guild.Antispam.Antispam.AntiSpamSkip.Any(x => context.Message.Content.ToLower().Contains(x.ToLower())))
                            {
                                await context.Message?.DeleteAsync();
                                var delay = AntiSpamMsgDelays.FirstOrDefault(x => x.GuildID == guild.GuildID);
                                if (delay != null)
                                {
                                    if (delay._delay > DateTime.UtcNow)
                                    {
                                        return true;
                                    }

                                    delay._delay = DateTime.UtcNow.AddSeconds(10);
                                    var emb = new EmbedBuilder
                                    {
                                        Description = $"{context.User} - No Spamming!!"
                                    };
                                    await context.Channel.SendMessageAsync(string.Empty, false, emb.Build());
                                    if (guild.Antispam.Antispam.WarnOnDetection)
                                    {
                                        await guild.AddWarn("AutoMod - AntiSpam", context.User as IGuildUser, context.Client.CurrentUser, context.Channel, context.Message.Content);
                                        guild.Save();
                                    }
                                }
                                else
                                {
                                    AntiSpamMsgDelays.Add(new Delays
                                    {
                                        _delay = DateTime.UtcNow.AddSeconds(10),
                                        GuildID = guild.GuildID
                                    });
                                }

                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        public async Task<bool> AntiInvite(LithiumContext context, List<GuildModel.Guild.antispams.IgnoreRole> exemptcheck)
        {
            var guild = context.Server;
            var bypass_invite = exemptcheck.Any(x => x.Advertising);
            if (!bypass_invite)
            {
                if (Regex.Match(context.Message.Content, @"(http|https)?(:)?(\/\/)?(discordapp|discord).(gg|io|me|com)\/(\w+:{0,1}\w*@)?(\S+)(:[0-9]+)?(\/|\/([\w#!:.?+=&%@!-/]))?").Success)
                {
                    await context.Message?.DeleteAsync();
                    var emb = new EmbedBuilder();
                    if (guild.Antispam.Advertising.NoInviteMessage != null)
                    {
                        emb.Description = Formatting.DoReplacements(guild.Antispam.Advertising.NoInviteMessage, context);
                    }
                    else
                    {
                        emb.Description = $"{context.User} - This server does not allow you to send invite links in chat";
                    }

                    // Description = guild.Antispam.Advertising.NoInviteMessage ?? $"{context.User?.Mention} - no sending invite links... the admins might get angry"
                    await context.Channel.SendMessageAsync(string.Empty, false, emb.Build());
                    if (guild.Antispam.Advertising.WarnOnDetection)
                    {
                        await guild.AddWarn("AutoMod - Anti Advertising", context.User as IGuildUser, context.Client.CurrentUser, context.Channel, context.Message.Content);
                        guild.Save();
                    }

                    return true;
                }
            }

            return false;
        }

        public async Task<bool> AntiMention(LithiumContext context, List<GuildModel.Guild.antispams.IgnoreRole> exemptcheck)
        {
            var guild = context.Server;
            var bypass_mention = exemptcheck.Any(x => x.Mention);

            if (!bypass_mention)
            {
                if (guild.Antispam.Mention.RemoveMassMention)
                {
                    if (context.Message.MentionedRoleIds.Count + context.Message.MentionedUserIds.Count >= 5)
                    {
                        await context.Message?.DeleteAsync();
                        var emb = new EmbedBuilder
                        {
                            Description = $"{context.User} - This server does not allow you to mention 5+ roles or uses at once"
                        };
                        await context.Channel.SendMessageAsync(string.Empty, false, emb.Build());
                        if (guild.Antispam.Mention.WarnOnDetection)
                        {
                            await guild.AddWarn("AutoMod - Mass Mention", context.User as IGuildUser, context.Client.CurrentUser, context.Channel, context.Message.Content);
                            guild.Save();
                        }

                        return true;
                    }
                }

                if (guild.Antispam.Mention.MentionAll)
                {
                    if (context.Message.Content.Contains("@everyone") || context.Message.Content.Contains("@here"))
                    {
                        await context.Message?.DeleteAsync();
                        var emb = new EmbedBuilder();
                        if (guild.Antispam.Mention.MentionAllMessage != null)
                        {
                            emb.Description = Formatting.DoReplacements(guild.Antispam.Mention.MentionAllMessage, context);
                        }
                        else
                        {
                            emb.Title = $"{context.User} - This server has disabled the ability for you to mention @everyone and @here";
                        }

                        await context.Channel.SendMessageAsync(string.Empty, false, emb.Build());
                        if (guild.Antispam.Mention.WarnOnDetection)
                        {
                            await guild.AddWarn("AutoMod - Mention All", context.User as IGuildUser, context.Client.CurrentUser, context.Channel, context.Message.Content);
                            guild.Save();
                        }

                        return true;
                    }
                }
            }

            return false;
        }

        public async Task<bool> AntiIp(LithiumContext context, List<GuildModel.Guild.antispams.IgnoreRole> exemptcheck)
        {
            var guild = context.Server;
            var BypassIP = exemptcheck.Any(x => x.Privacy);

            if (!BypassIP)
            {
                if (Regex.IsMatch(context.Message.Content, @"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$"))
                {
                    await context.Message?.DeleteAsync();
                    var emb = new EmbedBuilder
                    {
                        Title = $"{context.User} - This server does not allow you to post IP addresses"
                    };
                    await context.Channel.SendMessageAsync(string.Empty, false, emb.Build());
                    if (guild.Antispam.Privacy.WarnOnDetection)
                    {
                        await guild.AddWarn("AutoMod - Anti IP", context.User as IGuildUser, context.Client.CurrentUser, context.Channel, context.Message.Content);
                        guild.Save();
                    }

                    return true;
                }
            }

            return false;
        }

        public async Task<bool> CheckBlacklist(LithiumContext context, List<GuildModel.Guild.antispams.IgnoreRole> exemptcheck, CommandInfo CMDCheck)
        {
            var guild = context.Server;
            if (guild.Antispam.Blacklist.BlacklistWordSet.Any())
            {
                if (CMDCheck == null)
                {
                    var BypassBlacklist = exemptcheck.Any(x => x.Blacklist);

                    if (!BypassBlacklist)
                    {
                        var blacklistdetected = false;
                        var blacklistmessage = guild.Antispam.Blacklist.DefaultBlacklistMessage;
                        var detectedblacklistmodule = guild.Antispam.Blacklist.BlacklistWordSet.FirstOrDefault(blist => blist.WordList.Any(x => context.Message.Content.ToLower().Contains(x.ToLower())));
                        if (detectedblacklistmodule != null)
                        {
                            blacklistdetected = true;
                            blacklistmessage = detectedblacklistmodule.BlacklistResponse ?? guild.Antispam.Blacklist.DefaultBlacklistMessage;
                        }

                        if (blacklistdetected)
                        {
                            await context.Message?.DeleteAsync();

                            if (!string.IsNullOrEmpty(blacklistmessage))
                            {
                                var result = Formatting.DoReplacements(blacklistmessage, context);
                                await context.Channel.SendMessageAsync(result);
                            }

                            if (guild.Antispam.Blacklist.WarnOnDetection)
                            {
                                await guild.AddWarn("AutoMod - Blacklist", context.User as IGuildUser, context.Client.CurrentUser, context.Channel, context.Message.Content);
                                guild.Save();
                            }

                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public async Task<bool> CheckToxicity(LithiumContext context, List<GuildModel.Guild.antispams.IgnoreRole> exemptcheck, CommandInfo CMDCheck)
        {
            var guild = context.Server;
            if (guild.Antispam.Toxicity.UsePerspective)
            {
                var BypassToxicity = exemptcheck.Any(x => x.Toxicity);

                if (!BypassToxicity)
                {
                    var CheckUsingToxicity = CMDCheck == null;

                    if (ToxicityAPI != null && CheckUsingToxicity && !string.IsNullOrWhiteSpace(context.Message.Content))
                    {
                        try
                        {
                            var res = ToxicityAPI.QueryToxicity(context.Message.Content);
                            if (res.attributeScores.TOXICITY.summaryScore.value * 100 > guild.Antispam.Toxicity.ToxicityThreshHold)
                            {
                                await context.Message?.DeleteAsync();
                                var emb = new EmbedBuilder
                                {
                                    Title = "Toxicity Threshold Breached",
                                    Description = $"{context.User.Mention}"
                                };
                                await context.Channel.SendMessageAsync(string.Empty, false, emb.Build());
                                if (guild.Antispam.Toxicity.WarnOnDetection)
                                {
                                    await guild.AddWarn($"AutoMod - Toxicity ({res.attributeScores.TOXICITY.summaryScore.value * 100})", context.User as IGuildUser, context.Client.CurrentUser, context.Channel, context.Message.Content);
                                    guild.Save();
                                }

                                return true;
                            }
                        }
                        catch
                        {
                            //
                        }
                    }
                }
            }

            return false;
        }

        public bool CheckHidden(LithiumContext context)
        {
            if (context.Guild == null)
            {
                return false;
            }

            var guild = context.Server;
            if (guild.Settings.DisabledParts.BlacklistedCommands.Any() || guild.Settings.DisabledParts.BlacklistedModules.Any())
            {
                CommandInfo CMDCheck = null;
                const int argPos = 0;
                var cmdSearch = _commands.Search(context, argPos);
                if (cmdSearch.IsSuccess)
                {
                    CMDCheck = cmdSearch.Commands.FirstOrDefault().Command;
                }

                if (CMDCheck != null)
                {
                    var guser = (IGuildUser) context.User;
                    if (!guser.GuildPermissions.Administrator && !guild.ModerationSetup.AdminRoles.Any(x => guser.RoleIds.Contains(x)))
                    {
                        if (guild.Settings.DisabledParts.BlacklistedCommands.Any(x => string.Equals(x, CMDCheck.Name, StringComparison.CurrentCultureIgnoreCase)) ||
                            guild.Settings.DisabledParts.BlacklistedModules.Any(x => string.Equals(x, CMDCheck.Module.Name, StringComparison.CurrentCultureIgnoreCase)))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public async Task<bool> RunSpamChecks(LithiumContext context)
        {
            if (context.Guild == null)
            {
                return false;
            }

            if (context.Channel is IDMChannel)
            {
                return false;
            }

            if (context.Server == null)
            {
                return false;
            }

            try
            {
                var guild = context.Server;

                var exempt_check = new List<GuildModel.Guild.antispams.IgnoreRole>();
                if (guild.Antispam.IgnoreRoles.Any())
                {
                    exempt_check = guild.Antispam.IgnoreRoles.Where(x => ((IGuildUser) context.User).RoleIds.Contains(x.RoleID)).ToList();
                }

                if (guild.Antispam.Antispam.NoSpam)
                {
                    if (await AntiSpam(context, exempt_check))
                    {
                        return true;
                    }
                }

                if (guild.Antispam.Advertising.Invite)
                {
                    if (await AntiInvite(context, exempt_check))
                    {
                        return true;
                    }
                }

                if (guild.Antispam.Mention.RemoveMassMention || guild.Antispam.Mention.MentionAll)
                {
                    if (await AntiMention(context, exempt_check))
                    {
                        return true;
                    }
                }


                if (guild.Antispam.Privacy.RemoveIPs)
                {
                    if (await AntiIp(context, exempt_check))
                    {
                        return true;
                    }
                }


                if (guild.Antispam.Blacklist.BlacklistWordSet.Any() || guild.Antispam.Toxicity.UsePerspective)
                {
                    CommandInfo check = null;
                    var argPos = 0;
                    var cmdSearch = _commands.Search(context, argPos);
                    if (cmdSearch.IsSuccess)
                    {
                        check = cmdSearch.Commands.FirstOrDefault().Command;
                    }

                    if (await CheckBlacklist(context, exempt_check, check))
                    {
                        return true;
                    }

                    if (await CheckToxicity(context, exempt_check, check))
                    {
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogMessage($"AntiSpam Error G:[{context.Guild.Id}] GN:{context.Guild.Name} C:{context.Channel.Name} U:{context.User.Username}\n" +
                                  $"{e}", LogSeverity.Error);
            }


            return false;
        }

        public async Task DoCommand(SocketMessage parameterMessage)
        {
            try
            {
                if (!(parameterMessage is SocketUserMessage message)) return;
                var argPos = 0;
                var context = new LithiumContext(_client, message, Provider);

                //Do not react to commands initiated by a bot
                if (context.User.IsBot) return;

                if (await RunSpamChecks(context)) return;

                //Ensure that commands are only executed if they start with the bot's prefix
                if (!(message.HasMentionPrefix(_client.CurrentUser, ref argPos) ||
                      message.HasStringPrefix(Config.Load().DefaultPrefix, ref argPos) ||
                      context.Server?.Settings.Prefix != null && message.HasStringPrefix(context.Server.Settings.Prefix, ref argPos))) return;

                //Ensure that the message passes all checks before running as a command
                if (CheckHidden(context)) return;

                var result = await _commands.ExecuteAsync(context, argPos, Provider);

                var commandsuccess = result.IsSuccess;

                if (!commandsuccess)
                {
                    var embed = new EmbedBuilder
                    {
                        Title = $"ERROR: {result.Error.ToString().ToUpper()}",
                        Description = $"Command: {context.Message}\n" +
                                      $"Error: {result.ErrorReason}"
                    };
                    await context.Channel.SendMessageAsync(string.Empty, false, embed.Build());
                    Logger.LogMessage($"{message.Content} || {message.Author}", LogSeverity.Error);
                }
                else
                {
                    Logger.LogMessage($"{message.Content} || {message.Author}");
                }
            }
            catch (Exception e)
            {
                Logger.LogMessage(e.ToString(), LogSeverity.Error);
            }
        }

        public async Task ConfigureAsync()
        {
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly());
        }

        public class EventLogDelay
        {
            public ulong GuildID { get; set; }
            public int updates { get; set; } = 0;
            public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
        }


        private class NoSpamGuild
        {
            public ulong GuildID { get; set; }
            public List<NoSpam> Users { get; set; } = new List<NoSpam>();

            public class NoSpam
            {
                public ulong UserID { get; set; }
                public List<Msg> Messages { get; set; } = new List<Msg>();

                public class Msg
                {
                    public string LastMessage { get; set; }
                    public DateTime LastMessageDate { get; set; }
                }
            }
        }

        public class Delays
        {
            public DateTime _delay { get; set; } = DateTime.UtcNow;
            public ulong GuildID { get; set; }
        }
    }
}