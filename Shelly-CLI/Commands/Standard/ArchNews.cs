using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PackageManager.Alpm;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Standard;

public class ArchNews : AsyncCommand<ArchNewsSettings>
{
    
    private static readonly string?  Username = Environment.GetEnvironmentVariable("USER");
    
    private static readonly string FeedFolder =  Path.Combine("/home", Username ?? throw new InvalidOperationException(), ".cache", "Shelly", "archNewsFeed");

    private static readonly string FeedPath = Path.Combine(FeedFolder, "Feed.json");
    
    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] ArchNewsSettings settings)
    {
        if (settings.All)
        {
            try
            {
                var feed = await GetRssFeedAsync("https://archlinux.org/feeds/news/");
                foreach (var item in feed)
                {
                    AnsiConsole.MarkupLine($"[yellow]\n{item.Title.EscapeMarkup()}[/]");
                    AnsiConsole.MarkupLine($"[gray]{item.PubDate.EscapeMarkup()}[/]");
                    AnsiConsole.MarkupLine($"[blue]{item.Link.EscapeMarkup()}[/]");
                    AnsiConsole.MarkupLine($"[white]{item.Description.EscapeMarkup()}[/]");
                }

                CacheFeed(feed);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
        else
        {
            var cachedFeed = LoadCachedFeed();
            var feed = await GetRssFeedAsync("https://archlinux.org/feeds/news/");
            
            var newFeed = feed.Except(cachedFeed).ToList();
            foreach (var item in newFeed)
            {
                AnsiConsole.MarkupLine($"[yellow]\n{item.Title.EscapeMarkup()}[/]");
                AnsiConsole.MarkupLine($"[gray]{item.PubDate.EscapeMarkup()}[/]");
                AnsiConsole.MarkupLine($"[blue]{item.Link.EscapeMarkup()}[/]");
                AnsiConsole.MarkupLine($"[white]{item.Description.EscapeMarkup()}[/]");
            }
            if(newFeed.Count > 0) CacheFeed(feed);
            else AnsiConsole.MarkupLine("[green]No new news found[/]");
        }

        return 0;
    }

    private static void CacheFeed(List<RssModel> feed)
    {
        if (!Directory.Exists(FeedFolder)) Directory.CreateDirectory(FeedFolder);

        var json = JsonSerializer.Serialize(feed, ShellyCLIJsonContext.Default.ListRssModel);
        File.WriteAllText(FeedPath, json);
    }

    private static List<RssModel> LoadCachedFeed()
    {
        if (!File.Exists(FeedPath)) return [];

        try
        {
            var json = File.ReadAllText(FeedPath);
            return JsonSerializer.Deserialize(json, ShellyCLIJsonContext.Default.ListRssModel) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static async Task<List<RssModel>> GetRssFeedAsync(string url)
    {
        using var client = new HttpClient();
        var xmlString = await client.GetStringAsync(url);

        var xml = XDocument.Parse(xmlString);

        return xml.Descendants("item").Select(item => new RssModel
        {
            Title = item.Element("title")?.Value ?? "", Link = item.Element("link")?.Value ?? "",
            Description = Regex.Replace(item.Element("description")?.Value ?? "", "<.*?>", string.Empty),
            PubDate = item.Element("pubDate")?.Value ?? ""
        }).Reverse().ToList();
    }

    public record RssModel
    {
        public string? Title { get; init; }
        public string? Link { get; init; }
        public string? Description { get; init; }
        public string? PubDate { get; init; }
    }
}