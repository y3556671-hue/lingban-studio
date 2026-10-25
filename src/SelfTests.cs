using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Lingban
{
    public static class SelfTests
    {
        public static string Run()
        {
            // Storage tests must never replace a user's pet or imported artwork.
            if (Directory.Exists(ProfileStore.BaseDirectory))
                throw new InvalidOperationException("自检需要全新的隔离测试目录：data 文件夹已经存在，请将程序复制到新的测试目录后运行。");

            List<string> results = new List<string>();
            CheckCase(results, "默认角色可直接使用", delegate
            {
                PetProfile p = PetProfile.Default();
                Require(!String.IsNullOrWhiteSpace(p.Name), "默认角色必须有名字");
                Require(!String.IsNullOrWhiteSpace(p.Motto), "默认角色必须有口头禅");
                Require(Enum.IsDefined(typeof(PetKind), p.Kind), "默认外观有效");
                Require(InRange(p.Energy, 0, 100) && InRange(p.Affinity, 0, 100)
                    && InRange(p.Spice, 0, 100) && InRange(p.Size, 0.6, 1.7), "默认设置应在可用范围内");
                Require(p.ImagePath != null && !p.Quiet, "默认模式可正常互动");
            });

            CheckCase(results, "越界设置与无效外观自动修正", delegate
            {
                PetProfile p = PetProfile.Default();
                p.Energy = -12; p.Affinity = 180; p.Spice = -1; p.Size = 99;
                p.Kind = (PetKind)99;
                p.Normalize();
                Require(p.Energy == 0 && p.Affinity == 100 && p.Spice == 0 && p.Size == 1.7,
                    "越界设置应收敛到边界");
                Require(p.Kind == PetKind.Cat, "无效外观应恢复猫咪");
                p.Energy = 120; p.Affinity = -4; p.Spice = 109; p.Size = -1;
                p.Normalize();
                Require(p.Energy == 100 && p.Affinity == 0 && p.Spice == 100 && p.Size == 0.6,
                    "反方向越界也应修正");
            });

            CheckCase(results, "异常数值与空文本有可靠默认值", delegate
            {
                PetProfile p = PetProfile.Default();
                p.Name = " \r\n\t "; p.Motto = null; p.ImagePath = null;
                p.Energy = Double.NaN; p.Affinity = Double.PositiveInfinity;
                p.Spice = Double.NegativeInfinity; p.Size = Double.NaN;
                p.Normalize();
                PetProfile defaults = PetProfile.Default();
                Require(p.Name == defaults.Name && p.Motto == String.Empty && p.ImagePath == String.Empty,
                    "空文本应安全处理");
                Require(p.Energy == defaults.Energy && p.Affinity == defaults.Affinity
                    && p.Spice == defaults.Spice && p.Size == defaults.Size, "非有限数应恢复默认值");
            });

            CheckCase(results, "长文本和控制字符不会破坏角色资料", delegate
            {
                PetProfile p = PetProfile.Default();
                p.Name = "  名\n字\0" + new string('甲', 30);
                p.Motto = new string('乙', 200);
                p.ImagePath = "坏\0路径";
                p.Normalize();
                Require(p.Name.Length <= 20 && p.Motto.Length <= 160, "文字必须限长");
                foreach (char c in p.Name) Require(!Char.IsControl(c), "名字不应残留控制字符");
                Require(p.ImagePath == String.Empty, "包含空字符的图片路径应清除");
                p.Name = new string('甲', 19) + "\uD83D\uDE3A";
                p.Normalize();
                Require(!Char.IsHighSurrogate(p.Name[p.Name.Length - 1]), "截断不能留下半个表情字符");
            });

            CheckCase(results, "复制角色后可以独立修改", delegate
            {
                PetProfile original = PetProfile.Default();
                PetProfile copied = original.Clone();
                copied.Name = "另一个伙伴"; copied.Energy = 0; copied.Quiet = true;
                copied.Kind = PetKind.Rabbit; copied.Normalize();
                Require(!Object.ReferenceEquals(original, copied), "克隆应创建新对象");
                PetProfile defaults = PetProfile.Default();
                Require(original.Name == defaults.Name && original.Energy == defaults.Energy
                    && original.Quiet == defaults.Quiet && original.Kind == defaults.Kind,
                    "修改副本不能影响原角色");
            });

            CheckCase(results, "三项性格设置会改变性格说明", delegate
            {
                PetProfile low = PetProfile.Default();
                low.Energy = 0; low.Affinity = 0; low.Spice = 0;
                string baseline = Personality.Describe(low);
                PetProfile changed = low.Clone(); changed.Energy = 100;
                Require(Personality.Describe(changed) != baseline, "活力应影响性格说明");
                changed = low.Clone(); changed.Affinity = 100;
                Require(Personality.Describe(changed) != baseline, "亲近应影响性格说明");
                changed = low.Clone(); changed.Spice = 100;
                Require(Personality.Describe(changed) != baseline, "傲娇应影响性格说明");
            });

            CheckCase(results, "温柔和傲娇角色的摸头反应不同", delegate
            {
                PetProfile gentle = PetProfile.Default(); gentle.Spice = 0;
                PetProfile spicy = gentle.Clone(); spicy.Spice = 100;
                HashSet<string> gentleLines = Responses(gentle, "pet");
                HashSet<string> spicyLines = Responses(spicy, "pet");
                Require(!gentleLines.SetEquals(spicyLines), "傲娇设置必须实际影响摸头回应");
            });

            CheckCase(results, "活泼和沉稳角色的拖动反应不同", delegate
            {
                PetProfile calm = PetProfile.Default(); calm.Energy = 0;
                PetProfile active = calm.Clone(); active.Energy = 100;
                Require(!Responses(calm, "drag").SetEquals(Responses(active, "drag")),
                    "只改变活力也必须影响拖动回应");
            });

            CheckCase(results, "自定义口头禅进入专注陪伴台词", delegate
            {
                PetProfile p = PetProfile.Default(); p.Motto = "小步前进，也算前进。";
                bool used = false;
                foreach (string line in Responses(p, "focus"))
                    if (line.IndexOf(p.Motto, StringComparison.Ordinal) >= 0) used = true;
                Require(used, "专注回应应包含用户写下的口头禅");
            });

            CheckCase(results, "安静设置不改变角色性格", delegate
            {
                PetProfile normal = PetProfile.Default();
                PetProfile quiet = normal.Clone(); quiet.Quiet = true;
                Require(Personality.Describe(normal) == Personality.Describe(quiet), "安静不应改写性格");
                foreach (string situation in new[] { "pet", "drag", "focus", "idle", "hello" })
                    Require(Responses(normal, situation).SetEquals(Responses(quiet, situation)),
                        "安静模式不应改写 " + situation + " 台词");
            });

            CheckCase(results, "回应支持缺省输入且不修改原资料", delegate
            {
                Require(!String.IsNullOrWhiteSpace(Personality.Respond(null, null)), "空资料也应可回应");
                PetProfile p = PetProfile.Default(); p.Energy = -20; p.Name = "  小猫  ";
                Require(!String.IsNullOrWhiteSpace(Personality.Respond(p, "未知情境")), "未知情境应安全回退");
                Require(p.Energy == -20 && p.Name == "  小猫  ", "生成台词不应修改输入资料");
            });

            string scratch = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "self-test-inputs-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(scratch);
            string sourcePng = Path.Combine(scratch, "真实角色.png");
            WritePng(sourcePng, 32, 32);
            string importedImage = null;

            CheckCase(results, "真实 PNG 可以导入并独立保存", delegate
            {
                importedImage = ProfileStore.ImportImage(sourcePng);
                Require(Path.IsPathRooted(importedImage) && File.Exists(importedImage), "导入应返回可读取的绝对路径");
                Require(Path.GetFullPath(importedImage).StartsWith(
                    Path.GetFullPath(Path.Combine(ProfileStore.BaseDirectory, "assets")) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase), "图片应复制到专用素材目录");
                Require(!String.Equals(Path.GetFullPath(sourcePng), Path.GetFullPath(importedImage),
                    StringComparison.OrdinalIgnoreCase), "导入应保存副本");
                File.Delete(sourcePng);
                using (FileStream stream = File.OpenRead(importedImage))
                {
                    BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    Require(decoder.Frames[0].PixelWidth == 32 && decoder.Frames[0].PixelHeight == 32,
                        "源图移走后保存的图片仍应可解码");
                }
            });

            CheckCase(results, "伪造图片与超限文件会被拒绝", delegate
            {
                int before = AssetCount();
                string fake = Path.Combine(scratch, "伪造.png");
                File.WriteAllText(fake, "这是一段文字，不是图片。", Encoding.UTF8);
                ExpectInvalidImage(fake);
                string large = Path.Combine(scratch, "超限.png");
                using (FileStream stream = new FileStream(large, FileMode.CreateNew))
                    stream.SetLength(10L * 1024L * 1024L + 1);
                ExpectInvalidImage(large);
                string wrongExtension = Path.Combine(scratch, "文字.txt");
                File.WriteAllText(wrongExtension, "text", Encoding.UTF8);
                ExpectInvalidImage(wrongExtension);
                Require(AssetCount() == before, "拒绝的文件不应留下导入素材");
            });

            CheckCase(results, "超过尺寸限制的有效图片会被拒绝", delegate
            {
                int before = AssetCount();
                string tooWide = Path.Combine(scratch, "过宽.png");
                WritePng(tooWide, 4097, 1);
                ExpectInvalidImage(tooWide);
                Require(AssetCount() == before, "超尺寸图片不应留下导入素材");
            });

            CheckCase(results, "完整角色存档可以准确读回", delegate
            {
                PetProfile saved = PetProfile.Default();
                saved.Name = "测试伙伴"; saved.Motto = "认真生活，慢慢来。";
                saved.Kind = PetKind.Image; saved.ImagePath = importedImage;
                saved.Energy = 13.5; saved.Affinity = 87.25; saved.Spice = 92;
                saved.Size = 1.35; saved.Quiet = true;
                ProfileStore.Save(saved);
                Require(SameProfile(saved, ProfileStore.Load()), "名字、外观、图片、性格和安静设置应全部保留");
                saved.Name = "第二次保存";
                ProfileStore.Save(saved);
                Require(SameProfile(saved, ProfileStore.Load()), "覆盖保存后也应准确读回");
            });

            CheckCase(results, "保存会修正设置且不修改原角色", delegate
            {
                PetProfile original = PetProfile.Default();
                original.Energy = 200; original.Size = -1; original.Name = "  保存测试  ";
                ProfileStore.Save(original);
                PetProfile loaded = ProfileStore.Load();
                Require(loaded.Energy == 100 && loaded.Size == 0.6 && loaded.Name == "保存测试",
                    "保存的数据应经过规范化");
                Require(original.Energy == 200 && original.Size == -1 && original.Name == "  保存测试  ",
                    "保存过程不应改变正在编辑的原角色");
            });

            CheckCase(results, "损坏存档保留原文件并恢复可用默认角色", delegate
            {
                string profilePath = Path.Combine(ProfileStore.BaseDirectory, "profile.json");
                const string broken = "{ broken json: 原文件必须保留";
                File.WriteAllText(profilePath, broken, Encoding.UTF8);
                Require(SameProfile(PetProfile.Default(), ProfileStore.Load()), "损坏存档应回到默认角色");
                string[] preserved = Directory.GetFiles(ProfileStore.BaseDirectory, "profile.corrupt-*.json");
                Require(preserved.Length == 1 && File.ReadAllText(preserved[0], Encoding.UTF8) == broken,
                    "损坏存档原文应完整保留");
                Require(File.Exists(profilePath) && SameProfile(PetProfile.Default(), ProfileStore.Load()),
                    "恢复后再次启动也应能正常读档");
            });

            results.Insert(0, "灵伴原型自检：" + results.Count + " 项通过，0 项失败。");
            results.Add("存档和图片测试均在本次程序的隔离目录中执行。");
            return String.Join(Environment.NewLine, results.ToArray());
        }

        private static void CheckCase(List<string> results, string label, Action action)
        {
            try { action(); }
            catch (Exception exception)
            {
                if (exception is OutOfMemoryException || exception is StackOverflowException
                    || exception is AccessViolationException) throw;
                throw new InvalidOperationException("测试失败：" + label + "。" + exception.Message, exception);
            }
            results.Add("通过：" + label);
        }

        private static void Require(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
        }

        private static bool InRange(double value, double minimum, double maximum)
        {
            return !Double.IsNaN(value) && !Double.IsInfinity(value) && value >= minimum && value <= maximum;
        }

        private static HashSet<string> Responses(PetProfile p, string situation)
        {
            HashSet<string> lines = new HashSet<string>(StringComparer.Ordinal);
            // Sample two complete rotations so previous interactive calls cannot bias the comparison.
            for (int i = 0; i < 6; i++)
            {
                string line = Personality.Respond(p, situation);
                Require(!String.IsNullOrWhiteSpace(line), "回应不能为空");
                lines.Add(line);
            }
            return lines;
        }

        private static bool SameProfile(PetProfile first, PetProfile second)
        {
            return first.Name == second.Name && first.Motto == second.Motto && first.ImagePath == second.ImagePath
                && first.Kind == second.Kind && first.Energy == second.Energy && first.Affinity == second.Affinity
                && first.Spice == second.Spice && first.Size == second.Size && first.Quiet == second.Quiet;
        }

        private static int AssetCount()
        {
            string assets = Path.Combine(ProfileStore.BaseDirectory, "assets");
            return Directory.Exists(assets) ? Directory.GetFiles(assets).Length : 0;
        }

        private static void ExpectInvalidImage(string path)
        {
            bool rejected = false;
            try { ProfileStore.ImportImage(path); }
            catch (InvalidDataException) { rejected = true; }
            catch (FileFormatException) { rejected = true; }
            catch (NotSupportedException) { rejected = true; }
            catch (ArgumentException) { rejected = true; }
            Require(rejected, "无效或超限图片必须被明确拒绝：" + Path.GetFileName(path));
        }

        private static void WritePng(string path, int width, int height)
        {
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext context = visual.RenderOpen())
            {
                context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));
                context.DrawRoundedRectangle(Brushes.Coral, null, new Rect(0, 0, width, height), 5, 5);
            }
            RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                encoder.Save(stream);
        }
    }
}
