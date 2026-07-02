using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using Rhino.Geometry;
using Rhino.Input.Custom;

namespace DB3DFloora
{
    /// <summary>中文浮動操作介面主視窗（對照 Python 版 FlooraForm）。非模態、永遠置頂，
    /// 讓使用者在點選「選擇物件」後於 Rhino 視圖中選取一個平面，依目前設定即時預覽並生成磁磚圖案。</summary>
    public class FlooraForm : Form
    {
        private const int MaxUndo = 3;
        private static readonly (string Label, string Value)[] PaintModes =
        {
            ("目前材質", "current"), ("自訂顏色", "custom_color"), ("貼圖材質", "texture"),
        };
        private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff" };
        private const int MaxTextures = 5;
        private const int GridCols = 6;

        private static readonly Dictionary<string, double> Increments = new Dictionary<string, double>
        {
            {"gx", 1.0}, {"gy", 1.0}, {"gw", 0.1}, {"gd", 0.1}, {"r2r", 5.0}, {"twa", 1.0},
        };

        private static readonly Dictionary<string, string> TileLabels = new Dictionary<string, string>
        {
            {"HpScth1", "格子1"}, {"HpScth2", "格子2"}, {"HpScth3", "格子3"}, {"HpScth4", "格子4"},
            {"IrPoly", "不規則"},
        };

        private static FlooraForm _instance;

        public static void Show_()
        {
            if (_instance != null && !_instance.IsDisposed)
            {
                _instance.BringToFront();
                return;
            }
            _instance = new FlooraForm();
            _instance.Show();
        }

        private string _patternId;
        private TileOptions _opts;
        private readonly List<List<Guid>> _undoStack = new List<List<Guid>>();
        private readonly Dictionary<string, NumericStepper> _fields = new Dictionary<string, NumericStepper>();
        private readonly Dictionary<string, (Control Label, Control Box, HashSet<string> Patterns)> _fieldRows =
            new Dictionary<string, (Control, Control, HashSet<string>)>();
        private readonly Dictionary<string, PatternTile> _patternTiles = new Dictionary<string, PatternTile>();

        private string _paintMode = "current";
        private List<string> _texturePaths = new List<string>();
        private System.Drawing.Color _customColor;

        private bool _bevelEnabled;
        private double _bevelSize = 0.3;

        private TextureOptions _textureOpts = new TextureOptions();
        private bool _randomDefect;
        private double _defectMin;
        private double _defectMax = 2.0;
        private bool _backFace;
        private bool _keepGroup = true;

        private Curve _previewBoundary;
        private Plane _previewPlane;
        private Point3d? _previewAnchor;
        private List<Guid> _previewIds = new List<Guid>();

        private Label _gxLabel;
        private Label _bwbLabel;
        private DropDown _bwbDd;
        private string[] _bwbOptions;
        private DropDown _rotDd;
        private string[] _rotOptions;
        private DropDown _sptDd;
        private (string Label, string Value)[] _sptItems;
        private DropDown _paintDd;
        private ColorPicker _colorPicker;
        private StackLayout _colorRow;
        private Button _btnPickTextures;
        private Label _textureStatus;
        private StackLayout _texturePickRow;
        private StackLayout _textureThumbContainer;
        private Label _textureSectionLabel;
        private CheckBox _alignEdgeCheck;
        private CheckBox _randomPosCheck;
        private CheckBox _randomRotCheck;
        private StackLayout _textureEffectRow;
        private CheckBox _bevelCheck;
        private Label _bevelSizeLabel;
        private NumericStepper _bevelSizeBox;
        private CheckBox _defectCheck;
        private Label _defectMinLabel;
        private NumericStepper _defectMinBox;
        private Label _defectMaxLabel;
        private NumericStepper _defectMaxBox;
        private CheckBox _backFaceCheck;
        private CheckBox _keepGroupCheck;
        private Button _btnGenerate;
        private Label _status;

        public FlooraForm()
        {
            Title = "DB3D Floora for Rhino by Onon.Nihow";
            Topmost = true;
            Resizable = true;
            BackgroundColor = UiStyle.CBg;
            Padding = new Padding(10);

            try
            {
                var mainWindow = UiStyle.RhinoMainWindow();
                if (mainWindow != null)
                    Owner = mainWindow;
            }
            catch { }

            _patternId = StorageUtil.LoadCurrentPattern("Tile");
            if (!Defaults.Patterns.Contains(_patternId))
                _patternId = "Tile";
            _opts = StorageUtil.LoadPatternOpts(_patternId, Defaults.DefaultsFor(_patternId));
            _customColor = UiStyle.ToSysColor(UiStyle.DefaultTileColor);

            BuildLayout();
            Closed += OnClosed;
        }

