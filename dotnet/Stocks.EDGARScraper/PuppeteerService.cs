﻿using System;
using System.Threading.Tasks;
using PuppeteerSharp;

namespace EDGARScraper;

/// <summary>
/// Not yet in use, but the code is working so may be re-used in the future
/// </summary>
internal class PuppeteerService {
    internal static async Task<string> FetchRenderedHtmlAsync(string url) {
        using IBrowser browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
        using IPage page = await browser.NewPageAsync();

        // Navigate to the page and wait for network activity to idle
        await page.SetUserAgentAsync("EDGARScraper/0.1 (inno.and.logic@gmail.com)");
        _ = await page.GoToAsync(url, WaitUntilNavigation.Networkidle2);
        return await page.GetContentAsync();
    }

    internal static async Task EnsureBrowser() {
        Console.WriteLine("Downloading Chromium...");
        var browserFetcher = new BrowserFetcher();
        PuppeteerSharp.BrowserData.InstalledBrowser? latestRevision = await browserFetcher.DownloadAsync();

        if (latestRevision is null) {
            Console.WriteLine("Failed to download Chromium.");
            return;
        }

        Console.WriteLine("Chromium downloaded successfully. Build id: {0}", latestRevision.BuildId);

        // Get all downloaded revisions
        System.Collections.Generic.IEnumerable<PuppeteerSharp.BrowserData.InstalledBrowser> installedBrowsers = browserFetcher.GetInstalledBrowsers();

        // Delete older revisions
        foreach (PuppeteerSharp.BrowserData.InstalledBrowser? browser in installedBrowsers) {
            if (browser.BuildId == latestRevision.BuildId)
                continue;

            Console.WriteLine($"Removing old Chromium revision: {browser.BuildId}");
            browserFetcher.Uninstall(browser.BuildId);
        }
    }
}
