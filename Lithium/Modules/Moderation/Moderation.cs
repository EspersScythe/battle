using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Lithium.Discord.Contexts;
using Lithium.Discord.Extensions;
using Lithium.Discord.Preconditions;
using Lithium.Models;

namespace Lithium.Modules.Moderation
{
    [RequireRole.RequireModerator]
    [Group("Mod")]
    public class Moderation : Base
    {
        [Command("UnMute")]
        [Summary("Mod UnMute <@user>")]
        [Remarks("Unmute the specified user")]
        public async Task UnMute(IGuildUser user)
        {
            var mutedrole = Context.Guild.GetRole(Context.Server.ModerationSetup.Mutes.mutedrole);
            if (mutedrole == null)
            {
                throw new Exception("Muted Role has not been configured in this server");
            }

            if (Permissions.CheckHeirachy(user, Context.Client))
            {
                throw new Exception("This user has higher permissions than me. I cannot perform this action on them");
            }

            if (user.GuildPermissions.Administrator)
            {
                throw new Exception("This user has admin or user kick permissions, therefore I cannot perform this action on them");
            }

            if (user.RoleIds.Contains(mutedrole.Id))
            {
                await user.RemoveRoleAsync(mutedrole);
                Context.Server.ModerationSetup.Mutes.MutedUsers.Remove(Context.Server.ModerationSetup.Mutes.MutedUsers.FirstOrDefault(x => x.userid == user.Id));
                await ReplyAsync("User Unmuted");
            }
            else
            {
                throw new Exception("User is not muted");
            }

            Context.Server.Save();
        }

        [Command("Mute")]
        [Summary("Mod Mute <@user> <hours>")]
        [Remarks("Warn the specified user")]
        public async Task MuteUser(IGuildUser user, int hours = -1)
        {
            var mutedrole = Context.Guild.GetRole(Context.Server.ModerationSetup.Mutes.mutedrole);
            if (mutedrole == null)
            {
                throw new Exception("Muted Role has not been configured in this server");
            }

            if (Permissions.CheckHeirachy(user, Context.Client))
            {
                throw new Exception("This user has higher permissions than me. I cannot perform this action on them");
            }

            if (user.GuildPermissions.Administrator)
            {
                throw new Exception("This user has admin or user kick permissions, therefore I cannot perform this action on them");
            }

            if (!user.RoleIds.Contains(mutedrole.Id))
            {
                await user.AddRoleAsync(mutedrole);
                Context.Server.ModerationSetup.Mutes.MutedUsers.Add(new GuildModel.Guild.Moderation.muted.muteduser
                {
                    expires = hours != -1,
                    expiry = DateTime.UtcNow + TimeSpan.FromHours(hours),
                    userid = user.Id
                });
                await ReplyAsync($"User Muted for {hours} hours (-1 is unlimited)");
            }
            else
            {
                await user.RemoveRoleAsync(mutedrole);
                Context.Server.ModerationSetup.Mutes.MutedUsers.Remove(Context.Server.ModerationSetup.Mutes.MutedUsers.FirstOrDefault(x => x.userid == user.Id));
                await ReplyAsync("User Unmuted");
            }

            Context.Server.Save();
        }

        [Command("Warn")]
        [Summary("Mod Warn <@user> <reason>")]
        [Remarks("Warn the specified user")]
        public async Task WarnUser(IGuildUser user, [Remainder] string reason = null)
        {
            await Context.Server.AddWarn(reason, user, Context.User, Context.Channel);
            Context.Server.Save();
        }

        [Command("Kick")]
        [Summary("Mod Kick <@user> <reason>")]
        [Remarks("Kick the specified user")]
        public async Task KickUser(IGuildUser user, [Remainder] string reason = null)
        {
            if (Permissions.CheckHeirachy(user, Context.Client))
            {
                throw new Exception("This user has higher permissions than me. I cannot perform this action on them");
            }

            if (user.GuildPermissions.Administrator || user.GuildPermissions.KickMembers)
            {
                throw new Exception("This user has admin or user kick permissions, therefore I cannot perform this action on them");
            }

            Context.Server.ModerationSetup.Kicks.Add(new GuildModel.Guild.Moderation.kick
            {
                userID = user.Id,
                modID = Context.User.Id,
                modname = Context.User.Username,
                reason = reason,
                username = user.Username
            });
            try
            {
                await user.KickAsync(reason);
                await ReplyAsync("User has been Kicked");
                Context.Server.Save();
                await SendEmbedAsync(new EmbedBuilder
                {
                    Title = $"{user.Username} has been kicked",
                    Description = $"User: {user.Username}#{user.Discriminator}\n" +
                                  $"UserID: {user.Id}\n" +
                                  $"Mod: {Context.User.Username}#{Context.User.Discriminator}\n" +
                                  $"Mod ID: {Context.User.Id}\n\n" +
                                  "Reason:\n" +
                                  $"{reason ?? "N/A"}"
                });
            }
            catch
            {
                throw new Exception("User is unable to be kicked. ");
            }
        }

