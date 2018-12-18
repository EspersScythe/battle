using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Lithium.Models;
using Lithium.Services;
using Raven.Client.Documents;
using Raven.Client.Documents.Operations.Backups;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;

namespace Lithium.Handlers
{
    public class DatabaseHandler
    {
        public DatabaseHandler(IDocumentStore store)
        {
            Store = store;
        }

        ///This is our configuration for the database handler, DBName is the database you created in RavenDB when setting it up
        ///ServerURL is the URL to the local server. NOTE: This bot has not been configured to use public addresses
        public static string DBName { get; set; } = Config.Load().DBName;

        public static string ServerURL { get; set; } = Config.Load().ServerURL;

        /// <summary>
        ///     This is the document store, an interface that represents our database
        /// </summary>
        public static IDocumentStore Store { get; set; }

        public static bool Ping(string url)
        {
            try
            {
                WebClient webClient = new WebClient();
                byte[] result = webClient.DownloadData(url);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///     Check whether RavenDB is running
        ///     Check whether or not a database already exists with the DBName
        ///     Set up auto-backup of the database
        ///     Ensure that all guilds shared with the bot have been added to the database
        /// </summary>
        /// <param name="client">The discord client</param>
        public static async void DatabaseInitialise(DiscordSocketClient client)
        {
            /*
            if (Process.GetProcesses().FirstOrDefault(x => x.ProcessName == "Raven.Server") == null)
            {
                Logger.LogMessage("RavenDB: Server isn't running. Please make sure RavenDB is running.\nExiting ...", LogSeverity.Critical);
                await Task.Delay(5000);
                Environment.Exit(Environment.ExitCode);
            }
            */


            if (!Ping(Config.Load().ServerURL))
            {
                Logger.LogMessage("RavenDB: Server isn't running. Please make sure RavenDB is running.\nExiting ...", LogSeverity.Critical);
                await Task.Delay(5000);
                Environment.Exit(Environment.ExitCode);
            }


            var dbinitialised = false;
            if (Store.Maintenance.Server.Send(new GetDatabaseNamesOperation(0, 5)).All(x => x != DBName))
            {
                await Store.Maintenance.Server.SendAsync(new CreateDatabaseOperation(new DatabaseRecord(DBName)));
                Logger.LogMessage($"Created Database {DBName}.");
                dbinitialised = true;
            }


            Logger.LogMessage("RavenDB: Setting up backup operation...");
            var newbackup = new PeriodicBackupConfiguration
            {
                Name = "Backup",
                BackupType = BackupType.Backup,
                FullBackupFrequency = "0 */6 * * *",
                IncrementalBackupFrequency = "0 2 * * *",
                LocalSettings = new LocalSettings {FolderPath = Path.Combine(AppContext.BaseDirectory, "setup/backups/")}
            };
            var Record = Store.Maintenance.ForDatabase(DBName).Server.Send(new GetDatabaseRecordOperation(DBName));
            var backupop = Record.PeriodicBackups.FirstOrDefault(x => x.Name == "Backup");
            try
            {
                if (backupop == null)
                {
                    await Store.Maintenance.ForDatabase(DBName).SendAsync(new UpdatePeriodicBackupOperation(newbackup)).ConfigureAwait(false);
                }
                else
                {
                    //In the case that we already have a backup operation setup, ensure that we update the backup location accordingly
                    backupop.LocalSettings = new LocalSettings {FolderPath = Path.Combine(AppContext.BaseDirectory, "setup/backups/")};
                    await Store.Maintenance.ForDatabase(DBName).SendAsync(new UpdatePeriodicBackupOperation(backupop));
                }
            }
            catch
            {
                Logger.LogMessage("RavenDB: Error setting up backup operation, they may not be saved\n", LogSeverity.Warning);
            }


            if (dbinitialised) return;

            using (var session = Store.OpenSession(DBName))
            {
                try
                {
                    //Check to see wether or not we can actually load the Guilds List saved in our RavenDB
                    var _ = session.Query<GuildModel.Guild>().ToList();
                }
                catch
                {
                    //In the case that the check fails, ensure we initalise all servers that contain the bot.
                    var glist = client.Guilds.Select(x => new GuildModel.Guild
                    {
                        GuildID = x.Id
                    }).ToList();
                    foreach (var gobj in glist)
                    {
                        session.Store(gobj, gobj.GuildID.ToString());
                    }

                    session.SaveChanges();
                }
            }
        }


        /// <summary>
        ///     This adds a new guild to the RavenDB
        /// </summary>
        /// <param name="Id">The Server's ID</param>
        /// <param name="Name">Optionally say the server name was added to the DB</param>
        public static void AddGuild(ulong Id, string Name = null)
        {
            using (var Session = Store.OpenSession(DBName))
            {
                if (Session.Advanced.Exists($"{Id}")) return;
                Session.Store(new GuildModel.Guild
                {
                    GuildID = Id
                }, Id.ToString());
                Session.SaveChanges();
            }

            Logger.LogMessage(string.IsNullOrWhiteSpace(Name) ? $"Added Server With Id: {Id}" : $"Created Config For {Name}", LogSeverity.Debug);
        }

        /// <summary>
        ///     Remove a guild's config completely from the database
        /// </summary>
        /// <param name="Id"></param>
        /// <param name="Name"></param>
        public static void RemoveGuild(ulong Id, string Name = null)
        {
            using (var Session = Store.OpenSession(DBName))
            {
                Session.Delete($"{Id}");
            }

            Logger.LogMessage(string.IsNullOrWhiteSpace(Name) ? $"Removed Server With Id: {Id}" : $"Deleted Config For {Name}", LogSeverity.Debug);
        }

        /// <summary>
        ///     Add a newly initialised config to the database
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public static GuildModel.Guild GetGuild(ulong Id)
        {
            using (var Session = Store.OpenSession(DBName))
            {
                return Session.Load<GuildModel.Guild>(Id.ToString());
            }
        }

        /// <summary>
        ///     Load all documents matching GuildModel.Guild from the database
        /// </summary>
        /// <returns></returns>
        public static List<GuildModel.Guild> GetFullConfig()
        {
            using (var session = Store.OpenSession(DBName))
            {
                List<GuildModel.Guild> dbGuilds;
                try
                {
                    dbGuilds = session.Query<GuildModel.Guild>().ToList();
                }
                catch
                {
                    dbGuilds = new List<GuildModel.Guild>();
                }

                return dbGuilds;
            }
        }
    }
}