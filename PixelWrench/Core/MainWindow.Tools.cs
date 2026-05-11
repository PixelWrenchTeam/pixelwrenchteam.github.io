using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;

namespace PixelWrench
{
    public partial class MainWindow
    {
        private void SetActiveTool(string toolName)
        {
            activeTool = toolName;
    
            BtnBrush.Background = new SolidColorBrush(Color.Parse("#2e2640"));
            BtnErase.Background = new SolidColorBrush(Color.Parse("#2e2640"));
            BtnFill.Background = new SolidColorBrush(Color.Parse("#2e2640"));
            BtnPick.Background = new SolidColorBrush(Color.Parse("#2e2640"));
            
            if (activeTool == "brush") BtnBrush.Background = new SolidColorBrush(Color.Parse("#8b5cf6"));
            if (activeTool == "erase") BtnErase.Background = new SolidColorBrush(Color.Parse("#8b5cf6"));
            if (activeTool == "fill") BtnFill.Background = new SolidColorBrush(Color.Parse("#8b5cf6"));
            if (activeTool == "pick") BtnPick.Background = new SolidColorBrush(Color.Parse("#8b5cf6"));
            
            UpdateCursorAppearance();
        }

        private Point GetGridCoords(Point mousePos)
        {
            int gridX = (int)Math.Floor(mousePos.X / TILE_SIZE);
            int gridY = (int)Math.Floor(mousePos.Y / TILE_SIZE);
            return new Point(gridX, gridY);
        }

        private string GetLayerTarget(string item)
        {
            if (string.IsNullOrEmpty(item)) return "BlockOrProp";
            if (ItemLoader.Sprites.TryGetValue(item, out ItemData data))
            {
                if (data.Category == "Background") return "Background";
                if (data.Category == "Graffiti" || data.Category == "Fossil") return "WallMount"; 
                if (data.Category == "Liquid") return "Liquid";
                return "BlockOrProp"; 
            }
            return "BlockOrProp";
        }

        private List<Point> GetItemFootprint(string itemName, int originX, int originY)
        {
            List<Point> footprint = new List<Point>();
            if (string.IsNullOrEmpty(itemName) || !ItemLoader.Sprites.TryGetValue(itemName, out ItemData item)) {
                footprint.Add(new Point(originX, originY));
                return footprint;
            }

            bool isGridSnapped = item.Category == "Graffiti" || item.Category == "Fossil" || 
                                 (item.Image.PixelSize.Width % TILE_SIZE == 0 && item.Image.PixelSize.Height % TILE_SIZE == 0 && item.Image.PixelSize.Width > TILE_SIZE);

            if (!isGridSnapped) {
                footprint.Add(new Point(originX, originY));
                return footprint;
            }

            int tilesX = Math.Max(1, (int)Math.Ceiling(item.Image.PixelSize.Width / (double)TILE_SIZE));
            int tilesY = Math.Max(1, (int)Math.Ceiling(item.Image.PixelSize.Height / (double)TILE_SIZE));

            int anchorOffsetX = (tilesX - 1) / 2;
            int anchorOffsetY = tilesY - 1;

            string[] shapeMap = null;
            if (tilesX > 1 || tilesY > 1) { 
                foreach (var key in customHitboxes.Keys) {
                    if (itemName.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0) {
                        shapeMap = customHitboxes[key];
                        break;
                    }
                }
            }

            for (int y = 0; y < tilesY; y++) {
                for (int x = 0; x < tilesX; x++) {
                    bool isSolid = true;
                    if (shapeMap != null && y < shapeMap.Length && x < shapeMap[y].Length) {
                        isSolid = shapeMap[y][x] == '1';
                    }
                    if (isSolid) {
                        footprint.Add(new Point(originX - anchorOffsetX + x, originY - anchorOffsetY + y));
                    }
                }
            }
            return footprint;
        }

