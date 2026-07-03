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
        private static readonly string[] PaintModeValues = { "current", "custom_color", "texture" };
        private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff" };
        private const int MaxTextures = 5;
        private const int GridCols = 6;

        private static readonly Dictionary<string, double> Increments = new Dictionary<string, double>
        {
            {"gx", 1.0}, {"gy", 1.0}, {"gw", 0.1}, {"gd", 0.1}, {"r2r", 5.0}, {"twa", 1.0},
        };

        private static readonly Dictionary<string, (string Zh, string En)> TileLabels = new Dictionary<string, (string, string)>
        {
            {"HpScth1", ("格子1", "H-S 1")}, {"HpScth2", ("格子2", "H-S 2")},
            {"HpScth3", ("格子3", "H-S 3")}, {"HpScth4", ("格子4", "H-S 4")},
            {"IrPoly", ("不規則", "Irregular")},
        };

        private static string TileLabel(string patternId)
        {
            if (TileLabels.TryGetValue(patternId, out var pair))
                return Strings.Current == AppLanguage.En ? pair.En : pair.Zh;
            return Defaults.DisplayName(patternId);
        }

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
        private Guid? _previewSourceId;
        private List<Guid> _previewIds = new List<Guid>();

        private Label _gxLabel;
        private Label _bwbLabel;
        private DropDown _bwbDd;
        private string[] _bwbOptions;
        private DropDown _rotDd;
        private string[] _rotOptions;
        private DropDown _sptDd;
        private string[] _sptItems;
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
        private StackLayout _bevelSizeRow;
        private CheckBox _defectCheck;
        private Label _defectMinLabel;
        private NumericStepper _defectMinBox;
        private Label _defectMaxLabel;
        private NumericStepper _defectMaxBox;
        private StackLayout _defectRangeRow;
        private CheckBox _backFaceCheck;
        private CheckBox _keepGroupCheck;
        private Button _btnGenerate;
        private Label _status;
        private StackLayout _contentStack;

        public FlooraForm()
        {
            Strings.Current = StorageUtil.LoadLanguage(Strings.Current);

            Title = Strings.T("app.title");
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
            // BuildLayout 裡第一次呼叫 RefitWindow 時，視窗還沒真的顯示、原生控制項還沒完成排版，
            // 量出來的 GetPreferredSize 在部分平台會偏小，導致視窗一開起來下面的選項被擋住。
            // Shown 時機（原生視窗已經跑完一次真正的排版）再量一次，才會準。
            Shown += (s, e) => RefitWindow();
        }

        // ---------------------------------------------------------------- layout

        private void BuildLayout()
        {
            var outer = UiStyle.VStack();

            var sectionFont = UiStyle.F("section");
            var subFont = UiStyle.F("body");

            var langBtn = new Button { Text = Strings.T("btn.language") };
            langBtn.Click += OnToggleLanguage;
            UiStyle.AddRow(outer, UiStyle.HRow(null, langBtn));

            // 原本這裡是藍色的「DB3D Floora for Rhino by Onon.Nihow」標題，改成放即時狀態說明文字
            // （原本在視窗最下面的那行灰字），標題文字本身已經在視窗的 Title 列顯示過一次，不用在內容裡重複。
            _status = UiStyle.MakeLabel(Strings.T("status.ready"), UiStyle.F("subhead"), UiStyle.CTextSub, TextAlignment.Left);
            UiStyle.AddRow(outer, _status);
            UiStyle.AddRow(outer, UiStyle.Hr());

            UiStyle.AddRow(outer, UiStyle.SectionLabel(Strings.T("section.pattern"), sectionFont, UiStyle.CAccent));
            var flatOrder = new List<string>();
            foreach (var (_, ids) in Defaults.CategoriesTw)
                flatOrder.AddRange(ids);
            var row = new List<Control>();
            foreach (var pid in flatOrder)
            {
                string labelText = TileLabel(pid);
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
            UiStyle.AddRow(outer, UiStyle.SectionLabel(Strings.T("section.dimensions"), sectionFont, UiStyle.CAccent));

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

            _gxLabel = UiStyle.MakeLabel(Strings.T("field.length"));
            _gxLabel.Width = UiStyle.FieldLabelWidth;
            var gxBox = MakeField("gx");
            var gyLabel = UiStyle.MakeLabel(Strings.T("field.width"));
            gyLabel.Width = UiStyle.FieldLabelWidth;
            var gyBox = MakeField("gy");
            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(_gxLabel, gxBox, gyLabel, gyBox, null)));
            _fieldRows["gx"] = (_gxLabel, gxBox, null);
            _fieldRows["gy"] = (gyLabel, gyBox, null);

            var gwLabel = UiStyle.MakeLabel(Strings.T("field.groutWidth"));
            gwLabel.Width = UiStyle.FieldLabelWidth;
            var gwBox = MakeField("gw");
            var gdLabel = UiStyle.MakeLabel(Strings.T("field.groutDepth"));
            gdLabel.Width = UiStyle.FieldLabelWidth;
            var gdBox = MakeField("gd");
            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(gwLabel, gwBox, gdLabel, gdBox, null)));
            _fieldRows["gw"] = (gwLabel, gwBox, null);
            _fieldRows["gd"] = (gdLabel, gdBox, null);

            var r2rLabel = UiStyle.MakeLabel(Strings.T("field.staggerPct"));
            r2rLabel.Width = UiStyle.FieldLabelWidth;
            var r2rBox = MakeField("r2r");
            var twaLabel = UiStyle.MakeLabel(Strings.T("field.tweedAngle"));
            twaLabel.Width = UiStyle.FieldLabelWidth;
            var twaBox = MakeField("twa");
            _bwbLabel = UiStyle.MakeLabel(Strings.T("field.weaveCount"));
            _bwbLabel.Width = UiStyle.FieldLabelWidth;
            _bwbOptions = new[] { "2", "3", "4" };
            _bwbDd = new DropDown { DataStore = _bwbOptions, Width = 55 };
            _bwbDd.SelectedIndexChanged += OnBwbChanged;
            outer.Items.Add(new StackLayoutItem(
                UiStyle.HRow(r2rLabel, r2rBox, twaLabel, twaBox, _bwbLabel, _bwbDd, null)));
            _fieldRows["r2r"] = (r2rLabel, r2rBox, new HashSet<string> { "Brick", "Tile", "Wedge" });
            _fieldRows["twa"] = (twaLabel, twaBox, new HashSet<string> { "Tweed" });

            var rotLabel = UiStyle.MakeLabel(Strings.T("field.rotation"));
            rotLabel.Width = UiStyle.FieldLabelWidth;
            _rotOptions = new[] { "0", "45", "90" };
            _rotDd = new DropDown { DataStore = _rotOptions, Width = 65 };
            _rotDd.SelectedIndexChanged += OnRotChanged;

            var sptLabel = UiStyle.MakeLabel(Strings.T("field.startPoint"));
            sptLabel.Width = UiStyle.FieldLabelWidth;
            _sptItems = Defaults.StartPointValues;
            _sptDd = new DropDown { DataStore = _sptItems.Select(v => Strings.T("spt." + v)).ToArray(), Width = 65 };
            _sptDd.SelectedIndexChanged += OnSptChanged;
            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(rotLabel, _rotDd, sptLabel, _sptDd, null)));

            UiStyle.AddRow(outer, UiStyle.Hr());
            UiStyle.AddRow(outer, UiStyle.SectionLabel(Strings.T("section.material"), sectionFont, UiStyle.CAccent));

            var paintLabel = UiStyle.MakeLabel(Strings.T("field.paintMode"));
            paintLabel.Width = UiStyle.FieldLabelWidth;
            _paintDd = new DropDown { DataStore = PaintModeValues.Select(v => Strings.T("paint." + v)).ToArray(), Width = 90, SelectedIndex = 0 };
            _paintDd.SelectedIndexChanged += OnPaintModeChanged;
            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(paintLabel, _paintDd, null)));

            var colorLabel = UiStyle.MakeLabel(Strings.T("field.tileColor"));
            colorLabel.Width = UiStyle.FieldLabelWidth;
            _colorPicker = new ColorPicker { Value = UiStyle.DefaultTileColor };
            _colorPicker.ValueChanged += OnCustomColorChanged;
            _colorRow = UiStyle.HRow(colorLabel, _colorPicker, null);
            _colorRow.Visible = false;
            outer.Items.Add(new StackLayoutItem(_colorRow));

            _btnPickTextures = UiStyle.MakeButton(Strings.T("btn.pickTextures"));
            _btnPickTextures.Click += OnPickTextures;
            _textureStatus = UiStyle.MakeLabel(Strings.T("status.noTexturesChosen", MaxTextures), subFont, UiStyle.CTextSub);
            _texturePickRow = UiStyle.HRow(_btnPickTextures, _textureStatus, null);
            _texturePickRow.Visible = false;
            outer.Items.Add(new StackLayoutItem(_texturePickRow));

            _textureThumbContainer = UiStyle.VStack();
            _textureThumbContainer.Visible = false;
            outer.Items.Add(new StackLayoutItem(_textureThumbContainer));
            RebuildTextureThumbs();

            UiStyle.AddRow(outer, UiStyle.Hr());
            _textureSectionLabel = UiStyle.SectionLabel(Strings.T("section.texture"), sectionFont, UiStyle.CAccent);
            _textureSectionLabel.Visible = false;
            UiStyle.AddRow(outer, _textureSectionLabel);

            var alignLabel = UiStyle.MakeLabel(Strings.T("field.alignEdge"));
            alignLabel.Width = UiStyle.FieldLabelWidth;
            _alignEdgeCheck = new CheckBox { Checked = false };
            _alignEdgeCheck.CheckedChanged += OnTextureOptChanged;
            var rposLabel = UiStyle.MakeLabel(Strings.T("field.randomPosition"));
            rposLabel.Width = UiStyle.FieldLabelWidth;
            _randomPosCheck = new CheckBox { Checked = false };
            _randomPosCheck.CheckedChanged += OnTextureOptChanged;
            var rrotLabel = UiStyle.MakeLabel(Strings.T("field.randomRotate"));
            rrotLabel.Width = UiStyle.FieldLabelWidth;
            _randomRotCheck = new CheckBox { Checked = false };
            _randomRotCheck.CheckedChanged += OnTextureOptChanged;
            _textureEffectRow = UiStyle.HRow(
                alignLabel, _alignEdgeCheck, rposLabel, _randomPosCheck, rrotLabel, _randomRotCheck, null);
            _textureEffectRow.Visible = false;
            outer.Items.Add(new StackLayoutItem(_textureEffectRow));

            UiStyle.AddRow(outer, UiStyle.Hr());
            UiStyle.AddRow(outer, UiStyle.SectionLabel(Strings.T("section.effects"), sectionFont, UiStyle.CAccent));

            // 四個效果開關排成 2*2：倒斜角／隨機缺陷 一列，背面／獨立群組 一列；
            // 各自的細節欄位（角尺寸、最小/最大角度）另外放在下面，開關打開時才顯示。
            var bevelLabel = UiStyle.MakeLabel(Strings.T("field.bevel"));
            bevelLabel.Width = UiStyle.FieldLabelWidth;
            _bevelCheck = new CheckBox { Checked = false };
            _bevelCheck.CheckedChanged += OnBevelToggle;

            var defectLabel = UiStyle.MakeLabel(Strings.T("field.randomDefect"));
            defectLabel.Width = UiStyle.FieldLabelWidth;
            _defectCheck = new CheckBox { Checked = false };
            _defectCheck.CheckedChanged += OnDefectToggle;

            outer.Items.Add(new StackLayoutItem(
                UiStyle.HRow(bevelLabel, _bevelCheck, defectLabel, _defectCheck, null)));

            var backLabel = UiStyle.MakeLabel(Strings.T("field.backFace"));
            backLabel.Width = UiStyle.FieldLabelWidth;
            _backFaceCheck = new CheckBox { Checked = false };
            _backFaceCheck.CheckedChanged += OnBackFaceToggle;
            var groupLabel = UiStyle.MakeLabel(Strings.T("field.keepGroup"));
            groupLabel.Width = UiStyle.FieldLabelWidth;
            _keepGroupCheck = new CheckBox { Checked = true };
            _keepGroupCheck.CheckedChanged += OnKeepGroupToggle;
            outer.Items.Add(new StackLayoutItem(
                UiStyle.HRow(backLabel, _backFaceCheck, groupLabel, _keepGroupCheck, null)));

            _bevelSizeLabel = UiStyle.MakeLabel(Strings.T("field.bevelSize"));
            _bevelSizeLabel.Width = UiStyle.FieldLabelWidth;
            _bevelSizeBox = new NumericStepper
            {
                DecimalPlaces = 2, MinValue = 0.01, Increment = 0.1, Width = 70, Value = 0.3,
            };
            _bevelSizeBox.ValueChanged += OnBevelSizeChanged;
            var bevelSizeRow = UiStyle.HRow(_bevelSizeLabel, _bevelSizeBox, null);
            bevelSizeRow.Visible = false;
            _bevelSizeLabel.Visible = false;
            _bevelSizeBox.Visible = false;
            outer.Items.Add(new StackLayoutItem(bevelSizeRow));
            _bevelSizeRow = bevelSizeRow;

            _defectMinLabel = UiStyle.MakeLabel(Strings.T("field.defectMin"));
            _defectMinLabel.Width = UiStyle.FieldLabelWidth;
            _defectMinBox = new NumericStepper
            {
                DecimalPlaces = 1, MinValue = 0.0, MaxValue = 45.0, Width = 55, Value = 0.0,
            };
            _defectMinBox.ValueChanged += OnDefectRangeChanged;
            _defectMaxLabel = UiStyle.MakeLabel(Strings.T("field.defectMax"));
            _defectMaxLabel.Width = UiStyle.FieldLabelWidth;
            _defectMaxBox = new NumericStepper
            {
                DecimalPlaces = 1, MinValue = 0.0, MaxValue = 45.0, Width = 55, Value = 2.0,
            };
            _defectMaxBox.ValueChanged += OnDefectRangeChanged;
            var defectRangeRow = UiStyle.HRow(_defectMinLabel, _defectMinBox, _defectMaxLabel, _defectMaxBox, null);
            defectRangeRow.Visible = false;
            foreach (Control c in new Control[] { _defectMinLabel, _defectMinBox, _defectMaxLabel, _defectMaxBox })
                c.Visible = false;
            outer.Items.Add(new StackLayoutItem(defectRangeRow));
            _defectRangeRow = defectRangeRow;

            UiStyle.AddRow(outer, UiStyle.Hr());
            var btnPreview = UiStyle.MakeButton(Strings.T("btn.pickObject"));
            btnPreview.Click += OnPreview;
            _btnGenerate = UiStyle.MakeButton(Strings.T("btn.generate"), true);
            _btnGenerate.Click += OnGenerate;
            var btnUndo = UiStyle.MakeButton(Strings.T("btn.undo"));
            btnUndo.Click += OnUndo;
            var btnCalc = UiStyle.MakeButton(Strings.T("btn.calculator"));
            btnCalc.Click += OnCalc;
            var btnReset = UiStyle.MakeButton(Strings.T("btn.reset"));
            btnReset.Click += OnReset;
            var btnInfo = UiStyle.MakeButton(Strings.T("btn.info"));
            btnInfo.Click += OnInfo;

            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(btnPreview, _btnGenerate)));
            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(btnUndo, btnCalc, btnReset)));
            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(btnInfo)));

            string versionText = UiStyle.PluginVersionText();
            if (!string.IsNullOrEmpty(versionText))
            {
                var versionLabel = UiStyle.MakeLabel(versionText, UiStyle.F("caption"), UiStyle.CTextSub, TextAlignment.Center);
                outer.Items.Add(new StackLayoutItem(UiStyle.HRow(null, versionLabel, null)));
            }

            // 用 Scrollable 包住整個版面：視窗本身高度受限於螢幕（RefitWindow 會把高度上限
            // 卡在螢幕可用高度以內），內容如果比螢幕還高，靠這層 Scrollable 讓使用者用捲動的
            // 方式看到全部選項，而不是直接被裁掉、永遠看不到最下面的按鈕。
            _contentStack = outer;
            Content = new Scrollable { Content = outer, Border = BorderType.None };
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
                if (_sptItems[k] == curSpt)
                    _sptDd.SelectedIndex = k;
            }

            string curBwb = _opts.Bwb.ToString();
            int bwbIdx = Array.IndexOf(_bwbOptions, curBwb);
            if (bwbIdx >= 0)
                _bwbDd.SelectedIndex = bwbIdx;

            UpdateFieldVisibility();
            RefitWindow();
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

            _gxLabel.Text = pid == "IrPoly" ? Strings.T("field.length.irpoly") : (Defaults.NoGyPatterns.Contains(pid) ? Strings.T("field.length.edge") : Strings.T("field.length"));

            bool showBwb = pid == "BsktWv";
            _bwbLabel.Visible = showBwb;
            _bwbDd.Visible = showBwb;
        }

        // ---------------------------------------------------------------- handlers

        private void SelectPattern(string pid)
        {
            // 就算點的是目前已經選取的圖案也要重新觸發選物件：拿掉「同一個圖案就直接 return」的判斷，
            // 不然點到目前就已經選取的那個圖案磚塊時什麼事都不會發生，使用者得先點別的圖案再點回來，
            // 造成「有時候要點兩次」的假象。
            if (pid != _patternId)
                SetPattern(pid, refreshPreview: false);
            // 選圖案的同一個動作直接接著選物件：每次選圖案都馬上跳出選面／即時預覽起磚點流程，
            // 不只是沿用先前選過的面直接顯示圖案——使用者要的是選完圖案就接著選曲線或曲面。
            PickObjectAndPreview();
        }

        /// <summary>refreshPreview=false 時跳過用「舊」_previewBoundary 畫預覽這一步——
        /// 從 SelectPattern 呼叫時後面馬上會接著 PickObjectAndPreview 重新選面，
        /// 這裡先畫一次舊邊界的預覽純粹是浪費，而且畫出來的預覽線本身也是可被選取的 Curve，
        /// 使用者緊接著要選新的曲線／曲面時反而可能誤選到這些殘留的舊預覽線，導致新選取的物件套用不上圖案。</summary>
        private void SetPattern(string pid, bool refreshPreview = true)
        {
            _patternId = pid;
            _opts = StorageUtil.LoadPatternOpts(pid, Defaults.DefaultsFor(pid));
            foreach (var kv in _patternTiles)
                kv.Value.SetSelected(kv.Key == pid);
            RefreshFields();
            StorageUtil.SavePatternOpts(pid, _opts);
            if (refreshPreview)
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
            _opts.Spt = _sptItems[_sptDd.SelectedIndex];
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

        /// <summary>視窗大小是在第一次 Show() 的時候量好就固定住，之後單純切換某幾列的 Visible
        /// 不會讓視窗自動跟著長高——內容還是會照常排版，只是超出當時視窗高度的部分會被擠壓在下面看不到。
        /// 所以只要有列的 Visible 狀態改變（材質模式、倒斜角、隨機缺陷、切換圖案時欄位增減），
        /// 都要呼叫這個方法讓視窗依目前實際內容重新量一次高度。只會長高不會縮小，
        /// 避免使用者手動拉大視窗後又被縮回去。</summary>
        private void RefitWindow()
        {
            try
            {
                var pref = _contentStack.GetPreferredSize();
                // GetPreferredSize 量出來的尺寸跟 Scrollable 實際能給的可視區域常常會差個幾 px
                // （邊框、原生控制項排版誤差），太貼近的話內容會多出那幾 px 而冒出捲動軸。
                // 這裡故意多留一點緩衝，讓正常情況下視窗夠大、看不到捲動軸，
                // 捲動只在內容真的長到超過螢幕高度上限時才會用到。
                const int bufferW = 24;
                const int bufferH = 32;
                var prefSize = new Size(
                    (int)Math.Ceiling(pref.Width) + bufferW,
                    (int)Math.Ceiling(pref.Height) + bufferH);
                var cur = ClientSize;
                int targetW = Math.Max(cur.Width, prefSize.Width);
                int targetH = Math.Max(cur.Height, prefSize.Height);

                // 高度不能無限長高：超過螢幕可用高度時卡住，剩下的內容交給 Scrollable 捲動看，
                // 不然視窗會被 OS 直接裁到螢幕邊界，最下面的按鈕永遠按不到也看不到。
                var screenBounds = Screen.PrimaryScreen?.WorkingArea ?? Screen.PrimaryScreen?.Bounds;
                if (screenBounds.HasValue)
                {
                    int maxH = (int)screenBounds.Value.Height - 80;
                    if (maxH > 200)
                        targetH = Math.Min(targetH, maxH);
                }

                ClientSize = new Size(targetW, targetH);
            }
            catch { }
        }

        private void OnPaintModeChanged(object sender, EventArgs e)
        {
            int idx = _paintDd.SelectedIndex;
            if (idx < 0 || idx >= PaintModeValues.Length)
                return;
            _paintMode = PaintModeValues[idx];
            _colorRow.Visible = _paintMode == "custom_color";
            bool showTexture = _paintMode == "texture";
            _texturePickRow.Visible = showTexture;
            _textureThumbContainer.Visible = showTexture;
            _textureSectionLabel.Visible = showTexture;
            _textureEffectRow.Visible = showTexture;
            RefitWindow();
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
            _bevelSizeRow.Visible = _bevelEnabled;
            RefitWindow();
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
            _defectRangeRow.Visible = _randomDefect;
            RefitWindow();
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
            // 部分平台開完系統的「選取檔案」視窗後，母視窗（這個介面本身）的位置／大小
            // 會被 OS 悄悄改掉，關閉檔案選取視窗後要強制還原成原本的位置/大小，
            // 之後再用 RefitWindow 依實際縮圖內容重新量一次高度（縮圖跟空白佔位框大小不一樣）。
            var savedLocation = Location;
            var savedSize = ClientSize;

            var dlg = new OpenFileDialog { Title = Strings.T("dlg.chooseTextures"), MultiSelect = true };
            var f = new FileFilter(Strings.T("dlg.imageFiles"), ImageExtensions);
            dlg.Filters.Add(f);
            dlg.CurrentFilter = f;
            var result = dlg.ShowDialog(this);

            Location = savedLocation;
            ClientSize = savedSize;

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
            RefitWindow();
            UpdatePreview();
            if (skipped > 0)
                MessageBox.Show(this, Strings.T("msg.tooManyTextures", MaxTextures, skipped), Strings.T("dlg.appName"));
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
                ? Strings.T("status.texturesChosen", _texturePaths.Count, MaxTextures)
                : Strings.T("status.noTexturesChosen", MaxTextures);
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
                _status.Text = Strings.T("status.previewFailed", ex.Message);
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
            if (_keepGroup && ids.Count > 0)
            {
                try { doc.Groups.Add(ids); } catch { }
            }

            _previewIds = ids;
            doc.Views.Redraw();
            _status.Text = Strings.T("status.previewing", ids.Count);
        }

        private void OnPreview(object sender, EventArgs e)
        {
            PickObjectAndPreview();
        }

        /// <summary>「選擇物件」的核心流程：選面 → 互動預覽起磚點 → 記住邊界/平面/起磚點並更新即時預覽。
        /// 同時給「選擇物件」按鈕跟「點選圖案後馬上選物件」共用，不要各自重複一份選取邏輯。</summary>
        private void PickObjectAndPreview()
        {
            var objRef = TileService.PickFace();
            if (objRef == null)
            {
                _status.Text = Strings.T("status.cancelled");
                return;
            }

            var (boundary, plane) = GeometryUtil.GetBoundaryAndPlane(objRef);
            if (boundary == null)
            {
                MessageBox.Show(this, Strings.T("msg.notPlanar"), Strings.T("dlg.appName"));
                DeselectPicked(objRef);
                return;
            }

            var anchor = InteractivePickAnchor(boundary, plane);
            if (anchor == null)
            {
                // 還沒定案起磚點就按 Esc／右鍵取消：不留下任何東西，包括已經選取的物件本身
                // （GetObject 選面成功後物件會處於選取狀態，這裡要一併取消選取，不然畫面上會留著一個被選亮的物件）。
                _status.Text = Strings.T("status.anchorCancelled");
                DeselectPicked(objRef);
                return;
            }

            _previewBoundary = boundary;
            _previewPlane = plane;
            _previewAnchor = anchor;
            _previewSourceId = objRef.ObjectId;
            UpdatePreview();
        }

        private static void DeselectPicked(Rhino.DocObjects.ObjRef objRef)
        {
            try
            {
                var obj = objRef?.Object();
                if (obj != null)
                {
                    obj.Select(false);
                    Rhino.RhinoDoc.ActiveDoc?.Views.Redraw();
                }
            }
            catch { }
        }

        /// <summary>滑鼠在面上移動時即時畫出鋪磚預覽（DynamicDraw，純畫面顯示，不建立文件物件，用虛線繪製避免
        /// 誤認為已經是實際物件），左鍵點擊回傳該點作為起磚點；按 Esc/右鍵取消則回傳 null，畫面上不會留下任何東西。</summary>
        private Point3d? InteractivePickAnchor(Curve boundary, Plane plane)
        {
            var gp = new GetPoint();
            gp.SetCommandPrompt(Strings.T("prompt.dynamicPreview"));
            // 不用 gp.Constrain(plane, ...)：Rhino 對平面約束的原生 GetPoint 會在畫面上
            // 疊一條類似建構線的輔助線提示目前的約束平面，使用者容易誤以為那是真的產生出來的物件。
            // 下面 DynamicDraw 跟最終回傳值都已經手動用 plane.ClosestPoint 把點投影到面上，
            // 不靠 Constrain 也一樣能保證起磚點落在正確的平面內。

            (double X, double Y, double Z)? cacheKey = null;
            List<Curve> cacheCurves = new List<Curve>();
            var color = System.Drawing.Color.FromArgb(0x2A, 0x5F, 0x8A);

            // 用虛線畫預覽線段，跟按下「產生磚塊」後的實際線段（實線）明顯區分開來，
            // 提醒使用者這只是畫面預覽、還沒有真的產生物件。找不到 Dashed 線型時退回原本的實線畫法。
            Rhino.Display.DisplayPen dashPen = null;
            try
            {
                var lt = Rhino.RhinoDoc.ActiveDoc?.Linetypes.FindName("Dashed");
                if (lt != null)
                    dashPen = Rhino.Display.DisplayPen.FromLinetype(lt, color, 1.0);
            }
            catch { dashPen = null; }

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
                    try
                    {
                        if (dashPen != null)
                            de.Display.DrawCurve(c, dashPen);
                        else
                            de.Display.DrawCurve(c, color, 2);
                    }
                    catch { }
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
            Guid? sourceId;

            if (_previewBoundary != null)
            {
                boundary = _previewBoundary;
                plane = _previewPlane;
                anchorPt = _previewAnchor;
                sourceId = _previewSourceId;
            }
            else
            {
                var objRef = TileService.PickFace();
                if (objRef == null)
                {
                    _status.Text = Strings.T("status.cancelled");
                    return;
                }
                sourceId = objRef.ObjectId;

                (boundary, plane) = GeometryUtil.GetBoundaryAndPlane(objRef);
                if (boundary == null)
                {
                    MessageBox.Show(this, Strings.T("msg.notPlanar"), Strings.T("dlg.appName"));
                    DeselectPicked(objRef);
                    return;
                }

                anchorPt = null;
                if (_opts.Spt == "pick")
                {
                    var gp = new GetPoint();
                    gp.SetCommandPrompt(Strings.T("prompt.pickStartPoint"));
                    var result = gp.Get();
                    if (result != Rhino.Input.GetResult.Point)
                    {
                        _status.Text = Strings.T("status.startPointCancelled");
                        DeselectPicked(objRef);
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
                MessageBox.Show(this, Strings.T("status.generateFailed", ex.Message), Strings.T("dlg.appName"));
                return;
            }

            if (curves.Count == 0)
            {
                _status.Text = Strings.T("status.noTilesGenerated");
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

            // 磚塊產生完成後，把原本選來當參考邊界的曲線／曲面取消選取——GetObject 選取成功後
            // 那個物件會維持在「被選取（高亮）」狀態一直到現在，不取消的話畫面上會留著一條
            // 看起來像多出來的參考線（曲線邊界）或高亮邊線（曲面），使用者會誤以為是沒清乾淨的東西。
            if (sourceId.HasValue)
            {
                try { doc.Objects.Find(sourceId.Value)?.Select(false); } catch { }
            }

            doc.Views.Redraw();

            _undoStack.Add(batch);
            if (_undoStack.Count > MaxUndo)
                _undoStack.RemoveAt(0);

            StorageUtil.SavePatternOpts(_patternId, _opts);
            _previewBoundary = null;
            _previewAnchor = null;
            _previewSourceId = null;
            _status.Text = Strings.T("status.generated", batch.Count, Defaults.DisplayName(_patternId));
        }

        private void OnUndo(object sender, EventArgs e)
        {
            if (_undoStack.Count == 0)
            {
                _status.Text = Strings.T("status.noUndo");
                return;
            }
            var batch = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            var doc = Rhino.RhinoDoc.ActiveDoc;
            foreach (var gid in batch)
                doc.Objects.Delete(gid, true);
            doc.Views.Redraw();
            _status.Text = Strings.T("status.undone", _undoStack.Count);
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
                MessageBox.Show(this, Strings.T("status.calcFailed", ex.Message), Strings.T("dlg.appName"));
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
            _previewSourceId = null;
            SetPattern("Tile");
            _undoStack.Clear();
            _status.Text = Strings.T("status.resetDone");
        }

        private void OnToggleLanguage(object sender, EventArgs e)
        {
            Strings.Toggle();
            StorageUtil.SaveLanguage(Strings.Current);
            Close();
            Show_();
        }

        private void OnClosed(object sender, EventArgs e)
        {
            ClearPreview();
            _instance = null;
        }
    }
}
