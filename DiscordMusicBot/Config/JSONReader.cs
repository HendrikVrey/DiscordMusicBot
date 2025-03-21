﻿using Newtonsoft.Json;

namespace DiscordBot.Config
{
    internal class JSONReader
    {
        public string? token { get; set; }
        public string? prefix { get; set; }

        public async Task ReadJSON()
        {
            using (StreamReader sr = new StreamReader("Config.json"))
            {
                string json = await sr.ReadToEndAsync();
                JSONStructure? data = JsonConvert.DeserializeObject<JSONStructure>(json);

                this.token = data.token;
                this.prefix = data.prefix;
            }
        }

        internal sealed class JSONStructure
        {
            public string? token { get; set; }
            public string? prefix { get; set; }
        }
    }
}
