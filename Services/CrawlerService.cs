using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Playwright;

namespace OneZeroCrawler.Services;

public class CrawlerService
{
    private readonly string _baseDomain = "onezero.com";
    private readonly int _maxDepth = 3;

    public async Task CrawlAsync()
    {
        var visited = new HashSet<string>();

        var queue = new Queue<(string Url, int Depth)>();

        queue.Enqueue(("https://www.onezero.com", 0));

        using var playwright = await Playwright.CreateAsync();

        await using var browser =
            await playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions
                {
                    Headless = true
                });

        while (queue.Count > 0)
        {
            var (url, depth) = queue.Dequeue();

            if (visited.Contains(url))
                continue;

            if (depth > _maxDepth)
                continue;

            visited.Add(url);

            Console.WriteLine($"Depth:{depth} -> {url}");

            var page = await browser.NewPageAsync();

            try
            {
                await page.GotoAsync(url);

                await SavePageHtml(page, url);

                var links = await ExtractLinks(page);

                foreach (var link in links)
                {
                    if (!visited.Contains(link))
                    {
                        queue.Enqueue((link, depth + 1));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {url}");
                Console.WriteLine(ex.Message);
            }

            await page.CloseAsync();
        }

        Console.WriteLine();
        Console.WriteLine($"Total Pages Crawled: {visited.Count}");
    }

    private async Task<List<string>> ExtractLinks(IPage page)
    {
        var links = await page.EvaluateAsync<string[]>(
            @"() =>
        Array.from(document.querySelectorAll('a'))
             .map(a => a.href)");

        return links
            .Where(link =>
                !string.IsNullOrWhiteSpace(link) &&
                Uri.TryCreate(link, UriKind.Absolute, out var uri) &&
                uri.Host.Contains(_baseDomain))
            .Distinct()
            .ToList();
    }

    private async Task SavePageHtml(IPage page, string url)
    {
        Directory.CreateDirectory("CapturedPages");

        var html = await page.ContentAsync();

        string fileName =
            url.Replace("https://", "")
               .Replace("/", "_")
               .Replace("?", "_")
               .Replace("&", "_");

        await File.WriteAllTextAsync(
            $"CapturedPages/{fileName}.html",
            html);
    }
}