        [Command("Ban")]
        [Summary("Mod Ban <@user> <reason>")]
        [Remarks("Ban the specified user")]
        public async Task BanUser(IGuildUser user, [Remainder] string reason = null)
        {
            if (Permissions.CheckHeirachy(user, Context.Client))
            {
                throw new Exception("This user has higher permissions than me. I cannot perform this action on them");
            }

            if (user.GuildPermissions.Administrator || user.GuildPermissions.BanMembers)
            {
                throw new Exception("This user has admin or user ban permissions, therefore I cannot perform this action on them");
            }

            Context.Server.ModerationSetup.Bans.Add(new GuildModel.Guild.Moderation.ban
            {
                userID = user.Id,
                modID = Context.User.Id,
                modname = Context.User.Username,
                reason = reason,
                username = user.Username
            });
            try
            {
                await Context.Guild.AddBanAsync(user, 1, reason);
                await ReplyAsync("User has been Banned and messages from the last 24 hours have been cleared");
                Context.Server.Save();
                await SendEmbedAsync(new EmbedBuilder
                {
                    Title = $"{user.Username} has been banned",
                    Description = $"User: {user.Username}#{user.Discriminator}\n" +
                                  $"UserID: {user.Id}\n" +
                                  $"Mod: {Context.User.Username}#{Context.User.Discriminator}\n" +
                                  $"Mod ID: {Context.User.Id}\n\n" +
                                  "Reason:\n" +
                                  $"{reason ?? "N/A"}"
                });
            }
            catch
            {
                await ReplyAsync("User is unable to be Banned. ");
            }
        }

        [Command("SoftBan")]
        [Summary("Mod SoftBan <@user> <hours> <reason>")]
        [Remarks("Ban the specified user for the specified amount of hours")]
        public async Task SoftBanUser(IGuildUser user, int hours, [Remainder] string reason = null)
        {
            if (Permissions.CheckHeirachy(user, Context.Client))
            {
                throw new Exception("This user has higher permissions than me. I cannot perform this action on them");
            }

            if (user.GuildPermissions.Administrator || user.GuildPermissions.BanMembers)
            {
                throw new Exception("This user has admin or user ban permissions, therefore I cannot perform this action on them");
            }

            Context.Server.ModerationSetup.Bans.Add(new GuildModel.Guild.Moderation.ban
            {
                userID = user.Id,
                modID = Context.User.Id,
                modname = Context.User.Username,
                reason = reason,
                username = user.Username,
                ExpiryDate = DateTime.UtcNow + TimeSpan.FromHours(hours),
                Expires = true
            });
            try
            {
                await Context.Guild.AddBanAsync(user, 1, reason);
                await ReplyAsync("User has been Soft Banned and messages from the last 24 hours have been cleared");
                Context.Server.Save();
                await SendEmbedAsync(new EmbedBuilder
                {
                    Title = $"{user.Username} has been soft banned",
                    Description = $"User: {user.Username}#{user.Discriminator}\n" +
                                  $"UserID: {user.Id}\n" +
                                  $"Mod: {Context.User.Username}#{Context.User.Discriminator}\n" +
                                  $"Mod ID: {Context.User.Id}\n" +
                                  $"Expires After: {hours} hours\n\n" +
                                  "Reason:\n" +
                                  $"{reason ?? "N/A"}"
                });
            }
            catch
            {
                throw new Exception("User is unable to be Banned. ");
            }
        }


        [Command("Warns")]
        [Summary("Mod Warns")]
        [Remarks("List all logged server warnings")]
        public async Task Warns()
        {
            var pages = new List<PaginatedMessage.Page>();
            var desc = "";
            foreach (var warngroup in Context.Server.ModerationSetup.Warns.GroupBy(x => x.userID).ToList())
            {
                var first = warngroup.FirstOrDefault();
                var user = await Context.Client.GetUserAsync(first.userID);

                var dstr = $"**{user?.Username ?? first.username} `[{first.userID}]`**\n";
                foreach (var warn in warngroup)
                {
                    dstr += $"Warned By: {(await Context.Client.GetUserAsync(first.modID))?.Username ?? warn.modname} `[{warn.modID}]`\n" +
                            $"Reason: {warn.reason}\n";
                }

                dstr += $"-Count: {warngroup.Count()}\n";

                if (desc.Length + dstr.Length > 1024)
                {
                    pages.Add(new PaginatedMessage.Page
                    {
                        Description = desc
                    });
                    desc = dstr;
                }
                else
                {
                    desc += dstr;
                }
            }

            pages.Add(new PaginatedMessage.Page
            {
                Description = desc
            });
            var pager = new PaginatedMessage
            {
                Pages = pages,
                Title = "Warnings",
                Color = Color.DarkPurple
            };
            await PagedReplyAsync(pager, new ReactionList
            {
                Forward = true,
                Backward = true,
                Trash = true
            });
        }

