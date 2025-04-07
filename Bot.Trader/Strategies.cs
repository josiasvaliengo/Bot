using System.Text.Json;
using System.Globalization;

namespace Bot.Trader
{
    public static class Strategies
    {
        private static string? ultimaAcao = null;
        private static decimal? precoEntrada = null;
        private static decimal lucroAlvoPercentual = 1.5m; // Alvo de 1.5%
        private static decimal stopLossPercentual = 1.0m; // Stop Loss de 1%
        private static decimal trailingStopPercentual = 0.8m; // Trailing Stop de 0.8%
        private static decimal? maiorPrecoDesdeEntrada = null;
        private static decimal? valorCompraReais = null;
        private static decimal? cotacaoDolarCache = null;
        private static DateTime? ultimaAtualizacaoCotacao = null;
        private static readonly TimeSpan intervaloCache = TimeSpan.FromMinutes(5);
        private static decimal taxaBinancePercentual = 0.1m; // 0.1% por operação (ajuste conforme necessário)

        private static void AjustarParametrosPorContexto(decimal candleRange)
        {
            if (candleRange > 100) // Alta volatilidade
            {
                lucroAlvoPercentual = 2.0m;
                stopLossPercentual = 1.5m;
                trailingStopPercentual = 1.0m;
            }
            else if (candleRange > 50) // Volatilidade média
            {
                lucroAlvoPercentual = 1.5m;
                stopLossPercentual = 1.0m;
                trailingStopPercentual = 0.8m;
            }
            else // Baixa volatilidade
            {
                lucroAlvoPercentual = 1.0m;
                stopLossPercentual = 0.7m;
                trailingStopPercentual = 0.5m;
            }
        }

        public static async Task<decimal> GetCotacaoDolarAsync()
        {
            if (cotacaoDolarCache.HasValue && ultimaAtualizacaoCotacao.HasValue &&
                DateTime.Now - ultimaAtualizacaoCotacao < intervaloCache)
            {
                return cotacaoDolarCache.Value;
            }

            using var client = new HttpClient();
            var response = await client.GetStringAsync("https://economia.awesomeapi.com.br/json/last/USD-BRL");
            var jsonDoc = JsonDocument.Parse(response);
            var bid = jsonDoc.RootElement.GetProperty("USDBRL").GetProperty("bid").GetString();
            cotacaoDolarCache = decimal.Parse(bid!, CultureInfo.InvariantCulture);
            ultimaAtualizacaoCotacao = DateTime.Now;

            return cotacaoDolarCache.Value;
        }

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

