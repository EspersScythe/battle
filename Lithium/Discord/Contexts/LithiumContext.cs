using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Lithium.Handlers;
using Lithium.Models;
using Microsoft.Extensions.DependencyInjection;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;


namespace Lithium.Discord.Contexts
{
    public abstract class Base : ModuleBase<LithiumContext>
    {
        public InteractiveService Interactive { get; set; }

        /// <summary>
        ///     Reply in the server. This is a shortcut for context.channel.sendmessageasync
        /// </summary>
        public async Task<IUserMessage> ReplyAsync(string Message, Embed Embed = null)
        {
            await Context.Channel.TriggerTypingAsync();
            return await base.ReplyAsync(Message, false, Embed);
        }
        /// <summary>
        ///     Reply in the server. This is a shortcut for context.channel.sendmessageasync
        /// </summary>
        public async Task<IUserMessage> ReplyAsync(Embed Embed)
        {
            await Context.Channel.TriggerTypingAsync();
            return await base.ReplyAsync("", false, Embed);
        }
        /// <summary>
        ///     Reply in the server. This is a shortcut for context.channel.sendmessageasync
        /// </summary>
        public async Task<IUserMessage> ReplyAsync(EmbedBuilder Embed)
        {
            await Context.Channel.TriggerTypingAsync();
            return await base.ReplyAsync("", false, Embed.Build());
        }
        /// <summary>
        ///     Reply in the server and then delete after the provided delay.
        /// </summary>
        public async Task<IUserMessage> ReplyAndDeleteAsync(string Message, TimeSpan? Timeout = null)
        {
            return await Interactive.ReplyAndDeleteAsync(LithiumSocketContext(), Message, false, null, Timeout);
        }

        /// <summary>
        ///     Shorthand for  replying with just an embed
        /// </summary>
        public async Task<IUserMessage> SendEmbedAsync(EmbedBuilder embed)
        {
            return await base.ReplyAsync("", false, embed.Build());
        }

        public async Task<IUserMessage> SendEmbedAsync(Embed embed)
        {
            return await base.ReplyAsync("", false, embed);
        }

        /// <summary>
        ///     Will wait for the next message to be sent
        /// </summary>
        /// <param name="criterion"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public Task<SocketMessage> NextMessageAsync(ICriterion<SocketMessage> criterion, TimeSpan? timeout = null)
        {
            return Interactive.NextMessageAsync(LithiumSocketContext(), criterion, timeout);
        }

        public Task<SocketMessage> NextMessageAsync(bool fromSourceUser = true, bool inSourceChannel = true, TimeSpan? timeout = null)
        {
            return Interactive.NextMessageAsync(LithiumSocketContext(), fromSourceUser, inSourceChannel, timeout);
        }

        /// <summary>
        ///     Converts LithiumContext into SocketCommandContext, though most of this is accessible through Context.Socket
        /// </summary>
        /// <returns></returns>
        private SocketCommandContext LithiumSocketContext()
        {
            return new SocketCommandContext(Context.Client as DiscordSocketClient, Context.Message as SocketUserMessage);
        }

        /// <summary>
        ///     creates a new paginated message
        /// </summary>
        /// <param name="pager"></param>
        /// <param name="reactionList"></param>
        /// <param name="criterion"></param>
        /// <returns></returns>
        public Task<IUserMessage> PagedReplyAsync(PaginatedMessage pager, ReactionList reactionList, ICriterion<SocketReaction> criterion = null)
        {
            return Interactive.SendPaginatedMessageAsync(LithiumSocketContext(), pager, reactionList, criterion);
        }

        public Task<IUserMessage> InlineReactionReplyAsync(ReactionCallbackData data, bool fromSourceUser = true)
        {
            return Interactive.SendMessageWithReactionCallbacksAsync(LithiumSocketContext(), data, fromSourceUser);
        }
    }

    public class LithiumContext : ICommandContext
    {
        public LithiumContext(IDiscordClient ClientParam, IUserMessage MessageParam, IServiceProvider ServiceProvider)
        {
            Client = ClientParam;
            Message = MessageParam;
            User = MessageParam.Author;
            Channel = MessageParam.Channel;
            Guild = MessageParam.Channel is IDMChannel ? null : (MessageParam.Channel as IGuildChannel).Guild;

            //This is a shorthand conversion for our context, giving access to socket context stuff without the need to cast within out commands
            Socket = new SocketContext
            {
                Guild = Guild as SocketGuild,
                User = User as SocketUser,
                Client = Client as DiscordSocketClient,
                Message = Message as SocketUserMessage,
                Channel = Channel as ISocketMessageChannel
            };

            //These are our custom additions to the context, giving access to the server object and all server objects through Context.
            Server = Channel is IDMChannel ? null : DatabaseHandler.GetGuild(Guild.Id);
            Session = ServiceProvider.GetRequiredService<IDocumentStore>().OpenSession();
        }

        public GuildModel.Guild Server { get; }
        public IDocumentSession Session { get; }
        public SocketContext Socket { get; }
        public IUser User { get; }
        public IGuild Guild { get; }
        public IDiscordClient Client { get; }
        public IUserMessage Message { get; }
        public IMessageChannel Channel { get; }

        public class SocketContext
        {
            public SocketUser User { get; set; }
            public SocketGuild Guild { get; set; }
            public DiscordSocketClient Client { get; set; }
            public SocketUserMessage Message { get; set; }
            public ISocketMessageChannel Channel { get; set; }
        }
    }
}