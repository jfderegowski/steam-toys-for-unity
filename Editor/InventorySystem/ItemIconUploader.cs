using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace SteamToys.Editor.InventorySystem
{
    /// <summary>
    /// Sends the local icons of item definitions over FTP to the place on the server their URLs point at, e.g.
    /// <see cref="ItemDef.Icon"/> of <c>https://mygames.dev/steam/Tires/Gold.png</c> to
    /// <c>&lt;Remote Root&gt;/steam/Tires/Gold.png</c>, and opens that place in Windows Explorer. The server is set in
    /// Preferences > Steam Toys > Icon Upload, per user, since it holds a login.
    /// </summary>
    public static class ItemIconUploader
    {
        #region Constants

        private const string PREFERENCES_PATH = "Preferences/Steam Toys/Icon Upload";
        private const string PREFS_KEY = "SteamToys.IconUpload.";
        private const string SITE_KEY = PREFS_KEY + "Site";
        private const string HOST_KEY = PREFS_KEY + "FtpHost";
        private const string USER_KEY = PREFS_KEY + "FtpUser";
        private const string PASSWORD_KEY = PREFS_KEY + "FtpPassword";
        private const string SSL_KEY = PREFS_KEY + "FtpSsl";
        private const string ROOT_KEY = PREFS_KEY + "FtpRoot";

        private const string PROGRESS_TITLE = "Uploading Item Icons";
        private const int TIMEOUT_MILLISECONDS = 30_000;

        #endregion

        /// <summary>The host of the URLs this server serves, e.g. <c>mygames.dev</c>; empty for any.</summary>
        public static string Site
        {
            get => EditorPrefs.GetString(SITE_KEY);
            set => EditorPrefs.SetString(SITE_KEY, value?.Trim() ?? string.Empty);
        }

        /// <summary>The FTP server, e.g. <c>ftp.mygames.dev</c>, with <c>:port</c> when it is not 21.</summary>
        public static string Host
        {
            get => EditorPrefs.GetString(HOST_KEY);
            set => EditorPrefs.SetString(HOST_KEY, value?.Trim().TrimEnd('/') ?? string.Empty);
        }

        public static string User
        {
            get => EditorPrefs.GetString(USER_KEY);
            set => EditorPrefs.SetString(USER_KEY, value?.Trim() ?? string.Empty);
        }

        /// <summary>Kept in EditorPrefs, which on Windows is the registry of this user, not encrypted.</summary>
        public static string Password
        {
            get => EditorPrefs.GetString(PASSWORD_KEY);
            set => EditorPrefs.SetString(PASSWORD_KEY, value ?? string.Empty);
        }

        /// <summary>Whether the upload uses explicit FTPS (AUTH TLS).</summary>
        public static bool UseSsl
        {
            get => EditorPrefs.GetBool(SSL_KEY);
            set => EditorPrefs.SetBool(SSL_KEY, value);
        }

        /// <summary>
        /// The folder the path of a URL starts in, from the one FTP logs into, e.g. <c>public_html</c>; empty for that
        /// one.
        /// </summary>
        public static string Root
        {
            get => EditorPrefs.GetString(ROOT_KEY);
            set => EditorPrefs.SetString(ROOT_KEY, value?.Trim().Trim('/') ?? string.Empty);
        }

        private static bool IsConfigured => !string.IsNullOrEmpty(Host) && !string.IsNullOrEmpty(User);

        /// <summary>
        /// Sends <see cref="ItemDef.Icon"/> and <see cref="ItemDef.IconLarge"/> of <paramref name="items"/> to the
        /// paths of their URLs, overwriting what is there, after asking. An icon without a sprite or URL, or with the
        /// URL of another site, is skipped.
        /// </summary>
        public static async Task UploadAsync(IReadOnlyCollection<ItemDef> items)
        {
            if (!IsConfigured)
            {
                Debug.LogWarning("Upload Icons: set the server in Preferences > Steam Toys > Icon Upload first.");
                SettingsService.OpenUserPreferences(PREFERENCES_PATH);
                return;
            }

            var uploads = items
                .Where(item => item)
                .SelectMany(item => new[] { (item.IconUrl, item.Icon), (item.IconUrlLarge, item.IconLarge) })
                .Select(icon => (Local: GetLocalPath(icon.Item2), Remote: GetRemoteSegments(icon.Item1)))
                .Where(upload => upload.Local != null && upload.Remote is { Length: > 0 })
                .GroupBy(upload => string.Join("/", upload.Remote))
                .Select(group => group.First())
                .ToList();

            if (uploads.Count == 0)
            {
                Debug.LogWarning("Upload Icons: none of the selected definitions has an icon with a URL of " +
                                 (string.IsNullOrEmpty(Site) ? "a valid http(s) site." : $"{Site}."));
                return;
            }

            var list = string.Join("\n", uploads.Take(10).Select(upload => "/" + string.Join("/", upload.Remote))) +
                       (uploads.Count > 10 ? $"\n… and {uploads.Count - 10} more" : string.Empty);

            if (!EditorUtility.DisplayDialog("Upload Icons",
                    $"Overwrite {uploads.Count} file(s) on {Host}?\n\n{list}", "Upload", "Cancel"))
                return;

            var credentials = new NetworkCredential(User, Password);
            var useSsl = UseSsl;
            var host = Host;
            var done = 0;
            var failed = 0;

            try
            {
                EditorUtility.DisplayProgressBar(PROGRESS_TITLE, $"0 / {uploads.Count}", 0);

                // One at a time: many servers limit the connections of one login.
                foreach (var (local, remote) in uploads)
                {
                    var error = await Task.Run(() => Upload(host, credentials, useSsl, local, remote));

                    if (error != null)
                    {
                        failed++;
                        Debug.LogWarning($"Upload Icons: /{string.Join("/", remote)} failed: {error}");
                    }

                    done++;
                    EditorUtility.DisplayProgressBar(PROGRESS_TITLE, $"{done} / {uploads.Count}", (float)done / uploads.Count);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (failed == 0)
                Debug.Log($"Upload Icons: uploaded {done} icon(s) to {host}.");
            else
                Debug.LogWarning($"Upload Icons: uploaded {done - failed} icon(s), {failed} failed (see the warnings above).");
        }

        /// <summary>
        /// The folder of what <paramref name="url"/> points at on the server, as an <c>ftp://</c> address, e.g.
        /// <c>ftp://user@ftp.mygames.dev/public_html/steam/Tires/</c>: of a file the folder it is in, since Explorer
        /// cannot select a file on FTP. Null when the server is not set or the URL is not of <see cref="Site"/>. The
        /// program that opens it asks for the password itself, so it is not in the address.
        /// </summary>
        public static string GetFtpAddress(Url url)
        {
            var segments = IsConfigured ? GetRemoteSegments(url) : null;

            if (segments == null)
                return null;

            // A file is a last part with an extension, in a URL that does not end with a slash.
            if (segments.Length > 0 && !url.Value.TrimEnd().EndsWith("/") &&
                !string.IsNullOrEmpty(Path.GetExtension(segments[^1])))
                segments = segments[..^1];

            return $"ftp://{Uri.EscapeDataString(User)}@{Host}/" +
                   string.Concat(segments.Select(segment => Uri.EscapeDataString(segment) + "/"));
        }

        /// <summary>
        /// Whether <see cref="OpenOnServer"/> does something for <paramref name="url"/>: opens it on the server, or,
        /// while the server is not set, its settings.
        /// </summary>
        public static bool CanOpenOnServer(Url url) =>
            IsConfigured ? GetFtpAddress(url) != null : url is not null && url.IsValid();

        /// <summary>
        /// Opens the folder of <paramref name="url"/> on the server in the program set for <c>ftp://</c> links, e.g.
        /// WinSCP, or in Windows Explorer when none is; or the settings of the server while it is not set.
        /// </summary>
        public static void OpenOnServer(Url url)
        {
            if (!IsConfigured)
            {
                SettingsService.OpenUserPreferences(PREFERENCES_PATH);
                return;
            }

            var path = GetFtpAddress(url);

            if (path == null)
                return;

            try
            {
                // Without a program for ftp:// links Windows would ask which one to use, so Explorer opens it.
                if (Application.platform == RuntimePlatform.WindowsEditor && !HasFtpProgram())
                    Process.Start("explorer.exe", $"\"{path}\"");
                else
                    Application.OpenURL(path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Open on server: {path} failed: {exception.Message}");
            }
        }

        private const uint ASSOCF_IS_PROTOCOL = 0x1000;
        private const uint ASSOCSTR_EXECUTABLE = 2;

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int AssocQueryString(uint flags, uint str, string assoc, string extra,
                                                   StringBuilder output, ref uint size);

        // Browsers dropped FTP in 2021, but Windows may still send ftp:// links to one.
        private static readonly string[] Browsers = {
            "msedge", "ie_to_edge_stub", "iexplore", "chrome", "firefox", "brave", "opera", "vivaldi"
        };

        /// <summary>
        /// Whether Windows has a program set to open <c>ftp://</c> links that can: not a browser, i.e. neither the
        /// program of <c>http://</c> links nor a known one.
        /// </summary>
        private static bool HasFtpProgram()
        {
            var ftp = GetProtocolProgram("ftp");

            if (string.IsNullOrEmpty(ftp))
                return false;

            var name = Path.GetFileNameWithoutExtension(ftp);

            return !string.Equals(ftp, GetProtocolProgram("http"), StringComparison.OrdinalIgnoreCase) &&
                   !Browsers.Any(browser => string.Equals(name, browser, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The program Windows opens <paramref name="protocol"/> links with; null when there is none.</summary>
        private static string GetProtocolProgram(string protocol)
        {
            var size = 1024u;
            var output = new StringBuilder((int)size);

            return AssocQueryString(ASSOCF_IS_PROTOCOL, ASSOCSTR_EXECUTABLE, protocol, null, output, ref size) == 0
                ? output.ToString()
                : null;
        }

        private static string GetLocalPath(Sprite sprite)
        {
            var path = sprite ? AssetDatabase.GetAssetPath(sprite) : null;

            return string.IsNullOrEmpty(path) ? null : Path.GetFullPath(path);
        }

        /// <summary>
        /// The folders and file of <paramref name="url"/> on the server: <see cref="Root"/>, then the path of the URL.
        /// Null when it is not an http(s) URL of <see cref="Site"/>, or its path climbs out of the root.
        /// </summary>
        private static string[] GetRemoteSegments(Url url)
        {
            if (url is null || !url.IsValid() ||
                !Uri.TryCreate(url.Value, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return null;

            if (!string.IsNullOrEmpty(Site) && !string.Equals(uri.Host, Site, StringComparison.OrdinalIgnoreCase))
                return null;

            var segments = Uri.UnescapeDataString(uri.AbsolutePath).Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Any(segment => segment is "." or ".."))
                return null;

            return Root.Split('/', StringSplitOptions.RemoveEmptyEntries).Concat(segments).ToArray();
        }

        /// <summary>
        /// Makes the folders of <paramref name="remote"/> that are missing, then writes <paramref name="local"/> to
        /// it; the error, or null.
        /// </summary>
        private static string Upload(string host, NetworkCredential credentials, bool useSsl, string local,
                                     string[] remote)
        {
            try
            {
                var address = $"ftp://{host}/";

                for (var i = 0; i < remote.Length - 1; i++)
                {
                    address += Uri.EscapeDataString(remote[i]) + "/";

                    // Fails when the folder is there already, which is the usual case.
                    try
                    {
                        using var response = Send(address, WebRequestMethods.Ftp.MakeDirectory, credentials, useSsl);
                    }
                    catch (WebException) { }
                }

                address += Uri.EscapeDataString(remote[^1]);

                var request = CreateRequest(address, WebRequestMethods.Ftp.UploadFile, credentials, useSsl);

                using (var input = File.OpenRead(local))
                using (var output = request.GetRequestStream())
                    input.CopyTo(output);

                using (request.GetResponse()) { }

                return null;
            }
            catch (WebException exception) when (exception.Response is FtpWebResponse response)
            {
                return response.StatusDescription?.Trim();
            }
            catch (Exception exception)
            {
                return exception.Message;
            }
        }

        private static FtpWebResponse Send(string address, string method, NetworkCredential credentials, bool useSsl) =>
            (FtpWebResponse)CreateRequest(address, method, credentials, useSsl).GetResponse();

        // FtpWebRequest is obsolete in .NET, but the one FTP client Unity's runtime has.
#pragma warning disable SYSLIB0014
        private static FtpWebRequest CreateRequest(string address, string method, NetworkCredential credentials,
                                                   bool useSsl)
        {
            var request = (FtpWebRequest)WebRequest.Create(address);
#pragma warning restore SYSLIB0014

            request.Method = method;
            request.Credentials = credentials;
            request.EnableSsl = useSsl;
            request.UseBinary = true;
            request.UsePassive = true;
            request.KeepAlive = false;
            request.Timeout = TIMEOUT_MILLISECONDS;

            return request;
        }

        [SettingsProvider]
        private static SettingsProvider CreateSettingsProvider() =>
            new(PREFERENCES_PATH, SettingsScope.User, new[] { "steam", "icon", "upload", "ftp" }) {
                activateHandler = (_, root) => {
                    var content = new VisualElement {
                        style = { paddingLeft = 10, paddingRight = 10, paddingTop = 2 }
                    };

                    content.Add(new Label("Icon Upload") {
                        style = { fontSize = 19, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 10 }
                    });

                    content.Add(new HelpBox(
                        "Upload Icons sends an icon over FTP to Remote Root plus the path of its URL. Kept for this " +
                        "user only, not in the project; the password is stored unencrypted in EditorPrefs.",
                        HelpBoxMessageType.Info) { style = { marginBottom = 6 } });

                    AddText("Site", "Host of the icon URLs this server serves, e.g. mygames.dev. Icons of other " +
                                    "sites are skipped. Empty for any.", Site, value => Site = value);
                    AddText("FTP Host", "e.g. ftp.mygames.dev, with :port when it is not 21.", Host, value => Host = value);
                    AddText("User", null, User, value => User = value);
                    AddText("Password", null, Password, value => Password = value).isPasswordField = true;

                    var ssl = new Toggle("FTPS") { value = UseSsl, tooltip = "Explicit FTP over TLS (AUTH TLS)." };

                    ssl.RegisterValueChangedCallback(change => UseSsl = change.newValue);
                    ssl.AddToClassList(BaseField<bool>.alignedFieldUssClassName);
                    content.Add(ssl);

                    AddText("Remote Root", "Folder the URL path starts in, from the one FTP logs into, e.g. " +
                                           "public_html. Empty for that one.", Root, value => Root = value);

                    root.Add(content);

                    TextField AddText(string label, string tooltip, string value, Action<string> save)
                    {
                        var field = new TextField(label) { value = value, tooltip = tooltip, isDelayed = true };

                        field.RegisterValueChangedCallback(change => save(change.newValue));
                        field.AddToClassList(BaseField<string>.alignedFieldUssClassName);
                        content.Add(field);

                        return field;
                    }
                }
            };
    }
}
