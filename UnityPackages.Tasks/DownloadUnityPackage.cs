using ICSharpCode.SharpZipLib.Tar;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using UnityPackages.Tasks.Models;

namespace UnityPackages.Tasks
{
    public class DownloadUnityPackage : Task
    {
        private static readonly string[] ExcludeDirectories = ["arm32", "arm64", "android", "universalwindows"];
        private static readonly string[] ExcludeExtensions = [".meta"];

        [Required]
        public string Name { get; set; }

        public string Version { get; set; }

        [Required]
        public string DestinationFolder { get; set; }

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, $"Fetching information for {Name}");

            using HttpClient client = new();

            UnityPackageManifest packageManifest = client.GetFromJsonAsync<UnityPackageManifest>($"https://download.packages.unity.com/{Name}").GetAwaiter().GetResult();

            if (packageManifest == null)
            {
                Log.LogError($"Could not get manifest for package {Name}.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(Version))
            {
                Version = packageManifest.DistTags["latest"];
            }

            if (!packageManifest.Versions.TryGetValue(Version, out UnityPackageVersion version))
            {
                Log.LogError($"Version {Version} does not exist. Available versions are {string.Join(", ", packageManifest.Versions.Keys)}.");
                return false;
            }

            Log.LogMessage(MessageImportance.High, $"Downloading {Name} v{Version}");

            using HttpResponseMessage response = client.GetAsync(version.Dist.Tarball).GetAwaiter().GetResult();

            response.EnsureSuccessStatusCode();

            using MemoryStream stream = new(new byte[response.Content.Headers.ContentLength.Value], true);

            using (Stream responseContentStream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
            {
                responseContentStream.CopyTo(stream);
            }

            stream.Position = 0;
            if (!ValidateSha1Sum(stream, version.Dist.ShaSum))
            {
                Log.LogError($"Failed to validate downloaded package's SHA1 sum.");
                return false;
            }

            stream.Position = 0;
            ExtractFiles(stream);

            return true;
        }

        private bool ValidateSha1Sum(Stream stream, string expectedSha1Sum)
        {
            using SHA1 sha1 = SHA1.Create();
            
            sha1.ComputeHash(stream);
            string shaSum = string.Concat(sha1.Hash.Select(b => b.ToString("x2")));

            return shaSum.Equals(expectedSha1Sum, StringComparison.OrdinalIgnoreCase);
        }

        private void ExtractFiles(Stream stream)
        {
            using GZipStream gzipStream = new(stream, CompressionMode.Decompress);
            using TarInputStream reader = new(gzipStream, Encoding.UTF8);

            TarEntry entry;
            while ((entry = reader.GetNextEntry()) != null)
            {
                if (entry.IsDirectory)
                {
                    continue;
                }

                string relativePath = entry.Name;

                if (!relativePath.StartsWith("package/Runtime", StringComparison.Ordinal))
                {
                    continue;
                }

                string[] directories = relativePath.Split('/');
                if (ExcludeDirectories.Intersect(directories, StringComparer.Ordinal).Any())
                {
                    continue;
                }

                string extension = Path.GetExtension(relativePath);
                if (ExcludeExtensions.Any(e => e == extension))
                {
                    continue;
                }

                string destinationFile = Path.Combine(DestinationFolder, relativePath.Substring(relativePath.IndexOf('/') + 1));

                Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));

                using FileStream fileStream = File.OpenWrite(destinationFile);
                reader.CopyEntryContents(fileStream);
            }
        }
    }
}
