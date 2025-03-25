using Newtonsoft.Json;

namespace DiscordBot.Config
{
    internal class JSONReader
    {
        public string? token { get; set; }
        public string? prefix { get; set; }

        public async Task<bool> ReadJSON()
        {
            try
            {
                if (!File.Exists("Config.json"))
                {
                    Console.WriteLine("Error: Config.json file not found.");
                    return false;
                }

                using (StreamReader sr = new StreamReader("Config.json"))
                {
                    string json = await sr.ReadToEndAsync().ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        Console.WriteLine("Error: Config.json is empty.");
                        return false;
                    }

                    JSONStructure? data = JsonConvert.DeserializeObject<JSONStructure>(json);
                    if (data == null)
                    {
                        Console.WriteLine("Error: Failed to deserialize Config.json. Please verify its structure.");
                        return false;
                    }

                    token = data.token;
                    prefix = data.prefix;

                    if (string.IsNullOrEmpty(token))
                        Console.WriteLine("Warning: Bot token is not provided in Config.json.");
                    if (string.IsNullOrEmpty(prefix))
                        Console.WriteLine("Warning: Bot command prefix is not provided in Config.json.");

                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading Config.json: {ex.Message}");
                return false;
            }
        }

        internal sealed class JSONStructure
        {
            public string? token { get; set; }
            public string? prefix { get; set; }
        }
    }
}