        [Command("Kicks")]
        [Summary("Mod Kicks")]
        [Remarks("List all logged server Kicks")]
        public async Task Kicks()
        {
            var pages = new List<PaginatedMessage.Page>();
            var desc = "";
            foreach (var kickgroup in Context.Server.ModerationSetup.Kicks.GroupBy(x => x.userID).ToList())
            {
                var first = kickgroup.FirstOrDefault();
                var user = await Context.Client.GetUserAsync(first.userID);

                var dstr = $"**{user?.Username ?? first.username} `[{first.userID}]`**\n";
                foreach (var kick in kickgroup)
                {
                    dstr += $"Kicked By: {(await Context.Client.GetUserAsync(first.modID))?.Username ?? kick.modname} `[{kick.modID}]`\n" +
                            $"Reason: {kick.reason}\n";
                }

                dstr += $"-Count: {kickgroup.Count()}\n";
                if (desc.Length + dstr.Length > 1024)
                {
                    pages.Add(new PaginatedMessage.Page
                    {
                        Description = desc
                    });
                    desc = dstr;
                }
                else
                {
                    desc += dstr;
                }
            }

            pages.Add(new PaginatedMessage.Page
            {
                Description = desc
            });
            var pager = new PaginatedMessage
            {
                Pages = pages,
                Title = "Kicks",
                Color = Color.DarkMagenta
            };
            await PagedReplyAsync(pager, new ReactionList
            {
                Forward = true,
                Backward = true,
                Trash = true
            });
        }

        [Command("Bans")]
        [Summary("Mod Bans")]
        [Remarks("List all logged server Bans")]
        public async Task Bans()
        {
            var pages = new List<PaginatedMessage.Page>();
            var desc = "";
            foreach (var bangroup in Context.Server.ModerationSetup.Bans.GroupBy(x => x.userID).ToList())
            {
                var first = bangroup.FirstOrDefault();
                var user = await Context.Client.GetUserAsync(first.userID);

                var dstr = $"**{user?.Username ?? first.username} `[{first.userID}]`**\n";
                foreach (var ban in bangroup)
                {
                    dstr += $"Banned By: {(await Context.Client.GetUserAsync(first.modID))?.Username ?? ban.modname} `[{ban.modID}]`\n" +
                            $"Ban Type: {(ban.Expires ? $"Soft Ban, {(ban.ExpiryDate - DateTime.UtcNow).TotalMinutes} Minutes Remaining" : "Permanent")}\n" +
                            $"Reason: {ban.reason}\n";
                }

                if (desc.Length + dstr.Length > 1024)
                {
                    pages.Add(new PaginatedMessage.Page
                    {
                        Description = desc
                    });
                    desc = dstr;
                }
                else
                {
                    desc += dstr;
                }

            }

            pages.Add(new PaginatedMessage.Page
            {
                Description = desc
            });
            var pager = new PaginatedMessage
            {
                Pages = pages,
                Title = "Bans",
                Color = Color.DarkRed
            };
            await PagedReplyAsync(pager, new ReactionList
            {
                Forward = true,
                Backward = true,
                Trash = true
            });
        }

        public List<IMessage> GetmessagesAsync(int count = 100)
        {
            var msgs = Context.Channel.GetMessagesAsync(count).Flatten();
            return msgs.Where(x => x.Timestamp.UtcDateTime + TimeSpan.FromDays(14) > DateTime.UtcNow).ToList().Result;
        }

