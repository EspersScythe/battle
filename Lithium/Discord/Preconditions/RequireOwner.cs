using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace Lithium.Discord.Preconditions
{
    public class RequireOwner
    {
        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
        public class ServerOwner : PreconditionAttribute
        {
            public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command,
                IServiceProvider services)
            {
                //Ensure that the command is only run in a server and not in DMs

                if (context.Channel is IDMChannel) return Task.FromResult(PreconditionResult.FromError("This is a guild specific command."));

                //Allow the bot owner through in all instances
                var own = context.Client.GetApplicationInfoAsync();
                if (own.Result.Owner.Id == context.User.Id)
                    return Task.FromResult(PreconditionResult.FromSuccess());

                //Allow the server owner through in all instances
                if (context.Guild.OwnerId == context.User.Id)
                    return Task.FromResult(PreconditionResult.FromSuccess());

                //If all the previous checks fail, deny access
                return Task.FromResult(PreconditionResult.FromError("This command can only be run by the server owner!"));
            }
        }
    }
}