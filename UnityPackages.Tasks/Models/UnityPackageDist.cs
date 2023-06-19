using System.Text.Json.Serialization;

namespace UnityPackages.Tasks.Models
{
    internal record UnityPackageDist
    {
        [JsonPropertyName("shasum")]
        public string ShaSum { get; set; }

        public string Tarball { get; set; }
    }
}