        private void MarkDirty(int px, int py)
        {
            if (px < 0 || px >= WORLD_WIDTH || py < 0 || py >= WORLD_HEIGHT) return;
            
            activeModifiedChunks.Add(new Point(px / CHUNK_SIZE, py / CHUNK_SIZE));
            
            if (py > 0) activeModifiedChunks.Add(new Point(px / CHUNK_SIZE, (py - 1) / CHUNK_SIZE));
            if (py < WORLD_HEIGHT - 1) activeModifiedChunks.Add(new Point(px / CHUNK_SIZE, (py + 1) / CHUNK_SIZE));
            if (px > 0) activeModifiedChunks.Add(new Point((px - 1) / CHUNK_SIZE, py / CHUNK_SIZE));
            if (px < WORLD_WIDTH - 1) activeModifiedChunks.Add(new Point((px + 1) / CHUNK_SIZE, py / CHUNK_SIZE));
        }

        private bool EraseTarget(int tx, int ty, string targetLayer)
        {
            bool erasedAnything = false;
            for (int sx = Math.Max(0, tx - 5); sx <= Math.Min(WORLD_WIDTH - 1, tx + 5); sx++) {
                for (int sy = Math.Max(0, ty - 5); sy <= Math.Min(WORLD_HEIGHT - 4, ty + 5); sy++) {
                    TileData t = worldGrid[sx, sy];
                    
                    if ((targetLayer == null || targetLayer == "Background") && !string.IsNullOrEmpty(t.Background)) {
                        var fp = GetItemFootprint(t.Background, sx, sy);
                        if (fp.Contains(new Point(tx, ty))) {
                            t.Background = null;
                            foreach (var p in fp) MarkDirty((int)p.X, (int)p.Y);
                            erasedAnything = true;
                        }
                    }
                    if ((targetLayer == null || targetLayer == "WallMount") && !string.IsNullOrEmpty(t.WallMount)) {
                        var fp = GetItemFootprint(t.WallMount, sx, sy);
                        if (fp.Contains(new Point(tx, ty))) {
                            t.WallMount = null;
                            foreach (var p in fp) MarkDirty((int)p.X, (int)p.Y);
                            erasedAnything = true;
                        }
                    }
                    if ((targetLayer == null || targetLayer == "Liquid") && !string.IsNullOrEmpty(t.Liquid)) {
                        var fp = GetItemFootprint(t.Liquid, sx, sy);
                        if (fp.Contains(new Point(tx, ty))) {
                            t.Liquid = null;
                            foreach (var p in fp) MarkDirty((int)p.X, (int)p.Y);
                            erasedAnything = true;
                        }
                    }
                    if ((targetLayer == null || targetLayer == "BlockOrProp") && !string.IsNullOrEmpty(t.BlockOrProp)) {
                        var fp = GetItemFootprint(t.BlockOrProp, sx, sy);
                        if (fp.Contains(new Point(tx, ty))) {
                            t.BlockOrProp = null;
                            foreach (var p in fp) MarkDirty((int)p.X, (int)p.Y);
                            erasedAnything = true;
                        }
                    }
                }
            }
            return erasedAnything;
        }

        private string GetItemAt(int tx, int ty)
        {
            for (int sx = Math.Max(0, tx - 5); sx <= Math.Min(WORLD_WIDTH - 1, tx + 5); sx++) {
                for (int sy = Math.Max(0, ty - 5); sy <= Math.Min(WORLD_HEIGHT - 4, ty + 5); sy++) {
                    TileData t = worldGrid[sx, sy];
                    
                    if (!string.IsNullOrEmpty(t.BlockOrProp) && GetItemFootprint(t.BlockOrProp, sx, sy).Contains(new Point(tx, ty))) return t.BlockOrProp;
                    if (!string.IsNullOrEmpty(t.Liquid) && GetItemFootprint(t.Liquid, sx, sy).Contains(new Point(tx, ty))) return t.Liquid;
                    if (!string.IsNullOrEmpty(t.WallMount) && GetItemFootprint(t.WallMount, sx, sy).Contains(new Point(tx, ty))) return t.WallMount;
                    if (!string.IsNullOrEmpty(t.Background) && GetItemFootprint(t.Background, sx, sy).Contains(new Point(tx, ty))) return t.Background;
                }
            }
            return null;
        }

