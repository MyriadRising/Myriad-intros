using System;
using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.LocalIntros.Configuration;

public class IntroPluginConfiguration : BasePluginConfiguration
{
    public string Local { get; set; } = string.Empty;

    public List<IntroVideo> DetectedLocalVideos { get; set; } = new List<IntroVideo>();

    /// <summary>
    /// One rule per detected intro video, controlling when it is eligible to play.
    /// </summary>
    public List<IntroRule> IntroRules { get; set; } = new List<IntroRule>();

    /// <summary>
    /// Users in this list never see intros, regardless of any rule. Admin-managed hard override.
    /// </summary>
    public List<Guid> DisabledUserIds { get; set; } = new List<Guid>();
}

public class IntroVideo
{
    public string Name { get; set; }

    public Guid ItemId { get; set; }
}

public enum MediaTypeFilter
{
    Movies,
    Shows,
    Both,
    None
}

public class IntroRule
{
    public Guid IntroId { get; set; }

    /// <summary>
    /// Which kind of content this intro is eligible for. None means the intro never plays.
    /// </summary>
    public MediaTypeFilter MediaType { get; set; } = MediaTypeFilter.Both;

    /// <summary>
    /// Tags this intro is restricted to. Empty means no tag restriction (all).
    /// </summary>
    public List<string> Tags { get; set; } = new List<string>();

    /// <summary>
    /// Official ratings this intro is restricted to. Empty means no rating restriction (all).
    /// </summary>
    public List<string> Ratings { get; set; } = new List<string>();

    /// <summary>
    /// Users this intro is restricted to. Empty means no user restriction (all users).
    /// </summary>
    public List<Guid> UserIds { get; set; } = new List<Guid>();

    /// <summary>
    /// Relative weight of this intro versus other eligible intros, 1-100.
    /// </summary>
    public int Frequency { get; set; } = 50;
}
