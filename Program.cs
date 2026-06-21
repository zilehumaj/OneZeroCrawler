using Microsoft.Playwright;
using OneZeroCrawler.Services;

class Program
{
    static async Task Main(string[] args)
    {
        const int CrawlDepth = 3;

        var crawler = new CrawlerService(CrawlDepth);

        await crawler.CrawlAsync();

        crawler.PrintSummary();
    }
}