        private void FloodFill(int startX, int startY, string fillItem, bool isRightClick)
        {
            string targetLayer = GetLayerTarget(fillItem);
            TileData startTile = worldGrid[startX, startY];
            
            string targetItem = targetLayer == "Background" ? startTile.Background : 
                                targetLayer == "WallMount" ? startTile.WallMount :
                                targetLayer == "Liquid" ? startTile.Liquid : startTile.BlockOrProp;

            if (!isRightClick && targetItem == fillItem) return;
            if (isRightClick && targetItem == null) return;

            Stack<Point> pixels = new Stack<Point>();
            pixels.Push(new Point(startX, startY));

            while (pixels.Count > 0)
            {
                Point a = pixels.Pop();
                int x = (int)a.X; int y = (int)a.Y;

                if (x < 0 || x >= WORLD_WIDTH || y < 0 || y >= WORLD_HEIGHT - 3) continue;

                TileData t = worldGrid[x, y];
                string currentItem = targetLayer == "Background" ? t.Background : 
                                     targetLayer == "WallMount" ? t.WallMount :
                                     targetLayer == "Liquid" ? t.Liquid : t.BlockOrProp;

                if (currentItem == targetItem)
                {
                    if (!isRightClick)
                    {
                        if (targetLayer == "Background") t.Background = fillItem;
                        else if (targetLayer == "WallMount") t.WallMount = fillItem;
                        else if (targetLayer == "Liquid") t.Liquid = fillItem;
                        else t.BlockOrProp = fillItem;
                    }
                    else
                    {
                        if (targetLayer == "Background") t.Background = null;
                        else if (targetLayer == "WallMount") t.WallMount = null;
                        else if (targetLayer == "Liquid") t.Liquid = null;
                        else t.BlockOrProp = null;
                    }

                    MarkDirty(x, y); 
                    pixels.Push(new Point(x - 1, y)); pixels.Push(new Point(x + 1, y));
                    pixels.Push(new Point(x, y - 1)); pixels.Push(new Point(x, y + 1));
                }
            }
            
            foreach (var chunk in activeModifiedChunks) RenderChunk((int)chunk.X, (int)chunk.Y);
            activeModifiedChunks.Clear();
            strokeModified = true;
        }

