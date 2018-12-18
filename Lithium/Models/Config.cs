using System;
using System.IO;
using Lithium.Services;
using Newtonsoft.Json;

namespace Lithium.Models
{
    public class Config
    {
        [JsonIgnore] public static readonly string Appdir = AppContext.BaseDirectory;


        public static string ConfigPath = Path.Combine(AppContext.BaseDirectory, "setup/config.json");

        public string DefaultPrefix { get; set; } = "=";
        public string BotToken { get; set; } = "Token";
        public bool AutoRun { get; set; }
        public string ServerURL { get; set; } = "http://127.0.0.1:8080";
        public string SupportServer { get; set; } = "http://discord.me/passive";
        public string DBName { get; set; } = "Lithium";
        public string ToxicityToken { get; set; } = null;
        public string DBLToken { get; set; } = null;

        public void Save(string dir = "setup/config.json")
        {
            var file = Path.Combine(Appdir, dir);
            File.WriteAllText(file, ToJson());
        }

        public static Config Load(string dir = "setup/config.json")
        {
            var file = Path.Combine(Appdir, dir);
            return JsonConvert.DeserializeObject<Config>(File.ReadAllText(file));
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public static void CheckExistence()
        {
            bool auto;
            try
            {
                auto = Load().AutoRun;
            }
            catch
            {
                auto = false;
            }

            if (!auto)
            {
                Logger.LogMessage("Run (Y for run, N for setup Config)");

                Logger.LogMessage("Y or N: ");
                var res = Console.ReadKey();
                if (res.KeyChar == 'N' || res.KeyChar == 'n')
                    File.Delete("setup/config.json");

                if (!Directory.Exists(Path.Combine(AppContext.BaseDirectory, "setup/")))
                    Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "setup/"));
            }

            if (!File.Exists(ConfigPath))
            {
                var config = new Config();

                Logger.LogMessage(@"Please enter a prefix for the bot eg. '+' (do not include the '' outside of the prefix)");
                Console.Write("Prefix: ");
                config.DefaultPrefix = Console.ReadLine();

                Logger.LogMessage(@"After you input your token, a config will be generated at 'setup/config.json'");
                Console.Write("Token: ");
                config.BotToken = Console.ReadLine();

                Logger.LogMessage("Would you like to AutoRun the bot from now on? Y/N");
                var key = Console.ReadKey();
                if (key.KeyChar == 'y' || key.KeyChar == 'Y')
                    config.AutoRun = true;
                else
                    config.AutoRun = false;

                config.Save();
            }

            Logger.LogMessage("Config Loaded!");
            Logger.LogMessage($"Prefix: {Load().DefaultPrefix}");
            Logger.LogMessage($"Token Length: {Load().BotToken.Length} (should be 59)");
            Logger.LogMessage($"Autorun: {Load().AutoRun}");
        }
    }
}