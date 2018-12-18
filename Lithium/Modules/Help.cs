using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Lithium.Discord.Contexts;
using Lithium.Models;
using Lithium.Services;

namespace Lithium.Modules
{
    public class Help : Base
    {
        private readonly CommandService _service;

        private Help(CommandService service)
        {
            _service = service;
        }

        [Command("command")]
        [Summary("command <command name>")]
        [Remarks("get info about a specific command")]
        public async Task CommandAsync([Remainder] string command = null)
        {
            if (command == null)
            {
                await ReplyAsync($"Please specify a command, ie `{Config.Load().DefaultPrefix}command kick`");
                return;
            }

            try
            {
                var result = _service.Search(Context, command);
                var builder = new EmbedBuilder
                {
                    Color = new Color(179, 56, 216)
                };

                foreach (var match in result.Commands)
                {
                    var cmd = match.Command;
                    builder.Title = cmd.Name.ToUpper();
                    builder.Description +=
                        $"**Aliases:** {string.Join(", ", cmd.Aliases)}\n**Parameters:** {string.Join(", ", cmd.Parameters.Select(p => p.Name))}\n" +
                        $"**Remarks:** {cmd.Remarks}\n**Summary:** `{Config.Load().DefaultPrefix}{cmd.Summary}`\n";
                }

                await ReplyAsync("", false, builder.Build());
            }
            catch
            {
                await ReplyAsync($"**Command Name:** {command}\n**Error:** Not Found!");
            }
        }

