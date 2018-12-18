using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Lithium.Models;
using Lithium.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Lithium.Handlers
{
    public class EventHandler
    {
        public List<EventLogDelay> EventLogDelays = new List<EventLogDelay>();

        public IServiceProvider Provider;

        public EventHandler(IServiceProvider provider)
        {
            Provider = provider;
            var client = Provider.GetService<DiscordSocketClient>();


            //Guild Event Logging
            //User
            client.UserJoined += _client_UserJoined;
            client.UserLeft += _client_UserLeft;
            client.UserBanned += _client_UserBanned;
            client.UserUnbanned += _client_UserUnbanned;
            client.GuildMemberUpdated += _client_GuildMemberUpdated;
            //Message
            client.MessageUpdated += _client_MessageUpdated;
            client.MessageDeleted += _client_MessageDeleted;
            //Channel
            client.ChannelCreated += _client_ChannelCreated;
            client.ChannelDestroyed += _client_ChannelDestroyed;
            client.ChannelUpdated += _client_ChannelUpdated;
        }

        public async Task LogEvent(GuildModel.Guild GuildObj, IGuild Guild, EmbedBuilder embed)
        {
            if (EventLogDelays.All(x => x.GuildID != Guild.Id))
            {
                EventLogDelays.Add(new EventLogDelay
                {
                    GuildID = Guild.Id,
                    LastUpdate = DateTime.UtcNow,
                    Updates = 0
                });
            }
            else
            {
                var gdelays = EventLogDelays.First(x => x.GuildID == Guild.Id);
                //Ensure that we are only logging 1 event per second (to reduce lag from the bot and overall spam)
                if (gdelays.LastUpdate + TimeSpan.FromSeconds(5) >= DateTime.UtcNow)
                {
                    gdelays.Updates++;
                }
                else
                {
                    gdelays.LastUpdate = DateTime.UtcNow;
                    gdelays.Updates = 0;
                }

                if (gdelays.Updates >= 3 && gdelays.LastUpdate + TimeSpan.FromSeconds(5) > DateTime.UtcNow)
                {
                    Logger.LogMessage($"RateLimiting Events in {Guild.Name}", LogSeverity.Verbose);
                    return;
                }

                await GuildObj.EventLog(embed, Guild);
            }
        }

        public class EventLogDelay
        {
            public ulong GuildID { get; set; }
            public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
            public int Updates { get; set; }
        }

        #region EventChannelLogging

        private async Task _client_GuildMemberUpdated(SocketGuildUser UserBefore, SocketGuildUser UserAfter)
        {
            var logmsg = "";
            if (UserBefore.Nickname != UserAfter.Nickname)
            {
                logmsg += "__**NickName Updated**__\n" +
                          $"OLD: {UserBefore.Nickname ?? UserBefore.Username}\n" +
                          $"AFTER: {UserAfter.Nickname ?? UserAfter.Username}\n";
            }

            if (UserBefore.Roles.Count < UserAfter.Roles.Count)
            {
                var result = UserAfter.Roles.Where(b => UserBefore.Roles.All(a => b.Id != a.Id)).ToList();
                logmsg += "__**Role Added**__\n" +
                          $"{result[0].Name}\n";
            }
            else if (UserBefore.Roles.Count > UserAfter.Roles.Count)
            {
                var result = UserBefore.Roles.Where(b => UserAfter.Roles.All(a => b.Id != a.Id)).ToList();
                logmsg += "__**Role Removed**__\n" +
                          $"{result[0].Name}\n";
            }

            if (logmsg == "") return;
            var GuildConfig = DatabaseHandler.GetGuild(UserAfter.Guild.Id);
            if (!GuildConfig.EventLogger.Settings.guildmemberupdated) return;
            if (GuildConfig.EventLogger.LogEvents)
            {
                var embed = new EmbedBuilder
                {
                    Title = "User Updated",
                    Description = $"**User:** {UserAfter.Mention}\n" +
                                  $"**ID:** {UserAfter.Id}\n\n" + logmsg,
                    ThumbnailUrl = UserAfter.GetAvatarUrl(),
                    Footer = new EmbedFooterBuilder
                    {
                        Text = $"{DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)} UTC"
                    },
                    Color = Color.Blue
                };
                //await GuildConfig.EventLog(embed, UserAfter.Guild);
                await LogEvent(GuildConfig, UserAfter.Guild, embed);
            }
        }

        private async Task _client_MessageUpdated(Cacheable<IMessage, ulong> messageOld, SocketMessage messageNew, ISocketMessageChannel cchannel)
        {
            if (messageNew.Author.IsBot)
                return;

            if (string.Equals(messageOld.Value.Content, messageNew.Content, StringComparison.CurrentCultureIgnoreCase))
                return;

            if (messageOld.Value?.Embeds.Count > 0 || messageNew.Embeds.Count > 0)
                return;

            var guild = ((SocketGuildChannel) cchannel).Guild;

            var guildobj = DatabaseHandler.GetGuild(guild.Id);
            if (!guildobj.EventLogger.Settings.messageupdated) return;
            if (guildobj.EventLogger.LogEvents)
            {
                var embed = new EmbedBuilder
                {
                    Title = "Message Updated",
                    ThumbnailUrl = messageNew.Author.GetAvatarUrl(),
                    Footer = new EmbedFooterBuilder
                    {
                        Text = $"{DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)} UTC TIME"
                    },
                    Color = Color.Blue
                };
                embed.AddField("Old Message:", $"{messageOld.Value.Content}");
                embed.AddField("New Message:", $"{messageNew.Content}");
                embed.AddField("Info",
                    $"**Author:** {messageNew.Author.Username}\n" +
                    $"**Author ID:** {messageNew.Author.Id}\n" +
                    $"**Channel:** {messageNew.Channel.Name}\n" +
                    $"**Embeds:** {messageNew.Embeds.Any()}");

                //await guildobj.EventLog(embed, guild);
                await LogEvent(guildobj, guild, embed);
            }
        }

        private async Task _client_ChannelUpdated(SocketChannel s1, SocketChannel s2)
        {
            var ChannelBefore = s1 as SocketGuildChannel;
            var ChannelAfter = s2 as SocketGuildChannel;
            var guildobj = DatabaseHandler.GetGuild(ChannelAfter.Guild.Id);
            if (!guildobj.EventLogger.Settings.channelupdated) return;
            if (guildobj.EventLogger.LogEvents)
            {
                if (ChannelBefore.Position != ChannelAfter.Position)
                    return;
                var embed = new EmbedBuilder
                {
                    Title = "Channel Updated",
                    Description = ChannelAfter.Name,
                    Color = Color.Blue,
                    Footer = new EmbedFooterBuilder
                    {
                        Text = $"{DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)} UTC TIME"
                    }
                };
                //await guildobj.EventLog(embed, ChannelAfter.Guild);
                await LogEvent(guildobj, ChannelAfter.Guild, embed);
            }
        }

        private async Task _client_ChannelDestroyed(SocketChannel sChannel)
        {
            var guild = ((SocketGuildChannel) sChannel).Guild;
            var guildobj = DatabaseHandler.GetGuild(guild.Id);
            if (!guildobj.EventLogger.Settings.channeldeleted) return;
            if (guildobj.EventLogger.LogEvents)
            {
                var embed = new EmbedBuilder
                {
                    Title = "Channel Deleted",
                    Description = ((SocketGuildChannel) sChannel)?.Name,
                    Color = Color.DarkTeal,
                    Footer = new EmbedFooterBuilder
                    {
                        Text = $"{DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)} UTC TIME"
                    }
                };
                //await guildobj.EventLog(embed, guild);
                await LogEvent(guildobj, guild, embed);
            }
        }

        private async Task _client_ChannelCreated(SocketChannel sChannel)
        {
            var guild = ((SocketGuildChannel) sChannel).Guild;
            var guildobj = DatabaseHandler.GetGuild(guild.Id);
            if (!guildobj.EventLogger.Settings.channelcreated) return;
            if (guildobj.EventLogger.LogEvents)
            {
                var embed = new EmbedBuilder
                {
                    Title = "Channel Created",
                    Description = ((SocketGuildChannel) sChannel)?.Name,
                    Color = Color.Green,
                    Footer = new EmbedFooterBuilder
                    {
                        Text = $"{DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)} UTC TIME"
                    }
                };
                //await guildobj.EventLog(embed, guild);
                await LogEvent(guildobj, guild, embed);
            }
        }

        private async Task _client_MessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
        {
            var guild = ((SocketGuildChannel) channel).Guild;
            var guildobj = DatabaseHandler.GetGuild(guild.Id);
            if (!guildobj.EventLogger.Settings.messagedeleted) return;
            if (guildobj.EventLogger.LogEvents)
            {
                var embed = new EmbedBuilder();
                try
                {
                    embed.AddField("Message Deleted", $"Message: {message.Value.Content}\n" +
                                                      $"Author: {message.Value.Author}\n" +
                                                      $"Channel: {channel.Name}");
                }
                catch
                {
                    embed.AddField("Message Deleted", "Message was unable to be retrieved\n" +
                                                      $"Channel: {channel.Name}");
                }

                embed.WithFooter(x => { x.WithText($"{DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)} UTC TIME"); });
                embed.Color = Color.DarkTeal;

                //await guildobj.EventLog(embed, guild);
                await LogEvent(guildobj, guild, embed);
            }
        }

        private async Task _client_UserUnbanned(SocketUser User, SocketGuild Guild)
        {
            var guildobj = DatabaseHandler.GetGuild(Guild.Id);
            if (!guildobj.EventLogger.Settings.guilduserunbanned) return;
            if (guildobj.EventLogger.LogEvents)
            {
                var embed = new EmbedBuilder
                {
                    Title = "User UnBanned",
                    ThumbnailUrl = User.GetAvatarUrl(),
                    Description = $"**Username:** {User.Username}",
                    Color = Color.DarkTeal,
                    Footer = new EmbedFooterBuilder
                    {
                        Text = $"{DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)} UTC TIME"
                    }
                };
                //await guildobj.EventLog(embed, Guild);
                await LogEvent(guildobj, Guild, embed);
            }
        }

        private async Task _client_UserBanned(SocketUser User, SocketGuild Guild)
        {
            var guildobj = DatabaseHandler.GetGuild(Guild.Id);
            if (!guildobj.EventLogger.Settings.guilduserbanned) return;
            if (guildobj.EventLogger.LogEvents)
            {
                var embed = new EmbedBuilder
                {
                    Title = "User Banned",
                    ThumbnailUrl = User.GetAvatarUrl(),
                    Description = $"**Username:** {User.Username}",
                    Color = Color.DarkRed,
                    Footer = new EmbedFooterBuilder
                    {
                        Text = $"{DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)} UTC TIME"
                    }
                };
                //await guildobj.EventLog(embed, Guild);
                await LogEvent(guildobj, Guild, embed);
            }
        }

        private async Task _client_UserLeft(SocketGuildUser user)
        {
            var guildobj = DatabaseHandler.GetGuild(user.Guild.Id);
            if (!guildobj.EventLogger.Settings.guilduserleft) return;
            if (guildobj.EventLogger.LogEvents)
            {
                var embed = new EmbedBuilder
                {
                    Title = "User Left",
                    Description = $"{user.Mention} {user.Username}#{user.Discriminator}\n" +
                                  $"ID: {user.Id}",
                    ThumbnailUrl = user.GetAvatarUrl(),
                    Color = Color.Red,
                    Footer = new EmbedFooterBuilder
                        {Text = $"{DateTime.UtcNow} UTC TIME"}
                };
                //await guildobj.EventLog(embed, user.Guild);
                await LogEvent(guildobj, user.Guild, embed);
            }
        }

        private async Task _client_UserJoined(SocketGuildUser user)
        {
            var guildobj = DatabaseHandler.GetGuild(user.Guild.Id);
            if (!guildobj.EventLogger.Settings.guilduserjoined) return;
            if (guildobj.EventLogger.LogEvents)
            {
                var embed = new EmbedBuilder
                {
                    Title = "User Joined",
                    Description = $"{user.Mention} {user.Username}#{user.Discriminator}\n" +
                                  $"ID: {user.Id}",
                    ThumbnailUrl = user.GetAvatarUrl(),
                    Color = Color.Green,
                    Footer = new EmbedFooterBuilder
                        {Text = $"{DateTime.UtcNow} UTC TIME"}
                };
                //await guildobj.EventLog(embed, user.Guild);
                await LogEvent(guildobj, user.Guild, embed);
            }
        }

        #endregion
    }
}