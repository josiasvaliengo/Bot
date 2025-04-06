using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Bot.Trader
{
    public static class TelegramApi
    {
        private static readonly string token = string.Empty;
        private static readonly string chatId = string.Empty;

        static TelegramApi()
        {
            var config = new ConfigurationBuilder()
                .AddUserSecrets<Program>()
                .Build();

            token = config["TELEGRAM_TOKEN"]!;
            chatId = config["TELEGRAM_CHAT_ID"]!;
        }

        public static async Task SendMessageAsync(string message)
        {
            using var client = new HttpClient();
            var url = $"https://api.telegram.org/bot{token}/sendMessage?chat_id={chatId}&text={Uri.EscapeDataString(message)}";
            await client.GetAsync(url);
        }
    }
}
