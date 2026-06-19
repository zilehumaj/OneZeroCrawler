using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using Microsoft.Playwright;
using OneZeroCrawler.Models;

namespace OneZeroCrawler.Services;

public class CrawlerService
{
    private readonly string _startUrl = "https://www.onezero.com";
    private readonly string _allowedHost = "www.onezero.com";
    private readonly int _maxDepth = 3;

    private readonly HashSet<string> _visited = new();
    private readonly List<PageResult> _results = new();

    public List<PageResult> Results => _results;

    public async Task CrawlAsync()
    {
        var queue = new Queue<(string Url, int Depth)>();

        queue.Enqueue((_startUrl, 0));

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

            if (depth > _maxDepth)
                continue;

            url = NormalizeUrl(url);

            if (_visited.Contains(url))
                continue;

            _visited.Add(url);

            Console.WriteLine(
                $"Depth {depth} | Crawling: {url}");

            var page = await browser.NewPageAsync();

            try
            {
                int consoleErrors = 0;

                page.Console += (_, msg) =>
                {
                    if (msg.Type == "error")
                    {
                        consoleErrors++;
                    }
                };

                var stopwatch = Stopwatch.StartNew();

                var response = await page.GotoAsync(
                    url,
                    new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.Load,
                        Timeout = 30000
                    });

                stopwatch.Stop();

                await SavePageHtml(page, url);

                int missingAltText =
                    await CountImagesMissingAlt(page);

                var linkResult =
                    await CheckLinksAsync(page);

                var result = new PageResult
                {
                    Url = url,

                    HttpStatus =
                response?.Status ?? 0,

                    ResponseTimeMs =
                stopwatch.ElapsedMilliseconds,

                    ConsoleErrors =
                 consoleErrors,

                    MissingAltText =
                 missingAltText,

                    TotalLinks =
                linkResult.TotalLinks,

                    BrokenLinks =
                linkResult.BrokenLinks
                };

                AssignSeverity(result);

                result.Passed =
                    result.HttpStatus < 400 &&
                    result.ConsoleErrors == 0 &&
                    result.BrokenLinks == 0;

                _results.Add(result);

                Console.WriteLine(
                    $"Status:{result.HttpStatus} | " +
                    $"Time:{result.ResponseTimeMs}ms | " +
                    $"Errors:{result.ConsoleErrors}");

                if (depth < _maxDepth)
                {
                    var links = await ExtractLinks(page);

                    foreach (var link in links)
                    {
                        if (!_visited.Contains(link))
                        {
                            queue.Enqueue(
                                (link, depth + 1));
                        }
                    }
                }

                // Rate limiting
                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"ERROR: {url}");

                Console.WriteLine(ex.Message);
            }
            finally
            {
                await page.CloseAsync();
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            $"Total Pages Crawled: {_visited.Count}");
    }

    public void PrintSummary()
    {
        Console.WriteLine();
        Console.WriteLine("===== CRAWL SUMMARY =====");

        foreach (var result in _results)
        {
            Console.WriteLine(
                $"URL: {result.Url}");

            Console.WriteLine(
                $"Status: {result.HttpStatus}");

            Console.WriteLine(
                $"Response Time: {result.ResponseTimeMs}ms");

            Console.WriteLine(
                $"Console Errors: {result.ConsoleErrors}");

            Console.WriteLine(
                $"Severity: {result.Severity}");

            Console.WriteLine(
                $"Passed: {result.Passed}");

            Console.WriteLine(
                $"Missing Alt Text: {result.MissingAltText}");

            Console.WriteLine(
                $"Total Links: {result.TotalLinks}");

            Console.WriteLine(
                $"Broken Links: {result.BrokenLinks}");

            Console.WriteLine(
                new string('-', 60));

        }
    }

    //Private Methods//

    private async Task<List<string>> ExtractLinks(IPage page)
    {
        var urls = await page.EvaluateAsync<string[]>(
            @"() =>
            Array.from(document.querySelectorAll('a'))
                 .map(a => a.href)");

        return urls
            .Where(link => !string.IsNullOrWhiteSpace(link))
            .Select(NormalizeUrl)
            .Where(IsValidUrl)
            .Distinct()
            .ToList();
    }

    private bool IsValidUrl(string url)
    {
        if (!Uri.TryCreate(
            url,
            UriKind.Absolute,
            out var uri))
        {
            return false;
        }

        // Stay within onezero domain
        if (!uri.Host.Equals(
                _allowedHost,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string[] excludedExtensions =
        {
            ".pdf",
            ".jpg",
            ".jpeg",
            ".png",
            ".gif",
            ".svg",
            ".css",
            ".js",
            ".xml",
            ".zip"
        };

        if (excludedExtensions.Any(
            ext => uri.AbsolutePath.EndsWith(
                ext,
                StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        string[] excludedPaths =
        {
            "/author/",
            "/events/",
            "/awards/"
        };

        if (excludedPaths.Any(
            path => uri.AbsolutePath.Contains(
                path,
                StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }

    private string NormalizeUrl(string url)
    {
        if (!Uri.TryCreate(
            url,
            UriKind.Absolute,
            out var uri))
        {
            return url;
        }

        return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}"
            .TrimEnd('/');
    }

    private async Task SavePageHtml(
        IPage page,
        string url)
    {
        Directory.CreateDirectory(
            "CapturedPages");

        string fileName =
            url.Replace("https://", "")
               .Replace("/", "_");

        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "homepage";
        }

        string html =
            await page.ContentAsync();

        await File.WriteAllTextAsync(
            Path.Combine(
                "CapturedPages",
                $"{fileName}.html"),
            html);
    }

    private void AssignSeverity(PageResult result)
    {
        if (result.HttpStatus >= 500)
        {
            result.Severity = "Critical";
        }
        else if (result.BrokenLinks > 5)
        {
            result.Severity = "High";
        }
        else if (result.ResponseTimeMs > 5000)
        {
            result.Severity = "High";
        }
        else if (result.ResponseTimeMs > 3000)
        {
            result.Severity = "Medium";
        }
        else if (result.ConsoleErrors > 0)
        {
            result.Severity = "Medium";
        }
        else if (result.MissingAltText > 0)
        {
            result.Severity = "Low";
        }
        else
        {
            result.Severity = "Low";
        }
    }

    private async Task<int> CountImagesMissingAlt(IPage page)
    {
        return await page.EvaluateAsync<int>(
            @"() =>
        {
            const images =
                document.querySelectorAll('img');

            let count = 0;

            images.forEach(img =>
            {
                const alt =
                    img.getAttribute('alt');

                if(!alt || alt.trim() === '')
                {
                    count++;
                }
            });

            return count;
        }");
    }

    private async Task<(int TotalLinks, int BrokenLinks)>CheckLinksAsync(IPage page)
    {
        var links = await page.EvaluateAsync<string[]>(
            @"() =>
            Array.from(
                document.querySelectorAll('a'))
            .map(a => a.href)");

        int totalLinks = 0;
        int brokenLinks = 0;

        using HttpClient client = new();

        foreach (var link in links.Distinct())
        {
            if (string.IsNullOrWhiteSpace(link))
                continue;

            totalLinks++;

            try
            {
                var response =
                    await client.SendAsync(
                        new HttpRequestMessage(
                            HttpMethod.Head,
                            link));

                if (!response.IsSuccessStatusCode)
                {
                    brokenLinks++;
                }
            }
            catch
            {
                brokenLinks++;
            }
        }

        return (totalLinks, brokenLinks);
    }
}