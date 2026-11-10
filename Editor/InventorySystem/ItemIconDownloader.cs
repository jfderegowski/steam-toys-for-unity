using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace SteamToys.Editor.InventorySystem
{
    /// <summary>
    /// Downloads the icons of item definitions from <see cref="ItemDef.IconUrl"/> and <see cref="ItemDef.IconUrlLarge"/>
    /// into <see cref="ROOT"/>, in folders following each URL from the name of its site, e.g.
    /// <c>https://mygames.dev/steam/Tires/Gold.png</c> to <c>Icons/mygames/steam/Tires/Gold.png</c>, and sets
    /// <see cref="ItemDef.Icon"/> and <see cref="ItemDef.IconLarge"/> to them.
    /// </summary>
    public static class ItemIconDownloader
    {
        #region Constants

        public const string ROOT = "Assets/Resources/Steam/Items/Icons";

        private const string UNDO_NAME = "Download Icons";
        private const string PROGRESS_TITLE = "Downloading Item Icons";
        private const string DEFAULT_EXTENSION = ".png";
        private const string WWW_PREFIX = "www.";
        private const int TIMEOUT_SECONDS = 30;

        #endregion

        /// <summary>
        /// Downloads every icon URL of <paramref name="items"/> once, overwriting a file already there, then sets the
        /// sprites of the definitions in one undo step. A definition keeps its sprite when its URL is empty or fails.
        /// </summary>
        public static async Task DownloadAsync(IReadOnlyCollection<ItemDef> items)
        {
            var urls = items
                .Where(item => item)
                .SelectMany(item => new[] { item.IconUrl, item.IconUrlLarge })
                .Where(url => url is not null && url.IsValid())
                .Select(url => url.Value)
                .Distinct()
                .ToList();

            if (urls.Count == 0)
            {
                Debug.LogWarning("Download Icons: none of the selected definitions has a valid icon URL.");
                return;
            }

            var paths = new Dictionary<string, string>();
            var done = 0;

            try
            {
                EditorUtility.DisplayProgressBar(PROGRESS_TITLE, $"0 / {urls.Count}", 0);

                var results = await Task.WhenAll(urls.Select(async url => {
                    var path = await DownloadFileAsync(url);

                    done++;
                    EditorUtility.DisplayProgressBar(PROGRESS_TITLE, $"{done} / {urls.Count}", (float)done / urls.Count);

                    return (url, path);
                }));

                // One import for all the files written.
                AssetDatabase.Refresh();

                foreach (var (url, path) in results)
                    if (path != null)
                        paths[url] = path;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssignSprites(items, paths);

            var failed = urls.Count - paths.Count;

            if (failed == 0)
                Debug.Log($"Download Icons: downloaded {paths.Count} icon(s) into {ROOT}.");
            else
                Debug.LogWarning($"Download Icons: downloaded {paths.Count} icon(s), {failed} failed (see the warnings above).");
        }

        /// <summary>
        /// The project path of the icon at <paramref name="url"/>: <see cref="ROOT"/>, the name of the site, then the
        /// folders and file of the URL. Null when the URL is not a valid http(s) URL.
        /// </summary>
        public static string GetAssetPath(string url, string contentType = null)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return null;

            var segments = uri.AbsolutePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(segment => Sanitize(Uri.UnescapeDataString(segment)))
                .ToList();

            if (segments.Count == 0)
                segments.Add("icon");

            var last = segments.Count - 1;

            if (string.IsNullOrEmpty(Path.GetExtension(segments[last])))
                segments[last] += GetExtension(contentType);

            return string.Join("/", new[] { ROOT, Sanitize(GetSiteName(uri.Host)) }.Concat(segments));
        }

        /// <summary>
        /// Whether <paramref name="sprite"/> is the file <paramref name="url"/> downloads to. The extension is not
        /// compared, since a URL without one gets it from the type of the response.
        /// </summary>
        public static bool IsDownloadOf(Sprite sprite, Url url)
        {
            var expected = url is null ? null : GetAssetPath(url.Value);
            var actual = sprite ? AssetDatabase.GetAssetPath(sprite) : null;

            return expected != null && !string.IsNullOrEmpty(actual) &&
                   string.Equals(Path.ChangeExtension(expected, null), Path.ChangeExtension(actual, null), StringComparison.Ordinal);
        }

        /// <summary>The name of the site without its subdomains and domain ending, e.g. <c>mygames</c> for <c>cdn.mygames.dev</c>.</summary>
        private static string GetSiteName(string host)
        {
            if (host.StartsWith(WWW_PREFIX, StringComparison.OrdinalIgnoreCase))
                host = host.Substring(WWW_PREFIX.Length);

            var labels = host.Split('.', StringSplitOptions.RemoveEmptyEntries);

            return labels.Length >= 2 ? labels[^2] : host;
        }

        private static string Sanitize(string name)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');

            return name.Trim();
        }

        private static string GetExtension(string contentType) =>
            contentType?.ToLowerInvariant() switch {
                "image/jpeg" or "image/jpg" => ".jpg",
                "image/tga" or "image/x-tga" => ".tga",
                "image/bmp" => ".bmp",
                _ => DEFAULT_EXTENSION
            };

        /// <summary>Downloads <paramref name="url"/> and writes it to its project path; null when it fails.</summary>
        private static async Task<string> DownloadFileAsync(string url)
        {
            using var request = UnityWebRequest.Get(url);

            request.timeout = TIMEOUT_SECONDS;

            var completion = new TaskCompletionSource<bool>();

            request.SendWebRequest().completed += _ => completion.TrySetResult(true);

            await completion.Task;

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Download Icons: {url} failed: {request.error}");
                return null;
            }

            var contentType = request.GetResponseHeader("Content-Type")?.Split(';')[0].Trim();

            if (contentType != null && !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"Download Icons: {url} is {contentType}, not an image, so it is skipped.");
                return null;
            }

            var path = GetAssetPath(url, contentType);
            var fullPath = Path.GetFullPath(path);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(fullPath, request.downloadHandler.data);

            return path;
        }

        /// <summary>Sets the sprites of <paramref name="items"/> to the downloaded files of their URLs.</summary>
        private static void AssignSprites(IEnumerable<ItemDef> items, IReadOnlyDictionary<string, string> paths)
        {
            var live = items.Where(item => item).ToArray();

            Undo.RecordObjects(live.Cast<UnityEngine.Object>().ToArray(), UNDO_NAME);

            foreach (var item in live)
            {
                var changed = false;

                if (TryLoadSprite(item.IconUrl, paths, out var icon))
                {
                    item.Icon = icon;
                    changed = true;
                }

                if (TryLoadSprite(item.IconUrlLarge, paths, out var iconLarge))
                {
                    item.IconLarge = iconLarge;
                    changed = true;
                }

                if (changed)
                    EditorUtility.SetDirty(item);
            }
        }

        private static bool TryLoadSprite(Url url, IReadOnlyDictionary<string, string> paths, out Sprite sprite)
        {
            sprite = null;

            if (url is null || !url.HasValue || !paths.TryGetValue(url.Value, out var path))
                return false;

            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (!sprite)
                Debug.LogWarning($"Download Icons: {path} was downloaded but is not imported as a sprite.");

            return sprite;
        }

        /// <summary>Imports the files first written under <see cref="ROOT"/> as single sprites.</summary>
        internal sealed class IconPostprocessor : AssetPostprocessor
        {
            private void OnPreprocessTexture()
            {
                // Only on the first import, so changes made in the inspector stay.
                if (!assetPath.StartsWith(ROOT + "/", StringComparison.Ordinal) || !assetImporter.importSettingsMissing)
                    return;

                var importer = (TextureImporter)assetImporter;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
            }
        }
    }
}
