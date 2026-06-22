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

            <table>

            <tr>
            <th>URL</th>
            <th>Status</th>
            <th>Response Time</th>
            <th>Console Errors</th>
            <th>Missing Alt</th>
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
            <td>{r.ResponseTimeMs}</td>
            <td>{r.ConsoleErrors}</td>
            <td>{r.MissingAltText}</td>
            <td>{r.BrokenLinks}</td>
            <td>{r.Severity}</td>
            <td>{r.Passed}</td>
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