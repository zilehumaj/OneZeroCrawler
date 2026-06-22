# Architecture

## Overview

This solution is a Playwright-based website crawler and smoke-test tool.

## Components

### Program.cs

Application entry point.

### CrawlerService

Responsible for:

- Crawling pages
- Limiting crawl depth
- Preventing duplicate visits
- Capturing page HTML
- Executing health checks

### Health Checks

- HTTP status validation
- Response time measurement
- Console error detection
- Missing ALT text detection
- Broken link detection

### ReportGenerator

Generates:

- JSON report
- HTML report

## Infinite Loop Prevention

The crawler uses:

- HashSet of visited URLs
- URL normalization
- Maximum crawl depth

## Rate Limiting

A configurable delay of 500ms is applied between requests.

## CI/CD Integration

The tool can run within:

- GitHub Actions
- Azure DevOps Pipelines
- Jenkins

Pipeline Flow:

Build
→ Execute Crawl
→ Generate Reports
→ Publish Reports