            // Verifica se o lucro alvo foi atingido
            if (ultimaAcao == "compra" && precoEntrada.HasValue)
            {
                var precoAtual = await BinanceApi.GetBitcoinPriceAsync();
                var ganhoPercentual = ((precoAtual - precoEntrada.Value) / precoEntrada.Value) * 100;

                // Take Profit
                if (ganhoPercentual >= lucroAlvoPercentual && btcBalance > 0.0001m)
                {
                    await BinanceApi.PlaceMarketSellOrder(apiKey, secretKey, btcBalance);
                    var cotacaoDolar = await GetCotacaoDolarAsync();
                    var valorVendaReais = btcBalance * precoAtual * cotacaoDolar;
                    Console.WriteLine($"💰 LUCRO ALVO ATINGIDO - Venda realizada com {ganhoPercentual:F2}% de lucro.");
                    Console.WriteLine($"💸 Valor da venda em reais: R$ {valorVendaReais:F2}");
                    await TelegramApi.SendMessageAsync($"💰 LUCRO ALVO ATINGIDO - BTC vendido com {ganhoPercentual:F2}% de lucro.\n💸 Aproximadamente R$ {valorVendaReais:F2}");

                    if (valorCompraReais.HasValue)
                    {
                        var taxaCompra = valorCompraReais.Value * (taxaBinancePercentual / 100);
                        var taxaVenda = valorVendaReais * (taxaBinancePercentual / 100);
                        var resultado = valorVendaReais - valorCompraReais.Value - taxaCompra - taxaVenda;
                        Console.WriteLine($"📊 Resultado da operação: R$ {resultado:F2}");
                        await TelegramApi.SendMessageAsync($"📊 Resultado da operação: R$ {resultado:F2}");
                        valorCompraReais = null;
                    }

                    ultimaAcao = "venda";
                    precoEntrada = null;
                    maiorPrecoDesdeEntrada = null;
                    return;
                }

                // Atualiza o maior preço para trailing stop
                if (maiorPrecoDesdeEntrada == null || precoAtual > maiorPrecoDesdeEntrada.Value)
                    maiorPrecoDesdeEntrada = precoAtual;

                // Verifica se é necessário ativar o trailing stop e stop loss
                var perdaPercentual = ((precoAtual - precoEntrada.Value) / precoEntrada.Value) * 100;
                var quedaDesdeTopo = ((precoAtual - maiorPrecoDesdeEntrada.Value) / maiorPrecoDesdeEntrada.Value) * 100;

                if (perdaPercentual <= -stopLossPercentual && btcBalance > 0.0001m)
                {
                    await BinanceApi.PlaceMarketSellOrder(apiKey, secretKey, btcBalance);
                    var cotacaoDolar = await GetCotacaoDolarAsync();
                    var valorVendaReais = btcBalance * precoAtual * cotacaoDolar;
                    Console.WriteLine($"🛑 STOP LOSS - Venda realizada com {perdaPercentual:F2}% de prejuízo.");
                    Console.WriteLine($"💸 Valor da venda em reais: R$ {valorVendaReais:F2}");
                    await TelegramApi.SendMessageAsync($"🛑 STOP LOSS - BTC vendido com {perdaPercentual:F2}% de prejuízo.\n💸 Aproximadamente R$ {valorVendaReais:F2}");

                    if (valorCompraReais.HasValue)
                    {
                        var taxaCompra = valorCompraReais.Value * (taxaBinancePercentual / 100);
                        var taxaVenda = valorVendaReais * (taxaBinancePercentual / 100);
                        var resultado = valorVendaReais - valorCompraReais.Value - taxaCompra - taxaVenda;
                        Console.WriteLine($"📊 Resultado da operação: R$ {resultado:F2}");
                        await TelegramApi.SendMessageAsync($"📊 Resultado da operação: R$ {resultado:F2}");
                        valorCompraReais = null;
                    }

                    ultimaAcao = "venda";
                    precoEntrada = null;
                    maiorPrecoDesdeEntrada = null;
                    return;
                }

                if (quedaDesdeTopo <= -trailingStopPercentual && btcBalance > 0.0001m)
                {
                    await BinanceApi.PlaceMarketSellOrder(apiKey, secretKey, btcBalance);
                    var cotacaoDolar = await GetCotacaoDolarAsync();
                    var valorVendaReais = btcBalance * precoAtual * cotacaoDolar;
                    Console.WriteLine($"🏃‍♂️ TRAILING STOP - Venda após queda de {Math.Abs(quedaDesdeTopo):F2}% desde o topo.");
                    Console.WriteLine($"💸 Valor da venda em reais: R$ {valorVendaReais:F2}");
                    await TelegramApi.SendMessageAsync($"🏃‍♂️ TRAILING STOP - BTC vendido após queda de {Math.Abs(quedaDesdeTopo):F2}% desde o topo.\n💸 Aproximadamente R$ {valorVendaReais:F2}");

                    if (valorCompraReais.HasValue)
                    {
                        var taxaCompra = valorCompraReais.Value * (taxaBinancePercentual / 100);
                        var taxaVenda = valorVendaReais * (taxaBinancePercentual / 100);
                        var resultado = valorVendaReais - valorCompraReais.Value - taxaCompra - taxaVenda;
                        Console.WriteLine($"📊 Resultado da operação: R$ {resultado:F2}");
                        await TelegramApi.SendMessageAsync($"📊 Resultado da operação: R$ {resultado:F2}");
                        valorCompraReais = null;
                    }

                    ultimaAcao = "venda";
                    precoEntrada = null;
                    maiorPrecoDesdeEntrada = null;
                    return;
                }

                // Ainda não atingiu alvo, mas vamos deixar o stop armado se necessário mais tarde
            }

