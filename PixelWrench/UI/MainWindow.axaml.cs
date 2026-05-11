using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Interactivity;
using Avalonia.Controls.Shapes;

namespace PixelWrench
{
    public class TileData
    {
        public string Background { get; set; }
        public string WallMount { get; set; }  
        public string Liquid { get; set; }  
        public string BlockOrProp { get; set; }

        public bool IsEmpty => Background == null && WallMount == null && Liquid == null && BlockOrProp == null; 
    }

    public class ChunkHost : Control
    {
        public Action<DrawingContext> DrawAction { get; set; }

        public ChunkHost()
        {
            ClipToBounds = false; 
            RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        }

        public override void Render(DrawingContext context)
        {
            DrawAction?.Invoke(context);
            base.Render(context);
        }

        public void Redraw() => InvalidateVisual();
    }

    public partial class MainWindow : UserControl
    {
        private const int WORLD_WIDTH = 80;
        private const int WORLD_HEIGHT = 60;
        private const int TILE_SIZE = 32;
        private const int CHUNK_SIZE = 10;
        private const int CHUNKS_X = WORLD_WIDTH / CHUNK_SIZE; 
        private const int CHUNKS_Y = WORLD_HEIGHT / CHUNK_SIZE; 

        public readonly string ASSET_PATH = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sprites");

        private TileData[,] worldGrid;
        
        private ChunkHost[,] bgChunks;
        private ChunkHost[,] wmChunks;
        private ChunkHost[,] liquidChunks;
        private ChunkHost[,] blockChunks;

        private bool isPanning = false;
        private Point lastMousePosition;
        private bool showGrid = true;
        private Point? lastDrawPoint = null;
        private Point lastHoverGrid = new Point(-1, -1);
        private HashSet<Point> activeModifiedChunks = new HashSet<Point>();

        private string activeTool = "brush"; 
        private string selectedItem = ""; 
        private bool strokeModified = false; 
        private int libraryItemsPerRow = 4;

        private List<TileData[,]> history = new List<TileData[,]>();
        private int historyIndex = -1;
        
        private string currentLoadedWorldName = "";
		private string currentCategoryFilter = "Block";
        private string currentWorldBackground = "";
        
        public string boxFillMode = "select";
        public string customBackgroundColor = "#130f1c";
        private bool isUpdatingColor = false;
        
        private Point activeSelectionStart;
        private Point activeSelectionEnd;
        private bool hasSelection = false;
        private TileData[,] clipboard = null;
        private int clipboardWidth = 0;
        private int clipboardHeight = 0;
        
        private Point? strokeStartPoint = null;
        private char shiftLockAxis = 'N';

        private int brushSize = 1;
        private int eraseSize = 1; 
        private int gridSize = 1;

        private ScaleTransform CameraZoom => (ScaleTransform)((TransformGroup)WorldContainer.RenderTransform).Children[0];
        private TranslateTransform CameraPan => (TranslateTransform)((TransformGroup)WorldContainer.RenderTransform).Children[1];
        private TranslateTransform CursorTransform => (TranslateTransform)PreviewCursor.RenderTransform;

        public MainWindow()
        {
            InitializeComponent();
            InitializeEngine();
			InitializeNewWorld(100, 100);
			
			this.Loaded += (sender, e) => 
			{
				currentLoadedWorldName = "Demo World";
				PlannerView.IsVisible = true;
				Dispatcher.UIThread.Post(CenterCamera, DispatcherPriority.Loaded);
			};

            this.Opened += (s, e) => 
            {
                Dispatcher.UIThread.Post(CenterCamera, DispatcherPriority.Background);
            };
            
            LoadIcons();
        }

