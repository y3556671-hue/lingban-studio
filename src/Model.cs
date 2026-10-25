using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows.Media.Imaging;

namespace Lingban
{
    [DataContract]
    public enum PetKind
    {
        [EnumMember] Cat = 0,
        [EnumMember] Rabbit = 1,
        [EnumMember] Blob = 2,
        [EnumMember] Image = 3
    }

    [DataContract]
    public class PetProfile
    {
        [DataMember(Order = 1)] public string Name { get; set; }
        [DataMember(Order = 2)] public string Motto { get; set; }
        [DataMember(Order = 3)] public string ImagePath { get; set; }
        [DataMember(Order = 4)] public PetKind Kind { get; set; }
        [DataMember(Order = 5)] public double Energy { get; set; }
        [DataMember(Order = 6)] public double Affinity { get; set; }
        [DataMember(Order = 7)] public double Spice { get; set; }
        [DataMember(Order = 8)] public bool Quiet { get; set; }
        [DataMember(Order = 9)] public double Size { get; set; }

        public static PetProfile Default()
        {
            return new PetProfile
            {
                Name = "栗子",
                Motto = "陪你把普通的一天过得有趣一点。",
                ImagePath = String.Empty,
                Kind = PetKind.Cat,
                Energy = 65,
                Affinity = 80,
                Spice = 25,
                Quiet = false,
                Size = 1
            };
        }

        public PetProfile Clone()
        {
            return (PetProfile)MemberwiseClone();
        }

        [OnDeserializing]
        private void BeforeDeserializing(StreamingContext context)
        {
            PetProfile defaults = Default();
            Name = defaults.Name;
            Motto = defaults.Motto;
            ImagePath = defaults.ImagePath;
            Kind = defaults.Kind;
            Energy = defaults.Energy;
            Affinity = defaults.Affinity;
            Spice = defaults.Spice;
            Quiet = defaults.Quiet;
            Size = defaults.Size;
        }

        public void Normalize()
        {
            Name = CleanText(Name, 20);
            if (Name.Length == 0) Name = "栗子";
            Motto = CleanText(Motto, 160);
            ImagePath = ImagePath == null ? String.Empty : ImagePath.Trim();
            if (ImagePath.Length > 2048 || ImagePath.IndexOf('\0') >= 0)
                ImagePath = String.Empty;
            if (!Enum.IsDefined(typeof(PetKind), Kind)) Kind = PetKind.Cat;
            Energy = Clamp(Energy, 0, 100, 65);
            Affinity = Clamp(Affinity, 0, 100, 80);
            Spice = Clamp(Spice, 0, 100, 25);
            Size = Clamp(Size, 0.6, 1.7, 1);
        }