        // ---------------------------------------------------------------- layout

        private void BuildLayout()
        {
            var outer = UiStyle.VStack();

            var titleFont = UiStyle.F("title");
            var sectionFont = UiStyle.F("section");
            var subFont = UiStyle.F("body");

            var title = UiStyle.MakeLabel("DB3D Floora for Rhino by Onon.Nihow", titleFont, UiStyle.CAccent);
            title.TextAlignment = TextAlignment.Center;
            UiStyle.AddRow(outer, title);
            UiStyle.AddRow(outer, UiStyle.Hr());

            UiStyle.AddRow(outer, UiStyle.SectionLabel("圖案", sectionFont, UiStyle.CAccent));
            var flatOrder = new List<string>();
            foreach (var (_, ids) in Defaults.CategoriesTw)
                flatOrder.AddRange(ids);
            var row = new List<Control>();
            foreach (var pid in flatOrder)
            {
                string labelText = TileLabels.TryGetValue(pid, out var lt) ? lt : Defaults.NamesTw[pid];
                var tile = new PatternTile(pid, labelText, SelectPattern);
                tile.SetSelected(pid == _patternId);
                _patternTiles[pid] = tile;
                row.Add(tile);
                if (row.Count == GridCols)
                {
                    outer.Items.Add(new StackLayoutItem(UiStyle.HRow(row.ToArray())));
                    row = new List<Control>();
                }
            }
            if (row.Count > 0)
                outer.Items.Add(new StackLayoutItem(UiStyle.HRow(row.ToArray())));

            UiStyle.AddRow(outer, UiStyle.Hr());
            UiStyle.AddRow(outer, UiStyle.SectionLabel("尺寸與縫隙", sectionFont, UiStyle.CAccent));

            NumericStepper MakeField(string key)
            {
                var box = new NumericStepper
                {
                    DecimalPlaces = 2,
                    MinValue = 0,
                    MaxValue = 100000,
                    Increment = Increments.TryGetValue(key, out var inc) ? inc : 1.0,
                    Width = 70,
                    Value = _opts.GetField(key),
                };
                box.ValueChanged += MakeOptHandler(key);
                _fields[key] = box;
                return box;
            }

            _gxLabel = UiStyle.MakeLabel("長度 (cm)");
            _gxLabel.Width = UiStyle.FieldLabelWidth;
            var gxBox = MakeField("gx");
            var gyLabel = UiStyle.MakeLabel("寬度 (cm)");
            gyLabel.Width = UiStyle.FieldLabelWidth;
            var gyBox = MakeField("gy");
            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(_gxLabel, gxBox, gyLabel, gyBox, null)));
            _fieldRows["gx"] = (_gxLabel, gxBox, null);
            _fieldRows["gy"] = (gyLabel, gyBox, null);