        private void UpdateCursorAppearance()
        {
            int currentSize = 1;
            if (activeTool == "brush") currentSize = brushSize;
            else if (activeTool == "erase") currentSize = eraseSize;

            if (activeTool == "brush" && !string.IsNullOrEmpty(selectedItem) && ItemLoader.Sprites.TryGetValue(selectedItem, out ItemData itemDataPreview))
            {
                if (itemDataPreview.Category == "Graffiti" || itemDataPreview.Category == "Fossil") currentSize = 1;
            }

            if (activeTool == "pick") {
                PreviewBorder.Stroke = Brushes.Yellow; PreviewBorder.Fill = null; PreviewGhost.IsVisible = false;
                PreviewBorder.Data = new RectangleGeometry(new Rect(0, 0, TILE_SIZE, TILE_SIZE));
                Canvas.SetLeft(PreviewBorder, 0); Canvas.SetTop(PreviewBorder, 0);
            } 
            else if (activeTool == "erase") {
                PreviewBorder.Stroke = Brushes.Red; PreviewBorder.Fill = new SolidColorBrush(Color.FromArgb(100, 255, 0, 0)); PreviewGhost.IsVisible = false;
                PreviewBorder.Data = new RectangleGeometry(new Rect(0, 0, TILE_SIZE * currentSize, TILE_SIZE * currentSize));
                Canvas.SetLeft(PreviewBorder, 0); Canvas.SetTop(PreviewBorder, 0);
            } 
            else if (activeTool == "brush" || activeTool == "fill") {
                PreviewBorder.Stroke = Brushes.White; PreviewBorder.Fill = null;
                
                if (!string.IsNullOrEmpty(selectedItem) && ItemLoader.Sprites.TryGetValue(selectedItem, out ItemData item)) {
                    
                    if (currentSize > 1) {
                        var dg = new DrawingGroup();
                        for (int bx = 0; bx < currentSize; bx++) {
                            for (int by = 0; by < currentSize; by++) {
                                dg.Children.Add(new ImageDrawing {
                                    ImageSource = item.Image,
                                    Rect = GetSpriteRect(item, bx * TILE_SIZE, by * TILE_SIZE)
                                });
                            }
                        }
                        
                        PreviewGhost.Source = new DrawingImage(dg);
                        PreviewGhost.Opacity = 0.6; 
                        
                        PreviewGhost.Width = double.NaN; 
                        PreviewGhost.Height = double.NaN;
                        PreviewGhost.IsVisible = true;
                        PreviewBorder.Fill = null;
                    } else {
                        PreviewGhost.IsVisible = true;
                        PreviewGhost.Source = item.Image; 
                        PreviewGhost.Opacity = 1.0;
                        PreviewGhost.Width = item.Image.PixelSize.Width; 
                        PreviewGhost.Height = item.Image.PixelSize.Height;
                        PreviewBorder.Fill = null;
                    }
                    
                    Rect b = GetSpriteRect(item, 0, 0); 
                    Canvas.SetLeft(PreviewGhost, b.X); 
                    Canvas.SetTop(PreviewGhost, b.Y);

                    var combinedHitbox = new GeometryGroup();
                    int tilesX = Math.Max(1, (int)Math.Ceiling(item.Image.PixelSize.Width / (double)TILE_SIZE));
                    int tilesY = Math.Max(1, (int)Math.Ceiling(item.Image.PixelSize.Height / (double)TILE_SIZE));

                    string[] shapeMap = null;
                    if (tilesX > 1 || tilesY > 1) { 
                        foreach (var key in customHitboxes.Keys) {
                            if (selectedItem.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0) { shapeMap = customHitboxes[key]; break; }
                        }
                    }

                    for (int y = 0; y < tilesY; y++) {
                        for (int x = 0; x < tilesX; x++) {
                            bool isSolid = true;
                            if (shapeMap != null && y < shapeMap.Length && x < shapeMap[y].Length) isSolid = shapeMap[y][x] == '1';
                            
                            if (isSolid) combinedHitbox.Children.Add(new RectangleGeometry(new Rect(x * TILE_SIZE, y * TILE_SIZE, TILE_SIZE, TILE_SIZE)));
                        }
                    }

                    var finalHitbox = new GeometryGroup();
                    for (int bx = 0; bx < currentSize; bx++) {
                        for (int by = 0; by < currentSize; by++) {
                            foreach (var child in combinedHitbox.Children) {
                                if (child is RectangleGeometry rg) {
                                    finalHitbox.Children.Add(new RectangleGeometry(new Rect(rg.Rect.X + (bx * TILE_SIZE), rg.Rect.Y + (by * TILE_SIZE), rg.Rect.Width, rg.Rect.Height)));
                                }
                            }
                        }
                    }

                    PreviewBorder.Data = finalHitbox;
                    
                    bool isGridSnapped = item.Category == "Graffiti" || item.Category == "Fossil" || 
                                         (item.Image.PixelSize.Width % TILE_SIZE == 0 && item.Image.PixelSize.Height % TILE_SIZE == 0 && item.Image.PixelSize.Width > TILE_SIZE);
                    
                    if (isGridSnapped) {
                        int anchorOffsetX = (tilesX - 1) / 2;
                        int anchorOffsetY = tilesY - 1;
                        Canvas.SetLeft(PreviewBorder, -(anchorOffsetX * TILE_SIZE));
                        Canvas.SetTop(PreviewBorder, -(anchorOffsetY * TILE_SIZE));
                    } else {
                        Canvas.SetLeft(PreviewBorder, 0);
                        Canvas.SetTop(PreviewBorder, -(tilesY - 1) * TILE_SIZE);
                    }
                }
            }
        }

        private void UpdatePreviewPosition(int x, int y)
        {
            if (x < 0 || x >= WORLD_WIDTH || y < 0 || y >= WORLD_HEIGHT - 3) 
            { 
                if (PreviewCursor.IsVisible) PreviewCursor.IsVisible = false; 
                return; 
            }
            
            if (!PreviewCursor.IsVisible) PreviewCursor.IsVisible = true;

            CursorTransform.X = x * TILE_SIZE;
            CursorTransform.Y = y * TILE_SIZE;
        }
    }
}