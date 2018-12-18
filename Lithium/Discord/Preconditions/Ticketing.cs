using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Lithium.Handlers;

namespace Lithium.Discord.Preconditions
{
    public class Ticketing
    {
        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
        public class TicketEnabled : PreconditionAttribute
        {
            public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command,
                IServiceProvider services)
            {
                //Ensure that the command is only run in a server and not in DMs
                if (context.Channel is IDMChannel) return Task.FromResult(PreconditionResult.FromError("This command is to be used in a guild only!"));

                //We check if the server object has ticketing enabled
                var guildobj = DatabaseHandler.GetGuild(context.Guild.Id);
                if (guildobj.Tickets.Settings.useticketing)
                {
                    return Task.FromResult(PreconditionResult.FromSuccess());
                }

                //If all other checks fail, deny access
                return Task.FromResult(PreconditionResult.FromError("Ticketing is not enabled in this server."));
            }
        }
    }
}