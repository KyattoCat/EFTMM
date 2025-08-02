using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using SharpCompress.Archives;
using System.Windows.Forms;
using MessageBox = System.Windows.Forms.MessageBox;

namespace EFTModManager
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<ModItem> ModItems { get; } = new ObservableCollection<ModItem>();

        private Config _originalConfig;
        private bool _configModified = false;

        public MainWindow()
        {
            InitializeComponent();
            ModsListView.ItemsSource = ModItems;
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            GamePathTextBox.TextChanged += (sender, e) => _configModified = true;
            LoadConfig();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 保存原始配置，以便在关闭时进行比较
            _originalConfig = new Config
            {
                GamePath = GamePathTextBox.Text,
                Mods = new List<ModConfig>()
            };

            foreach (var modItem in ModItems)
            {
                _originalConfig.Mods.Add(new ModConfig
                {
                    Name = modItem.Name,
                    FilePath = modItem.FilePath,
                    Type = modItem.Type,
                    IsEnabled = modItem.IsEnabled
                });
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 比较当前配置和原始配置
            if (_configModified && !IsConfigSame(_originalConfig))
            {
                var result = MessageBox.Show("您有未保存的更改，是否保存？", "提示", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (result == System.Windows.Forms.DialogResult.Yes)
                {
                    SaveButton_Click(sender, new RoutedEventArgs());
                }
                else if (result == System.Windows.Forms.DialogResult.Cancel)
                {
                    e.Cancel = true; // 取消关闭窗口
                }
            }
        }

        private bool IsConfigSame(Config originalConfig)
        {
            // 检查游戏路径是否相同
            if (originalConfig.GamePath != GamePathTextBox.Text)
                return false;

            // 检查模组列表是否相同
            if (originalConfig.Mods.Count != ModItems.Count)
                return false;

            for (int i = 0; i < originalConfig.Mods.Count; i++)
            {
                var originalMod = originalConfig.Mods[i];
                var currentMod = ModItems[i];

                if (originalMod.Name != currentMod.Name ||
                    originalMod.FilePath != currentMod.FilePath ||
                    originalMod.Type != currentMod.Type ||
                    originalMod.IsEnabled != currentMod.IsEnabled)
                    return false;
            }

            return true;
        }

        private void LoadConfig()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = Newtonsoft.Json.JsonConvert.DeserializeObject<Config>(json);

                    GamePathTextBox.Text = config.GamePath;

                    ModItems.Clear();
                    foreach (var modConfig in config.Mods)
                    {
                        ModItems.Add(new ModItem
                        {
                            Name = modConfig.Name,
                            FilePath = modConfig.FilePath,
                            Type = modConfig.Type,
                            IsEnabled = modConfig.IsEnabled
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载配置时出错: {ex.Message}");
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog
            {
                ShowNewFolderButton = true,
                SelectedPath = GamePathTextBox.Text
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                GamePathTextBox.Text = dialog.SelectedPath;
                _configModified = true;
            }

        }

        private void AddModButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "压缩文件|*.zip;*.rar;*.7z|所有文件|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                foreach (var filename in dialog.FileNames)
                {
                    var modType = DetectModType(filename);
                    ModItems.Add(new ModItem
                    {
                        Name = Path.GetFileNameWithoutExtension(filename),
                        FilePath = filename,
                        Type = modType,
                        IsEnabled = false
                    });
                }
                _configModified = true;
            }
        }

        private string DetectModType(string filePath)
        {
            try
            {
                using var archive = SharpCompress.Archives.ArchiveFactory.Open(filePath);
                bool hasBepInEx = false;
                bool hasUser = false;
                bool hasDll = false;

                foreach (var entry in archive.Entries)
                {
                    if (!entry.IsDirectory)
                    {
                        var path = entry.Key.Replace('\\', '/').ToLower();
                        if (path.StartsWith("bepinex/") || path.Contains("/bepinex/"))
                        {
                            hasBepInEx = true;
                        }
                        else if (path.StartsWith("user/") || path.Contains("/user/"))
                        {
                            hasUser = true;
                        }
                        else if (path.EndsWith(".dll"))
                        {
                            hasDll = true;
                        }
                    }
                }

                if (hasBepInEx && hasUser) return "组合模组";
                if (hasBepInEx) return "客户端模组";
                if (hasUser) return "服务器模组";
                if (hasDll) return "插件模组";
                return "未知类型";
            }
            catch
            {
                return "无法识别";
            }
        }

        private void RemoveModButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = ModsListView.SelectedItems;
            if (selectedItems.Count == 0) return;
            for (int i = selectedItems.Count - 1; i >= 0; i--)
            {
                ModItems.Remove((ModItem)selectedItems[i]);
            }
            _configModified = true;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(GamePathTextBox.Text))
            {
                MessageBox.Show("请先选择游戏目录", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var config = new Config
            {
                GamePath = GamePathTextBox.Text,
                Mods = new List<ModConfig>()
            };

            foreach (var modItem in ModItems)
            {
                config.Mods.Add(new ModConfig
                {
                    Name = modItem.Name,
                    FilePath = modItem.FilePath,
                    Type = modItem.Type,
                    IsEnabled = modItem.IsEnabled
                });
            }

            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(configPath, json);
                
                // 应用模组启用/禁用状态
                ApplyMods(config);
                _configModified = false;
                MessageBox.Show("配置已保存并应用", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyMods(Config config)
        {
            foreach (var mod in config.Mods)
            {
                try
                {
                    if (mod.IsEnabled)
                    {
                        InstallMod(config.GamePath, mod);
                    }
                    else
                    {
                        UninstallMod(config.GamePath, mod);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"处理模组 {mod.Name} 时出错: {ex.Message}");
                }
            }
        }

        private void InstallMod(string gamePath, ModConfig mod)
        {
            using var archive = SharpCompress.Archives.ArchiveFactory.Open(mod.FilePath);
            
            foreach (var entry in archive.Entries)
            {
                if (entry.IsDirectory) continue;

                var entryPath = entry.Key.Replace('\\', '/');
                string targetPath = GetTargetPath(gamePath, mod.Type, entryPath);

                if (string.IsNullOrEmpty(targetPath)) continue;

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                using var fileStream = File.Create(targetPath);
                entry.WriteTo(fileStream);
            }
        }

        private void UninstallMod(string gamePath, ModConfig mod)
        {
            using var archive = SharpCompress.Archives.ArchiveFactory.Open(mod.FilePath);
            
            foreach (var entry in archive.Entries)
            {
                if (entry.IsDirectory) continue;

                var entryPath = entry.Key.Replace("\\", "/");
                string targetPath = GetTargetPath(gamePath, mod.Type, entryPath);

                if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath)) continue;

                try
                {
                    File.Delete(targetPath);
                    // 删除空文件夹
                    DeleteEmptyParentDirectories(Path.GetDirectoryName(targetPath), gamePath);
                }
                catch
                {
                    // 忽略删除失败的文件
                }
            }
        }

        // 递归删除空文件夹，直到到达根目录（gamePath）
        private void DeleteEmptyParentDirectories(string directory, string root)
        {
            if (string.IsNullOrEmpty(directory) || string.Equals(directory, root, StringComparison.OrdinalIgnoreCase))
                return;
            if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length == 0)
            {
                try
                {
                    Directory.Delete(directory);
                }
                catch { }
                DeleteEmptyParentDirectories(Path.GetDirectoryName(directory), root);
            }
        }

        private string GetTargetPath(string gamePath, string modType, string entryPath)
        {
            string targetSubPath = modType switch
            {
                "客户端模组" => entryPath.StartsWith("BepInEx/", StringComparison.OrdinalIgnoreCase) 
                    ? entryPath 
                    : Path.Combine("BepInEx", "plugins", entryPath),
                "服务器模组" => entryPath.StartsWith("user/", StringComparison.OrdinalIgnoreCase)
                    ? entryPath
                    : Path.Combine("user", "mods", entryPath),
                "插件模组" => Path.Combine("BepInEx", "plugins", Path.GetFileName(entryPath)),
                "组合模组" => entryPath.StartsWith("BepInEx/", StringComparison.OrdinalIgnoreCase)
                    ? entryPath
                    : entryPath.StartsWith("user/", StringComparison.OrdinalIgnoreCase)
                        ? entryPath
                        : null,
                _ => null
            };

            return targetSubPath != null ? Path.Combine(gamePath, targetSubPath) : null;
        }

        public class Config
        {
            public string GamePath { get; set; }
            public List<ModConfig> Mods { get; set; }
        }

        public class ModConfig
        {
            public string Name { get; set; }
            public string FilePath { get; set; }
            public string Type { get; set; }
            public bool IsEnabled { get; set; }
        }
    }

    public class ModItem
    {
        public string Name { get; set; }
        public string FilePath { get; set; }
        public string Type { get; set; }
        public bool IsEnabled { get; set; }
    }
}
