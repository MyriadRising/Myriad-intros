using System;
using System.Collections.Generic;
using Jellyfin.Plugin.LocalIntros.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.LocalIntros
{
    public class LocalIntrosPlugin : BasePlugin<IntroPluginConfiguration>, IHasWebPages
    {
        public override string Name => "Myriad's Pre-Rolls";

        public override Guid Id => Guid.Parse("5d9f187d-c598-478d-8dc3-0223f3c39938");

        public const int DefaultResolution = 1080;

        public static LocalIntrosPlugin Instance { get; private set; }

        public static ILibraryManager LibraryManager { get; private set; }

        public LocalIntrosPlugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILibraryManager libraryManager)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
            LibraryManager = libraryManager;
        }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            yield return new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.config.html"
            };
        }
    }
}
