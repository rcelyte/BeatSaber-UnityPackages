using System.Collections.Generic;

namespace UnityPackages.Tasks.Models
{
    internal record UnityPackageManifest
    {
        public Dictionary<string, string> DistTags { get; set; }

        public Dictionary<string, UnityPackageVersion> Versions { get; set; }
    }
}
