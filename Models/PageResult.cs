using System;
using System.Collections.Generic;
using System.Text;

namespace OneZeroCrawler.Models;

public class PageResult
{
    public string Url { get; set; } = string.Empty;

    public int HttpStatus { get; set; }

    public long ResponseTimeMs { get; set; }

    public int ConsoleErrors { get; set; }

    public List<ConsoleErrorInfo> ConsoleErrorDetails { get; set; } = new();

    public int MissingAltText { get; set; }

    public int TotalLinks { get; set; }

    public List<BrokenLinkInfo> BrokenLinks { get; set; } = new();

    public string Severity { get; set; } = "None";

    public bool Passed { get; set; }
}