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
   private readonly int _maxDepth;

public CrawlerService(int maxDepth = 3)
{
    _maxDepth = maxDepth;
}

    private readonly HashSet<string> _visited = new();
    private readonly List<PageResult> _results = new();
    private readonly Dictionary<string, (bool IsValid, int StatusCode)> _linkValidationCache = new();
    private readonly HttpClient _httpClient = new();
    private const int CrawlDelayMs = 500;

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
                List<ConsoleErrorInfo> consoleErrorDetails = new();

                page.Console += (_, msg) =>
                {
                    if (msg.Type == "error")
                    {
                        consoleErrors++;

                        consoleErrorDetails.Add(new ConsoleErrorInfo
                        {
                            Type = msg.Type,
                            Message = msg.Text
                        });
                    }
                };

                var stopwatch = Stopwatch.StartNew();

                var response = await page.GotoAsync(
                    url,
                    new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.DOMContentLoaded,
                        Timeout = 30000
                    });

                stopwatch.Stop();

                await SavePageHtml(page, url);

                int missingAltText =
                    await CountImagesMissingAlt(page);

                var (totalLinks, brokenLinks) = await CheckLinksAsync(page, url);

                var result = new PageResult
                {
                    Url = url,

                    HttpStatus =
                response?.Status ?? 0,

                    ResponseTimeMs =
                stopwatch.ElapsedMilliseconds,

                    ConsoleErrorDetails = consoleErrorDetails,
                    ConsoleErrors = consoleErrorDetails.Count,

                    MissingAltText =
                 missingAltText,

                    TotalLinks = totalLinks,

                    BrokenLinks = brokenLinks
                };

                AssignSeverity(result);

                result.Passed =
                    result.HttpStatus < 400 &&
                    result.ConsoleErrors == 0 &&
                    result.BrokenLinks.Count == 0;

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
                await Task.Delay(CrawlDelayMs);
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
        var links = await page.EvaluateAsync<LinkInfo[]>(
        @"() =>
    Array.from(document.querySelectorAll('a'))
    .map(a => ({
        href: a.href,
        anchorText:
            a.innerText.trim() ||
            a.getAttribute('aria-label') ||
            a.getAttribute('title') ||
            ''
    }))");

        return links
            .Where(link => !string.IsNullOrWhiteSpace(link.Href))
            .Select(link => NormalizeUrl(link.Href))
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
        string pagesFolder =
            Path.Combine(
                Directory.GetParent(AppContext.BaseDirectory)!
                    .Parent!
                    .Parent!
                    .Parent!
                    .FullName,
                "CapturedPages");

        Directory.CreateDirectory(pagesFolder);

        string fileName =
            url.Replace("https://", "")
               .Replace("/", "_");

        string html =
            await page.ContentAsync();

        await File.WriteAllTextAsync(
            Path.Combine(
                pagesFolder,
                $"{fileName}.html"),
            html);
    }

    private void AssignSeverity(PageResult result)
    {
        if (result.HttpStatus >= 400 || result.BrokenLinks.Count > 0)
        {
            result.Severity = "High";
        }
        else if (result.ConsoleErrors > 0 || result.ResponseTimeMs > 3000)
        {
            result.Severity = "Medium";
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

    private async Task<(int TotalLinks, List<BrokenLinkInfo> BrokenLinks)> CheckLinksAsync(
    IPage page,
    string sourcePage)
    {
        var links = await page.EvaluateAsync<LinkInfo[]>(
             @"() =>
            Array.from(document.querySelectorAll('a'))
            .map(a => ({
                href: a.href,
                anchorText:
                    a.innerText.trim() ||
                    a.getAttribute('aria-label') ||
                    a.getAttribute('title') ||
                    a.querySelector('img')?.getAttribute('alt') ||
                    a.querySelector('img')?.getAttribute('title') ||
                    '[No Text]'
            }))");

        int totalLinks = 0;
        int statusCode = 0;
        List<BrokenLinkInfo> brokenLinks = new();



        foreach (var link in links
    .GroupBy(l => l.Href)
    .Select(g => g.First()))
        {
            if (string.IsNullOrWhiteSpace(link.Href))
                continue;

            // Ignore page anchors and non-web links
            if (link.Href.StartsWith("#") ||
             link.Href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
             link.Href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
             link.Href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!Uri.TryCreate(link.Href, UriKind.Absolute, out var uri))
                continue;

            // Ignore page fragments such as #content, #section
             if (!string.IsNullOrEmpty(uri.Fragment))
            { 
                continue;
            
            }

            // Skip external domains
            if (!uri.Host.EndsWith("onezero.com", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            totalLinks++;

            bool isValid;

            if (_linkValidationCache.TryGetValue(link.Href, out var cached))
            {
                isValid = cached.IsValid;

                if (!isValid)
                {
                    brokenLinks.Add(new BrokenLinkInfo
                    {
                        SourcePage = sourcePage,
                        BrokenUrl = link.Href,
                        AnchorText = link.AnchorText,
                        StatusCode = cached.StatusCode
                    });
                }

                continue;
            }


            
            try
            {
                HttpResponseMessage response;

                try
                {
                    response = await _httpClient.SendAsync(
                        new HttpRequestMessage(HttpMethod.Head, link.Href));
                }
                catch
                {
                    response = await _httpClient.GetAsync(link.Href);
                }

                statusCode = (int)response.StatusCode;
                isValid = response.IsSuccessStatusCode;
            }
            catch
            {
                statusCode = 0;   // No HTTP response (timeout, DNS error, etc.)
                isValid = false;
            }

            _linkValidationCache[link.Href] = (isValid, statusCode);

            if (!isValid)
                
                {
                brokenLinks.Add(new BrokenLinkInfo
                {
                    SourcePage = sourcePage,
                    BrokenUrl = link.Href,
                    AnchorText = link.AnchorText,
                    StatusCode = statusCode
                });
            }
        }

        return (totalLinks, brokenLinks);
    } 
}