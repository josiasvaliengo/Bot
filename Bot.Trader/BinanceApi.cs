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

    public static string CreateHmacSignature(string message, string key)
    {
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(key);
        var messageBytes = System.Text.Encoding.UTF8.GetBytes(message);

        using var hmac = new System.Security.Cryptography.HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(messageBytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }
}