        [Command("help")]
        [Summary("help")]
        [Remarks("all help commands")]
        public async Task HelpAsync([Remainder] string modulearg = null)
        {
            try
            {
                var gobj = Context.Server;
                string isserver;
                if (Context.Channel is IPrivateChannel)
                    isserver = Config.Load().DefaultPrefix;
                else
                    isserver = gobj?.Settings.Prefix ?? Config.Load().DefaultPrefix;

                if (modulearg == null) //ShortHelp
                {
                    var pages = new List<PaginatedMessage.Page>();
                    foreach (var module in _service.Modules.Where(x => x.Commands.Count > 0 && gobj?.Settings.DisabledParts.BlacklistedModules.Any(bm => string.Equals(bm, x.Name, StringComparison.CurrentCultureIgnoreCase)) != true))
                    {
                        var list = module.Commands.Where(x => gobj?.Settings.DisabledParts.BlacklistedCommands.Any(bc => string.Equals(x.Name, bc, StringComparison.CurrentCultureIgnoreCase)) != true)
                            .Select(command => $"`{isserver}{command.Summary}` - {command.Remarks}")
                            .ToList();

                        if (module.Commands.Count <= 0) continue;
                        if (string.Join("\n", list).Length > 1000)
                        {
                            pages.Add(new PaginatedMessage.Page
                            {
                                Title = $"{module.Name} (1)",
                                Description = string.Join("\n", list.Take(list.Count / 2))
                            });
                            pages.Add(new PaginatedMessage.Page
                            {
                                Title = $"{module.Name} (2)",
                                Description = string.Join("\n", list.Skip(list.Count / 2))
                            });
                        }
                        else
                        {
                            pages.Add(new PaginatedMessage.Page
                            {
                                Title = module.Name,
                                Description = string.Join("\n", list)
                            });
                        }
                    }

                    var moduleselect = new List<string>
                    {
                        "`1` - This Page",
                        "`2` - List of all commands(1)",
                        "`3` - List of all commands(2)"
                    };
                    var i = 3;
                    foreach (var module in pages.Where(x => x.Title != null))
                    {
                        i++;
                        moduleselect.Add($"`{i}` - {module.Title}");
                    }

                    var fullpages = new List<PaginatedMessage.Page>
                    {
                        new PaginatedMessage.Page
                        {
                            Title = $"{Context.Client.CurrentUser.Username} | Modules | Prefix: {isserver}",
                            Description = $"Here is a list of all the {Context.Client.CurrentUser.Username} command modules\n" +
                                          $"There are {_service.Commands.Count()} commands\n" +
                                          "Click the arrows to view each one!\n" +
                                          $"{(Context.Channel is IDMChannel ? "\n" : "Or Click :1234: and reply with the page number you would like\n\n")}" +
                                          string.Join("\n", moduleselect)
                        },
                        new PaginatedMessage.Page
                        {
                            Title = $"{Context.Client.CurrentUser.Username} | All Commands | Prefix: {isserver}",
                            Description = string.Join("\n",
                                _service.Modules.Where(x => x.Commands.Count > 0 && gobj?.Settings.DisabledParts.BlacklistedModules.Any(bm => string.Equals(bm, x.Name, StringComparison.CurrentCultureIgnoreCase)) != true)
                                    .Take(_service.Modules.Count() / 2)
                                    .Select(x =>
                                        $"__**{x.Name}**__\n{string.Join(", ", x.Commands.Where(c => gobj?.Settings.DisabledParts.BlacklistedCommands.Any(bc => string.Equals(c.Name, bc, StringComparison.CurrentCultureIgnoreCase)) != true).Select(c => c.Name))}"))
                        },
                        new PaginatedMessage.Page
                        {
                            Title = $"{Context.Client.CurrentUser.Username} | All Commands | Prefix: {isserver}",
                            Description = string.Join("\n",
                                _service.Modules.Where(x => x.Commands.Count > 0 && gobj?.Settings.DisabledParts.BlacklistedModules.Any(bm => string.Equals(bm, x.Name, StringComparison.CurrentCultureIgnoreCase)) != true)
                                    .Skip(_service.Modules.Count() / 2)
                                    .Select(x =>
                                        $"__**{x.Name}**__\n{string.Join(", ", x.Commands.Where(c => gobj?.Settings.DisabledParts.BlacklistedCommands.Any(bc => string.Equals(c.Name, bc, StringComparison.CurrentCultureIgnoreCase)) != true).Select(c => c.Name))}"))
                        }
                    };
                    foreach (var page in pages) fullpages.Add(page);

                    var msg = new PaginatedMessage
                    {
                        Color = Color.Green,
                        Pages = fullpages
                    };
                    await PagedReplyAsync(msg, new ReactionList
                    {
                        Forward = true,
                        Backward = true, 
                        Jump = true,
                        Trash = true
                    });
                    return;
                }

                var mod = _service.Modules.FirstOrDefault(x =>
                    string.Equals(x.Name, modulearg, StringComparison.CurrentCultureIgnoreCase));
                var embed = new EmbedBuilder
                {
                    Color = new Color(114, 137, 218),
                    Title = $"{Context.Client.CurrentUser.Username} | Commands | Prefix: {isserver}"
                };
                if (mod == null)
                {
                    var list = _service.Modules.Where(x => x.Commands.Count > 0).Select(x => x.Name);
                    var response = string.Join("\n", list);
                    embed.AddField("ERROR, Module not found", response);
                    await ReplyAsync("", false, embed.Build());
                    return;
                }

                var commands = mod.Commands.Select(x => $"`{isserver}{x.Summary}` - {x.Remarks}").ToList();
                if (commands.Count > 8)
                {
                    embed.AddField($"{mod.Name} Commands (1)",
                        commands.Count == 0 ? "N/A" : string.Join("\n", commands.Take(commands.Count / 2)));
                    embed.AddField($"{mod.Name} Commands (2)",
                        commands.Count == 0 ? "N/A" : string.Join("\n", commands.Skip(commands.Count / 2)));
                }
                else
                {
                    embed.AddField($"{mod.Name} Commands", commands.Count == 0 ? "N/A" : string.Join("\n", commands));
                }

                await ReplyAsync("", false, embed.Build());
            }
            catch (Exception e)
            {
                Logger.LogMessage(e.ToString(), LogSeverity.Error);
            }
        }
    }
}