        private static double Clamp(double value, double minimum, double maximum, double fallback)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value)) return fallback;
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static string CleanText(string value, int maximumLength)
        {
            if (String.IsNullOrWhiteSpace(value)) return String.Empty;
            StringBuilder text = new StringBuilder();
            foreach (char character in value.Trim())
            {
                if (!Char.IsControl(character)) text.Append(character);
                else if (character == '\r' || character == '\n' || character == '\t') text.Append(' ');
                if (text.Length >= maximumLength) break;
            }
            // Keep a shortened name or motto from ending halfway through an emoji.
            if (text.Length > 0 && Char.IsHighSurrogate(text[text.Length - 1]))
                text.Length--;
            return text.ToString().Trim();
        }
    }

    public static class Personality
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, int> NextLine = new Dictionary<string, int>();

        public static string Describe(PetProfile p)
        {
            PetProfile profile = Prepare(p);
            string energy = profile.Energy >= 65 ? "活泼好动" : profile.Energy <= 35 ? "沉稳悠闲" : "动静皆宜";
            string affinity = profile.Affinity >= 65 ? "喜欢亲近" : profile.Affinity <= 35 ? "独立自在" : "慢慢熟悉";
            string spice = profile.Spice >= 65 ? "嘴硬心软" : profile.Spice <= 35 ? "温柔直率" : "俏皮随和";
            return energy + " · " + affinity + " · " + spice;
        }

        public static string Respond(PetProfile p, string situation)
        {
            PetProfile profile = Prepare(p);
            string action = String.IsNullOrWhiteSpace(situation) ? "idle" : situation.ToLowerInvariant();
            if (action != "hello" && action != "pet" && action != "drag" && action != "focus") action = "idle";
            string style = profile.Spice >= 65 ? "spicy" : profile.Affinity >= 65 ? "close" : profile.Energy >= 65 ? "active" : "calm";
            if (action == "drag" && profile.Spice < 65)
                style = profile.Energy >= 65 ? "active" : profile.Energy <= 35 ? "calm" : style;
            string[] lines;
            switch (action)
            {
                case "hello":
                    if (style == "spicy") lines = new[] { "我叫" + profile.Name + "。旁边的位置先替你留着。", "来啦？我也刚好在这里。", "哼，今天也一起把日子过好吧。" };
                    else if (style == "close") lines = new[] { "我是" + profile.Name + "，今天也陪在你身边。", "见到你真好，给你留了一个软软的位置。", "今天想一起做点什么？" };
                    else if (style == "active") lines = new[] { "我是" + profile.Name + "，出发探索今天吧！", "准备好了！今天会遇到什么有趣的事呢？", "你好呀，我已经热完身啦！" };
                    else lines = new[] { "我是" + profile.Name + "。在这里安静待一会儿也很好。", "你好，今天按自己的节奏来。", "窗边的位置不错，我们慢慢开始吧。" };
                    break;
                case "pet":
                    if (style == "spicy") lines = new[] { "才没有很喜欢……再摸一下也可以。", "嗯，手法勉强合格。", "只许轻轻摸哦。" };
                    else if (style == "close") lines = new[] { "摸摸头，开心加一。", "蹭蹭你的手，今天也很温暖。", "这个位置刚刚好，再停一小会儿吧。" };
                    else if (style == "active") lines = new[] { "接到摸摸！现在想蹦一下！", "嘿嘿，要一起玩个小游戏吗？", "充电完成，尾巴都要摇起来啦！" };
                    else lines = new[] { "嗯，很舒服。", "谢谢，轻轻的就很好。", "安安静静地待在这里，也很惬意。" };
                    break;
                case "drag":
                    if (style == "spicy") lines = new[] { "喂，搬家也要提前通知吧。", "这趟顺风车，我就收下了。", "放稳一点，我可要保持帅气。" };
                    else if (style == "close") lines = new[] { "你带路，我看看新地方。", "抱稳啦，一起换个风景。", "这里也很棒，就在旁边陪你。" };
                    else if (style == "active") lines = new[] { "起飞！下一站是哪里？", "换个角落继续探险！", "哇，这里的风景有点不一样！" };
                    else lines = new[] { "慢慢放下就好。", "换个位置，继续发呆。", "这里也很安静，我喜欢。" };
                    break;
                case "focus":
                    if (style == "spicy") lines = new[] { "你专心，我替你看着时间。", "这段时间交给正事，我会安静点。", "别急，先把眼前这一件做好。" };
                    else if (style == "close") lines = new[] { "我陪你一起专心，结束后再伸个懒腰。", "现在进入安静陪伴模式。", "一小步一小步来，我在旁边。" };
                    else if (style == "active") lines = new[] { "专注挑战开始！先完成这一小段。", "我也坐好啦，一起开工！", "准备，开始！把注意力交给眼前的事。" };
                    else lines = new[] { "时间开始了，慢慢进入状态。", "不着急，专注眼前就好。", "我就在这里，安静陪你。" };
                    break;
                case "idle":
                default:
                    if (style == "spicy") lines = new[] { "我只是刚好路过你的桌面。", "这个角落，暂时归我坐啦。", "偶尔发呆，也是很正经的安排。" };
                    else if (style == "close") lines = new[] { "陪你坐一会儿，普通的一天也很好。", "在旁边伸个懒腰，和你共享这片小天地。", "给今天留一点轻松的时间吧。" };
                    else if (style == "active") lines = new[] { "发现一块空地，适合伸个懒腰！", "今天的桌面探险还在继续。", "我绕一小圈，再回来看看。" };
                    else lines = new[] { "这里很舒服，适合安静歇一会儿。", "看着时间慢慢走，也挺好。", "不忙的时候，就伸个懒腰吧。" };
                    break;
            }
            string key = action + ":" + style;
            lock (Sync)
            {
                int index;
                if (!NextLine.TryGetValue(key, out index)) index = 0;
                NextLine[key] = (index + 1) % lines.Length;
                string response = lines[index];
                if (action == "focus" && profile.Motto.Length > 0)
                    response += " " + profile.Motto;
                return response;
            }
        }

        private static PetProfile Prepare(PetProfile p)
        {
            PetProfile result = p == null ? PetProfile.Default() : p.Clone();
            result.Normalize();
            return result;
        }
    }

    public static class ProfileStore
    {
        private const long MaximumImageBytes = 10L * 1024L * 1024L;
        private const long MaximumProfileBytes = 64L * 1024L;
        private const int MaximumImageDimension = 4096;
        private static readonly object Sync = new object();

        public static string BaseDirectory
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data"); }
        }

        public static PetProfile Load()
        {
            lock (Sync)
            {
                string path = Path.Combine(BaseDirectory, "profile.json");
                if (!File.Exists(path)) return PetProfile.Default();
                try
                {
                    FileInfo info = new FileInfo(path);
                    if (info.Length <= 0 || info.Length > MaximumProfileBytes)
                        throw new SerializationException("存档大小无效。");
                    PetProfile result;
                    using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(PetProfile));
                        // A text editor may add a BOM; deserialize a normalized UTF-8 stream.
                        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
                        using (MemoryStream json = new MemoryStream(Encoding.UTF8.GetBytes(reader.ReadToEnd())))
                            result = serializer.ReadObject(json) as PetProfile;
                    }
                    if (result == null) throw new SerializationException("存档内容无效。");
                    result.Normalize();
                    result.ImagePath = ResolveImagePath(result.ImagePath);
                    return result;
                }
                catch (Exception exception)
                {
                    if (IsFatal(exception)) throw;
                    // Preserve the exact unreadable file before restoring a default profile.
                    // No profile contents, paths, or exception details leave this module.
                    string preservedPath = Path.Combine(BaseDirectory,
                        "profile.corrupt-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                        + "-" + Guid.NewGuid().ToString("N") + ".json");
                    try
                    {
                        File.Move(path, preservedPath);
                        Save(PetProfile.Default());
                    }
                    catch (Exception recoveryException)
                    {
                        if (IsFatal(recoveryException)) throw;
                        // If the directory is not writable, keep the original file in place
                        // (or at preservedPath) and allow a safe default in this session.
                    }
                    return PetProfile.Default();
                }
            }
        }

        public static void Save(PetProfile p)
        {
            if (p == null) throw new ArgumentNullException("p");
            PetProfile profile = p.Clone();
            profile.Normalize();
            string resolvedImagePath = ResolveImagePath(profile.ImagePath);
            string assetsDirectory = Path.GetFullPath(Path.Combine(BaseDirectory, "assets"));
            profile.ImagePath = resolvedImagePath.Length > 0 && String.Equals(Path.GetDirectoryName(resolvedImagePath), assetsDirectory,
                StringComparison.OrdinalIgnoreCase)
                ? Path.Combine("assets", Path.GetFileName(resolvedImagePath)) : resolvedImagePath;
            lock (Sync)
            {
                Directory.CreateDirectory(BaseDirectory);
                string path = Path.Combine(BaseDirectory, "profile.json");
                string temporaryPath = Path.Combine(BaseDirectory, "profile-" + Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                        FileShare.None, 4096, FileOptions.WriteThrough))
                    {
                        DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(PetProfile));
                        serializer.WriteObject(stream, profile);
                        stream.Flush(true);
                    }
                    if (File.Exists(path))
                        File.Replace(temporaryPath, path, null, true);
                    else
                        File.Move(temporaryPath, path);
                }
                finally
                {
                    try
                    {
                        if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }

        public static string ImportImage(string source)
        {
            if (String.IsNullOrWhiteSpace(source)) throw new ArgumentException("请先选择一张图片。", "source");
            string sourcePath = Path.GetFullPath(source);
            string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
                throw new InvalidDataException("仅支持 PNG、JPG 或 JPEG 图片。");

            using (FileStream stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length <= 0 || stream.Length > MaximumImageBytes)
                    throw new InvalidDataException("请选择大小不超过 10 MB 的图片。");

                BitmapDecoder decoder = BitmapDecoder.Create(stream,
                    BitmapCreateOptions.DelayCreation | BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.None);
                if (!(decoder is PngBitmapDecoder) && !(decoder is JpegBitmapDecoder))
                    throw new InvalidDataException("文件内容必须是 PNG 或 JPEG 图片。");
                if (decoder.Frames.Count != 1) throw new InvalidDataException("请选择一张静态图片。");
                CheckDimensions(decoder.Frames[0].PixelWidth, decoder.Frames[0].PixelHeight);

                stream.Position = 0;
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                CheckDimensions(image.PixelWidth, image.PixelHeight);

                string assetsDirectory = Path.Combine(BaseDirectory, "assets");
                Directory.CreateDirectory(assetsDirectory);
                string targetPath = Path.Combine(assetsDirectory,
                    Guid.NewGuid().ToString("N") + (decoder is PngBitmapDecoder ? ".png" : ".jpg"));
                try
                {
                    stream.Position = 0;
                    using (FileStream target = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write,
                        FileShare.None, 81920, FileOptions.WriteThrough))
                    {
                        stream.CopyTo(target);
                        target.Flush(true);
                    }
                    return targetPath;
                }
                catch
                {
                    try { if (File.Exists(targetPath)) File.Delete(targetPath); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                    throw;
                }
            }
        }

        private static string ResolveImagePath(string storedPath)
        {
            if (String.IsNullOrWhiteSpace(storedPath)) return String.Empty;
            try
            {
                string assets = Path.GetFullPath(Path.Combine(BaseDirectory, "assets"));
                string extension = Path.GetExtension(storedPath).ToLowerInvariant();
                if (extension != ".png" && extension != ".jpg" && extension != ".jpeg") return String.Empty;
                // Imported assets are flat files. Resolve only beneath that directory;
                // a relative save must never read a parent folder or a network share.
                if (!Path.IsPathRooted(storedPath))
                {
                    string resolved = Path.GetFullPath(Path.Combine(BaseDirectory, storedPath));
                    return String.Equals(Path.GetDirectoryName(resolved), assets, StringComparison.OrdinalIgnoreCase)
                        ? resolved : String.Empty;
                }
                if (storedPath.StartsWith("\\\\", StringComparison.Ordinal)
                    || storedPath.Length < 3 || storedPath[1] != ':' || (storedPath[2] != '\\' && storedPath[2] != '/'))
                    return String.Empty;
                string absolute = Path.GetFullPath(storedPath);
                string parent = Path.GetDirectoryName(absolute);
                if (String.Equals(parent, assets, StringComparison.OrdinalIgnoreCase)) return absolute;
                // Older versions saved an absolute <app>/data/assets path. Prefer the
                // matching asset next to this executable when the app folder moves.
                if (!String.Equals(Path.GetFileName(parent), "assets", StringComparison.OrdinalIgnoreCase)
                    || !String.Equals(Path.GetFileName(Path.GetDirectoryName(parent)), "data", StringComparison.OrdinalIgnoreCase))
                    return String.Empty;
                string local = Path.Combine(assets, Path.GetFileName(absolute));
                if (File.Exists(local)) return local;
                return File.Exists(absolute) ? absolute : String.Empty;
            }
            catch (ArgumentException) { return String.Empty; }
            catch (NotSupportedException) { return String.Empty; }
            catch (PathTooLongException) { return String.Empty; }
            catch (System.Security.SecurityException) { return String.Empty; }
        }

        private static void CheckDimensions(int width, int height)
        {
            if (width <= 0 || height <= 0 || width > MaximumImageDimension || height > MaximumImageDimension)
                throw new InvalidDataException("图片的宽和高都不能超过 4096 像素。");
        }

        private static bool IsFatal(Exception exception)
        {
            return exception is OutOfMemoryException || exception is StackOverflowException
                || exception is AccessViolationException || exception is System.Threading.ThreadAbortException;
        }
    }
}
