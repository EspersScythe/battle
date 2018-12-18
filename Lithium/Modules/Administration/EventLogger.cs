using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Lithium.Discord.Contexts;
using Lithium.Discord.Preconditions;

namespace Lithium.Modules.Administration
{
    [RequireRole.RequireAdmin]
    [Group("Event")]
    public class EventLogger : Base
    {
        [Command("SetChannel")]
        [Summary("Event SetChannel")]
        [Remarks("set the current channel for event logging")]
        public async Task EventChannel()
        {
            Context.Server.EventLogger.EventChannel = Context.Channel.Id;
            Context.Server.EventLogger.LogEvents = true;
            Context.Server.Save();
            await ReplyAsync($"Success! Events will now be logged in {Context.Channel.Name}");
        }

        [Command("ToggleLog")]
        [Summary("Event ToggleLog")]
        [Remarks("toggle event logging")]
        public async Task LogEventToggle()
        {
            Context.Server.EventLogger.LogEvents = !Context.Server.EventLogger.LogEvents;
            Context.Server.Save();
            await ReplyAsync($"EventLogging: {Context.Server.EventLogger.LogEvents}");
        }

        [Command("ChannelCreated")]
        [Summary("Event ChannelCreated")]
        [Remarks("toggle ChannelCreated event logging")]
        public async Task ChannelCreated()
        {
            Context.Server.EventLogger.Settings.channelcreated = !Context.Server.EventLogger.Settings.channelcreated;
            Context.Server.Save();
            await ReplyAsync($"Channel Created: {Context.Server.EventLogger.Settings.channelcreated}");
        }

        [Command("ChannelDeleted")]
        [Summary("Event ChannelDeleted")]
        [Remarks("toggle ChannelDeleted event logging")]
        public async Task ChannelDeleted()
        {
            Context.Server.EventLogger.Settings.channeldeleted = !Context.Server.EventLogger.Settings.channeldeleted;
            Context.Server.Save();
            await ReplyAsync($"Channel Deleted: {Context.Server.EventLogger.Settings.channeldeleted}");
        }

        [Command("ChannelUpdated")]
        [Summary("Event ChannelUpdated")]
        [Remarks("toggle ChannelUpdated event logging")]
        public async Task ChannelUpdated()
        {
            Context.Server.EventLogger.Settings.channelupdated = !Context.Server.EventLogger.Settings.channelupdated;
            Context.Server.Save();
            await ReplyAsync($"Channel Updated: {Context.Server.EventLogger.Settings.channelupdated}");
        }

        [Command("UserJoined")]
        [Summary("Event UserJoined")]
        [Remarks("toggle UserJoined event logging")]
        public async Task UserJoined()
        {
            Context.Server.EventLogger.Settings.guilduserjoined = !Context.Server.EventLogger.Settings.guilduserjoined;
            Context.Server.Save();
            await ReplyAsync($"User Joined: {Context.Server.EventLogger.Settings.guilduserjoined}");
        }

        [Command("UserLeft")]
        [Summary("Event UserLeft")]
        [Remarks("toggle UserLeft event logging")]
        public async Task UserLeft()
        {
            Context.Server.EventLogger.Settings.guilduserleft = !Context.Server.EventLogger.Settings.guilduserleft;
            Context.Server.Save();
            await ReplyAsync($"User Left: {Context.Server.EventLogger.Settings.guilduserleft}");
        }

        [Command("UserUpdated")]
        [Summary("Event UserUpdated")]
        [Remarks("toggle UserUpdated event logging")]
        public async Task UserUpdated()
        {
            Context.Server.EventLogger.Settings.guildmemberupdated = !Context.Server.EventLogger.Settings.guildmemberupdated;
            Context.Server.Save();
            await ReplyAsync($"User Updated: {Context.Server.EventLogger.Settings.guildmemberupdated}");
        }

        [Command("MessageDeleted")]
        [Summary("Event MessageDeleted")]
        [Remarks("toggle MessageDeleted event logging")]
        public async Task MessageDeleted()
        {
            Context.Server.EventLogger.Settings.messagedeleted = !Context.Server.EventLogger.Settings.messagedeleted;
            Context.Server.Save();
            await ReplyAsync($"Message Deleted: {Context.Server.EventLogger.Settings.messagedeleted}");
        }

        [Command("MessageUpdated")]
        [Summary("Event MessageUpdated")]
        [Remarks("toggle MessageUpdated event logging")]
        public async Task MessageUpdated()
        {
            Context.Server.EventLogger.Settings.messageupdated = !Context.Server.EventLogger.Settings.messageupdated;
            Context.Server.Save();
            await ReplyAsync($"Message Updated: {Context.Server.EventLogger.Settings.messageupdated}");
        }

        [Command("ViewConfig")]
        [Summary("Event ViewConfig")]
        [Remarks("View the event logging config")]
        public async Task LogEventConfig()
        {
            var g = Context.Server.EventLogger.Settings;
            var embed = new EmbedBuilder
            {
                Description = $"User Updated: {g.guildmemberupdated}\n" +
                              $"User Joined: {g.guilduserjoined}\n" +
                              $"User Left: {g.guilduserleft}\n" +
                              $"User Banned: {g.guilduserbanned}\n" +
                              $"User UnBanned: {g.guilduserunbanned}\n" +
                              $"Channel Created: {g.channelcreated}\n" +
                              $"Channel Deleted: {g.channeldeleted}\n" +
                              $"Channel Updated: {g.channelupdated}\n" +
                              $"Message Updated: {g.messageupdated}\n" +
                              $"Message Deleted: {g.messagedeleted}",
                Color = Color.Blue,
                Title = "Event Config"
            };
            await SendEmbedAsync(embed);
        }
    }
}