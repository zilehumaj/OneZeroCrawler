# oneZero Dynamic Web Crawler & Smoke Test Tool

## Features

- Crawl onezero.com to configurable depth
- Capture page HTML
- HTTP status validation
- Response time monitoring
- Console error detection
- Accessibility checks (missing ALT text)
- Broken link detection
- JSON report generation
- HTML report generation

## Run

dotnet build

dotnet run

## Output

CapturedPages/

Reports/report.json

Reports/report.html

## Architecture

The solution uses:
- BFS crawling
- Playwright browser automation
- Health checks
- Accessibility validation
- JSON and HTML reporting

## Crawl Configuration

Current depth: 3

## Health Checks

- HTTP status
- Response time
- Console errors
- Missing ALT text
- Broken links

## Rate Limiting

500ms delay between requests.
