using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using MediaBrowser.Common;
using MediaBrowser.Controller.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Jellyfin.Plugin.LocalIntros.Configuration;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.LocalIntros;


[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route("[controller]")]
[Produces(MediaTypeNames.Application.Json)]
public class LocalIntrosController : ControllerBase
{
    private readonly ILogger<LocalIntrosController> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LdapController"/> class.
    /// </summary>
    /// <param name="appHost">The application host to get the LDAP Authentication Provider from.</param>
    public LocalIntrosController(IApplicationHost appHost, ILoggerFactory loggerFactory)
    {
        this.logger = loggerFactory.CreateLogger<LocalIntrosController>();
    }

    /// <summary>
    /// Tests the server connection and bind settings.
    /// </summary>
    /// <remarks>
    /// Accepts server connection configuration as JSON body.
    /// </remarks>
    /// <response code="200">Server connection was tested.</response>
    /// <response code="400">Body is missing required data.</response>
    /// <param name="body">The request body.</param>
    /// <returns>
    /// An <see cref="OkResult"/> containing the connection results if able to test,
    /// or a <see cref="BadRequestResult"/> if the request body is missing data.
    /// </returns>
    [HttpPost("LoadIntros")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult LoadIntros()
    {
        logger.LogDebug("Loading Intros");
        PopulateIntroLibrary();
        return Ok();
    }

    [HttpPost("ClearIntros")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult ClearIntros()
    {
        logger.LogInformation("Clearing Intros");
        LocalIntrosPlugin.LibraryManager.GetItemsResult(new InternalItemsQuery
        {
            HasAnyProviderId = new Dictionary<string, string>
            {
                {"prerolls.video", ""}
            }
        }).Items.ToList().ForEach(x =>
        {
            logger.LogInformation($"Removing {x.Path} from library.");
            LocalIntrosPlugin.LibraryManager.DeleteItem(x, new DeleteOptions());
        });
        return Ok();
    }


    private static string introsPath => LocalIntrosPlugin.Instance.Configuration.Local;

    private Dictionary<Guid, BaseItem> PopulateIntroLibrary()
    {
        logger.LogTrace($"Retrieving attributes of {introsPath}");
        var attrs = System.IO.File.GetAttributes(introsPath);

        bool needsConfigUpdate = false;

        Dictionary<Guid, BaseItem> libraryResults = new Dictionary<Guid, BaseItem>();

        logger.LogTrace($"Retrieving existing items from library.");
        var inLibrary = LocalIntrosPlugin.LibraryManager.GetItemsResult(new InternalItemsQuery
        {
            HasAnyProviderId = new Dictionary<string, string>
            {
                {"prerolls.video", ""}
            }
        }).Items;
        logger.LogInformation($"Found {inLibrary.Count()} items in library.");

        logger.LogTrace($"Creating dictionaries for comparison. (path => item, id => isFound, id => item)");
        var byPath = inLibrary.ToDictionary(x => x.Path, x => x);
        var isFound = inLibrary.ToDictionary(x => x.Id, x => false);
        var byId = inLibrary.ToDictionary(x => x.Id, x => x);

        IEnumerable<string> filesOnDisk;

        if (attrs.HasFlag(FileAttributes.Directory))
        {
            logger.LogInformation($"Retrieving files from directory at {introsPath}");
            filesOnDisk = Directory.EnumerateFiles(introsPath);
        }
        else if (System.IO.File.Exists(introsPath))
        {
            logger.LogInformation($"Retrieving file at {introsPath}");
            filesOnDisk = new List<string> { introsPath };
        }
        else
        {
            throw new DirectoryNotFoundException($"Directory Not Found: {introsPath}. Please check your configuration.");
        }

        logger.LogTrace($"Retrieving item IDs in configuration file");
        var configDetectedVideos = LocalIntrosPlugin.Instance.Configuration.DetectedLocalVideos.Select(x => x.ItemId).ToHashSet();

        logger.LogTrace($"Comparing files on disk to items in library.");
        foreach (var file in filesOnDisk)
        {
            if (byPath.ContainsKey(file))
            {
                logger.LogTrace($"Found {file} in library, marking as found and adding to results.");
                isFound[byPath[file].Id] = true;
                libraryResults[byPath[file].Id] = byPath[file];
                if (!configDetectedVideos.Contains(byPath[file].Id) && !needsConfigUpdate)
                {
                    logger.LogInformation("Flagging for config update.");
                    needsConfigUpdate = true;
                }
            }
            else
            {
                logger.LogInformation($"Adding {file} to library and adding to results.");
                var video = new Video
                {
                    Id = Guid.NewGuid(),
                    Path = file,
                    ProviderIds = new Dictionary<string, string>
                    {
                        {"prerolls.video", file}
                    },
                    Name = Path.GetFileNameWithoutExtension(file)
                        .Replace("jellyfin", string.Empty, StringComparison.InvariantCultureIgnoreCase)
                        .Replace("pre-roll", string.Empty, StringComparison.InvariantCultureIgnoreCase)
                        .Replace("_", " ", StringComparison.InvariantCultureIgnoreCase)
                        .Replace("-", " ", StringComparison.InvariantCultureIgnoreCase)
                        .Trim()
                };
                LocalIntrosPlugin.LibraryManager.CreateItem(video, null);
                if (!needsConfigUpdate)
                {
                    logger.LogInformation("Flagging for config update.");
                    needsConfigUpdate = true;
                }
                libraryResults[video.Id] = video;
            }
        }
        foreach (var item in isFound.Where(f => !f.Value))
        {
            logger.LogWarning($"Removing {byId[item.Key].Path} from library.");
            LocalIntrosPlugin.LibraryManager.DeleteItem(byId[item.Key], new DeleteOptions());
        }
        if (libraryResults.Count > 0)
        {
            if (inLibrary.Count() == 0)
            {
                logger.LogInformation($"No existing items in library, erasing configuration.");
                LocalIntrosPlugin.Instance.Configuration.DetectedLocalVideos = new();
                LocalIntrosPlugin.Instance.Configuration.IntroRules = new();

                UpdateOptionsConfig(libraryResults.Values);
            }
            if (needsConfigUpdate)
            {
                logger.LogInformation($"Updating configuration file.");
                UpdateOptionsConfig(libraryResults.Values);
            }
        }
        if (libraryResults.Count == 0)
        {
            logger.LogWarning($"No videos found in {introsPath}, updating configuration file.");
            UpdateOptionsConfig(libraryResults.Values);
        }
        return libraryResults;
    }

    /// <summary>
    /// Refreshes DetectedLocalVideos to match what's actually on disk, drops IntroRules for videos
    /// that no longer exist, and ensures every remaining detected video has a rule row (defaulting
    /// to Both/no restrictions/Frequency 50) so it shows up in the admin UI.
    /// </summary>
    private void UpdateOptionsConfig(IEnumerable<BaseItem> libraryResults)
    {
        logger.LogTrace($"Adding detected videos to configuration.");
        LocalIntrosPlugin.Instance.Configuration.DetectedLocalVideos = libraryResults.Select(x => new IntroVideo
        {
            ItemId = x.Id,
            Name = x.Name
        }).ToList();

        var validIds = LocalIntrosPlugin.Instance.Configuration.DetectedLocalVideos.Select(x => x.ItemId).ToHashSet();

        logger.LogTrace($"Removing rules for videos no longer on disk.");
        LocalIntrosPlugin.Instance.Configuration.IntroRules = LocalIntrosPlugin.Instance.Configuration.IntroRules
            .Where(r => validIds.Contains(r.IntroId))
            .ToList();

        var existingRuleIds = LocalIntrosPlugin.Instance.Configuration.IntroRules.Select(r => r.IntroId).ToHashSet();

        logger.LogTrace($"Adding default rules for newly detected videos.");
        foreach (var video in LocalIntrosPlugin.Instance.Configuration.DetectedLocalVideos)
        {
            if (!existingRuleIds.Contains(video.ItemId))
            {
                logger.LogInformation($"Adding default rule for new intro: {video.Name}");
                LocalIntrosPlugin.Instance.Configuration.IntroRules.Add(new IntroRule
                {
                    IntroId = video.ItemId
                });
            }
        }

        LocalIntrosPlugin.Instance.SaveConfiguration();
    }

}
