using Microsoft.Playwright;
using OneZeroCrawler.Services;

class Program
{
    static async Task Main(string[] args)
    {
        using var playwright = await Playwright.CreateAsync();

        await using var browser =
            await playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions
                {
                    Headless = true
                });

        var page = await browser.NewPageAsync();

        await page.GotoAsync("https://www.onezero.com");

        Console.WriteLine(
            $"Title: {await page.TitleAsync()}");

        var html = await page.ContentAsync();

        Directory.CreateDirectory("CapturedPages");

        await File.WriteAllTextAsync(
            "CapturedPages/homepage.html",
            html);

        Console.WriteLine("HTML saved successfully.");

       

        var crawler = new CrawlerService();

        await crawler.CrawlAsync();
    }
}