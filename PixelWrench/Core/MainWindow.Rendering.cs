using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace PixelWrench
{
    public partial class MainWindow
    {
        private readonly Dictionary<string, string[]> customHitboxes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "t-rex", new[] {
                "11100", 
                "11111", 
                "00100"  
            }},
            { "dragon", new[] {
                "1111",  
                "0111"   
            }},
            { "winged humanoid", new[] {
                "111",   
                "010"    
            }},
            { "pikamon", new[] {
                "11",    
                "11"
            }}
        };

        private void InitializeEngine()
        {
            ItemLoader.LoadAll();

            worldGrid = new TileData[WORLD_WIDTH, WORLD_HEIGHT];
            for (int x = 0; x < WORLD_WIDTH; x++)
                for (int y = 0; y < WORLD_HEIGHT; y++)
                    worldGrid[x, y] = new TileData(); 

            bgChunks = new ChunkHost[CHUNKS_X, CHUNKS_Y]; 
            wmChunks = new ChunkHost[CHUNKS_X, CHUNKS_Y]; 
            liquidChunks = new ChunkHost[CHUNKS_X, CHUNKS_Y]; 
            blockChunks = new ChunkHost[CHUNKS_X, CHUNKS_Y]; 

            for (int cy = 0; cy < CHUNKS_Y; cy++)
            {
                for (int cx = 0; cx < CHUNKS_X; cx++)
                {
                    bgChunks[cx, cy] = CreateChunkHost(cx, cy, 1);
                    wmChunks[cx, cy] = CreateChunkHost(cx, cy, 2);
                    liquidChunks[cx, cy] = CreateChunkHost(cx, cy, 3);
                    blockChunks[cx, cy] = CreateChunkHost(cx, cy, 4);
                }
            }

            GenerateBedrock();
            SaveHistoryState(); 
            RenderEntireGrid(); 
            PopulateLibrary("");

            if (ItemLoader.Sprites.ContainsKey("Forest"))
            {
                currentWorldBackground = "Forest";
                StatusItem.Text = "World: Forest";
            }
            RenderWorldBackground();

            if (ItemLoader.Sprites.Count > 0)
            {
                selectedItem = ItemLoader.Sprites.Keys.First();
                if (selectedItem == "Custom Color") selectedItem = "Bedrock";
                StatusItem.Text = "Selected: " + selectedItem;
            }

            UpdateCursorAppearance();
        }

        private ChunkHost CreateChunkHost(int cx, int cy, int zIndex)
        {
            ChunkHost host = new ChunkHost { Width = CHUNK_SIZE * TILE_SIZE, Height = CHUNK_SIZE * TILE_SIZE, IsHitTestVisible = false };
            Canvas.SetLeft(host, cx * CHUNK_SIZE * TILE_SIZE);
            Canvas.SetTop(host, cy * CHUNK_SIZE * TILE_SIZE);
            
            host.ZIndex = zIndex; 
            
            WorldContainer.Children.Add(host);
            return host;
        }

        private void GenerateBedrock()
        {
            for (int x = 0; x < WORLD_WIDTH; x++)
            {
                worldGrid[x, WORLD_HEIGHT - 3].BlockOrProp = "bedrock flat"; 
                worldGrid[x, WORLD_HEIGHT - 2].BlockOrProp = "bedrock";     
                worldGrid[x, WORLD_HEIGHT - 1].BlockOrProp = "bedrock lava"; 
            }
        }

        private void RenderEntireGrid()
        {
            for (int cx = 0; cx < CHUNKS_X; cx++)
                for (int cy = 0; cy < CHUNKS_Y; cy++)
                    RenderChunk(cx, cy);
        }

        private string GetSmartSprite(int x, int y, string rawItem, string layer)
        {
            if (string.IsNullOrEmpty(rawItem)) return rawItem;

            bool isTopExposed = true;
            if (y > 0)
            {
                TileData tileAbove = worldGrid[x, y - 1];
                string itemAbove = "";
                if (layer == "BlockOrProp") itemAbove = tileAbove.BlockOrProp;
                else if (layer == "WallMount") itemAbove = tileAbove.WallMount;
                else if (layer == "Liquid") itemAbove = tileAbove.Liquid;
                else if (layer == "Background") itemAbove = tileAbove.Background;

                if (!string.IsNullOrEmpty(itemAbove) && 
                    (itemAbove.Equals(rawItem, StringComparison.OrdinalIgnoreCase) || 
                     itemAbove.StartsWith(rawItem + " ", StringComparison.OrdinalIgnoreCase) ||
                     itemAbove.StartsWith(rawItem + "_", StringComparison.OrdinalIgnoreCase)))
                {
                    isTopExposed = false;
                }
            }

            if (isTopExposed)
            {
                string top1 = rawItem + " Top";
                if (ItemLoader.Sprites.ContainsKey(top1)) return top1;

                string top2 = rawItem + "_Top";
                if (ItemLoader.Sprites.ContainsKey(top2)) return top2;
            }
            else
            {
                string mid1 = rawItem + " Mid";
                if (ItemLoader.Sprites.ContainsKey(mid1)) return mid1;
                
                string mid2 = rawItem + "_Mid";
                if (ItemLoader.Sprites.ContainsKey(mid2)) return mid2;
            }

            return rawItem; 
        }

        private void RenderChunk(int cx, int cy)
        {
            int startX = cx * CHUNK_SIZE;
            int startY = cy * CHUNK_SIZE;

            bgChunks[cx, cy].DrawAction = (dc) => {
                for (int y = startY; y < startY + CHUNK_SIZE; y++)
                    for (int x = startX; x < startX + CHUNK_SIZE; x++) {
                        string bgRaw = worldGrid[x, y].Background;
                        if (!string.IsNullOrEmpty(bgRaw)) {
                            string smartBg = GetSmartSprite(x, y, bgRaw, "Background");
                            if (ItemLoader.Sprites.TryGetValue(smartBg, out ItemData bg))
                                dc.DrawImage(bg.Image, GetSpriteRect(bg, (x % CHUNK_SIZE) * TILE_SIZE, (y % CHUNK_SIZE) * TILE_SIZE));
                        }
                    }
            };
            bgChunks[cx, cy].Redraw();

            wmChunks[cx, cy].DrawAction = (dc) => {
                for (int y = startY; y < startY + CHUNK_SIZE; y++)
                    for (int x = startX; x < startX + CHUNK_SIZE; x++) {
                        string wmRaw = worldGrid[x, y].WallMount;
                        if (!string.IsNullOrEmpty(wmRaw)) {
                            string smartWm = GetSmartSprite(x, y, wmRaw, "WallMount");
                            if (ItemLoader.Sprites.TryGetValue(smartWm, out ItemData wm))
                                dc.DrawImage(wm.Image, GetSpriteRect(wm, (x % CHUNK_SIZE) * TILE_SIZE, (y % CHUNK_SIZE) * TILE_SIZE));
                        }
                    }
            };
            wmChunks[cx, cy].Redraw();

            liquidChunks[cx, cy].DrawAction = (dc) => {
                for (int y = startY; y < startY + CHUNK_SIZE; y++)
                    for (int x = startX; x < startX + CHUNK_SIZE; x++) {
                        string lqRaw = worldGrid[x, y].Liquid;
                        if (!string.IsNullOrEmpty(lqRaw)) {
                            string smartLq = GetSmartSprite(x, y, lqRaw, "Liquid");
                            if (ItemLoader.Sprites.TryGetValue(smartLq, out ItemData lq))
                                dc.DrawImage(lq.Image, GetSpriteRect(lq, (x % CHUNK_SIZE) * TILE_SIZE, (y % CHUNK_SIZE) * TILE_SIZE));
                        }
                    }
            };
            liquidChunks[cx, cy].Redraw();

            blockChunks[cx, cy].DrawAction = (dc) => {
                for (int y = startY; y < startY + CHUNK_SIZE; y++)
                    for (int x = startX; x < startX + CHUNK_SIZE; x++) {
                        string bpRaw = worldGrid[x, y].BlockOrProp;
                        if (!string.IsNullOrEmpty(bpRaw)) {
                            string smartBp = GetSmartSprite(x, y, bpRaw, "BlockOrProp");
                            if (ItemLoader.Sprites.TryGetValue(smartBp, out ItemData bp))
                                dc.DrawImage(bp.Image, GetSpriteRect(bp, (x % CHUNK_SIZE) * TILE_SIZE, (y % CHUNK_SIZE) * TILE_SIZE));
                        }
                    }
            };
            blockChunks[cx, cy].Redraw();
        }

        private Rect GetSpriteRect(ItemData item, double baseX, double baseY)
        {
            double w = item.Image.PixelSize.Width;
            double h = item.Image.PixelSize.Height;
            double drawX = baseX;
            double drawY = baseY;

            bool isGridSnapped = item.Category == "Graffiti" || item.Category == "Fossil" || 
                                 (w % TILE_SIZE == 0 && h % TILE_SIZE == 0 && w > TILE_SIZE);

            if (isGridSnapped) {
                int tilesWide = Math.Max(1, (int)Math.Ceiling(w / (double)TILE_SIZE));
                int tilesHigh = Math.Max(1, (int)Math.Ceiling(h / (double)TILE_SIZE));
                
                int anchorOffsetX = (tilesWide - 1) / 2;
                int anchorOffsetY = tilesHigh - 1; 

                drawX = baseX - (anchorOffsetX * TILE_SIZE);
                drawY = Math.Floor((baseY - (anchorOffsetY * TILE_SIZE)) + ((tilesHigh * TILE_SIZE) - h));
            }
            else {
                drawX = Math.Round(baseX + ((TILE_SIZE - w) / 2.0));
                drawY = Math.Round(baseY + ((TILE_SIZE - h) / 2.0));
            }
            
            return new Rect(drawX, drawY, w, h);
        }

        private void RenderWorldBackground()
        {
            if (ParallaxContainer == null || ViewportCanvas == null) return;
            ParallaxContainer.Children.Clear();

            ViewportCanvas.Background = new SolidColorBrush(Color.Parse("#130f1c"));

            if (currentWorldBackground == "Custom Color")
            {
                if (Color.TryParse(customBackgroundColor, out Color parsedColor))
                {
                    var bgRect = new Avalonia.Controls.Shapes.Rectangle { Width = WORLD_WIDTH * TILE_SIZE, Height = WORLD_HEIGHT * TILE_SIZE, Fill = new SolidColorBrush(parsedColor) };
                    Canvas.SetLeft(bgRect, 0); Canvas.SetTop(bgRect, 0);
                    ParallaxContainer.Children.Add(bgRect);
                }
                return;
            }

            if (string.IsNullOrEmpty(currentWorldBackground) || !ItemLoader.Sprites.ContainsKey(currentWorldBackground)) return;

            var bgData = ItemLoader.Sprites[currentWorldBackground];
            if (bgData.Image == null) return;

            Image img = new Image();
            img.Source = (bgData.Layers != null && bgData.Layers.Count > 0) ? bgData.Layers[0] : bgData.Image;
            
            img.Width = WORLD_WIDTH * TILE_SIZE;
            img.Height = WORLD_HEIGHT * TILE_SIZE;
            img.Stretch = Stretch.Fill; 
            
            RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.HighQuality);

            Canvas.SetLeft(img, 0);
            Canvas.SetTop(img, 0);
            ParallaxContainer.Children.Add(img);
        }
    }
}