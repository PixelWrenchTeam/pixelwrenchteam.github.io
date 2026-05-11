using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.VisualTree;

namespace PixelWrench
{
    public partial class MainWindow
    {
        private Point boxStartGrid;
        private bool isDraggingBox = false;
        private Rectangle selectionRect = null;
        private bool isRightClickDragging = false; 

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (SearchBox.IsFocused) return;

            if (e.KeyModifiers == KeyModifiers.Control)
            {
                if (e.Key == Key.Z) { Undo(); e.Handled = true; }
                if (e.Key == Key.Y) { Redo(); e.Handled = true; }
                
                if (e.Key == Key.C) { CopySelection(); e.Handled = true; }
                if (e.Key == Key.X) { CutSelection(); e.Handled = true; }
                if (e.Key == Key.V) { 
                    if (clipboard != null) SetActiveTool("paste"); 
                    e.Handled = true; 
                }
            }
            else
            {
                if (e.Key == Key.B) { SetActiveTool("brush"); e.Handled = true; }
                if (e.Key == Key.E) { SetActiveTool("erase"); e.Handled = true; }
                if (e.Key == Key.G) { SetActiveTool("fill"); e.Handled = true; }
                if (e.Key == Key.I) { SetActiveTool("pick"); e.Handled = true; }
                if (e.Key == Key.R) 
                {
                    if (activeTool == "box") {
                        if (boxFillMode == "select") SetBoxMode("full");
                        else if (boxFillMode == "full") SetBoxMode("checkered");
                        else if (boxFillMode == "checkered") SetBoxMode("lined");
                        else SetBoxMode("select");
                    }
                    else SetActiveTool("box");
                    e.Handled = true;
                }

                if (e.Key == Key.Tab)
                {
                    showGrid = !showGrid;
                    GridOverlay.IsVisible = showGrid;
                    e.Handled = true; 
                }
            }
            base.OnKeyDown(e);
        }

        private void CopySelection()
        {
            if (!hasSelection || selectionRect == null) return;
            
            int startX = (int)Math.Max(0, Math.Min(activeSelectionStart.X, activeSelectionEnd.X));
            int endX = (int)Math.Min(WORLD_WIDTH - 1, Math.Max(activeSelectionStart.X, activeSelectionEnd.X));
            int startY = (int)Math.Max(0, Math.Min(activeSelectionStart.Y, activeSelectionEnd.Y));
            int endY = (int)Math.Min(WORLD_HEIGHT - 4, Math.Max(activeSelectionStart.Y, activeSelectionEnd.Y));

            clipboardWidth = endX - startX + 1;
            clipboardHeight = endY - startY + 1;
            clipboard = new TileData[clipboardWidth, clipboardHeight];

            for (int x = 0; x < clipboardWidth; x++) {
                for (int y = 0; y < clipboardHeight; y++) {
                    var t = worldGrid[startX + x, startY + y];
                    clipboard[x, y] = new TileData {
                        Background = t.Background, WallMount = t.WallMount,
                        Liquid = t.Liquid, BlockOrProp = t.BlockOrProp
                    };
                }
            }
            StatusItem.Text = $"Copied {clipboardWidth}x{clipboardHeight} Area";
        }

        private void CutSelection()
        {
            if (!hasSelection || selectionRect == null) return;
            CopySelection(); 
            
            int startX = (int)Math.Max(0, Math.Min(activeSelectionStart.X, activeSelectionEnd.X));
            int endX = (int)Math.Min(WORLD_WIDTH - 1, Math.Max(activeSelectionStart.X, activeSelectionEnd.X));
            int startY = (int)Math.Max(0, Math.Min(activeSelectionStart.Y, activeSelectionEnd.Y));
            int endY = (int)Math.Min(WORLD_HEIGHT - 4, Math.Max(activeSelectionStart.Y, activeSelectionEnd.Y));

            for (int x = startX; x <= endX; x++) {
                for (int y = startY; y <= endY; y++) {
                    worldGrid[x, y].Background = null;
                    worldGrid[x, y].WallMount = null;
                    worldGrid[x, y].Liquid = null;
                    worldGrid[x, y].BlockOrProp = null;
                    MarkDirty(x, y);
                }
            }
            
            foreach (var chunk in activeModifiedChunks) RenderChunk((int)chunk.X, (int)chunk.Y);
            activeModifiedChunks.Clear();
            SaveHistoryState();
            
            WorldContainer.Children.Remove(selectionRect);
            selectionRect = null;
            hasSelection = false;
            
            StatusItem.Text = $"Cut {clipboardWidth}x{clipboardHeight} Area";
        }

        private void Viewport_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            var pointerProps = e.GetCurrentPoint(WorldContainer);
            Point pos = pointerProps.Position;
            Point gridCoords = GetGridCoords(pos);

