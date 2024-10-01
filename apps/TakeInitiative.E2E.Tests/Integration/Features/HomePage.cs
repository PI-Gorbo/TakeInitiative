using DotNet.Testcontainers.Builders;
using Microsoft.Playwright;

namespace TakeInitiative.E2E.Tests;

public class HomePage : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>, IAsyncLifetime
{
    private readonly string? csprojDirectory;
    private readonly AuthenticatedWebAppWithDatabaseFixture fixture;
    private IPlaywright playwright;
    private IBrowser browser;

    public HomePage(AuthenticatedWebAppWithDatabaseFixture fixture)
    {
        this.fixture = fixture;
        if (!VerifyImageHash.Initialized)
        {
            VerifyImageHash.Initialize();
            VerifyImageHash.RegisterComparers();
        }
    }

    [Fact]
    public async Task CaptureHomePage_Phone()
    {
        await using var context = await browser.NewContextAsync(playwright.Devices["iPhone 13"]);
        var page = await browser.NewPageAsync(new() { 
            IsMobile = true,
            ViewportSize = new ViewportSize()
            {
                Height = 812,
                Width = 375
            },
        });

        await page.GotoAsync("http://127.0.0.1:3000");
        var filePath = Path.Combine(CurrentFile.Directory(), $"HomePage-Mobile.png");
        _ = await page.ScreenshotAsync(new()
        {
            Path = filePath,
            FullPage = true

        });
        await VerifyFile(filePath);
    }

    [Fact]
    public async Task CaptureHomePage_Desktop()
    {
        await using var context = await browser.NewContextAsync(playwright.Devices["Desktop Chrome"]);
        var page = await browser.NewPageAsync();

        await page.GotoAsync("http://127.0.0.1:3000");
        var filePath = Path.Combine(CurrentFile.Directory(), $"HomePage-Desktop.png");
        _ = await page.ScreenshotAsync(new()
        {
            Path = filePath,
            FullPage = true,

        });
        await VerifyFile(filePath);
    }

    public async Task DisposeAsync()
    {
        await browser.DisposeAsync();
        playwright.Dispose();
    }

    public async Task InitializeAsync()
    {
        this.playwright = await Playwright.CreateAsync();
        this.browser = await playwright.Chromium.LaunchAsync();
    }
}