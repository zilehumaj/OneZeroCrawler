using System.Text.Json;
using OneZeroCrawler.Models;

namespace OneZeroCrawler.Services;

public static class ReportGenerator
{
    private static string GetProjectRoot()
    {
        return Directory.GetParent(AppContext.BaseDirectory)!
            .Parent!    // net10.0
            .Parent!    // Debug
            .Parent!    // bin
            .FullName;
    }
    public static async Task GenerateJsonReport(
        List<PageResult> results)
    {
        string reportFolder =
            Path.Combine(GetProjectRoot(), "Reports");

        Directory.CreateDirectory(reportFolder);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        string json =
            JsonSerializer.Serialize(
                results,
                options);

        await File.WriteAllTextAsync(
            Path.Combine(reportFolder, "report.json"),
            json);

        Console.WriteLine("JSON report generated.");
    }

    public static async Task GenerateHtmlReport(
    List<PageResult> results)
    {
        string reportFolder =
        Path.Combine(GetProjectRoot(), "Reports");

        Directory.CreateDirectory(reportFolder);


        var html = $@"
            <html>
            <head>
            <title>oneZero Crawl Report</title>
            <style>
            table {{
            border-collapse: collapse;
            width:100%;
            }}

            th,td {{
            border:1px solid black;
            padding:8px;
            text-align:left;
            }}
            </style>
            </head>
            <body>

            <h1>oneZero Crawl Report</h1>

            <h2>Summary</h2>

            <p>
            Pages Crawled: {results.Count}<br/>
            Pages Passed: {results.Count(r => r.Passed)}<br/>
            Pages Failed: {results.Count(r => !r.Passed)}<br/>
            Slow Pages (>3000 ms): {results.Count(r => r.ResponseTimeMs > 3000)}<br/>
            Pages with Broken Links: {results.Count(r => r.BrokenLinks.Any())}
            </p>
            <table>

            <tr>
            <th>URL</th>
            <th>Status</th>
            <th>Response Time (ms)</th>
            <th>Console Errors</th>
            <th>Missing Alt</th>
            <th>Total Links</th>
            <th>Broken Links</th>
            <th>Severity</th>
            <th>Passed</th>
            </tr>

            {string.Join("",
                results.Select(r =>
                $@"
            <tr>
            <td>{r.Url}</td>
            <td>{r.HttpStatus}</td>
           <td>{(r.ResponseTimeMs > 3000
            ? $"{r.ResponseTimeMs} ⚠"
            : r.ResponseTimeMs.ToString())}</td>
            <td>
            {(r.ConsoleErrors == 0
                ? "None"
                : string.Join("<br/>",
                    r.ConsoleErrorDetails.Select(c =>
                        $"{c.Type}: {System.Net.WebUtility.HtmlEncode(c.Message)}")))}
            </td>
            <td>{r.MissingAltText}</td>
            <td>{r.TotalLinks}</td>
            <td>
            {string.Join("<hr/>",
            r.BrokenLinks.Select(b =>
            $@"Source: {b.SourcePage}<br/>
            Anchor Text: {System.Net.WebUtility.HtmlEncode(b.AnchorText)}<br/>
            Broken URL: {b.BrokenUrl}<br/>
            Status: {b.StatusCode}"))}
            </td>
            <td>{r.Severity}</td>
            <td>{(r.Passed ? "PASS" : "FAIL")}</td>
            </tr>"
                ))}
            </table>

            </body>
            </html>";

        await File.WriteAllTextAsync(
        Path.Combine(reportFolder, "report.html"),
        html);

        Console.WriteLine(
            "HTML report generated.");
    }
}