            if (pointerProps.Properties.IsLeftButtonPressed || pointerProps.Properties.IsRightButtonPressed)
            {
                isPanning = false;
                lastDrawPoint = gridCoords;
                bool isRightClick = pointerProps.Properties.IsRightButtonPressed;

                if (activeTool == "box" || activeTool == "mirror_v" || activeTool == "mirror_h" || activeTool == "stencil")
                {
                    if (selectionRect != null && !isDraggingBox) {
                        WorldContainer.Children.Remove(selectionRect);
                        selectionRect = null;
                        hasSelection = false;
                    }

                    boxStartGrid = gridCoords;
                    isDraggingBox = true;
                    isRightClickDragging = isRightClick;
                    
                    if (selectionRect == null) {
                        selectionRect = new Rectangle {
                            Fill = new SolidColorBrush(Color.FromArgb(100, 0, 120, 215)),
                            Stroke = Brushes.LightBlue,
                            StrokeThickness = 2,
                            IsHitTestVisible = false
                        };
                        selectionRect.ZIndex = 500;
                        WorldContainer.Children.Add(selectionRect);
                    }
                    
                    strokeStartPoint = gridCoords;
                    UpdateSelectionBox(gridCoords);
                    return; 
                }

                if (activeTool == "line")
                {
                    strokeStartPoint = gridCoords;
                    isDraggingBox = true; 
                    return;
                }

                shiftLockAxis = 'N'; 
                strokeStartPoint = gridCoords; 

                if (activeModifiedChunks.Count > 0)
                {
                    foreach (var chunk in activeModifiedChunks) RenderChunk((int)chunk.X, (int)chunk.Y);
                    activeModifiedChunks.Clear();
                    strokeModified = true;
                }
            }
            else if (pointerProps.Properties.IsMiddleButtonPressed)
            {
                isPanning = true;
                lastMousePosition = e.GetPosition(this);
                ViewportCanvas.Cursor = new Cursor(StandardCursorType.Hand);
            }
            