        private void LoadIcons()
        {
            IconLib.Source = LoadInterfaceIcon("Library.png");
            IconFolder.Source = LoadInterfaceIcon("folder.png");
            IconUndo.Source = LoadInterfaceIcon("undo.png");
            IconRedo.Source = LoadInterfaceIcon("redo.png");
            IconGrid.Source = LoadInterfaceIcon("grid.png");
            IconBrush.Source = LoadInterfaceIcon("draw.png");
            IconErase.Source = LoadInterfaceIcon("erase.png");
            IconFill.Source = LoadInterfaceIcon("fill.png");
            IconPick.Source = LoadInterfaceIcon("eyedropper.png");
            BtnBoxIcon.Source = LoadInterfaceIcon("select.png");
            IconClear.Source = LoadInterfaceIcon("clear.png");
            IconSave.Source = LoadInterfaceIcon("save.png");
            IconLoad.Source = LoadInterfaceIcon("load.png");
            IconExport.Source = LoadInterfaceIcon("screenshot.png");
            IconStats.Source = LoadInterfaceIcon("stats.png");
            IconBug.Source = LoadInterfaceIcon("bug.png");
            IconInfo.Source = LoadInterfaceIcon("info.png");
            IconLocation.Source = LoadInterfaceIcon("Location.png");
            
            var iconFull = this.FindControl<Image>("IconBoxFull");
            if (iconFull != null) iconFull.Source = LoadInterfaceIcon("full.png");

            var iconCheck = this.FindControl<Image>("IconBoxCheckered");
            if (iconCheck != null) iconCheck.Source = LoadInterfaceIcon("checkered.png");

            var iconLined = this.FindControl<Image>("IconBoxLined");
            if (iconLined != null) iconLined.Source = LoadInterfaceIcon("lined.png");
            
            var iconSelect = this.FindControl<Image>("IconBoxSelect");
            if (iconSelect != null) iconSelect.Source = LoadInterfaceIcon("select.png");

            if (this.FindControl<Image>("IconLine") != null) this.FindControl<Image>("IconLine").Source = LoadInterfaceIcon("linetool.png");
            if (this.FindControl<Image>("IconMirrorV") != null) this.FindControl<Image>("IconMirrorV").Source = LoadInterfaceIcon("vertical.png");
            if (this.FindControl<Image>("IconMirrorH") != null) this.FindControl<Image>("IconMirrorH").Source = LoadInterfaceIcon("horizontal.png");
            if (this.FindControl<Image>("IconStencil") != null) this.FindControl<Image>("IconStencil").Source = LoadInterfaceIcon("flipdraw.png");
            if (this.FindControl<Image>("IconKofi") != null) this.FindControl<Image>("IconKofi").Source = LoadInterfaceIcon("coffee.png");
        }

        private Bitmap LoadInterfaceIcon(string name)
        {
            try
            {
                var uri = new Uri($"avares://PixelWrench/wwwroot/Sprites/Interface/Buttons/{name}");
                if (Avalonia.Platform.AssetLoader.Exists(uri))
                {
                    using (var stream = Avalonia.Platform.AssetLoader.Open(uri))
                    {
                        return new Bitmap(stream);
                    }
                }
            }
            catch { }
            return null;
        }

        private void Window_PointerMoved(object sender, PointerEventArgs e)
        {
        }

        private async Task ShowMessageBox(string message, string title, bool isSelectable = false, string selectableText = null)
        {
            await MessageBoxCustom.Show(this, message, title, MessageBoxCustom.MessageBoxButtons.Ok, isSelectable, selectableText);
        }

        private async void Bug_Click(object sender, RoutedEventArgs e)
        {
            var result = await MessageBoxCustom.Show(this, "Would you like to join our Discord Server?", "Join Discord", MessageBoxCustom.MessageBoxButtons.YesNo);
            if (result == MessageBoxCustom.MessageBoxResult.Yes)
            try {
                Avalonia.Controls.TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(new Uri("https://discord.com/invite/96whSKbGqJ"));
            } catch {
                _ = ShowMessageBox("Join Discord: https://discord.com/invite/96whSKbGqJ", "Discord");
            }
        }
        
        private async void Stats_Click(object sender, RoutedEventArgs e) 
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();

            if (!string.IsNullOrEmpty(currentWorldBackground)) 
                counts["[World] " + currentWorldBackground] = 1;