            var gwLabel = UiStyle.MakeLabel("縫寬 (cm)");
            gwLabel.Width = UiStyle.FieldLabelWidth;
            var gwBox = MakeField("gw");
            var gdLabel = UiStyle.MakeLabel("縫深 (cm)");
            gdLabel.Width = UiStyle.FieldLabelWidth;
            var gdBox = MakeField("gd");
            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(gwLabel, gwBox, gdLabel, gdBox, null)));
            _fieldRows["gw"] = (gwLabel, gwBox, null);
            _fieldRows["gd"] = (gdLabel, gdBox, null);

            var r2rLabel = UiStyle.MakeLabel("錯縫 %");
            r2rLabel.Width = UiStyle.FieldLabelWidth;
            var r2rBox = MakeField("r2r");
            var twaLabel = UiStyle.MakeLabel("斜紋角度");
            twaLabel.Width = UiStyle.FieldLabelWidth;
            var twaBox = MakeField("twa");
            _bwbLabel = UiStyle.MakeLabel("編織數");
            _bwbLabel.Width = UiStyle.FieldLabelWidth;
            _bwbOptions = new[] { "2", "3", "4" };
            _bwbDd = new DropDown { DataStore = _bwbOptions, Width = 55 };
            _bwbDd.SelectedIndexChanged += OnBwbChanged;
            outer.Items.Add(new StackLayoutItem(
                UiStyle.HRow(r2rLabel, r2rBox, twaLabel, twaBox, _bwbLabel, _bwbDd, null)));
            _fieldRows["r2r"] = (r2rLabel, r2rBox, new HashSet<string> { "Brick", "Tile", "Wedge" });
            _fieldRows["twa"] = (twaLabel, twaBox, new HashSet<string> { "Tweed" });

            var rotLabel = UiStyle.MakeLabel("旋轉角度");
            rotLabel.Width = UiStyle.FieldLabelWidth;
            _rotOptions = new[] { "0", "45", "90" };
            _rotDd = new DropDown { DataStore = _rotOptions, Width = 65 };
            _rotDd.SelectedIndexChanged += OnRotChanged;

            var sptLabel = UiStyle.MakeLabel("起始點");
            sptLabel.Width = UiStyle.FieldLabelWidth;
            _sptItems = Defaults.StartPointOptions;
            _sptDd = new DropDown { DataStore = _sptItems.Select(t => t.Label).ToArray(), Width = 65 };
            _sptDd.SelectedIndexChanged += OnSptChanged;
            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(rotLabel, _rotDd, sptLabel, _sptDd, null)));

            UiStyle.AddRow(outer, UiStyle.Hr());
            UiStyle.AddRow(outer, UiStyle.SectionLabel("材質", sectionFont, UiStyle.CAccent));

            var paintLabel = UiStyle.MakeLabel("上色模式");
            paintLabel.Width = UiStyle.FieldLabelWidth;
            _paintDd = new DropDown { DataStore = PaintModes.Select(t => t.Label).ToArray(), Width = 90, SelectedIndex = 0 };
            _paintDd.SelectedIndexChanged += OnPaintModeChanged;
            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(paintLabel, _paintDd, null)));

            var colorLabel = UiStyle.MakeLabel("磚片顏色");
            colorLabel.Width = UiStyle.FieldLabelWidth;
            _colorPicker = new ColorPicker { Value = UiStyle.DefaultTileColor };
            _colorPicker.ValueChanged += OnCustomColorChanged;
            _colorRow = UiStyle.HRow(colorLabel, _colorPicker, null);
            _colorRow.Visible = false;
            outer.Items.Add(new StackLayoutItem(_colorRow));

            _btnPickTextures = UiStyle.MakeButton("選擇材質圖片…");
            _btnPickTextures.Click += OnPickTextures;
            _textureStatus = UiStyle.MakeLabel("尚未選擇材質圖片", subFont, UiStyle.CTextSub);
            _texturePickRow = UiStyle.HRow(_btnPickTextures, _textureStatus, null);
            _texturePickRow.Visible = false;
            outer.Items.Add(new StackLayoutItem(_texturePickRow));

            _textureThumbContainer = UiStyle.VStack();
            _textureThumbContainer.Visible = false;
            outer.Items.Add(new StackLayoutItem(_textureThumbContainer));
            RebuildTextureThumbs();

            UiStyle.AddRow(outer, UiStyle.Hr());
            _textureSectionLabel = UiStyle.SectionLabel("紋理", sectionFont, UiStyle.CAccent);
            _textureSectionLabel.Visible = false;
            UiStyle.AddRow(outer, _textureSectionLabel);

            var alignLabel = UiStyle.MakeLabel("對齊邊");
            alignLabel.Width = UiStyle.FieldLabelWidth;
            _alignEdgeCheck = new CheckBox { Checked = false };
            _alignEdgeCheck.CheckedChanged += OnTextureOptChanged;
            var rposLabel = UiStyle.MakeLabel("隨機位置");
            rposLabel.Width = UiStyle.FieldLabelWidth;
            _randomPosCheck = new CheckBox { Checked = false };
            _randomPosCheck.CheckedChanged += OnTextureOptChanged;
            var rrotLabel = UiStyle.MakeLabel("隨機旋轉");
            rrotLabel.Width = UiStyle.FieldLabelWidth;
            _randomRotCheck = new CheckBox { Checked = false };
            _randomRotCheck.CheckedChanged += OnTextureOptChanged;
            _textureEffectRow = UiStyle.HRow(
                alignLabel, _alignEdgeCheck, rposLabel, _randomPosCheck, rrotLabel, _randomRotCheck, null);
            _textureEffectRow.Visible = false;
            outer.Items.Add(new StackLayoutItem(_textureEffectRow));

            UiStyle.AddRow(outer, UiStyle.Hr());
            UiStyle.AddRow(outer, UiStyle.SectionLabel("效果", sectionFont, UiStyle.CAccent));

            var bevelLabel = UiStyle.MakeLabel("倒斜角");
            bevelLabel.Width = UiStyle.FieldLabelWidth;
            _bevelCheck = new CheckBox { Checked = false };
            _bevelCheck.CheckedChanged += OnBevelToggle;
            _bevelSizeLabel = UiStyle.MakeLabel("角尺寸 (cm)");
            _bevelSizeLabel.Width = UiStyle.FieldLabelWidth;
            _bevelSizeBox = new NumericStepper
            {
                DecimalPlaces = 2, MinValue = 0.01, Increment = 0.1, Width = 70, Value = 0.3,
            };
            _bevelSizeBox.ValueChanged += OnBevelSizeChanged;
            _bevelSizeLabel.Visible = false;
            _bevelSizeBox.Visible = false;
            outer.Items.Add(new StackLayoutItem(
                UiStyle.HRow(bevelLabel, _bevelCheck, _bevelSizeLabel, _bevelSizeBox, null)));

            var defectLabel = UiStyle.MakeLabel("隨機缺陷");
            defectLabel.Width = UiStyle.FieldLabelWidth;
            _defectCheck = new CheckBox { Checked = false };
            _defectCheck.CheckedChanged += OnDefectToggle;
            _defectMinLabel = UiStyle.MakeLabel("最小角度");
            _defectMinLabel.Width = UiStyle.FieldLabelWidth;
            _defectMinBox = new NumericStepper
            {
                DecimalPlaces = 1, MinValue = 0.0, MaxValue = 45.0, Width = 55, Value = 0.0,
            };
            _defectMinBox.ValueChanged += OnDefectRangeChanged;
            _defectMaxLabel = UiStyle.MakeLabel("最大角度");
            _defectMaxLabel.Width = UiStyle.FieldLabelWidth;
            _defectMaxBox = new NumericStepper
            {
                DecimalPlaces = 1, MinValue = 0.0, MaxValue = 45.0, Width = 55, Value = 2.0,
            };
            _defectMaxBox.ValueChanged += OnDefectRangeChanged;
            foreach (Control c in new Control[] { _defectMinLabel, _defectMinBox, _defectMaxLabel, _defectMaxBox })
                c.Visible = false;
            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(
                defectLabel, _defectCheck, _defectMinLabel, _defectMinBox, _defectMaxLabel, _defectMaxBox, null)));

            var backLabel = UiStyle.MakeLabel("背面");
            backLabel.Width = UiStyle.FieldLabelWidth;
            _backFaceCheck = new CheckBox { Checked = false };
            _backFaceCheck.CheckedChanged += OnBackFaceToggle;
            var groupLabel = UiStyle.MakeLabel("獨立群組");
            groupLabel.Width = UiStyle.FieldLabelWidth;
            _keepGroupCheck = new CheckBox { Checked = true };
            _keepGroupCheck.CheckedChanged += OnKeepGroupToggle;
            outer.Items.Add(new StackLayoutItem(
                UiStyle.HRow(backLabel, _backFaceCheck, groupLabel, _keepGroupCheck, null)));

            UiStyle.AddRow(outer, UiStyle.Hr());
            var btnPreview = UiStyle.MakeButton("選擇物件");
            btnPreview.Click += OnPreview;
            _btnGenerate = UiStyle.MakeButton("產生磚塊", true);
            _btnGenerate.Click += OnGenerate;
            var btnUndo = UiStyle.MakeButton("復原");
            btnUndo.Click += OnUndo;
            var btnCalc = UiStyle.MakeButton("材料計算機");
            btnCalc.Click += OnCalc;
            var btnReset = UiStyle.MakeButton("重設");
            btnReset.Click += OnReset;
            var btnInfo = UiStyle.MakeButton("外掛說明");
            btnInfo.Click += OnInfo;

            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(btnPreview, _btnGenerate)));
            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(btnUndo, btnCalc, btnReset)));
            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(btnInfo)));

            _status = UiStyle.MakeLabel("請點選「產生磚塊」開始", subFont, UiStyle.CTextSub);
            UiStyle.AddRow(outer, _status);

            Content = outer;
            RefreshFields();
        }

        // ---------------------------------------------------------------- state sync

        private void RefreshFields()
        {
            foreach (var kv in _fields)
                kv.Value.Value = _opts.GetField(kv.Key);

            string curRot = ((int)_opts.Rot).ToString();
            int rotIdx = Array.IndexOf(_rotOptions, curRot);
            if (rotIdx >= 0)
                _rotDd.SelectedIndex = rotIdx;

            string curSpt = _opts.Spt ?? "corner";
            for (int k = 0; k < _sptItems.Length; k++)
            {
                if (_sptItems[k].Value == curSpt)
                    _sptDd.SelectedIndex = k;
            }

            string curBwb = _opts.Bwb.ToString();
            int bwbIdx = Array.IndexOf(_bwbOptions, curBwb);
            if (bwbIdx >= 0)
                _bwbDd.SelectedIndex = bwbIdx;

            UpdateFieldVisibility();
        }

        private void UpdateFieldVisibility()
        {
            string pid = _patternId;
            foreach (var kv in _fieldRows)
            {
                string key = kv.Key;
                var (lbl, box, pats) = kv.Value;
                bool visible = pats == null || pats.Contains(pid);
                if (key == "gy" && Defaults.NoGyPatterns.Contains(pid))
                    visible = false;
                if (key == "gw" && Defaults.NoGwPatterns.Contains(pid))
                    visible = false;
                lbl.Visible = visible;
                box.Visible = visible;
            }

            _gxLabel.Text = pid == "IrPoly" ? "密度 (cm)" : (Defaults.NoGyPatterns.Contains(pid) ? "邊長 (cm)" : "長度 (cm)");

            bool showBwb = pid == "BsktWv";
            _bwbLabel.Visible = showBwb;
            _bwbDd.Visible = showBwb;
        }

        // ---------------------------------------------------------------- handlers

        private void SelectPattern(string pid)
        {
            if (pid == _patternId)
                return;
            SetPattern(pid);
        }

        private void SetPattern(string pid)
        {
            _patternId = pid;
            _opts = StorageUtil.LoadPatternOpts(pid, Defaults.DefaultsFor(pid));
            foreach (var kv in _patternTiles)
                kv.Value.SetSelected(kv.Key == pid);
            RefreshFields();
            StorageUtil.SavePatternOpts(pid, _opts);
            UpdatePreview();
        }

        private EventHandler<EventArgs> MakeOptHandler(string key)
        {
            return (sender, e) =>
            {
                _opts.SetField(key, ((NumericStepper)sender).Value);
                StorageUtil.SavePatternOpts(_patternId, _opts);
                UpdatePreview();
            };
        }

        private void OnRotChanged(object sender, EventArgs e)
        {
            if (_rotDd.SelectedIndex < 0)
                return;
            _opts.Rot = double.Parse(_rotOptions[_rotDd.SelectedIndex]);
            StorageUtil.SavePatternOpts(_patternId, _opts);
            UpdatePreview();
        }

        private void OnSptChanged(object sender, EventArgs e)
        {
            if (_sptDd.SelectedIndex < 0)
                return;
            _opts.Spt = _sptItems[_sptDd.SelectedIndex].Value;
            StorageUtil.SavePatternOpts(_patternId, _opts);
            UpdatePreview();
        }

        private void OnBwbChanged(object sender, EventArgs e)
        {
            if (_bwbDd.SelectedIndex < 0)
                return;
            _opts.Bwb = int.Parse(_bwbOptions[_bwbDd.SelectedIndex]);
            StorageUtil.SavePatternOpts(_patternId, _opts);
            UpdatePreview();
        }

        private void OnPaintModeChanged(object sender, EventArgs e)
        {
            int idx = _paintDd.SelectedIndex;
            if (idx < 0 || idx >= PaintModes.Length)
                return;
            _paintMode = PaintModes[idx].Value;
            _colorRow.Visible = _paintMode == "custom_color";
            bool showTexture = _paintMode == "texture";
            _texturePickRow.Visible = showTexture;
            _textureThumbContainer.Visible = showTexture;
            _textureSectionLabel.Visible = showTexture;
            _textureEffectRow.Visible = showTexture;
            UpdatePreview();
        }

        private void OnCustomColorChanged(object sender, EventArgs e)
        {
            _customColor = UiStyle.ToSysColor(_colorPicker.Value);
            UpdatePreview();
        }

        private void OnTextureOptChanged(object sender, EventArgs e)
        {
            _textureOpts = new TextureOptions
            {
                AlignEdge = _alignEdgeCheck.Checked ?? false,
                RandomPosition = _randomPosCheck.Checked ?? false,
                RandomRotate = _randomRotCheck.Checked ?? false,
            };
        }

        private void OnBevelToggle(object sender, EventArgs e)
        {
            _bevelEnabled = _bevelCheck.Checked ?? false;
            _bevelSizeLabel.Visible = _bevelEnabled;
            _bevelSizeBox.Visible = _bevelEnabled;
            UpdatePreview();
        }

        private void OnBevelSizeChanged(object sender, EventArgs e)
        {
            _bevelSize = _bevelSizeBox.Value;
            UpdatePreview();
        }

        private void OnDefectToggle(object sender, EventArgs e)
        {
            _randomDefect = _defectCheck.Checked ?? false;
            foreach (Control c in new Control[] { _defectMinLabel, _defectMinBox, _defectMaxLabel, _defectMaxBox })
                c.Visible = _randomDefect;
        }

        private void OnDefectRangeChanged(object sender, EventArgs e)
        {
            _defectMin = _defectMinBox.Value;
            _defectMax = Math.Max(_defectMaxBox.Value, _defectMin);
        }

        private void OnBackFaceToggle(object sender, EventArgs e)
        {
            _backFace = _backFaceCheck.Checked ?? false;
        }

        private void OnKeepGroupToggle(object sender, EventArgs e)
        {
            _keepGroup = _keepGroupCheck.Checked ?? false;
        }

        private void OnPickTextures(object sender, EventArgs e)
        {
            var dlg = new OpenFileDialog { Title = "選擇材質圖片", MultiSelect = true };
            var f = new FileFilter("圖片檔案", ImageExtensions);
            dlg.Filters.Add(f);
            dlg.CurrentFilter = f;
            var result = dlg.ShowDialog(this);
            if (result != DialogResult.Ok)
                return;
            var paths = dlg.Filenames?.Where(p => !string.IsNullOrEmpty(p)).ToList() ?? new List<string>();
            if (paths.Count == 0)
                return;
            var existing = new HashSet<string>(_texturePaths);
            int skipped = 0;
            foreach (var p in paths)
            {
                if (existing.Contains(p))
                    continue;
                if (_texturePaths.Count >= MaxTextures)
                {
                    skipped++;
                    continue;
                }
                _texturePaths.Add(p);
                existing.Add(p);
            }
            MaterialsUtil.ResetCache();
            RebuildTextureThumbs();
            UpdatePreview();
            if (skipped > 0)
                MessageBox.Show(this, $"最多只能選 {MaxTextures} 張材質圖片，已略過 {skipped} 張。", "DB3D-Floora");
        }

        private void RemoveTexture(string path)
        {
            _texturePaths.Remove(path);
            MaterialsUtil.ResetCache();
            RebuildTextureThumbs();
            UpdatePreview();
        }

        private void RebuildTextureThumbs()
        {
            _textureThumbContainer.Items.Clear();
            var rowControls = new List<Control>();
            foreach (var path in _texturePaths)
                rowControls.Add(new TextureThumb(path, RemoveTexture));
            while (rowControls.Count < MaxTextures)
                rowControls.Add(TextureThumb.EmptySlot());
            _textureThumbContainer.Items.Add(new StackLayoutItem(UiStyle.HRow(rowControls.ToArray())));

            _textureStatus.Text = _texturePaths.Count > 0
                ? $"已選 {_texturePaths.Count}／{MaxTextures} 張材質圖片"
                : $"尚未選擇材質圖片（最多 {MaxTextures} 張）";
        }

        // ------------------------------------------------------------ 預覽

        private void ClearPreview(Rhino.RhinoDoc doc = null)
        {
            if (_previewIds.Count == 0)
                return;
            doc = doc ?? Rhino.RhinoDoc.ActiveDoc;
            foreach (var gid in _previewIds)
            {
                try { doc.Objects.Delete(gid, true); } catch { }
            }
            _previewIds = new List<Guid>();
        }

        private void UpdatePreview()
        {
            if (_previewBoundary == null)
                return;
            var doc = Rhino.RhinoDoc.ActiveDoc;
            ClearPreview(doc);
            List<Curve> curves;
            try
            {
                curves = Patterns.Generate(_patternId, _previewBoundary, _previewPlane, _opts, _previewAnchor);
            }
            catch (Exception ex)
            {
                doc.Views.Redraw();
                _status.Text = $"預覽失敗：{ex.Message}";
                return;
            }

            int layerIdx = TileService.PreviewLayer(doc);
            var attrs = new Rhino.DocObjects.ObjectAttributes { LayerIndex = layerIdx };
            var ids = new List<Guid>();
            foreach (var c in curves)
            {
                Guid gid;
                try { gid = doc.Objects.AddCurve(c, attrs); } catch { continue; }
                if (gid != Guid.Empty)
                    ids.Add(gid);
            }
            _previewIds = ids;
            doc.Views.Redraw();
            _status.Text = $"預覽中：{ids.Count} 片磚（調整參數會即時更新，按「產生磚塊」確認）";
        }

        private void OnPreview(object sender, EventArgs e)
        {
            var objRef = TileService.PickFace();
            if (objRef == null)
            {
                _status.Text = "已取消選取";
                return;
            }

            var (boundary, plane) = GeometryUtil.GetBoundaryAndPlane(objRef);
            if (boundary == null)
            {
                MessageBox.Show(this, "選取的物件不是平面（面或封閉平面曲線），請重新選取。", "DB3D-Floora");
                return;
            }

            var anchor = InteractivePickAnchor(boundary, plane);
            if (anchor == null)
            {
                _status.Text = "已取消選取起磚點";
                return;
            }

            _previewBoundary = boundary;
            _previewPlane = plane;
            _previewAnchor = anchor;
            UpdatePreview();
        }

        /// <summary>滑鼠在面上移動時即時畫出鋪磚預覽（DynamicDraw，純畫面顯示，不建立文件物件），
        /// 左鍵點擊回傳該點作為起磚點；按 Esc/右鍵取消則回傳 null，畫面上不會留下任何東西。</summary>
        private Point3d? InteractivePickAnchor(Curve boundary, Plane plane)
        {
            var gp = new GetPoint();
            gp.SetCommandPrompt("移動滑鼠預覽鋪磚位置，左鍵點擊決定起磚點（Esc 取消）");
            try { gp.Constrain(plane, false); } catch { }

            (double X, double Y, double Z)? cacheKey = null;
            List<Curve> cacheCurves = new List<Curve>();
            var color = System.Drawing.Color.FromArgb(0x2A, 0x5F, 0x8A);

            void DynamicDraw(object sender, GetPointDrawEventArgs de)
            {
                Point3d pt;
                try { pt = plane.ClosestPoint(de.CurrentPoint); } catch { return; }
                var key = (Math.Round(pt.X, 1), Math.Round(pt.Y, 1), Math.Round(pt.Z, 1));
                if (cacheKey == null || cacheKey.Value != key)
                {
                    cacheKey = key;
                    try { cacheCurves = Patterns.Generate(_patternId, boundary, plane, _opts, pt); }
                    catch { cacheCurves = new List<Curve>(); }
                }
                foreach (var c in cacheCurves)
                {
                    try { de.Display.DrawCurve(c, color, 2); } catch { }
                }
            }

            gp.DynamicDraw += DynamicDraw;
            Rhino.Input.GetResult result;
            try { result = gp.Get(); }
            finally { gp.DynamicDraw -= DynamicDraw; }

            if (result != Rhino.Input.GetResult.Point)
                return null;
            return plane.ClosestPoint(gp.Point());
        }

        // ------------------------------------------------------------ 產生 / 復原 / 計算機 / 重設

        private void OnGenerate(object sender, EventArgs e)
        {
            Curve boundary;
            Plane plane;
            Point3d? anchorPt;

            if (_previewBoundary != null)
            {
                boundary = _previewBoundary;
                plane = _previewPlane;
                anchorPt = _previewAnchor;
            }
            else
            {
                var objRef = TileService.PickFace();
                if (objRef == null)
                {
                    _status.Text = "已取消選取";
                    return;
                }

                (boundary, plane) = GeometryUtil.GetBoundaryAndPlane(objRef);
                if (boundary == null)
                {
                    MessageBox.Show(this, "選取的物件不是平面（面或封閉平面曲線），請重新選取。", "DB3D-Floora");
                    return;
                }

                anchorPt = null;
                if (_opts.Spt == "pick")
                {
                    var gp = new GetPoint();
                    gp.SetCommandPrompt("請點選圖案起始點");
                    var result = gp.Get();
                    if (result != Rhino.Input.GetResult.Point)
                    {
                        _status.Text = "已取消選取起始點";
                        return;
                    }
                    anchorPt = plane.ClosestPoint(gp.Point());
                }
            }

            List<Curve> curves;
            try
            {
                curves = Patterns.Generate(_patternId, boundary, plane, _opts, anchorPt);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"產生磚塊失敗：{ex.Message}", "DB3D-Floora");
                return;
            }

            if (curves.Count == 0)
            {
                _status.Text = "沒有產生任何磚片，請檢查尺寸設定";
                return;
            }

            var doc = Rhino.RhinoDoc.ActiveDoc;
            ClearPreview(doc);
            int layerIdx = TileService.PatternLayer(doc, _patternId);
            double gd = _opts.Gd;

            var refPlane = new Plane(plane.Origin, plane.XAxis, plane.YAxis);
            double totalRot = _opts.Rot + (Patterns.PatternExtraRot.TryGetValue(_patternId, out var extra) ? extra : 0.0);
            if (totalRot != 0)
                refPlane.Rotate(totalRot * Math.PI / 180.0, refPlane.ZAxis);
            double texW = _opts.Gx;
            double texH = _opts.Gy != 0 ? _opts.Gy : texW;

            var batch = new List<Guid>();
            var buildOpts = new TileService.TileBuildOptions
            {
                PaintMode = _paintMode,
                TexturePaths = _texturePaths,
                CustomColor = _customColor,
                BevelEnabled = _bevelEnabled,
                BevelSize = _bevelSize,
                RandomDefect = _randomDefect,
                DefectMin = _defectMin,
                DefectMax = _defectMax,
                BackFace = _backFace,
            };
            foreach (var c in curves)
            {
                var gid = TileService.AddTile(doc, c, layerIdx, gd, buildOpts);
                if (gid.HasValue && gid.Value != Guid.Empty)
                {
                    if (_paintMode == "texture" && _texturePaths.Count > 0)
                        MaterialsUtil.ApplyTextureMapping(doc, gid.Value, refPlane, texW, texH, _textureOpts);
                    batch.Add(gid.Value);
                }
            }

            if (_keepGroup && batch.Count > 0)
            {
                try { doc.Groups.Add(batch); } catch { }
            }

            doc.Views.Redraw();

            _undoStack.Add(batch);
            if (_undoStack.Count > MaxUndo)
                _undoStack.RemoveAt(0);

            StorageUtil.SavePatternOpts(_patternId, _opts);
            _previewBoundary = null;
            _previewAnchor = null;
            _status.Text = $"已產生 {batch.Count} 片磚（{(Defaults.NamesTw.TryGetValue(_patternId, out var nm) ? nm : _patternId)}）";
        }

        private void OnUndo(object sender, EventArgs e)
        {
            if (_undoStack.Count == 0)
            {
                _status.Text = "沒有可復原的操作";
                return;
            }
            var batch = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            var doc = Rhino.RhinoDoc.ActiveDoc;
            foreach (var gid in batch)
                doc.Objects.Delete(gid, true);
            doc.Views.Redraw();
            _status.Text = $"已復原上一步（剩餘 {_undoStack.Count} 步可復原）";
        }

        private void OnCalc(object sender, EventArgs e)
        {
            List<LayerStat> stats;
            try
            {
                stats = CalculatorUtil.LayerAreaStats(Rhino.RhinoDoc.ActiveDoc);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"計算失敗：{ex.Message}", "DB3D-Floora");
                return;
            }
            new CalculatorForm(stats).Show();
        }

        private void OnInfo(object sender, EventArgs e)
        {
            new InfoForm().Show();
        }

        private void OnReset(object sender, EventArgs e)
        {
            StorageUtil.ResetAll();
            ClearPreview();
            _previewBoundary = null;
            _previewAnchor = null;
            SetPattern("Tile");
            _undoStack.Clear();
            _status.Text = "已重設為預設值";
        }

        private void OnClosed(object sender, EventArgs e)
        {
            ClearPreview();
            _instance = null;
        }
    }
}
