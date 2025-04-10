﻿using Microsoft.Extensions.Configuration;

class Program
{
    private static string apiKey = string.Empty;
    private static string secretKey = string.Empty;

    static async Task Main(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .Build();

        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = new System.Globalization.CultureInfo("en-US");

        apiKey = config["BINANCE_API_KEY"]!;
        secretKey = config["BINANCE_SECRET_KEY"]!;

        while (true)
        {
            decimal price = await BinanceApi.GetBitcoinPriceAsync();
            decimal usdToBrlRate = await BinanceApi.GetUsdToBrlExchangeRateAsync();
            decimal priceInBrl = price * usdToBrlRate;
            Console.WriteLine($"Bitcoin price: {price.ToString("C2", new System.Globalization.CultureInfo("en-US"))} (USD)");
            Console.WriteLine($"Bitcoin price: {priceInBrl.ToString("C2", new System.Globalization.CultureInfo("pt-BR"))} (BRL)");
            decimal btcBalance = await BinanceApi.GetBtcBalanceAsync(apiKey, secretKey);
            Console.WriteLine($"Binance BTC Balance: {btcBalance.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)} BTC");
            decimal usdtBalance = await BinanceApi.GetUsdtBalanceAsync(apiKey, secretKey);
            Console.WriteLine($"Binance USDT Balance: {usdtBalance.ToString("C2", new System.Globalization.CultureInfo("en-US"))}");
            decimal usdtBalanceInBrl = usdtBalance * usdToBrlRate;
            Console.WriteLine($"Binance USDT Balance: {usdtBalanceInBrl.ToString("C2", new System.Globalization.CultureInfo("pt-BR"))} (BRL)");
            await Bot.Trader.Strategies.ExecuteCombinedStrategy(apiKey, secretKey);
            await Task.Delay(10000); // Aguarda 10 segundos
        }
    }
}