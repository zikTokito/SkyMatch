using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SkyMatch
{
    public class WorkshopItemDto
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string PreviewUrl { get; set; }
        public string Type { get; set; }
        public List<string> Tags { get; set; }
        public int Page { get; set; }
        public int Rank { get; set; }
        public int Votes { get; set; }
        public int Subscriptions { get; set; }
        public long FileSize { get; set; }
        public string WorkshopUrl { get; set; }
        public bool HasPeople { get; set; }
        public bool PeopleScanned { get; set; }
        public bool BlankPreview { get; set; }
        public bool PreviewQualityChecked { get; set; }

        public WorkshopItemDto()
        {
            Id = "";
            Title = "";
            Description = "";
            Author = "";
            PreviewUrl = "";
            Type = "Wallpaper";
            Tags = new List<string>();
            WorkshopUrl = "";
        }
    }

    public class WorkshopCatalogDto
    {
        public DateTime FetchedAt { get; set; }
        public int PagesFetched { get; set; }
        public int ClassifierVersion { get; set; }
        public bool IncludesAllTags { get; set; }
        public int PreviewQualityVersion { get; set; }
        public List<WorkshopItemDto> Items { get; set; }

        public WorkshopCatalogDto()
        {
            Items = new List<WorkshopItemDto>();
        }
    }

    public class WorkshopBatch
    {
        public int Page { get; set; }
        public List<WorkshopItemDto> Items { get; set; }

        public WorkshopBatch()
        {
            Items = new List<WorkshopItemDto>();
        }
    }

    public class WorkshopFetchResult
    {
        public List<WorkshopItemDto> Items { get; set; }
        public int PagesFetched { get; set; }
        public int PagesRequested { get; set; }
        public DateTime FetchedAt { get; set; }
        public string Error { get; set; }

        public WorkshopFetchResult()
        {
            Items = new List<WorkshopItemDto>();
            FetchedAt = DateTime.Now;
            Error = "";
        }
    }

    public static class WorkshopCatalogStore
    {
        public static void Save(WorkshopCatalogDto catalog)
        {
            if (catalog == null) return;
            try
            {
                AppPaths.Ensure();
                string json = Json.Stringify(catalog);
                string tmp = AppPaths.WorkshopCacheFile + ".tmp";
                File.WriteAllText(tmp, json, new UTF8Encoding(false));
                if (File.Exists(AppPaths.WorkshopCacheFile)) File.Delete(AppPaths.WorkshopCacheFile);
                File.Move(tmp, AppPaths.WorkshopCacheFile);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to save workshop cache", ex);
            }
        }

        public static WorkshopCatalogDto Load()
        {
            try
            {
                if (!File.Exists(AppPaths.WorkshopCacheFile)) return null;
                string json = File.ReadAllText(AppPaths.WorkshopCacheFile, new UTF8Encoding(true));
                System.Web.Script.Serialization.JavaScriptSerializer serializer =
                    new System.Web.Script.Serialization.JavaScriptSerializer();
                serializer.MaxJsonLength = Int32.MaxValue;
                WorkshopCatalogDto catalog = serializer.Deserialize<WorkshopCatalogDto>(json);
                if (catalog == null || catalog.Items == null) return null;
                return catalog;
            }
            catch (Exception ex)
            {
                Log.Error("Failed to read workshop cache", ex);
                return null;
            }
        }
    }

    public class SteamWorkshopService
    {
        public const int DefaultPages = 40;
        private const int MaxItems = 100;
        public const long MaxVideoBytes = 10L * 1024 * 1024;
        private const int SlowAfterItems = 50;
        private static readonly Regex SsrScriptRegex = new Regex(
            "(?is)<script[^>]*id=\"valve-ssr-data\"[^>]*>(.*?)</script>", RegexOptions.Compiled);
        private static readonly Regex ItemStartRegex = new Regex(
            "(?is)<(?:div|a)[^>]*class=\"[^\"]*\\bworkshopItem\\b[^\"]*\"[^>]*>",
            RegexOptions.Compiled);
        private static readonly Regex IdRegex = new Regex(
            "sharedfiles/filedetails/\\?id=(\\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex TitleRegex = new Regex(
            "(?is)class=\"workshopItemTitle\"[^>]*>(.*?)</div>", RegexOptions.Compiled);
        private static readonly Regex PreviewRegex = new Regex(
            "(?is)<img[^>]+>", RegexOptions.Compiled);
        private static readonly Regex DescriptionRegex = new Regex(
            "(?is)class=\"workshopItemShortDesc\"[^>]*>(.*?)</div>", RegexOptions.Compiled);
        private static readonly Regex AuthorRegex = new Regex(
            "(?is)class=\"workshopItemAuthorName\"[^>]*>(.*?)</(?:div|a|span)>", RegexOptions.Compiled);
        private static readonly Regex TagsBlockRegex = new Regex(
            "(?is)class=\"workshopTags\"[^>]*>(.*?)</div>", RegexOptions.Compiled);
        private static readonly Regex AnchorRegex = new Regex(
            "(?is)<a[^>]*>(.*?)</a>", RegexOptions.Compiled);
        private static readonly Regex SpanRegex = new Regex(
            "(?is)<span[^>]*>(.*?)</span>", RegexOptions.Compiled);
        private static readonly Regex TagRegex = new Regex("<[^>]+>", RegexOptions.Compiled);

        private static string BuildBrowseUrl(string requiredTag, int page, bool english, bool country)
        {
            string url = "https://steamcommunity.com/workshop/browse/?appid=431960&browsefilter=mostsubscribed"
                + "&p=" + page.ToString();
            if (english) url += "&l=english";
            if (country) url += "&cc=US";
            if (!String.IsNullOrEmpty(requiredTag))
                url += "&requiredtags%5B%5D=" + Uri.EscapeDataString(requiredTag);
            return url;
        }

        public async Task<WorkshopFetchResult> FetchTopAsync(int pages, IProgress<string> progress,
            IProgress<WorkshopBatch> batchProgress, CancellationToken cancellationToken,
            Func<WorkshopItemDto, bool> accept, string requiredTag, int maxItems)
        {
            if (maxItems <= 0) maxItems = MaxItems;
            if (pages <= 0) pages = DefaultPages;
            WorkshopFetchResult result = new WorkshopFetchResult();
            result.PagesRequested = pages;
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            int rank = 1;
            string firstError = "";
            for (int page = 1; page <= pages; page++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (progress != null) progress.Report("Reading creative workshop subscription list" + page + " / " + pages + "Page"
                    + (String.IsNullOrEmpty(requiredTag) ? "" : " · " + requiredTag) + "…");
                string url = BuildBrowseUrl(requiredTag, page, true, true);
                string html = null;
                List<WorkshopItemDto> pageItems = null;
                string lastError = "";
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    if (attempt == 1)
                    {
                        url = BuildBrowseUrl(requiredTag, page, true, false);
                        await Task.Delay(300, cancellationToken).ConfigureAwait(false);
                    }
                    else if (attempt == 2)
                    {
                        url = BuildBrowseUrl(requiredTag, page, false, false);
                        await Task.Delay(600, cancellationToken).ConfigureAwait(false);
                    }
                    try
                    {
                        html = await Http.GetStringAsync(url, 25).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        lastError = ex.Message;
                        if (attempt < 2) continue;
                        if (page == 1)
                            throw new InvalidOperationException("Unable to connect to Steam Workshop."
                                + "English and US store lines have been tried; please confirm that the current network can access steamcommunity.com and try again.", ex);
                        Log.Warn("Read the creative workshop chapter" + page + "Page failed, retaining the obtained content:" + ex.Message);
                        result.Error = "No." + page + "Reading breaks after page:" + ex.Message;
                        break;
                    }
                    pageItems = ParseSsrPage(html, page);
                    if (pageItems.Count == 0) pageItems = ParsePage(html, page, rank);
                    if (pageItems.Count > 0) break;
                    lastError = "The page returns but the wallpaper is not resolved";
                }
                if (pageItems == null || pageItems.Count == 0)
                {
                    if (page == 1)
                    {
                        throw new InvalidOperationException("The Steam Workshop page has been returned, but no wallpapers were resolved."
                            + "Maybe the current network is redirected to the verification page, or the page structure changes." + lastError);
                    }
                    firstError = "No." + page + "The page has no more items and has ended early.";
                    break;
                }

                List<WorkshopItemDto> added = new List<WorkshopItemDto>();
                int remaining = maxItems - result.Items.Count;
                foreach (WorkshopItemDto item in pageItems)
                {
                    if (added.Count >= remaining) break;
                    if (seen.Contains(item.Id)) continue;
                    if (accept != null && !accept(item)) continue;
                    seen.Add(item.Id);
                    item.Rank = rank++;
                    result.Items.Add(item);
                    added.Add(item);
                }
                result.PagesFetched = page;
                if (batchProgress != null && added.Count > 0)
                {
                    WorkshopBatch batch = new WorkshopBatch();
                    batch.Page = page;
                    batch.Items = added;
                    batchProgress.Report(batch);
                }
                if (result.Items.Count >= maxItems) break;
                if (page < pages)
                {
                    int delay = result.Items.Count > SlowAfterItems ? 450 : 180;
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
            result.FetchedAt = DateTime.Now;

            // The modern Steam page exposes exact subscription and vote counts. Rank by
            // subscriptions (the most-subscribed ordering), not by the page it happened
            // to appear on. This is important because Steam's default list is "trend",
            // not an all-time most-subscribed list.
            result.Items.Sort(delegate(WorkshopItemDto a, WorkshopItemDto b)
            {
                int subscriptions = b.Subscriptions.CompareTo(a.Subscriptions);
                if (subscriptions != 0) return subscriptions;
                int votes = b.Votes.CompareTo(a.Votes);
                if (votes != 0) return votes;
                return a.Rank.CompareTo(b.Rank);
            });
            for (int i = 0; i < result.Items.Count; i++) result.Items[i].Rank = i + 1;

            if (!String.IsNullOrEmpty(firstError) && String.IsNullOrEmpty(result.Error)) result.Error = firstError;

            // Keep the long tail available for ranking, but cap a pathological page response.
            if (result.Items.Count > maxItems) result.Items.RemoveRange(maxItems, result.Items.Count - maxItems);
            if (progress != null)
                progress.Report("Read" + result.PagesFetched + "page, get" + result.Items.Count + "candidate wallpapers");
            return result;
        }

        public List<WorkshopItemDto> ParseSsrPage(string html, int page)
        {
            List<WorkshopItemDto> result = new List<WorkshopItemDto>();
            if (String.IsNullOrEmpty(html)) return result;
            Match script = SsrScriptRegex.Match(html);
            if (!script.Success) return result;
            object root;
            try
            {
                root = Json.Parse(script.Groups[1].Value);
            }
            catch (Exception ex)
            {
                Log.Warn("Failed to parse workshop SSR data:" + ex.Message);
                return result;
            }
            List<Dictionary<string, object>> records = new List<Dictionary<string, object>>();
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            CollectSsrResults(root, records, ids, 0);
            foreach (Dictionary<string, object> record in records)
            {
                string id = Json.GetString(record, "publishedfileid");
                string title = CleanHtml(Json.GetString(record, "title"));
                if (String.IsNullOrEmpty(id) || String.IsNullOrEmpty(title)) continue;
                List<string> tags = ParseTagObjects(record);
                if (tags.Count > 0 && !HasWallpaperTag(tags)) continue;
                if (HasAnyTag(tags, "Collection")) continue;
                long fileSize = (long)Json.GetDouble(record, "file_size", 0);
                string itemType = InferType(tags);
                if (String.Equals(itemType, "video", StringComparison.OrdinalIgnoreCase)
                    && fileSize > MaxVideoBytes) continue;

                WorkshopItemDto item = new WorkshopItemDto();
                item.Id = id;
                item.Title = title;
                item.Description = CleanHtml(Json.GetString(record, "short_description"));
                item.Author = Json.GetString(record, "creator");
                item.PreviewUrl = NormalizeUrl(Json.GetString(record, "preview_url"));
                item.Type = itemType;
                item.FileSize = fileSize;
                item.Tags = tags;
                item.Page = page;
                item.Rank = result.Count + 1;
                item.Votes = Json.GetInt(record, "total_votes", 0);
                item.Subscriptions = Json.GetInt(record, "subscriptions", 0);
                item.WorkshopUrl = "https://steamcommunity.com/sharedfiles/filedetails/?id=" + id;
                result.Add(item);
            }
            return result;
        }

        private static void CollectSsrResults(object node, List<Dictionary<string, object>> records,
            HashSet<string> ids, int depth)
        {
            if (node == null || depth > 7) return;
            Dictionary<string, object> dict = node as Dictionary<string, object>;
            if (dict != null)
            {
                object resultsObject;
                if (dict.TryGetValue("results", out resultsObject))
                {
                    object[] array = resultsObject as object[];
                    if (array != null)
                    {
                        foreach (object entry in array)
                        {
                            Dictionary<string, object> record = Json.Object(entry);
                            if (record == null) continue;
                            string id = Json.GetString(record, "publishedfileid");
                            if (String.IsNullOrEmpty(id) || ids.Contains(id)) continue;
                            ids.Add(id);
                            records.Add(record);
                        }
                    }
                }
                foreach (object value in dict.Values)
                    CollectSsrResults(value, records, ids, depth + 1);
                return;
            }
            object[] list = node as object[];
            if (list != null)
            {
                foreach (object value in list) CollectSsrResults(value, records, ids, depth + 1);
                return;
            }
            string text = node as string;
            if (!String.IsNullOrEmpty(text) && text.Length > 1000
                && text.IndexOf("\"publishedfileid\"", StringComparison.Ordinal) >= 0
                && text.IndexOf("\"results\"", StringComparison.Ordinal) >= 0)
            {
                string trimmed = text.TrimStart();
                if (trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal))
                {
                    try
                    {
                        object parsed = Json.Parse(text);
                        CollectSsrResults(parsed, records, ids, depth + 1);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static List<string> ParseTagObjects(Dictionary<string, object> record)
        {
            List<string> tags = new List<string>();
            List<object> raw = Json.GetArray(record, "tags");
            foreach (object entry in raw)
            {
                Dictionary<string, object> tag = Json.Object(entry);
                if (tag == null) continue;
                string value = Json.GetString(tag, "tag");
                if (String.IsNullOrEmpty(value)) value = Json.GetString(tag, "display_name");
                if (!String.IsNullOrEmpty(value)) tags.Add(value);
            }
            return tags;
        }

        public List<WorkshopItemDto> ParsePage(string html, int page, int rankStart)
        {
            List<WorkshopItemDto> result = new List<WorkshopItemDto>();
            if (String.IsNullOrEmpty(html)) return result;
            MatchCollection starts = ItemStartRegex.Matches(html);
            for (int i = 0; i < starts.Count; i++)
            {
                int start = starts[i].Index;
                int end = i + 1 < starts.Count ? starts[i + 1].Index : Math.Min(html.Length, start + 12000);
                if (end <= start) continue;
                string block = html.Substring(start, end - start);
                Match id = IdRegex.Match(block);
                if (!id.Success) continue;
                string title = CleanHtml(GetGroup(TitleRegex, block));
                if (String.IsNullOrEmpty(title)) continue;
                string tagsBlock = GetGroup(TagsBlockRegex, block);
                List<string> tags = ParseTags(tagsBlock);
                if (tags.Count > 0 && !HasWallpaperTag(tags)) continue;
                if (HasAnyTag(tags, "Collection")) continue;
                string preview = FindPreview(block);
                string type = InferType(tags);
                WorkshopItemDto item = new WorkshopItemDto();
                item.Id = id.Groups[1].Value;
                item.Title = title;
                item.Description = CleanHtml(GetGroup(DescriptionRegex, block));
                item.Author = CleanHtml(GetGroup(AuthorRegex, block));
                item.PreviewUrl = NormalizeUrl(preview);
                item.Type = type;
                item.Tags = tags;
                item.Page = page;
                item.Rank = rankStart + result.Count;
                item.WorkshopUrl = "https://steamcommunity.com/sharedfiles/filedetails/?id=" + item.Id;
                result.Add(item);
            }
            return result;
        }

        private static bool HasWallpaperTag(List<string> tags)
        {
            foreach (string tag in tags)
            {
                if (String.Equals(tag, "Wallpaper", StringComparison.OrdinalIgnoreCase)
                    || String.Equals(tag, "Scene", StringComparison.OrdinalIgnoreCase)
                    || String.Equals(tag, "Video", StringComparison.OrdinalIgnoreCase)
                    || String.Equals(tag, "Web", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool HasAnyTag(List<string> tags, params string[] needles)
        {
            foreach (string tag in tags)
            {
                foreach (string needle in needles)
                    if (String.Equals(tag, needle, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static string InferType(List<string> tags)
        {
            foreach (string tag in tags)
            {
                if (String.Equals(tag, "Scene", StringComparison.OrdinalIgnoreCase)) return "scene";
                if (String.Equals(tag, "Video", StringComparison.OrdinalIgnoreCase)) return "video";
                if (String.Equals(tag, "Web", StringComparison.OrdinalIgnoreCase)) return "web";
            }
            return "wallpaper";
        }

        private static List<string> ParseTags(string block)
        {
            List<string> tags = new List<string>();
            if (String.IsNullOrEmpty(block)) return tags;
            MatchCollection anchors = AnchorRegex.Matches(block);
            if (anchors.Count > 0)
            {
                foreach (Match anchor in anchors)
                {
                    string tag = CleanHtml(anchor.Groups[1].Value);
                    if (!String.IsNullOrEmpty(tag)) tags.Add(tag);
                }
            }
            else
            {
                MatchCollection spans = SpanRegex.Matches(block);
                if (spans.Count > 0)
                {
                    foreach (Match span in spans)
                    {
                        string tag = CleanHtml(span.Groups[1].Value);
                        if (!String.IsNullOrEmpty(tag)) tags.Add(tag);
                    }
                }
                else
                {
                    string plain = CleanHtml(block);
                    string[] parts = plain.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string part in parts)
                    {
                        string tag = part.Trim();
                        if (!String.IsNullOrEmpty(tag)) tags.Add(tag);
                    }
                }
            }
            return tags;
        }

        private static string FindPreview(string block)
        {
            MatchCollection images = PreviewRegex.Matches(block);
            foreach (Match image in images)
            {
                string tag = image.Value;
                if (tag.IndexOf("workshopItemPreviewImage", StringComparison.OrdinalIgnoreCase) < 0
                    && tag.IndexOf("steamusercontent", StringComparison.OrdinalIgnoreCase) < 0) continue;
                Match src = Regex.Match(tag, "(?is)(?:data-src|src)=\"([^\"]+)\"");
                if (src.Success) return WebUtility.HtmlDecode(src.Groups[1].Value);
            }
            return "";
        }

        private static string GetGroup(Regex regex, string text)
        {
            Match match = regex.Match(text);
            return match.Success ? match.Groups[1].Value : "";
        }

        private static string NormalizeUrl(string url)
        {
            if (String.IsNullOrEmpty(url)) return "";
            url = WebUtility.HtmlDecode(url).Trim();
            if (url.StartsWith("//", StringComparison.Ordinal)) url = "https:" + url;
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url.Substring(7);
            return url;
        }

        private static List<string> PreviewCandidates(string url)
        {
            List<string> result = new List<string>();
            string normalized = NormalizeUrl(url);
            int query = normalized.IndexOf('?');
            string baseUrl = query >= 0 ? normalized.Substring(0, query) : normalized;
            if (baseUrl.IndexOf("steamusercontent.com/ugc/", StringComparison.OrdinalIgnoreCase) >= 0
                && baseUrl.EndsWith("/", StringComparison.Ordinal))
            {
                result.Add(baseUrl + "?imw=637&imh=358&ima=fit&impolicy=Letterbox&imcolor=%23000000&letterbox=true");
                result.Add(baseUrl + "?imw=1024&imh=1024&ima=fit&impolicy=Letterbox&imcolor=%23000000&letterbox=false");
                result.Add(baseUrl + "?imw=5000&imh=5000&ima=fit&impolicy=Letterbox&imcolor=%23000000&letterbox=false");
            }
            if (result.Count == 0) result.Add(normalized);
            else if (query >= 0 && !result.Contains(normalized)) result.Insert(0, normalized);
            return result;
        }

        private static string CleanHtml(string value)
        {
            if (String.IsNullOrEmpty(value)) return "";
            string text = TagRegex.Replace(value, " ");
            text = WebUtility.HtmlDecode(text);
            text = Regex.Replace(text, "\\s+", " ");
            return text.Trim();
        }

        public async Task<string> EnsurePreviewAsync(string id, string previewUrl)
        {
            if (String.IsNullOrEmpty(id) || String.IsNullOrEmpty(previewUrl)) return "";
            AppPaths.Ensure();
            string path = Path.Combine(AppPaths.WorkshopPreviewDirectory, id + ".jpg");
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length > 256)
                {
                    if (IsUsableImage(path)) return path;
                    try { File.Delete(path); } catch { }
                }
                string lastError = "";
                List<string> candidates = PreviewCandidates(previewUrl);
                foreach (string candidate in candidates)
                {
                    try
                    {
                        byte[] bytes = await Http.GetBytesAsync(candidate, 15).ConfigureAwait(false);
                        if (bytes == null || bytes.Length < 256 || bytes.Length > 8 * 1024 * 1024)
                        {
                            lastError = "Response size exception";
                            continue;
                        }
                        string tmp = path + ".tmp";
                        File.WriteAllBytes(tmp, bytes);
                        if (!IsUsableImage(tmp))
                        {
                            try { File.Delete(tmp); } catch { }
                            lastError = "The returned content is not a valid image";
                            continue;
                        }
                        if (File.Exists(path)) File.Delete(path);
                        File.Move(tmp, path);
                        return path;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex.Message;
                    }
                }
                Log.Warn("Failed to download workshop preview:" + id + " | " + lastError);
                return "";
            }
            catch (Exception ex)
            {
                Log.Warn("Failed to download workshop preview:" + id + " | " + ex.Message);
                return "";
            }
        }
        private static bool IsUsableImage(string path)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (Image image = Image.FromStream(stream, false, false))
                {
                    return image.Width > 16 && image.Height > 16;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
