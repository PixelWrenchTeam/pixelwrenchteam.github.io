using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;


namespace PixelWrench
{
    public partial class MainWindow
    {
        private async void WipeBoard()
        {
            var result = await MessageBoxCustom.Show(this, "Do you want to clear the World?", "Clear Canvas", MessageBoxCustom.MessageBoxButtons.YesNo);
            if (result == MessageBoxCustom.MessageBoxResult.Yes)
            {
                worldGrid = new TileData[WORLD_WIDTH, WORLD_HEIGHT];
                for (int x = 0; x < WORLD_WIDTH; x++)
                    for (int y = 0; y < WORLD_HEIGHT; y++)
                        worldGrid[x, y] = new TileData();

                GenerateBedrock();
                RenderEntireGrid();
                SaveHistoryState();
            }
        }

        private void SaveHistoryState()
        {
            if (historyIndex < history.Count - 1)
                history.RemoveRange(historyIndex + 1, history.Count - historyIndex - 1);

            TileData[,] stateCopy = new TileData[WORLD_WIDTH, WORLD_HEIGHT];
            for (int x = 0; x < WORLD_WIDTH; x++)
            {
                for (int y = 0; y < WORLD_HEIGHT; y++)
                {
                    stateCopy[x, y] = new TileData 
                    { 
                        Background = worldGrid[x, y].Background, 
                        WallMount = worldGrid[x, y].WallMount,
                        Liquid = worldGrid[x, y].Liquid,
                        BlockOrProp = worldGrid[x, y].BlockOrProp
                    };
                }
            }
            
            history.Add(stateCopy);

            if (history.Count > 50) history.RemoveAt(0); 
            historyIndex = history.Count - 1;
        }

        private void Undo()
        {
            if (historyIndex > 0)
            {
                historyIndex--;
                RestoreGridState(history[historyIndex]);
            }
        }

        private void Redo()
        {
            if (historyIndex < history.Count - 1)
            {
                historyIndex++;
                RestoreGridState(history[historyIndex]);
            }
        }

        private void RestoreGridState(TileData[,] targetState)
        {
            for (int x = 0; x < WORLD_WIDTH; x++)
            {
                for (int y = 0; y < WORLD_HEIGHT; y++)
                {
                    worldGrid[x, y].Background = targetState[x, y].Background;
                    worldGrid[x, y].WallMount = targetState[x, y].WallMount;
                    worldGrid[x, y].Liquid = targetState[x, y].Liquid;
                    worldGrid[x, y].BlockOrProp = targetState[x, y].BlockOrProp;
                }
            }
            RenderEntireGrid();
        }

        private async void ExportToPNG()
        {
            string watermarkPath = Path.Combine(ASSET_PATH, "interface", "watermark", "PixelWrenchWatermark.png");
            if (!File.Exists(watermarkPath))
            {
                await ShowMessageBox("Screenshot failed: Watermark is missing.", "Export Error");
                return; 
            }

            int pxWidth = WORLD_WIDTH * TILE_SIZE;
            int pxHeight = WORLD_HEIGHT * TILE_SIZE;

            var rtb = new RenderTargetBitmap(new PixelSize(pxWidth, pxHeight), new Vector(96, 96));
            
            using (var dc = rtb.CreateDrawingContext())
            {
                Color voidColor = Color.Parse("#0d1117");
                if (currentWorldBackground == "Custom Color" && Color.TryParse(customBackgroundColor, out Color customCol))
                {
                    voidColor = customCol;
                }

                dc.DrawRectangle(new SolidColorBrush(voidColor), null, new Rect(0, 0, pxWidth, pxHeight));
                
                if (currentWorldBackground != "Custom Color" && !string.IsNullOrEmpty(currentWorldBackground) && ItemLoader.Sprites.TryGetValue(currentWorldBackground, out ItemData wBg))
                {
                    var imgSource = (wBg.Layers != null && wBg.Layers.Count > 0) ? wBg.Layers[0] : wBg.Image;
                    dc.DrawImage(imgSource, new Rect(0, 0, pxWidth, pxHeight));
                }

                for (int y = 0; y < WORLD_HEIGHT; y++)
                    for (int x = 0; x < WORLD_WIDTH; x++)
                        if (!string.IsNullOrEmpty(worldGrid[x, y].Background) && ItemLoader.Sprites.TryGetValue(worldGrid[x, y].Background, out ItemData bg))
                            dc.DrawImage(bg.Image, GetSpriteRect(bg, x * TILE_SIZE, y * TILE_SIZE));

                for (int y = 0; y < WORLD_HEIGHT; y++)
                    for (int x = 0; x < WORLD_WIDTH; x++)
                        if (!string.IsNullOrEmpty(worldGrid[x, y].WallMount) && ItemLoader.Sprites.TryGetValue(worldGrid[x, y].WallMount, out ItemData wm))
                            dc.DrawImage(wm.Image, GetSpriteRect(wm, x * TILE_SIZE, y * TILE_SIZE));

                for (int y = 0; y < WORLD_HEIGHT; y++)
                    for (int x = 0; x < WORLD_WIDTH; x++)
                        if (!string.IsNullOrEmpty(worldGrid[x, y].Liquid) && ItemLoader.Sprites.TryGetValue(worldGrid[x, y].Liquid, out ItemData lq))
                            dc.DrawImage(lq.Image, GetSpriteRect(lq, x * TILE_SIZE, y * TILE_SIZE));

                for (int y = 0; y < WORLD_HEIGHT; y++)
                    for (int x = 0; x < WORLD_WIDTH; x++)
                        if (!string.IsNullOrEmpty(worldGrid[x, y].BlockOrProp) && ItemLoader.Sprites.TryGetValue(worldGrid[x, y].BlockOrProp, out ItemData bp))
                            dc.DrawImage(bp.Image, GetSpriteRect(bp, x * TILE_SIZE, y * TILE_SIZE));

                DrawWatermark(dc, pxWidth, pxHeight, watermarkPath);
            }

            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PixelWrench", "Screenshots");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, $"World_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            
            rtb.Save(file);
            var result = await MessageBoxCustom.Show(this, $"Screenshot saved successfully to:\n{file}", "Export Complete", MessageBoxCustom.MessageBoxButtons.OkOpenFolder);
            if (result == MessageBoxCustom.MessageBoxResult.OpenFolder)
            {

            }
        }
    }
}