        [Command("prune")]
        [Alias("purge", "clear")]
        [Summary("Mod Prune <no. of messages>")]
        [Remarks("removes specified amount of messages")]
        public async Task Prune(int count = 100)
        {
            if (count < 1)
            {
                await ReplyAsync("**ERROR: **Please Specify the amount of messages you want to clear");
            }
            else if (count > 100)
            {
                await ReplyAsync("**Error: **I can only clear 100 Messages at a time!");
            }
            else
            {
                await Context.Message.DeleteAsync().ConfigureAwait(false);
                var limit = count < 100 ? count : 100;
                //var enumerable = await Context.Channel.GetMessagesAsync(limit).Flatten().ConfigureAwait(false);
                var enumerable = GetmessagesAsync(limit);
                try
                {
                    await (Context.Channel as ITextChannel).DeleteMessagesAsync(enumerable).ConfigureAwait(false);
                }
                catch
                {
                    //
                }

                await ReplyAsync($"Cleared **{enumerable.Count}** Messages");

                await Context.Server.ModLog(new EmbedBuilder()
                    .WithColor(Color.DarkTeal)
                    .AddField("Pruned Messages",
                        $"{count} messages cleared")
                    .AddField("Moderator",
                        $"Mod: {Context.User.Username}\n" +
                        $"Mod Nick: {((IGuildUser) Context.User)?.Nickname ?? "N/A"}\n" +
                        $"Channel: {Context.Channel.Name}")
                    .WithCurrentTimestamp(), Context.Guild);
            }
        }

        [Command("prune")]
        [Alias("purge", "clear")]
        [Summary("Mod Prune <user>")]
        [Remarks("removes messages from a user in the last 100 messages")]
        public async Task Prune(IUser user)
        {
            await Context.Message.DeleteAsync().ConfigureAwait(false);
            //var enumerable = await Context.Channel.GetMessagesAsync().Flatten().ConfigureAwait(false);
            var enumerable = GetmessagesAsync();
            var newlist = enumerable.Where(x => x.Author == user).ToList();
            try
            {
                await (Context.Channel as ITextChannel).DeleteMessagesAsync(newlist).ConfigureAwait(false);
            }
            catch
            {
                //
            }

            await ReplyAsync($"Cleared **{user.Username}'s** Messages (Count = {newlist.Count})");

            await Context.Server.ModLog(new EmbedBuilder()
                .WithColor(Color.DarkTeal)
                .AddField($"Pruned Messages from {user.Username}",
                    $"{newlist.Count} messages cleared")
                .AddField("Moderator",
                    $"Mod: {Context.User.Username}\n" +
                    $"Mod Nick: {((IGuildUser) Context.User)?.Nickname ?? "N/A"}\n" +
                    $"Channel: {Context.Channel.Name}")
                .WithCurrentTimestamp(), Context.Guild);
        }


        [Command("pruneID")]
        [Summary("Mod pruneID <userID>")]
        [Remarks("removes messages from a user ID in the last 100 messages")]
        public async Task Prune(ulong userID)
        {
            await Context.Message.DeleteAsync().ConfigureAwait(false);
            var enumerable = GetmessagesAsync();
            var newlist = enumerable.Where(x => x.Author.Id == userID).ToList();
            try
            {
                await (Context.Channel as ITextChannel).DeleteMessagesAsync(newlist).ConfigureAwait(false);
            }
            catch
            {
                //
            }

            await ReplyAsync($"Cleared Messages (Count = {newlist.Count})");

            await Context.Server.ModLog(new EmbedBuilder()
                .WithColor(Color.DarkTeal)
                .AddField($"Pruned Messages from ID: {userID}",
                    $"{newlist.Count} messages cleared")
                .AddField("Moderator",
                    $"Mod: {Context.User.Username}\n" +
                    $"Mod Nick: {((IGuildUser) Context.User)?.Nickname ?? "N/A"}\n" +
                    $"Channel: {Context.Channel.Name}")
                .WithCurrentTimestamp(), Context.Guild);
        }

        [Command("prune")]
        [Alias("purge", "clear")]
        [Summary("Mod Prune <@role>")]
        [Remarks("removes messages from a role in the last 100 messages")]
        public async Task Prune(IRole role)
        {
            await Context.Message.DeleteAsync().ConfigureAwait(false);
            var enumerable = GetmessagesAsync();
            var newerlist = enumerable.ToList().Where(x =>
                Context.Socket.Guild.GetUser(x.Author.Id) != null &&
                ((IGuildUser) Context.Socket.Guild.GetUser(x.Author.Id)).RoleIds.Contains(role.Id)).ToList();

            try
            {
                await (Context.Channel as ITextChannel).DeleteMessagesAsync(newerlist).ConfigureAwait(false);
            }
            catch
            {
                //
            }

            await ReplyAsync($"Cleared Messages (Count = {newerlist.Count})");

            await Context.Server.ModLog(new EmbedBuilder()
                .WithColor(Color.DarkTeal)
                .AddField($"Pruned Messages from Role: {role.Name}",
                    $"{newerlist.Count} messages cleared")
                .AddField("Moderator",
                    $"Mod: {Context.User.Username}\n" +
                    $"Mod Nick: {((IGuildUser) Context.User)?.Nickname ?? "N/A"}\n" +
                    $"Channel: {Context.Channel.Name}")
                .WithCurrentTimestamp(), Context.Guild);
        }
    }
}