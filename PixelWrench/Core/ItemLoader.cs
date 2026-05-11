using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;

namespace PixelWrench
{
    public class ItemData
    {
        public Bitmap Image { get; set; }
        public string Category { get; set; }
        public List<Bitmap> Layers { get; set; } 
        public bool IsVariant { get; set; }
    }

    public static class ItemLoader
    {
        public static Dictionary<string, ItemData> Sprites = new Dictionary<string, ItemData>(StringComparer.OrdinalIgnoreCase);

        private static void LogInfo(string message)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }

        public static void LoadAll()
        {
            Sprites.Clear();

            var mapUri = new Uri("avares://PixelWrench/wwwroot/sprites.txt");
            if (!Avalonia.Platform.AssetLoader.Exists(mapUri)) {
                LogInfo("WARNING: sprites.txt map not found!");
                return;
            }

            string[] files;
            using (var stream = Avalonia.Platform.AssetLoader.Open(mapUri))
            using (var reader = new System.IO.StreamReader(stream))
            {
                files = reader.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            }

            Dictionary<string, Bitmap> orbIcons = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);

            foreach (string line in files) {
                string file = line.Replace('\\', '/');
                if (file.ToLower().Contains("/orbicons") || file.ToLower().Contains("orbicons/")) {
                    string name = Path.GetFileNameWithoutExtension(file);
                    orbIcons[name] = new Bitmap(Avalonia.Platform.AssetLoader.Open(new Uri($"avares://PixelWrench/wwwroot/Sprites/{file}")));
                }
            }

            foreach (string line in files)
            {
                try
                {
                    string file = line.Replace('\\', '/');
                    string lowerFile = file.ToLower();
                    
                    if (lowerFile.Contains("/orbicons") || lowerFile.Contains("orbicons/") || lowerFile.Contains("/interface") || lowerFile.Contains("interface/")) continue;

                    string nameDefault = Path.GetFileNameWithoutExtension(file);
                    if (Sprites.ContainsKey(nameDefault)) continue;

                    string category = "Block";
                    bool isVariant = false;
                    Bitmap displayImg = null;
                    List<Bitmap> layers = null;

                    var uri = new Uri($"avares://PixelWrench/wwwroot/Sprites/{file}");

                    if (lowerFile.Contains("/backgrounds") || lowerFile.Contains("backgrounds/")) category = "Background";
                    else if (lowerFile.Contains("/graffitis") || lowerFile.Contains("graffitis/")) { category = "Graffiti"; if (!lowerFile.Contains("full")) isVariant = true; }
                    else if (lowerFile.Contains("/fossils") || lowerFile.Contains("fossils/")) { category = "Fossil"; if (!lowerFile.Contains("full")) isVariant = true; }
                    else if (lowerFile.Contains("/props") || lowerFile.Contains("props/")) category = "Prop";
                    else if (lowerFile.Contains("/worldbackgrounds") || lowerFile.Contains("worldbackgrounds/")) 
                    {
                        category = "WorldBackground";
                        layers = new List<Bitmap> { new Bitmap(Avalonia.Platform.AssetLoader.Open(uri)) }; 
                        string orbKey = nameDefault + " Orb";
                        if (nameDefault.Equals("Cemetery", StringComparison.OrdinalIgnoreCase)) orbKey = "Cemetary Orb"; 
                        
                        if (orbIcons.ContainsKey(orbKey)) displayImg = orbIcons[orbKey];
                        else displayImg = layers[0]; 
                    }
                    else if (lowerFile.Contains("water") || lowerFile.Contains("lava") || lowerFile.Contains("oil") || lowerFile.Contains("naphtha") || lowerFile.Contains("jelly")) 
                    {
                        category = "Liquid"; 
                    }

                    Sprites[nameDefault] = new ItemData { 
                        Image = displayImg ?? new Bitmap(Avalonia.Platform.AssetLoader.Open(uri)), 
                        Category = category,
                        IsVariant = isVariant,
                        Layers = layers
                    };
                }
                catch (Exception ex)
                {
                    LogInfo($"ERROR: Failed to load sprite '{line}': {ex.Message}");
                }
            }

            if (orbIcons.ContainsKey("Custom Orb")) {
                Sprites["Custom Color"] = new ItemData { Image = orbIcons["Custom Orb"], Category = "WorldBackground", IsVariant = false };
            }

            LogInfo($"SUCCESS: Loaded {Sprites.Count} textures via Map.");
        }
    }
}