            // Executa PriceAction e coleta candle mais recente
            using var client = new HttpClient();
            var response = await client.GetStringAsync("https://api.binance.com/api/v3/klines?symbol=BTCUSDT&interval=1m&limit=1");
            var doc = JsonDocument.Parse(response);
            var candle = doc.RootElement.EnumerateArray().First();

            var open = decimal.Parse(candle[1].GetString()!, CultureInfo.InvariantCulture);
            var high = decimal.Parse(candle[2].GetString()!, CultureInfo.InvariantCulture);
            var low = decimal.Parse(candle[3].GetString()!, CultureInfo.InvariantCulture);
            var close = decimal.Parse(candle[4].GetString()!, CultureInfo.InvariantCulture);

            var candleRange = high - low;
            AjustarParametrosPorContexto(candleRange);

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
                var price = await BinanceApi.GetBitcoinPriceAsync();
                precoEntrada = price;
                maiorPrecoDesdeEntrada = price;
                var stepSize = 0.000001m;
                var quantidade = Math.Floor((usdtBalance / price) / stepSize) * stepSize;
                var valorCompra = quantidade * price;
                var cotacaoDolar = await GetCotacaoDolarAsync();
                valorCompraReais = valorCompra * cotacaoDolar;
                Console.WriteLine($"💵 Valor aproximado da compra em reais: R$ {valorCompraReais:F2}");
                await BinanceApi.PlaceMarketBuyOrder(apiKey, secretKey, quantidade);
                Console.WriteLine("✅ COMPRA EXECUTADA");
                await TelegramApi.SendMessageAsync($"✅ COMPRA EXECUTADA\nQuantidade: {quantidade} BTC\nValor aproximado: {(quantidade * price):C2}\n💵 Aproximadamente R$ {valorCompraReais:F2}");
                ultimaAcao = "compra";
            }
            else if (pressaoVenda && marteloReversao && btcBalance > 0.0001m)
            {
                if (ultimaAcao == "venda")
                {
                    Console.WriteLine("🚫 Venda ignorada (já executada anteriormente).");
                    return;
                }
                var sellPrice = await BinanceApi.GetBitcoinPriceAsync();
                var cotacaoDolar = await GetCotacaoDolarAsync();
                var valorVendaReais = btcBalance * sellPrice * cotacaoDolar;
                await BinanceApi.PlaceMarketSellOrder(apiKey, secretKey, btcBalance);
                Console.WriteLine("⚠️ VENDA EXECUTADA");
                Console.WriteLine($"💸 Valor aproximado da venda em reais: R$ {valorVendaReais:F2}");

                if (valorCompraReais.HasValue)
                {
                    var taxaCompra = valorCompraReais.Value * (taxaBinancePercentual / 100);
                    var taxaVenda = valorVendaReais * (taxaBinancePercentual / 100);
                    var resultado = valorVendaReais - valorCompraReais.Value - taxaCompra - taxaVenda;
                    Console.WriteLine($"📊 Resultado da operação: R$ {resultado:F2}");
                    await TelegramApi.SendMessageAsync($"📊 Resultado da operação: R$ {resultado:F2}");
                    valorCompraReais = null;
                }
                await TelegramApi.SendMessageAsync($"⚠️ VENDA EXECUTADA\nQuantidade: {btcBalance} BTC\nValor aproximado: {(btcBalance * sellPrice):C2}\n💸 Aproximadamente R$ {valorVendaReais:F2}");
                ultimaAcao = "venda";
                precoEntrada = null;
                maiorPrecoDesdeEntrada = null;
            }
            else
            {
                Console.WriteLine("📊 Nenhuma ação executada. Condições não atendidas.");
            }
        }
    }
}
