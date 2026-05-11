using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace PixelWrench
{
    public class WorldSaveData
    {
        public string WorldBackground { get; set; }
        
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string CustomBackgroundColor { get; set; }
        
        public List<TileSaveData> Tiles { get; set; } = new List<TileSaveData>();
    }

    public class TileSaveData
    {
        public int X { get; set; }
        public int Y { get; set; }
        
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Background { get; set; }
        
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string WallMount { get; set; }
        
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Liquid { get; set; }
        
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string BlockOrProp { get; set; }
    }

    public partial class MainWindow
    {
        private async void Save_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var saveData = new WorldSaveData { 
                WorldBackground = currentWorldBackground,
                CustomBackgroundColor = customBackgroundColor
            };

            for (int x = 0; x < WORLD_WIDTH; x++)
            {
                for (int y = 0; y < WORLD_HEIGHT - 3; y++)
                {
                    var tile = worldGrid[x, y];
                    if (!tile.IsEmpty)
                    {
                        saveData.Tiles.Add(new TileSaveData
                        {
                            X = x,
                            Y = y,
                            Background = tile.Background,
                            WallMount = tile.WallMount,
                            Liquid = tile.Liquid,
                            BlockOrProp = tile.BlockOrProp
                        });
                    }
                }
            }

            var tl = Avalonia.Controls.TopLevel.GetTopLevel(this);
            var file = await tl.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save PixelWrench Demo World",
                DefaultExtension = "json",
                FileTypeChoices = new[] { new FilePickerFileType("PixelWrench World") { Patterns = new[] { "*.json" } } }
            });

            if (file != null)
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(saveData, options);
                json += "\n\n--- Make sure to Join the Discord for the full experience! https://discord.com/invite/96whSKbGqJ ---";
                
                await using var stream = await file.OpenWriteAsync();
                using var writer = new StreamWriter(stream);
                await writer.WriteAsync(json);
                
                await ShowMessageBox("World saved successfully!", "Save");
            }
        }

        private async void Load_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var tlLoad = Avalonia.Controls.TopLevel.GetTopLevel(this);
            var files = await tlLoad.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Load PixelWrench Demo World",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("PixelWrench World") { Patterns = new[] { "*.json" } } }
            });

            if (files != null && files.Count > 0)
            {
                string name = files[0].Name;
                if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 5);
                currentLoadedWorldName = name;

                await using var stream = await files[0].OpenReadAsync();
                using var reader = new StreamReader(stream);
                string rawJson = await reader.ReadToEndAsync();
                
                LoadWorldJson(rawJson);
            }
        }

        private async void LoadWorldJson(string rawJson)
        {
            try
            {
                int discordLinkIndex = rawJson.IndexOf("\n\n--- Make sure");
                string cleanJson = discordLinkIndex >= 0 ? rawJson.Substring(0, discordLinkIndex) : rawJson;

                var saveData = JsonSerializer.Deserialize<WorldSaveData>(cleanJson);

                worldGrid = new TileData[WORLD_WIDTH, WORLD_HEIGHT];
                for (int x = 0; x < WORLD_WIDTH; x++)
                    for (int y = 0; y < WORLD_HEIGHT; y++)
                        worldGrid[x, y] = new TileData();

                GenerateBedrock();

                if (!string.IsNullOrEmpty(saveData.CustomBackgroundColor))
                {
                    customBackgroundColor = saveData.CustomBackgroundColor;
                    if (CustomColorInput != null) CustomColorInput.Text = customBackgroundColor;
                }

                if (!string.IsNullOrEmpty(saveData.WorldBackground))
                {
                    currentWorldBackground = saveData.WorldBackground;
                    StatusItem.Text = "World: " + currentWorldBackground;
                    RenderWorldBackground();
                }

                foreach (var t in saveData.Tiles)
                {
                    if (t.X >= 0 && t.X < WORLD_WIDTH && t.Y >= 0 && t.Y < WORLD_HEIGHT)
                    {
                        worldGrid[t.X, t.Y].Background = t.Background;
                        worldGrid[t.X, t.Y].WallMount = t.WallMount;
                        worldGrid[t.X, t.Y].Liquid = t.Liquid;
                        worldGrid[t.X, t.Y].BlockOrProp = t.BlockOrProp;
                    }
                }

                RenderEntireGrid(); 
                SaveHistoryState();
            }
            catch (Exception ex)
            {
                await ShowMessageBox("Failed to load world: " + ex.Message, "Error");
            }
        }
    
	public void InitializeNewWorld(int w = 0, int h = 0)
        {
            worldGrid = new TileData[WORLD_WIDTH, WORLD_HEIGHT];
            for (int x = 0; x < WORLD_WIDTH; x++)
            {
                for (int y = 0; y < WORLD_HEIGHT; y++)
                {
                    worldGrid[x, y] = new TileData();
                }
            }
            GenerateBedrock();
            currentWorldBackground = "Night"; 
            RenderEntireGrid();
            SaveHistoryState();
        }
	
	}
}