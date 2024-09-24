using Microsoft.Playwright;

namespace TakeInitiative.E2E.Tests;

public class HomePage : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>, IAsyncLifetime
{
    private readonly AuthenticatedWebAppWithDatabaseFixture fixture;
    private IPlaywright playwright;
    private IBrowser browser;

    public HomePage(AuthenticatedWebAppWithDatabaseFixture fixture)
    {
        this.fixture = fixture;
        VerifyImageHash.Initialize();
        VerifyImageHash.RegisterComparers();
    }

    [Theory]
    [InlineData("iPhone 13")]
    [InlineData("Desktop Chrome")]
    public async Task CaptureHomePage(string device)
    {
        await using var context = await browser.NewContextAsync(playwright.Devices[device]);
        var page = await browser.NewPageAsync();

        await page.GotoAsync("http://127.0.0.1:3000");

        var formattedDeviceName = device.Replace(" ", "-");
        _ = await page.ScreenshotAsync(new()
        {
            Path = $"./HomePage-{formattedDeviceName}.png",
            FullPage = true,
        });
        await VerifyFile($"./HomePage-{formattedDeviceName}.png");
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