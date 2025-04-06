using System.Text.Json;

public static class BinanceApi
{
    public static async Task<decimal> GetBitcoinPriceAsync()
    {
        using var httpClient = new HttpClient();
        var response = await httpClient.GetStringAsync("https://api.binance.com/api/v3/ticker/price?symbol=BTCUSDT");

        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;
        var price = root.GetProperty("price").GetString();

        return decimal.Parse(price!, System.Globalization.CultureInfo.InvariantCulture);
    }

    public static async Task<string> GetAccountBalanceAsync(string apiKey, string secretKey)
    {
        using var httpClient = new HttpClient();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var queryString = $"timestamp={timestamp}";

        var signature = CreateHmacSignature(queryString, secretKey);
        var requestUri = $"https://api.binance.com/api/v3/account?{queryString}&signature={signature}";

        httpClient.DefaultRequestHeaders.Add("X-MBX-APIKEY", apiKey);

        var response = await httpClient.GetAsync(requestUri);
        var content = await response.Content.ReadAsStringAsync();

        return content;
    }

    public static async Task<decimal> GetUsdtBalanceAsync(string apiKey, string secretKey)
    {
        using var httpClient = new HttpClient();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var queryString = $"timestamp={timestamp}";

        var signature = CreateHmacSignature(queryString, secretKey);
        var requestUri = $"https://api.binance.com/api/v3/account?{queryString}&signature={signature}";

        httpClient.DefaultRequestHeaders.Add("X-MBX-APIKEY", apiKey);

        var response = await httpClient.GetAsync(requestUri);
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var balances = doc.RootElement.GetProperty("balances");

        foreach (var asset in balances.EnumerateArray())
        {
            if (asset.GetProperty("asset").GetString() == "USDT")
            {
                var free = asset.GetProperty("free").GetString();
                return decimal.Parse(free!, System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return 0m;
    }

    public static async Task<decimal> GetBtcBalanceAsync(string apiKey, string secretKey)
    {
        using var httpClient = new HttpClient();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var queryString = $"timestamp={timestamp}";

        var signature = CreateHmacSignature(queryString, secretKey);
        var requestUri = $"https://api.binance.com/api/v3/account?{queryString}&signature={signature}";

        httpClient.DefaultRequestHeaders.Add("X-MBX-APIKEY", apiKey);

        var response = await httpClient.GetAsync(requestUri);
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var balances = doc.RootElement.GetProperty("balances");

        foreach (var asset in balances.EnumerateArray())
        {
            if (asset.GetProperty("asset").GetString() == "BTC")
            {
                var free = asset.GetProperty("free").GetString();
                return decimal.Parse(free!, System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return 0m;
    }
    
    public static async Task<(List<(decimal price, decimal quantity)> bids, List<(decimal price, decimal quantity)> asks)> GetOrderBookAsync()
    {
        using var httpClient = new HttpClient();
        var response = await httpClient.GetStringAsync("https://api.binance.com/api/v3/depth?symbol=BTCUSDT&limit=5");
        
        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;
        
        var bids = new List<(decimal price, decimal quantity)>();
        var asks = new List<(decimal price, decimal quantity)>();
        
        foreach (var bid in root.GetProperty("bids").EnumerateArray())
        {
            var price = decimal.Parse(bid[0].GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            var quantity = decimal.Parse(bid[1].GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            bids.Add((price, quantity));
        }
        
        foreach (var ask in root.GetProperty("asks").EnumerateArray())
        {
            var price = decimal.Parse(ask[0].GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            var quantity = decimal.Parse(ask[1].GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            asks.Add((price, quantity));
        }
        
        return (bids, asks);
    }
    
    public static async Task<string> PlaceMarketBuyOrder(string apiKey, string secretKey, decimal usdtAmount)
    {
        using var httpClient = new HttpClient();

        // 1. Buscar preço atual do BTC para estimar quantidade
        var price = await GetBitcoinPriceAsync();
        var quantity = Math.Round(usdtAmount / price, 6); // Binance aceita até 6 casas para BTC

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var queryString = $"symbol=BTCUSDT&side=BUY&type=MARKET&quantity={quantity}&timestamp={timestamp}";

        var signature = CreateHmacSignature(queryString, secretKey);
        var requestUri = $"https://api.binance.com/api/v3/order?{queryString}&signature={signature}";

        httpClient.DefaultRequestHeaders.Add("X-MBX-APIKEY", apiKey);

        var response = await httpClient.PostAsync(requestUri, null);
        return await response.Content.ReadAsStringAsync();
    }

    public static async Task<string> PlaceMarketSellOrder(string apiKey, string secretKey, decimal btcAmount)
    {
        using var httpClient = new HttpClient();

        var quantity = Math.Round(btcAmount, 6); // Arredondar conforme precisões da Binance
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var queryString = $"symbol=BTCUSDT&side=SELL&type=MARKET&quantity={quantity}&timestamp={timestamp}";

        var signature = CreateHmacSignature(queryString, secretKey);
        var requestUri = $"https://api.binance.com/api/v3/order?{queryString}&signature={signature}";

        httpClient.DefaultRequestHeaders.Add("X-MBX-APIKEY", apiKey);

        var response = await httpClient.PostAsync(requestUri, null);
        return await response.Content.ReadAsStringAsync();
    }
    
    public static string CreateHmacSignature(string message, string key)
    {
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(key);
        var messageBytes = System.Text.Encoding.UTF8.GetBytes(message);

        using var hmac = new System.Security.Cryptography.HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(messageBytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }
}