using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using SteamToys.Editor.Core;
using SteamToys.Runtime.AchievementsSystem;
using Steamworks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SteamToys.Editor.AchievementsSystem
{
    /// <summary>
    /// Downloads the icons of achievements from Steam into PNG files imported as sprites, in
    /// <see cref="Folder"/>, so a game has them without a Steam session.
    /// <para>
    /// The schema the Steam client downloads names each icon by its file name on the Steam CDN,
    /// which serves it publicly, with no login. The runtime API would give only the icon of the
    /// state the user is in, and only while a session runs, which is why this happens in the editor.
    /// </para>
    /// <para>
    /// A file is written over rather than made again, so references to its sprite survive an icon
    /// replaced on the partner site, and so do import settings changed by hand. Each achievement
    /// keeps files of its own, even where two share an icon on Steam, so that changing one never
    /// changes the other.
    /// </para>
    /// </summary>
    internal static class AchievementIcons
    {
        /// <summary>Where downloaded icons go.</summary>
        internal const string Folder = "Assets/Resources/SteamAchievementsIcons";

        private const string CdnUrl = "https://cdn.akamai.steamstatic.com/steamcommunity/public/images/apps/{0}/{1}";

        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

        /// <summary>
        /// Downloads every icon of <paramref name="achievements"/> that differs from the one Steam
        /// has. Returns how many icons were downloaded; an icon that failed is logged and left as it
        /// was.
        /// <para>
        /// Assigning the sprites to the achievements has undo, writing the files has not: undoing
        /// leaves the files in the folder, where the next download finds and reuses them.
        /// </para>
        /// </summary>
        public static int Download(AppId_t appId, IEnumerable<(SerializedObject achievement, AchievementDefinition steam)> achievements)
        {
            var jobs = new List<Job>();

            foreach (var (achievement, steam) in achievements)
            {
                AddJob(achievement, steam.Icon, AchievementSettings.IconField, AchievementSettings.IconHashField, "Icon", string.Empty);
                AddJob(achievement, steam.IconGray, AchievementSettings.LockedIconField, AchievementSettings.LockedIconHashField, "Locked Icon", "_Locked");
            }

            if (jobs.Count == 0)
                return 0;

            var downloaded = 0;

            try
            {
                for (var i = 0; i < jobs.Count; i++)
                {
                    var job = jobs[i];
                    var owner = (SteamAchievement)job.Achievement.targetObject;

                    if (EditorUtility.DisplayCancelableProgressBar("Achievement Icons",
                            $"{owner.name}: {job.Label}", (float)i / jobs.Count))
                        break;

                    var url = string.Format(CdnUrl, appId.m_AppId, job.FileName);
                    byte[] bytes;

                    try
                    {
                        // Blocking on purpose: a handful of small icons, fetched while the progress bar
                        // is up, which keeps the undo group of the caller in one piece.
                        bytes = Http.GetByteArrayAsync(url).GetAwaiter().GetResult();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning($"[SteamToys] Could not download the {job.Label.ToLowerInvariant()} of achievement '{owner.name}' from {url}: {exception.Message}", owner);

                        continue;
                    }

                    if (Apply(job, owner, bytes))
                        downloaded++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return downloaded;

            void AddJob(SerializedObject achievement, string fileName, string spriteField, string hashField, string label, string suffix)
            {
                if (string.IsNullOrEmpty(fileName))
                    return;

                achievement.Update();

                if (achievement.FindProperty(spriteField).objectReferenceValue &&
                    achievement.FindProperty(hashField).stringValue == fileName)
                    return;

                // A schema that names an icon without its extension still means the jpg the CDN serves.
                jobs.Add(new Job(achievement, Path.HasExtension(fileName) ? fileName : $"{fileName}.jpg", fileName,
                    spriteField, hashField, label, suffix));
            }
        }

        private static bool Apply(Job job, SteamAchievement owner, byte[] bytes)
        {
            // Steam serves jpg, and the project wants png, so the icon is decoded and encoded again.
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            byte[] png;

            try
            {
                if (!decoded.LoadImage(bytes))
                {
                    Debug.LogWarning($"[SteamToys] The {job.Label.ToLowerInvariant()} of achievement '{owner.name}' downloaded from Steam is not an image Unity can read.", owner);

                    return false;
                }

                png = decoded.EncodeToPNG();
            }
            finally
            {
                Object.DestroyImmediate(decoded);
            }

            var achievement = job.Achievement;

            achievement.Update();

            var property = achievement.FindProperty(job.SpriteField);
            var path = GetOwnPath(property.objectReferenceValue as Sprite);
            var created = path == null;

            if (created)
            {
                EnsureFolder();

                var baseName = string.IsNullOrWhiteSpace(owner.ApiName) ? owner.name : owner.ApiName;

                path = AssetDatabase.GenerateUniqueAssetPath($"{Folder}/{Sanitize(baseName)}{job.Suffix}.png");
            }

            File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            // Only a new file is set up: one written over keeps whatever import settings it was given.
            if (created && AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (!sprite)
            {
                Debug.LogWarning($"[SteamToys] {path} was written, but is not imported as a sprite, so achievement '{owner.name}' cannot use it. Set its Texture Type to Sprite.", owner);

                return false;
            }

            property.objectReferenceValue = sprite;
            achievement.FindProperty(job.HashField).stringValue = job.Hash;
            achievement.ApplyModifiedProperties();

            return true;
        }

        /// <summary>
        /// The file of <paramref name="sprite"/> when it is one this class manages, in
        /// <see cref="Folder"/>, and so may be written over; null otherwise. A sprite from anywhere
        /// else was put there by hand and is not ours to overwrite.
        /// </summary>
        private static string GetOwnPath(Sprite sprite)
        {
            if (!sprite)
                return null;

            var path = AssetDatabase.GetAssetPath(sprite);

            return path.StartsWith(Folder + "/", StringComparison.Ordinal) && path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ? path
                : null;
        }

        /// <summary>
        /// Names the icon files of <paramref name="achievement"/> after its API Name again, after it
        /// changed. Only files in <see cref="Folder"/>; a sprite from elsewhere keeps its name.
        /// </summary>
        public static void SyncNames(SteamAchievement achievement)
        {
            if (string.IsNullOrWhiteSpace(achievement.ApiName))
                return;

            Rename(achievement.Icon, string.Empty);
            Rename(achievement.LockedIcon, "_Locked");

            void Rename(Sprite sprite, string suffix)
            {
                var path = GetOwnPath(sprite);
                var name = $"{Sanitize(achievement.ApiName)}{suffix}";

                if (path == null || Path.GetFileNameWithoutExtension(path) == name)
                    return;

                var error = AssetDatabase.RenameAsset(path, name);

                if (!string.IsNullOrEmpty(error))
                    Debug.LogWarning($"[SteamToys] Could not rename {path} after achievement '{achievement.ApiName}': {error}", achievement);
            }
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(Folder))
                return;

            var parent = "Assets";

            foreach (var part in Folder.Substring("Assets/".Length).Split('/'))
            {
                if (!AssetDatabase.IsValidFolder($"{parent}/{part}"))
                    AssetDatabase.CreateFolder(parent, part);

                parent = $"{parent}/{part}";
            }
        }

        private static string Sanitize(string name)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');

            return name.Trim();
        }

        private readonly struct Job
        {
            public readonly SerializedObject Achievement;
            public readonly string FileName;
            public readonly string Hash;
            public readonly string SpriteField;
            public readonly string HashField;
            public readonly string Label;
            public readonly string Suffix;

            public Job(SerializedObject achievement, string fileName, string hash, string spriteField, string hashField, string label, string suffix)
            {
                Achievement = achievement;
                FileName = fileName;
                Hash = hash;
                SpriteField = spriteField;
                HashField = hashField;
                Label = label;
                Suffix = suffix;
            }
        }
    }
}