            for (int x = 0; x < WORLD_WIDTH; x++)
            {
                for (int y = 0; y < WORLD_HEIGHT; y++)
                {
                    var t = worldGrid[x, y];
                    
                    if (y == WORLD_HEIGHT - 3 && t.BlockOrProp == "bedrock flat") continue;
                    if (y == WORLD_HEIGHT - 2 && t.BlockOrProp == "bedrock") continue;
                    if (y == WORLD_HEIGHT - 1 && t.BlockOrProp == "bedrock lava") continue;

                    if (!string.IsNullOrEmpty(t.Background)) counts[t.Background] = counts.TryGetValue(t.Background, out int b) ? b + 1 : 1;
                    if (!string.IsNullOrEmpty(t.Liquid)) counts[t.Liquid] = counts.TryGetValue(t.Liquid, out int l) ? l + 1 : 1;
                    if (!string.IsNullOrEmpty(t.BlockOrProp)) counts[t.BlockOrProp] = counts.TryGetValue(t.BlockOrProp, out int p) ? p + 1 : 1;
                    
                    if (!string.IsNullOrEmpty(t.WallMount)) {
                        if (t.WallMount.EndsWith(" Full", StringComparison.OrdinalIgnoreCase)) {
                            string prefix1 = t.WallMount.Substring(0, t.WallMount.Length - 5).Trim();
                            string prefix2 = prefix1.Replace(" Fossil", "", StringComparison.OrdinalIgnoreCase).Replace(" Graffiti", "", StringComparison.OrdinalIgnoreCase).Trim();
                            
                            bool foundParts = false;
                            foreach (var key in ItemLoader.Sprites.Keys) {
                                if ((key.StartsWith(prefix1, StringComparison.OrdinalIgnoreCase) || key.StartsWith(prefix2, StringComparison.OrdinalIgnoreCase)) 
                                    && !key.EndsWith(" Full", StringComparison.OrdinalIgnoreCase) && !key.Equals(t.WallMount, StringComparison.OrdinalIgnoreCase)) {
                                    counts[key] = counts.TryGetValue(key, out int c) ? c + 1 : 1;
                                    foundParts = true;
                                }
                            }
                            if (!foundParts) counts[t.WallMount] = counts.TryGetValue(t.WallMount, out int c2) ? c2 + 1 : 1;
                        } else {
                            counts[t.WallMount] = counts.TryGetValue(t.WallMount, out int w) ? w + 1 : 1;
                        }
                    }
                }
            }

            var sortedCounts = counts
                .OrderBy(kvp => {
                    if (kvp.Key.StartsWith("[World]")) return "0_World";
                    if (ItemLoader.Sprites.TryGetValue(kvp.Key, out var data)) return "1_" + data.Category;
                    return "2_Other";
                })
                .ThenBy(kvp => kvp.Key)
                .ToList();

            string selectableText = "";
            string currentCat = "";

            foreach (var kvp in sortedCounts)
            {
                string cat = kvp.Key.StartsWith("[World]") ? "World" : ItemLoader.Sprites.TryGetValue(kvp.Key, out var d) ? d.Category : "Other";
                
                string headerName = cat.ToUpper();
                if (headerName != "WORLD" && headerName != "GRAFFITI" && headerName != "OTHER") headerName += "S";
                
                if (cat != currentCat) { selectableText += $"\n--- {headerName} ---\n"; currentCat = cat; }
                selectableText += $"{kvp.Key}: {kvp.Value}\n";
            }

            if (counts.Count == 0) selectableText += "The world is completely empty!";

            await ShowMessageBox("World Statistics:", "PixelWrench Stats", true, selectableText);
        }
        
        public void SetBoxMode(string mode)
        {
            boxFillMode = mode;
            string iconName = (boxFillMode == "select") ? "select.png" : $"{boxFillMode}.png";
            BtnBoxIcon.Source = LoadInterfaceIcon(iconName);
        }

