using System.Text.Json;
using System.Globalization;

namespace Bot.Trader
{
    public static class Strategies
    {
        private static string? ultimaAcao = null;

        public static async Task TapeReading()
        {
            Console.WriteLine("Executando estratégia de Tape Reading...");

            var (bids, asks) = await BinanceApi.GetOrderBookAsync();

            Console.WriteLine("\nTop 5 Ordens de Compra (Bids):");
            foreach (var bid in bids)
            {
                Console.WriteLine($"Preço: {bid.price:C2} - Quantidade: {bid.quantity}");
            }

            Console.WriteLine("\nTop 5 Ordens de Venda (Asks):");
            foreach (var ask in asks)
            {
                Console.WriteLine($"Preço: {ask.price:C2} - Quantidade: {ask.quantity}");
            }

            var totalBidVolume = bids.Sum(b => b.quantity);
            var totalAskVolume = asks.Sum(a => a.quantity);

            Console.WriteLine($"\nVolume Total de Compra: {totalBidVolume}");
            Console.WriteLine($"Volume Total de Venda: {totalAskVolume}");

            if (totalBidVolume > totalAskVolume * 1.5m)
            {
                Console.WriteLine("Pressão de COMPRA detectada!");
                await TelegramApi.SendMessageAsync("📈 Pressão de COMPRA detectada!");
            }
            else if (totalAskVolume > totalBidVolume * 1.5m)
            {
                Console.WriteLine("Pressão de VENDA detectada!");
                await TelegramApi.SendMessageAsync("📉 Pressão de VENDA detectada!");
            }
            else
            {
                Console.WriteLine("Mercado equilibrado.");
                await TelegramApi.SendMessageAsync("⚖️ Mercado equilibrado.");
            }
        }

        public static async Task PriceActionAnalysis()
        {
            Console.WriteLine("Executando estratégia de Price Action...");

            string[] intervals = { "1m", "5m" };

            foreach (var interval in intervals)
            {
                Console.WriteLine($"\nAnalisando timeframe: {interval}");

                using HttpClient client = new HttpClient();
                var response = await client.GetStringAsync($"https://api.binance.com/api/v3/klines?symbol=BTCUSDT&interval={interval}&limit=5");
                JsonDocument doc = JsonDocument.Parse(response);
                var candles = doc.RootElement.EnumerateArray();

                int index = 1;
                foreach (var candle in candles)
                {
                    var open = candle[1].GetDecimal();
                    var high = candle[2].GetDecimal();
                    var low = candle[3].GetDecimal();
                    var close = candle[4].GetDecimal();

                    var body = Math.Abs(close - open);
                    var upperShadow = high - Math.Max(open, close);
                    var lowerShadow = Math.Min(open, close) - low;

                    Console.WriteLine($"\nCandle {index++} - Abertura: {open}, Fechamento: {close}, Máxima: {high}, Mínima: {low}");

                    if (body < upperShadow && lowerShadow > body * 2)
                    {
                        Console.WriteLine("🔍 Possível Martelo detectado");
                    }

                    if (close > open)
                        Console.WriteLine("📈 Candle de alta");
                    else if (close < open)
                        Console.WriteLine("📉 Candle de baixa");
                    else
                        Console.WriteLine("➡️ Candle neutro");
                }
            }
        }

        public static async Task ExecuteCombinedStrategy(string apiKey, string secretKey)
        {
            Console.WriteLine("Executando estratégia combinada...");

            var (bids, asks) = await BinanceApi.GetOrderBookAsync();
            decimal btcBalance = await BinanceApi.GetBtcBalanceAsync(apiKey, secretKey);
            decimal usdtBalance = await BinanceApi.GetUsdtBalanceAsync(apiKey, secretKey);

            decimal totalBid = bids.Sum(b => b.quantity);
            decimal totalAsk = asks.Sum(a => a.quantity);

            bool pressaoCompra = totalBid > totalAsk * 1.5m;
            bool pressaoVenda = totalAsk > totalBid * 1.5m;

            // Executa PriceAction e coleta candle mais recente
            using var client = new HttpClient();
            var response = await client.GetStringAsync("https://api.binance.com/api/v3/klines?symbol=BTCUSDT&interval=1m&limit=1");
            var doc = JsonDocument.Parse(response);
            var candle = doc.RootElement.EnumerateArray().First();

            var open = decimal.Parse(candle[1].GetString()!, CultureInfo.InvariantCulture);
            var high = decimal.Parse(candle[2].GetString()!, CultureInfo.InvariantCulture);
            var low = decimal.Parse(candle[3].GetString()!, CultureInfo.InvariantCulture);
            var close = decimal.Parse(candle[4].GetString()!, CultureInfo.InvariantCulture);

            var body = Math.Abs(close - open);
            var upperShadow = high - Math.Max(open, close);
            var lowerShadow = Math.Min(open, close) - low;

            bool marteloReversao = body < upperShadow && lowerShadow > body * 2;

            if (pressaoCompra && marteloReversao && usdtBalance > 10)
            {
                if (ultimaAcao == "compra")
                {
                    Console.WriteLine("🚫 Compra ignorada (já executada anteriormente).");
                    return;
                }
                var result = await BinanceApi.PlaceMarketBuyOrder(apiKey, secretKey, usdtBalance);
                Console.WriteLine("✅ COMPRA EXECUTADA");
                await TelegramApi.SendMessageAsync($"✅ COMPRA EXECUTADA: {result}");
                ultimaAcao = "compra";
            }
            else if (pressaoVenda && marteloReversao && btcBalance > 0.0001m)
            {
                if (ultimaAcao == "venda")
                {
                    Console.WriteLine("🚫 Venda ignorada (já executada anteriormente).");
                    return;
                }
                var result = await BinanceApi.PlaceMarketSellOrder(apiKey, secretKey, btcBalance);
                Console.WriteLine("⚠️ VENDA EXECUTADA");
                await TelegramApi.SendMessageAsync($"⚠️ VENDA EXECUTADA: {result}");
                ultimaAcao = "venda";
            }
            else
            {
                Console.WriteLine("📊 Nenhuma ação executada. Condições não atendidas.");
            }
        }
    }
}
