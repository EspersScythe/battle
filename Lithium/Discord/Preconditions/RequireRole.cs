using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Lithium.Handlers;

namespace Lithium.Discord.Preconditions
{
    public class RequireRole
    {
        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
        public class RequireModerator : PreconditionAttribute
        {
            private readonly bool _allowAdministrator;
            private readonly bool _allowAdministratorRole;

            public RequireModerator(bool AllowAdminPermission = true, bool allowAdministratorRole = true)
            {
                _allowAdministrator = AllowAdminPermission;
                _allowAdministratorRole = allowAdministratorRole;
            }

            public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command,
                IServiceProvider services)
            {
                //Ensure that the command is only run in a server and not in DMs

                if (context.Channel is IDMChannel) return Task.FromResult(PreconditionResult.FromError("User is not a Moderator or an Admin and Command is only accessible within a guild"));

                //Allow the bot owner through in all instances
                var own = context.Client.GetApplicationInfoAsync();
                if (own.Result.Owner.Id == context.User.Id)
                    return Task.FromResult(PreconditionResult.FromSuccess());

                //Allow the server owner through in all instances
                if (context.Guild.OwnerId == context.User.Id)
                    return Task.FromResult(PreconditionResult.FromSuccess());

                //If applicable allow users with administration permissions in the server
                var guser = (IGuildUser) context.User;
                var guild = DatabaseHandler.GetGuild(context.Guild.Id);
                if (_allowAdministrator && guser.GuildPermissions.Administrator)
                    return Task.FromResult(PreconditionResult.FromSuccess());

                //Check to see if the user has an admin role in the server
                if (_allowAdministratorRole && guild.ModerationSetup.AdminRoles.Any(x => guser.RoleIds.Contains(x)))
                    return Task.FromResult(PreconditionResult.FromSuccess());

                //Check to see if the user has a moderator role in the server
                if (guild.ModerationSetup.ModeratorRoles.Any(x => guser.RoleIds.Contains(x)))
                    return Task.FromResult(PreconditionResult.FromSuccess());

                //If all the previous checks fail, deny access
                return Task.FromResult(PreconditionResult.FromError("User is Not A Moderator or an Admin!"));
            }
        }

        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
        public class RequireAdmin : PreconditionAttribute
        {
            private readonly bool _allowAdministrator;

            public RequireAdmin(bool AllowAdminPermission = true)
            {
                _allowAdministrator = AllowAdminPermission;
            }

            public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command,
                IServiceProvider services)
            {
                //Ensure that the command is only run in a server and not in DMs
                if (context.Channel is IDMChannel) return Task.FromResult(PreconditionResult.FromError("User is not an Admin and Command is only accessible within a guild"));

                //Allow bot owner through in all instances
                var own = context.Client.GetApplicationInfoAsync();
                if (own.Result.Owner.Id == context.User.Id)
                    return Task.FromResult(PreconditionResult.FromSuccess());

                //Allow the server owner through in all instances
                if (context.Guild.OwnerId == context.User.Id)
                    return Task.FromResult(PreconditionResult.FromSuccess());

                //If applicable, allow users with admin permissions in the server through
                var guser = (IGuildUser) context.User;
                var guild = DatabaseHandler.GetGuild(context.Guild.Id);
                if (_allowAdministrator && guser.GuildPermissions.Administrator)
                    return Task.FromResult(PreconditionResult.FromSuccess());

                //Allow users with an admin role through
                if (guild.ModerationSetup.AdminRoles.Any(x => guser.RoleIds.Contains(x)))
                    return Task.FromResult(PreconditionResult.FromSuccess());

                //If all other checks fail, deny access
                return Task.FromResult(PreconditionResult.FromError("User does not have an Administrator role!"));
            }
        }
    }
}