            e.Pointer.Capture(ViewportCanvas);
        }

        private void UpdateSelectionBox(Point currentGrid)
        {
            if (selectionRect == null) return;
            
            double left = Math.Min(boxStartGrid.X, currentGrid.X) * TILE_SIZE;
            double top = Math.Min(boxStartGrid.Y, currentGrid.Y) * TILE_SIZE;
            double width = (Math.Abs(currentGrid.X - boxStartGrid.X) + 1) * TILE_SIZE;
            double height = (Math.Abs(currentGrid.Y - boxStartGrid.Y) + 1) * TILE_SIZE;
            
            Canvas.SetLeft(selectionRect, left);
            Canvas.SetTop(selectionRect, top);
            selectionRect.Width = width;
            selectionRect.Height = height;
            
            if (isRightClickDragging) {
                selectionRect.Fill = new SolidColorBrush(Color.FromArgb(100, 215, 0, 0)); 
                selectionRect.Stroke = Brushes.Red;
            } else {
                selectionRect.Fill = new SolidColorBrush(Color.FromArgb(100, 0, 120, 215)); 
                selectionRect.Stroke = Brushes.LightBlue;
            }
        }

        private void Viewport_PointerMoved(object sender, PointerEventArgs e)
        {
            var pointerProps = e.GetCurrentPoint(WorldContainer);
            Point pos = pointerProps.Position;
            Point gridCoords = GetGridCoords(pos);

            if (isDraggingBox) { 
                if (activeTool == "line" && strokeStartPoint.HasValue) {
                } else {
                    UpdateSelectionBox(gridCoords); 
                }
                return; 
            }

            if (gridCoords != lastHoverGrid)
            {
                lastHoverGrid = gridCoords;
                UpdatePreviewPosition((int)gridCoords.X, (int)gridCoords.Y);
        
                if (gridCoords.X >= 0 && gridCoords.X < WORLD_WIDTH && gridCoords.Y >= 0 && gridCoords.Y < WORLD_HEIGHT)
                {
                    StatusCoords.Text = $"X: {gridCoords.X + 1} | Y: {60 - gridCoords.Y}";
                }
                else
                {
                    StatusCoords.Text = "X: - | Y: -";
                }

                if (!isPanning && (pointerProps.Properties.IsLeftButtonPressed || pointerProps.Properties.IsRightButtonPressed))
                {
                    if (activeTool == "brush" || activeTool == "erase" || activeTool == "stencil")
                    {
                        bool isRightClick = pointerProps.Properties.IsRightButtonPressed;

                        Point targetGrid = gridCoords;
                        
                        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && strokeStartPoint.HasValue)
                        {
                            if (shiftLockAxis == 'N') {
                                double diffX = Math.Abs(gridCoords.X - strokeStartPoint.Value.X);
                                double diffY = Math.Abs(gridCoords.Y - strokeStartPoint.Value.Y);
                                if (diffX > 0 || diffY > 0) {
                                    shiftLockAxis = (diffX > diffY) ? 'X' : 'Y';
                                }
                            }
                            
                            if (shiftLockAxis == 'X') targetGrid = new Point(gridCoords.X, strokeStartPoint.Value.Y); 
                            else if (shiftLockAxis == 'Y') targetGrid = new Point(strokeStartPoint.Value.X, gridCoords.Y); 
                        }
                        else
                        {
                            shiftLockAxis = 'N'; 
                        }

                        lastDrawPoint = targetGrid;

                        if (activeModifiedChunks.Count > 0)
                        {
                            foreach (var chunk in activeModifiedChunks) RenderChunk((int)chunk.X, (int)chunk.Y);
                            activeModifiedChunks.Clear();
                            strokeModified = true;
                        }
                    }
                }
            }

            if (isPanning)
            {
                Point currentPosition = e.GetPosition(this);
                Vector diff = currentPosition - lastMousePosition;
                CameraPan.X += diff.X;
                CameraPan.Y += diff.Y;
                lastMousePosition = currentPosition;
                ClampCamera();
            }
        }

        private void Viewport_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            var pointerProps = e.GetCurrentPoint(WorldContainer);
            
            if (isDraggingBox)
            {
                Point pos = pointerProps.Position;
                Point endGrid = GetGridCoords(pos);
                
                isDraggingBox = false;

                if (activeTool == "line" && strokeStartPoint.HasValue)
                {
                    if (activeModifiedChunks.Count > 0) {
                        foreach (var chunk in activeModifiedChunks) RenderChunk((int)chunk.X, (int)chunk.Y);
                        activeModifiedChunks.Clear();
                    }
                    SaveHistoryState();
                    strokeModified = false;
                    UpdateCursorAppearance();
                    return;
                }

                activeSelectionStart = boxStartGrid;
                activeSelectionEnd = endGrid;
                hasSelection = true;

                if (activeTool == "box")
                {
                    if (boxFillMode != "select")
                    {
                        int startX = (int)Math.Min(activeSelectionStart.X, activeSelectionEnd.X);
                        int endX = (int)Math.Max(activeSelectionStart.X, activeSelectionEnd.X);
                        int startY = (int)Math.Min(activeSelectionStart.Y, activeSelectionEnd.Y);
                        int endY = (int)Math.Max(activeSelectionStart.Y, activeSelectionEnd.Y);

                        bool useCheckered = boxFillMode == "checkered";
                        bool useLines = boxFillMode == "lined";

                        for (int x = startX; x <= endX; x++) {
                            for (int y = startY; y <= endY; y++) {
                                bool shouldPlace = true;
                                if (useCheckered) shouldPlace = ((x + y) % 2) == 0;
                                else if (useLines) shouldPlace = (y % 2) == 0;
                            }
                        }
                        
                        if (activeModifiedChunks.Count > 0) {
                            foreach (var chunk in activeModifiedChunks) RenderChunk((int)chunk.X, (int)chunk.Y);
                            activeModifiedChunks.Clear();
                            SaveHistoryState();
                        }

                    }
                }
                return;
            }

            if (e.InitialPressMouseButton == MouseButton.Middle) 
            {
                isPanning = false;
                ViewportCanvas.Cursor = new Cursor(StandardCursorType.Hand);
            }

            if ((e.InitialPressMouseButton == MouseButton.Left || e.InitialPressMouseButton == MouseButton.Right) && strokeModified)
            {
                SaveHistoryState();
                strokeModified = false;
                lastDrawPoint = null;
            }
            
            e.Pointer.Capture(null);
        }

        private void Viewport_PointerExited(object sender, PointerEventArgs e)
        {
            PreviewCursor.IsVisible = false;
            
            if (isDraggingBox)
            {
                isDraggingBox = false;
                if (selectionRect != null) {
                    WorldContainer.Children.Remove(selectionRect);
                    selectionRect = null;
                }
            }
        }
        
        private void Viewport_PointerEntered(object sender, PointerEventArgs e)
        {
			
        }

        private void Viewport_PointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            double[] zoomLevels = { 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 1.25, 1.5, 1.75, 2.0, 2.5, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0 };
    
            double currentScale = CameraZoom.ScaleX;
            double newScale = currentScale;
            
            double minZoom = GetMinZoom();

            if (e.Delta.Y > 0)
            {
                foreach (double level in zoomLevels) {
                    if (level > currentScale + 0.01) { newScale = level; break; }
                }
            }
            else
            {
                for (int i = zoomLevels.Length - 1; i >= 0; i--) {
                    if (zoomLevels[i] < currentScale - 0.01) { newScale = zoomLevels[i]; break; }
                }
                
                if (newScale < minZoom) newScale = minZoom;
            }

            Point mousePos = e.GetPosition(WorldContainer);
            CameraPan.X -= mousePos.X * (newScale - CameraZoom.ScaleX);
            CameraPan.Y -= mousePos.Y * (newScale - CameraZoom.ScaleY);

            CameraZoom.ScaleX = newScale;
            CameraZoom.ScaleY = newScale;
    
            StatusZoom.Text = $"Zoom: {Math.Round(newScale * 100)}%";
            ClampCamera();
        }
    }
}