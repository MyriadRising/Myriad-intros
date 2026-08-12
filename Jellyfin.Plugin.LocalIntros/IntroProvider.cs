
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.LocalIntros.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Entities.Movies;
using Microsoft.Extensions.Logging;


namespace Jellyfin.Plugin.LocalIntros;
public class IntroProvider : IIntroProvider
{
    private readonly ILogger<IntroProvider> logger;

    public IntroProvider(ILoggerFactory loggerFactory)
    {
        logger = loggerFactory.CreateLogger<IntroProvider>();
    }

    public string Name { get; } = "Intros";

    public Task<IEnumerable<IntroInfo>> GetIntros(BaseItem item, User user)
    {
        try
        {
            if (LocalIntrosPlugin.Instance.Configuration.Local != string.Empty)
            {
                logger.LogTrace("Local Config Detected, retrieving local intros.");
                return Task.FromResult(Local(item, user));
            }
            else
            {
                logger.LogError("No Local Config Detected, retrieving library intros.");
                return Task.FromResult(Enumerable.Empty<IntroInfo>());
            }

        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving intros");
            return Task.FromResult(Enumerable.Empty<IntroInfo>());
        }
    }

    private readonly Random _random = new Random();

    private static string introsPath => LocalIntrosPlugin.Instance.Configuration.Local;

    /// <summary>
    /// Gets the tags, official rating, and media type (Movies/Shows) relevant to matching this item
    /// against configured intro rules. Tags and rating are inherited up from episode -> season -> series,
    /// matching how Jellyfin displays them for episodes.
    /// </summary>
    private (HashSet<string> tags, string rating, MediaTypeFilter mediaType) GetCriteria(BaseItem item)
    {
        switch (item.GetBaseItemKind())
        {
            case Data.Enums.BaseItemKind.Movie:
                var movie = item as Movie;
                return (
                    movie.Tags.ToHashSet(StringComparer.OrdinalIgnoreCase),
                    movie.OfficialRating,
                    MediaTypeFilter.Movies
                );
            case Data.Enums.BaseItemKind.Episode:
                var episode = item as Episode;
                var season = episode.Season;
                var series = episode.Series;
                var tags = episode.Tags
                    .Concat(season?.Tags ?? Array.Empty<string>())
                    .Concat(series?.Tags ?? Array.Empty<string>())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var rating = episode.OfficialRating ?? season?.OfficialRating ?? series?.OfficialRating;
                return (tags, rating, MediaTypeFilter.Shows);
            default:
                // Unsupported item type - MediaTypeFilter.None will never match a real rule and
                // will short-circuit selection below.
                return (new HashSet<string>(StringComparer.OrdinalIgnoreCase), null, MediaTypeFilter.None);
        }
    }

    /// <summary>
    /// Determines whether a rule is eligible to play for this item and user.
    /// Empty Tags/Ratings/UserIds on the rule mean "no restriction" (all).
    /// </summary>
    private bool RuleMatches(IntroRule rule, MediaTypeFilter itemMediaType, HashSet<string> itemTags, string itemRating, Guid userId)
    {
        if (rule.MediaType == MediaTypeFilter.None)
        {
            return false;
        }

        if (rule.MediaType != MediaTypeFilter.Both && rule.MediaType != itemMediaType)
        {
            return false;
        }

        if (rule.Tags.Count > 0 && !rule.Tags.Any(t => itemTags.Contains(t)))
        {
            return false;
        }

        if (rule.Ratings.Count > 0)
        {
            if (itemRating == null || !rule.Ratings.Any(r => r.Equals(itemRating, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        if (rule.UserIds.Count > 0 && !rule.UserIds.Contains(userId))
        {
            return false;
        }

        return true;
    }

    private IEnumerable<IntroInfo> Local(BaseItem item, User user)
    {
        if (LocalIntrosPlugin.Instance.Configuration.DisabledUserIds.Contains(user.Id))
        {
            logger.LogInformation($"Intros disabled for user {user.Username}, skipping.");
            return Enumerable.Empty<IntroInfo>();
        }

        if (!File.Exists(introsPath) && !Directory.Exists(introsPath))
        {
            throw new Exception("No intros found in local path");
        }

        var libraryResults = RetrieveIntroLibrary();

        if (!libraryResults.Any())
        {
            throw new Exception("No intros found in library");
        }

        var (itemTags, itemRating, itemMediaType) = GetCriteria(item);

        if (itemMediaType == MediaTypeFilter.None)
        {
            logger.LogTrace("Item is not a movie or episode, no intro to select.");
            return Enumerable.Empty<IntroInfo>();
        }

        var eligibleRules = LocalIntrosPlugin.Instance.Configuration.IntroRules
            .Where(r => libraryResults.ContainsKey(r.IntroId))
            .Where(r => RuleMatches(r, itemMediaType, itemTags, itemRating, user.Id))
            .ToList();

        if (!eligibleRules.Any())
        {
            logger.LogInformation("No eligible intros for this item/user.");
            return Enumerable.Empty<IntroInfo>();
        }

        logger.LogInformation($"Selecting intro from {eligibleRules.Count} eligible rule(s).");

        var totalWeight = eligibleRules.Sum(r => Math.Max(r.Frequency, 1));
        var index = _random.Next(0, totalWeight);

        var selectedRule = eligibleRules[eligibleRules.Count - 1];
        foreach (var rule in eligibleRules)
        {
            var weight = Math.Max(rule.Frequency, 1);
            if (index < weight)
            {
                selectedRule = rule;
                break;
            }
            index -= weight;
        }

        var selectedItem = libraryResults[selectedRule.IntroId];

        logger.LogInformation($"Selected intro: {selectedItem.Name} ({selectedItem.Path})");

        return new[]
        {
            new IntroInfo
            {
                Path = selectedItem.Path,
                ItemId = selectedItem.Id
            }
        };
    }

    private Dictionary<Guid, BaseItem> RetrieveIntroLibrary() =>
        LocalIntrosPlugin.LibraryManager.GetItemsResult(new InternalItemsQuery
        {
            HasAnyProviderId = new Dictionary<string, string>
            {
                {"prerolls.video", ""}
            }
        }).Items.ToDictionary(x => x.Id, x => x);

}