        private void ToggleLibrary_Click(object sender, RoutedEventArgs e)
        {
            PlannerView.ColumnDefinitions[0].Width = PlannerView.ColumnDefinitions[0].Width.Value > 0 ? new GridLength(0) : new GridLength(300);
            Dispatcher.UIThread.Post(() => ClampCamera(), DispatcherPriority.Render);
        }

        private void SearchBox_GotFocus(object sender, GotFocusEventArgs e) => SearchPlaceholder.IsVisible = false;
        private void SearchBox_LostFocus(object sender, RoutedEventArgs e) { if (string.IsNullOrEmpty(SearchBox.Text)) SearchPlaceholder.IsVisible = true; }
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => PopulateLibrary(SearchBox.Text);

        private void CategoryFilter_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag != null)
            {
                currentCategoryFilter = rb.Tag.ToString();
                if (SearchBox != null) PopulateLibrary(SearchBox.Text);
            }
        }

        private void PopulateLibrary(string filter)
		{
			if (LibraryPanel == null) return; 

			LibraryPanel.Children.Clear();
			filter = filter?.ToLower() ?? "";

			double itemSize = 255.0 / libraryItemsPerRow;
			LibraryPanel.ItemWidth = itemSize;
			LibraryPanel.ItemHeight = itemSize;
			double imageSize = Math.Max(10, itemSize - 10); 

			foreach (var kvp in ItemLoader.Sprites)
			{
				if (kvp.Value.Category != currentCategoryFilter) continue;
				if (kvp.Value.IsVariant) continue; 
				if (!string.IsNullOrEmpty(filter) && !kvp.Key.ToLower().Contains(filter)) continue;

				Image img = new Image { Source = kvp.Value.Image, Width = imageSize, Height = imageSize, Cursor = new Cursor(StandardCursorType.Hand), Tag = kvp.Key };
				RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.None);
				Border border = new Border { Background = Brushes.Transparent, BorderThickness = new Thickness(1), BorderBrush = Brushes.Transparent, CornerRadius = new CornerRadius(3), Child = img };

				img.PointerPressed += (s, e) => {
					string clickedItem = ((Image)s).Tag.ToString();

					if (ItemLoader.Sprites[clickedItem].Category == "WorldBackground") 
					{
						currentWorldBackground = clickedItem;
						StatusItem.Text = "World: " + clickedItem;
						RenderWorldBackground();

						if (clickedItem == "Custom Color")
						{
							ColorPickerPopup.PlacementTarget = (Control)s;
							ColorPickerPopup.IsOpen = true;
						}

						if (VariantsPanel != null) VariantsPanel.Children.Clear(); 
						if (VariantsContainer != null) VariantsContainer.IsVisible = false;
					}
					else
					{
						selectedItem = clickedItem;
						StatusItem.Text = "Selected: " + selectedItem;
						SetActiveTool("brush");
						PopulateVariants(clickedItem); 
					}
				};

				LibraryPanel.Children.Add(border);
			}
		}
        
        private void Library_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            {
                LibrarySizeDropdown.PlacementTarget = (Control)sender;
                LibrarySizeDropdown.IsOpen = true;
                
                LibrarySizeInput.Text = libraryItemsPerRow.ToString();
                LibrarySizeInput.Focus();
                LibrarySizeInput.SelectionStart = 0;
                LibrarySizeInput.SelectionEnd = LibrarySizeInput.Text.Length;
                
                e.Handled = true;
            }
        }

        private void LibrarySizeInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (LibrarySizeInput == null) return;

            if (int.TryParse(LibrarySizeInput.Text, out int size))
            {
                if (size < 3) size = 1;
                if (size > 10) size = 10;
                libraryItemsPerRow = size;
                
                if (SearchBox != null) PopulateLibrary(SearchBox.Text);
            }
            else if (!string.IsNullOrEmpty(LibrarySizeInput.Text))
            {
                LibrarySizeInput.Text = libraryItemsPerRow.ToString();
                LibrarySizeInput.SelectionStart = LibrarySizeInput.Text.Length;
            }
        }
        
        private void ColorSlider_ValueChanged(object sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (isUpdatingColor || CustomColorInput == null) return;

            isUpdatingColor = true; 

            byte r = (byte)SliderR.Value;
            byte g = (byte)SliderG.Value;
            byte b = (byte)SliderB.Value;

            string hex = $"#{r:X2}{g:X2}{b:X2}";

            CustomColorInput.Text = hex;
            customBackgroundColor = hex;

            if (currentWorldBackground == "Custom Color")
            {
                RenderWorldBackground();
            }

            isUpdatingColor = false; 
        }

        private void CustomColorInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isUpdatingColor || CustomColorInput == null || SliderR == null) return;

            string input = CustomColorInput.Text;
            
            if (!string.IsNullOrEmpty(input) && Color.TryParse(input, out Color c))
            {
                isUpdatingColor = true; 

                SliderR.Value = c.R;
                SliderG.Value = c.G;
                SliderB.Value = c.B;

                customBackgroundColor = input;

                if (currentWorldBackground == "Custom Color")
                {
                    RenderWorldBackground();
                }

                isUpdatingColor = false; 
            }
        }
        
        private void PopulateVariants(string baseItem)
        {
            if (VariantsPanel == null) return;
            VariantsPanel.Children.Clear();

            var data = ItemLoader.Sprites[baseItem];
            if (data.Category != "Graffiti" && data.Category != "Fossil")
            {
                VariantsContainer.IsVisible = false;
                return;
            }

            string prefix = baseItem.ToLower().Replace(" full", "").Replace(" fossil", "").Replace(" graffiti", "").Trim();
            bool hasVariants = false;

            foreach (var kvp in ItemLoader.Sprites) {
                if (kvp.Key == baseItem) continue; 
                if (kvp.Value.IsVariant && kvp.Key.ToLower().StartsWith(prefix)) {
                    hasVariants = true;
                    break;
                }
            }

            if (hasVariants)
            {
                VariantsContainer.IsVisible = true;
                AddVariantButton(baseItem); 

                foreach (var kvp in ItemLoader.Sprites) {
                    if (kvp.Key == baseItem) continue; 
                    if (kvp.Value.IsVariant && kvp.Key.ToLower().StartsWith(prefix)) {
                        AddVariantButton(kvp.Key);
                    }
                }
            }
            else 
            {
                VariantsContainer.IsVisible = false;
            }
        }

        private void AddVariantButton(string itemName)
        {
            var data = ItemLoader.Sprites[itemName];
            Image img = new Image { Source = data.Image, Width = 45, Height = 45, Cursor = new Cursor(StandardCursorType.Hand), Tag = itemName };
            ToolTip.SetTip(img, itemName);
            Border border = new Border { Background = Brushes.Transparent, BorderThickness = new Thickness(1), BorderBrush = Brushes.Transparent, CornerRadius = new CornerRadius(3), Child = img };

            img.PointerPressed += (s, e) => {
                selectedItem = ((Image)s).Tag.ToString();
                StatusItem.Text = "Selected Part: " + selectedItem;
                SetActiveTool("brush");
            };
            
            VariantsPanel.Children.Add(border);
        }

        private void Tool_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                SetActiveTool(btn.Tag.ToString());
            }
        }

        private void BrushSizeInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (BrushSizeInput == null) return;

            if (int.TryParse(BrushSizeInput.Text, out int size))
            {
                if (size < 1) size = 1;
                if (size > 5) size = 5;
                
                if (activeTool == "brush") brushSize = size;
                else if (activeTool == "erase") eraseSize = size;
                
                UpdateCursorAppearance(); 
            }
            else if (!string.IsNullOrEmpty(BrushSizeInput.Text))
            {
                BrushSizeInput.Text = (activeTool == "brush") ? brushSize.ToString() : eraseSize.ToString();
                BrushSizeInput.SelectionStart = BrushSizeInput.Text.Length;
            }
        }
        
        private void GridToggle_Click(object sender, RoutedEventArgs e) 
        {
            showGrid = !showGrid;
            GridOverlay.IsVisible = showGrid;
        }

        private void GridToggle_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            {
                GridSizeDropdown.IsOpen = true;
                GridSizeInput.Text = gridSize.ToString();
                GridSizeInput.Focus();
                GridSizeInput.SelectionStart = 0;
                GridSizeInput.SelectionEnd = GridSizeInput.Text.Length;
                e.Handled = true;
            }
        }

        private void GridSizeInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (GridSizeInput == null) return;

            if (int.TryParse(GridSizeInput.Text, out int size))
            {
                if (size < 1) size = 1;
                if (size > 4) size = 4;
                gridSize = size;
                UpdateGridVisuals();
            }
            else if (!string.IsNullOrEmpty(GridSizeInput.Text))
            {
                GridSizeInput.Text = gridSize.ToString();
                GridSizeInput.SelectionStart = GridSizeInput.Text.Length;
            }
        }

        private void UpdateGridVisuals()
        {
            if (GridOverlay == null || CameraZoom == null) return;
            
            var geometryGroup = new GeometryGroup();
            int step = gridSize * TILE_SIZE;
            int pxWidth = WORLD_WIDTH * TILE_SIZE;
            int pxHeight = WORLD_HEIGHT * TILE_SIZE;

            for (int x = 0; x <= pxWidth; x += step)
                geometryGroup.Children.Add(new LineGeometry(new Point(x, 0), new Point(x, pxHeight)));
            
            for (int y = 0; y <= pxHeight; y += step)
                geometryGroup.Children.Add(new LineGeometry(new Point(0, y), new Point(pxWidth, y)));

            GridOverlay.Data = geometryGroup;
            GridOverlay.StrokeThickness = Math.Max(0.5, 2.0 / CameraZoom.ScaleX);
        }

        private void BtnBox_Click(object sender, RoutedEventArgs e)
        {
            SetActiveTool("box");
        }

        private void Tool_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed && sender is Button btn)
            {
                string tool = btn.Tag?.ToString() ?? "";
                
                if (tool == "brush" || tool == "erase")
                {
                    SetActiveTool(tool);
                    BrushSizeDropdown.PlacementTarget = btn;
                    BrushSizeDropdown.IsOpen = true;
                    
                    BrushSizeInput.Text = (tool == "brush") ? brushSize.ToString() : eraseSize.ToString();
                    BrushSizeInput.Focus();
                    BrushSizeInput.SelectionStart = 0;
                    BrushSizeInput.SelectionEnd = BrushSizeInput.Text.Length;
                }
                else if (btn.Name == "BtnBox")
                {
                    SetActiveTool("box");
                    BoxDropdown.PlacementTarget = btn;
                    BoxDropdown.IsOpen = true;
                }
                
                e.Handled = true;
            }
        }
        
        private void SetBoxMode_Click(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)sender;
            boxFillMode = btn.Tag.ToString();
            BoxDropdown.IsOpen = false;
            
            string iconName = (boxFillMode == "select") ? "select.png" : $"{boxFillMode}.png";
            BtnBoxIcon.Source = LoadInterfaceIcon(iconName);
        }

        private void Export_Click(object sender, RoutedEventArgs e) => ExportToPNG();
        private void Clear_Click(object sender, RoutedEventArgs e) => WipeBoard();
        
        private void Undo_Click(object sender, RoutedEventArgs e) => Undo();
        private void Redo_Click(object sender, RoutedEventArgs e) => Redo();
        
        private void Folder_Click(object sender, RoutedEventArgs e)
        {
            string appDataPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PixelWrench");
            
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(appDataPath, "Saves"));
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(appDataPath, "Screenshots"));
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(appDataPath, "Templates"));
            
        }

        private async void Info_Click(object sender, RoutedEventArgs e)
        {
            string help = 
                "B = Brush Tool\n" +
                "E = Eraser Tool\n" +
                "G = Fill Tool\n" +
                "I = Block Pick Tool\n" +
                "R = Select Area Tool\n" +
                "Move Cam = Middle Mouse (Hold)\n" +
                "Zoom In/Out = Scroll Wheel\n" +
                "Toggle Grid = Tab\n" +
                "CTRL + Z = Undo\n" +
                "CTRL + Y = Redo";
            await ShowMessageBox(help, "PixelWrench Shortcuts");
        }

        private void TitleBar_PointerPressed(object sender, PointerPressedEventArgs e) { }
        private void Minimize_Click(object sender, RoutedEventArgs e) { }
        private void Maximize_Click(object sender, RoutedEventArgs e) { }
        private void Close_Click(object sender, RoutedEventArgs e) { }
        
        public double GetMinZoom()
        {
            if (ViewportCanvas.Bounds.Width == 0 || ViewportCanvas.Bounds.Height == 0) return 0.5;
            double worldPixelWidth = WORLD_WIDTH * TILE_SIZE;
            double worldPixelHeight = WORLD_HEIGHT * TILE_SIZE;
            
            double scaleX = ViewportCanvas.Bounds.Width / worldPixelWidth;
            double scaleY = ViewportCanvas.Bounds.Height / worldPixelHeight;
            
            double scale = Math.Min(scaleX, scaleY);
            return Math.Floor(scale * 10) / 10.0; 
        }

        private void CenterCamera()
        {
            double minZoom = GetMinZoom();

            CameraZoom.ScaleX = minZoom;
            CameraZoom.ScaleY = minZoom;
            StatusZoom.Text = $"Zoom: {Math.Round(minZoom * 100)}%";

            double scaledWidth = WORLD_WIDTH * TILE_SIZE * minZoom;
            double scaledHeight = WORLD_HEIGHT * TILE_SIZE * minZoom;

            CameraPan.X = (ViewportCanvas.Bounds.Width - scaledWidth) / 2;
            CameraPan.Y = (ViewportCanvas.Bounds.Height - scaledHeight) / 2;
            
            ClampCamera();
        }

        private void ClampCamera()
        {
            if (ViewportCanvas.Bounds.Width == 0) return;

            double minZoom = GetMinZoom();
            if (CameraZoom.ScaleX < minZoom) {
                CameraZoom.ScaleX = minZoom;
                CameraZoom.ScaleY = minZoom;
                StatusZoom.Text = $"Zoom: {Math.Round(minZoom * 100)}%";
            }

            double scaledWidth = WORLD_WIDTH * TILE_SIZE * CameraZoom.ScaleX;
            double scaledHeight = WORLD_HEIGHT * TILE_SIZE * CameraZoom.ScaleY;

            if (scaledWidth <= ViewportCanvas.Bounds.Width) CameraPan.X = (ViewportCanvas.Bounds.Width - scaledWidth) / 2;
            else {
                if (CameraPan.X > 0) CameraPan.X = 0;
                else if (CameraPan.X < ViewportCanvas.Bounds.Width - scaledWidth) CameraPan.X = ViewportCanvas.Bounds.Width - scaledWidth;
            }

            if (scaledHeight <= ViewportCanvas.Bounds.Height) CameraPan.Y = (ViewportCanvas.Bounds.Height - scaledHeight) / 2;
            else {
                if (CameraPan.Y > 0) CameraPan.Y = 0;
                else if (CameraPan.Y < ViewportCanvas.Bounds.Height - scaledHeight) CameraPan.Y = ViewportCanvas.Bounds.Height - scaledHeight;
            }
            
            UpdateGridVisuals();
        }

        private async void Kofi_Click(object sender, RoutedEventArgs e)
        {
            var result = await MessageBoxCustom.Show(this, "Would you like to support my project?", "Support PixelWrench", MessageBoxCustom.MessageBoxButtons.YesNo);
            if (result == MessageBoxCustom.MessageBoxResult.Yes)
            try {
                Avalonia.Controls.TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(new Uri("https://ko-fi.com/pixelwrench"));
            } catch {
                _ = ShowMessageBox("Support Project: https://ko-fi.com/pixelwrench", "Support");
            }
        }
    }
}