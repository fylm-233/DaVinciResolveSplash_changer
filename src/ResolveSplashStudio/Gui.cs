using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using ResolveSplashStudio;

public sealed class MainForm : Form
{
    private TextBox txtExe;
    private Button btnBrowse;
    private Button btnScan;
    private Label lblHeaderInfo;
    private TextBox txtFilter;
    private NumericUpDown numMin;
    private CheckBox chkSplashOnly;
    private ListView lvAssets;
    private PictureBox picOriginal;
    private PictureBox picNew;
    private Label lblOriginalInfo;
    private Label lblNewInfo;
    private Button btnChooseReplacement;
    private CheckBox chkPatchCopies;
    private CheckBox chkClearSignature;
    private Button btnCreatePatch;
    private Button btnApply;
    private Button btnRestore;
    private TextBox txtLog;
    private ToolStripStatusLabel statusLabel;
    private ToolStripProgressBar progressBar;

    private PeInfo currentPe;
    private List<PngAsset> allAssets = new List<PngAsset>();
    private List<PngAsset> selectedAssets = new List<PngAsset>();
    private PngAsset selectedAsset;
    private byte[] selectedOriginalBytes;
    private byte[] replacementOriginalBytes;
    private byte[] replacementFittedBytes;
    private Dictionary<int, byte[]> replacementByLength = new Dictionary<int, byte[]>();
    private string replacementPath;
    private bool scanning;
    private bool suppressSelectionEvent;
    private bool draggingSelection;
    private Point dragStartPoint;
    private Rectangle dragRect;
    private bool dragFrameVisible;
    private HashSet<long> dragBaseSelection = new HashSet<long>();
    private Button btnSelectSameSize;

    public MainForm()
    {
        Text = "Resolve Splash Studio - 达芬奇启动图精确替换工具";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1050, 680);
        Size = new Size(1320, 850);
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildUi();

        string found = ResolveLocator.Find();
        if (!String.IsNullOrEmpty(found)) txtExe.Text = found;
        Log("就绪。请选择 Resolve.exe 后点击“扫描”。");
        UpdateButtons();
    }

    private void BuildUi()
    {
        Panel top = new Panel();
        top.Dock = DockStyle.Top;
        top.Height = 78;
        top.Padding = new Padding(10, 8, 10, 6);
        Controls.Add(top);

        Label lblExe = new Label();
        lblExe.Text = "Resolve.exe";
        lblExe.AutoSize = true;
        lblExe.Location = new Point(12, 13);
        top.Controls.Add(lblExe);

        txtExe = new TextBox();
        txtExe.Location = new Point(96, 10);
        txtExe.Width = 810;
        txtExe.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        top.Controls.Add(txtExe);

        btnBrowse = new Button();
        btnBrowse.Text = "浏览...";
        btnBrowse.Location = new Point(916, 8);
        btnBrowse.Size = new Size(85, 28);
        btnBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnBrowse.Click += delegate { BrowseExe(); };
        top.Controls.Add(btnBrowse);

        btnScan = new Button();
        btnScan.Text = "扫描";
        btnScan.Location = new Point(1008, 8);
        btnScan.Size = new Size(85, 28);
        btnScan.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnScan.Click += delegate { ScanFile(); };
        top.Controls.Add(btnScan);

        lblHeaderInfo = new Label();
        lblHeaderInfo.AutoSize = false;
        lblHeaderInfo.Location = new Point(96, 43);
        lblHeaderInfo.Size = new Size(1000, 24);
        lblHeaderInfo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lblHeaderInfo.ForeColor = Color.DimGray;
        lblHeaderInfo.Text = "尚未扫描";
        top.Controls.Add(lblHeaderInfo);

        SplitContainer split = new SplitContainer();
        split.Dock = DockStyle.Fill;
        split.FixedPanel = FixedPanel.Panel1;
        Controls.Add(split);
        split.BringToFront();
        this.Shown += delegate
        {
            if (split.Width < 1030) return;
            split.Panel1MinSize = 650;
            split.Panel2MinSize = 360;
            int maximum = split.Width - split.Panel2MinSize - split.SplitterWidth;
            int distance = Math.Min(770, maximum);
            if (distance >= split.Panel1MinSize && distance <= maximum) split.SplitterDistance = distance;
        };

        BuildLeftPanel(split.Panel1);
        BuildRightPanel(split.Panel2);

        StatusStrip status = new StatusStrip();
        status.SizingGrip = false;
        statusLabel = new ToolStripStatusLabel("就绪");
        statusLabel.Spring = true;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        progressBar = new ToolStripProgressBar();
        progressBar.Style = ProgressBarStyle.Continuous;
        progressBar.Visible = false;
        status.Items.Add(statusLabel);
        status.Items.Add(progressBar);
        Controls.Add(status);
    }

    private void BuildLeftPanel(Control parent)
    {
        Panel filters = new Panel();
        filters.Dock = DockStyle.Top;
        filters.Height = 72;
        parent.Controls.Add(filters);

        Label lblFilter = new Label();
        lblFilter.Text = "筛选";
        lblFilter.AutoSize = true;
        lblFilter.Location = new Point(10, 13);
        filters.Controls.Add(lblFilter);

        txtFilter = new TextBox();
        txtFilter.Location = new Point(54, 9);
        txtFilter.Width = 260;
        txtFilter.TextChanged += delegate { ApplyFilter(); };
        filters.Controls.Add(txtFilter);

        Label lblMin = new Label();
        lblMin.Text = "最小边长";
        lblMin.AutoSize = true;
        lblMin.Location = new Point(330, 13);
        filters.Controls.Add(lblMin);

        numMin = new NumericUpDown();
        numMin.Location = new Point(394, 9);
        numMin.Width = 70;
        numMin.Minimum = 32;
        numMin.Maximum = 10000;
        numMin.Value = 500;
        numMin.ValueChanged += delegate { ApplyFilter(); };
        filters.Controls.Add(numMin);

        chkSplashOnly = new CheckBox();
        chkSplashOnly.Text = "仅显示疑似启动图";
        chkSplashOnly.Location = new Point(480, 9);
        chkSplashOnly.Width = 150;
        chkSplashOnly.Checked = true;
        chkSplashOnly.CheckedChanged += delegate { ApplyFilter(); };
        filters.Controls.Add(chkSplashOnly);

        btnSelectSameSize = new Button();
        btnSelectSameSize.Text = "全选同尺寸";
        btnSelectSameSize.Location = new Point(636, 7);
        btnSelectSameSize.Size = new Size(108, 28);
        btnSelectSameSize.Click += delegate { SelectSameSize(); };
        filters.Controls.Add(btnSelectSameSize);

        Label hint = new Label();
        hint.Text = "支持 Shift/Ctrl 多选，也可按住左键框选；批量替换要求所选尺寸一致。";
        hint.ForeColor = Color.DimGray;
        hint.AutoSize = false;
        hint.Location = new Point(10, 42);
        hint.Size = new Size(730, 20);
        filters.Controls.Add(hint);

        lvAssets = new ListView();
        lvAssets.Dock = DockStyle.Fill;
        lvAssets.View = View.Details;
        lvAssets.FullRowSelect = true;
        lvAssets.GridLines = true;
        lvAssets.HideSelection = false;
        lvAssets.MultiSelect = true;
        lvAssets.Columns.Add("#", 52, HorizontalAlignment.Right);
        lvAssets.Columns.Add("尺寸", 104, HorizontalAlignment.Left);
        lvAssets.Columns.Add("字节", 92, HorizontalAlignment.Right);
        lvAssets.Columns.Add("文件偏移", 120, HorizontalAlignment.Left);
        lvAssets.Columns.Add("颜色", 78, HorizontalAlignment.Left);
        lvAssets.Columns.Add("判定", 110, HorizontalAlignment.Left);
        lvAssets.SelectedIndexChanged += delegate { OnAssetSelected(); };
        lvAssets.DoubleClick += delegate { ReloadSelectedPreview(); };
        lvAssets.MouseDown += delegate(object sender, MouseEventArgs e) { OnListMouseDown(e); };
        lvAssets.MouseMove += delegate(object sender, MouseEventArgs e) { OnListMouseMove(e); };
        lvAssets.MouseUp += delegate(object sender, MouseEventArgs e) { OnListMouseUp(e); };
        parent.Controls.Add(lvAssets);
        lvAssets.BringToFront();
    }

    private void BuildRightPanel(Control parent)
    {
        TableLayoutPanel layout = new TableLayoutPanel();
        layout.Dock = DockStyle.Fill;
        layout.ColumnCount = 1;
        layout.RowCount = 7;
        layout.Padding = new Padding(8);
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 31F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 31F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 38F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        parent.Controls.Add(layout);

        GroupBox gbOriginal = new GroupBox();
        gbOriginal.Text = "原启动图预览";
        gbOriginal.Dock = DockStyle.Fill;
        picOriginal = new PictureBox();
        picOriginal.Dock = DockStyle.Fill;
        picOriginal.SizeMode = PictureBoxSizeMode.Zoom;
        picOriginal.BackColor = Color.FromArgb(35, 35, 35);
        gbOriginal.Controls.Add(picOriginal);
        layout.Controls.Add(gbOriginal, 0, 0);

        GroupBox gbNew = new GroupBox();
        gbNew.Text = "新图片预览";
        gbNew.Dock = DockStyle.Fill;
        picNew = new PictureBox();
        picNew.Dock = DockStyle.Fill;
        picNew.SizeMode = PictureBoxSizeMode.Zoom;
        picNew.BackColor = Color.FromArgb(35, 35, 35);
        gbNew.Controls.Add(picNew);
        layout.Controls.Add(gbNew, 0, 1);

        lblOriginalInfo = new Label();
        lblOriginalInfo.AutoSize = false;
        lblOriginalInfo.Height = 42;
        lblOriginalInfo.Dock = DockStyle.Top;
        lblOriginalInfo.ForeColor = Color.DimGray;
        lblOriginalInfo.Text = "未选择资源";
        layout.Controls.Add(lblOriginalInfo, 0, 2);

        Panel choosePanel = new Panel();
        choosePanel.Height = 40;
        choosePanel.Dock = DockStyle.Top;
        btnChooseReplacement = new Button();
        btnChooseReplacement.Text = "选择新的 PNG...";
        btnChooseReplacement.Location = new Point(0, 5);
        btnChooseReplacement.Size = new Size(150, 30);
        btnChooseReplacement.Click += delegate { ChooseReplacement(); };
        choosePanel.Controls.Add(btnChooseReplacement);
        layout.Controls.Add(choosePanel, 0, 3);

        lblNewInfo = new Label();
        lblNewInfo.AutoSize = false;
        lblNewInfo.Height = 42;
        lblNewInfo.Dock = DockStyle.Top;
        lblNewInfo.ForeColor = Color.DimGray;
        lblNewInfo.Text = "尚未选择替换图";
        layout.Controls.Add(lblNewInfo, 0, 4);

        GroupBox gbLog = new GroupBox();
        gbLog.Text = "处理日志";
        gbLog.Dock = DockStyle.Fill;
        txtLog = new TextBox();
        txtLog.Dock = DockStyle.Fill;
        txtLog.Multiline = true;
        txtLog.ScrollBars = ScrollBars.Vertical;
        txtLog.ReadOnly = true;
        txtLog.BackColor = Color.White;
        gbLog.Controls.Add(txtLog);
        layout.Controls.Add(gbLog, 0, 5);

        Panel actions = new Panel();
        actions.Height = 78;
        actions.Dock = DockStyle.Bottom;
        chkPatchCopies = new CheckBox();
        chkPatchCopies.Text = "同时替换内容完全相同的副本";
        chkPatchCopies.Location = new Point(0, 3);
        chkPatchCopies.Width = 260;
        chkPatchCopies.Checked = true;
        actions.Controls.Add(chkPatchCopies);

        chkClearSignature = new CheckBox();
        chkClearSignature.Text = "清除失效 Authenticode 目录（非必需）";
        chkClearSignature.Location = new Point(0, 26);
        chkClearSignature.Width = 310;
        chkClearSignature.Checked = false;
        actions.Controls.Add(chkClearSignature);

        btnCreatePatch = new Button();
        btnCreatePatch.Text = "生成补丁副本";
        btnCreatePatch.Location = new Point(0, 48);
        btnCreatePatch.Size = new Size(130, 28);
        btnCreatePatch.Click += delegate { CreatePatch(false); };
        actions.Controls.Add(btnCreatePatch);

        btnApply = new Button();
        btnApply.Text = "直接应用到 Resolve.exe";
        btnApply.Location = new Point(140, 48);
        btnApply.Size = new Size(170, 28);
        btnApply.Click += delegate { ApplyPatch(); };
        actions.Controls.Add(btnApply);

        btnRestore = new Button();
        btnRestore.Text = "恢复原始备份";
        btnRestore.Location = new Point(320, 48);
        btnRestore.Size = new Size(130, 28);
        btnRestore.Click += delegate { RestoreBackup(); };
        actions.Controls.Add(btnRestore);
        layout.Controls.Add(actions, 0, 6);
    }
    private bool IsSuspectedSplash(PngAsset a)
    {
        if (a.Width < 1000 || a.Height < 450) return false;
        double ratio = (double)a.Width / (double)a.Height;
        return ratio >= 2.15 && ratio <= 2.42;
    }

    private bool SelectionIsSameSize()
    {
        if (selectedAssets.Count == 0) return false;
        int width = selectedAssets[0].Width;
        int height = selectedAssets[0].Height;
        for (int i = 1; i < selectedAssets.Count; i++)
        {
            if (selectedAssets[i].Width != width || selectedAssets[i].Height != height) return false;
        }
        return true;
    }

    private void ApplyFilter()
    {
        if (lvAssets == null) return;
        suppressSelectionEvent = true;
        string filter = txtFilter == null ? String.Empty : txtFilter.Text.Trim().ToLowerInvariant();
        int min = numMin == null ? 500 : (int)numMin.Value;
        bool splashOnly = chkSplashOnly != null && chkSplashOnly.Checked;
        lvAssets.BeginUpdate();
        try
        {
            lvAssets.Items.Clear();
            for (int i = 0; i < allAssets.Count; i++)
            {
                PngAsset a = allAssets[i];
                if (Math.Max(a.Width, a.Height) < min) continue;
                bool suspected = IsSuspectedSplash(a);
                if (splashOnly && !suspected) continue;
                if (filter.Length > 0)
                {
                    string haystack = (a.Index + " " + a.Dimensions + " " + a.Length + " " + a.OffsetHex + " " + a.ColorTypeText).ToLowerInvariant();
                    if (haystack.IndexOf(filter, StringComparison.Ordinal) < 0) continue;
                }
                ListViewItem item = new ListViewItem(a.Index.ToString(CultureInfo.InvariantCulture));
                item.SubItems.Add(a.Dimensions);
                item.SubItems.Add(a.Length.ToString("N0", CultureInfo.InvariantCulture));
                item.SubItems.Add(a.OffsetHex);
                item.SubItems.Add(a.ColorTypeText);
                item.SubItems.Add(suspected ? "启动图候选" : "其他 PNG");
                item.Tag = a;
                lvAssets.Items.Add(item);
            }
            if (lvAssets.Items.Count > 0) lvAssets.Items[0].Selected = true;
        }
        finally
        {
            lvAssets.EndUpdate();
            suppressSelectionEvent = false;
        }
        statusLabel.Text = "显示 " + lvAssets.Items.Count + " / " + allAssets.Count + " 个资源";
        OnAssetSelected();
    }

    private void OnAssetSelected()
    {
        if (suppressSelectionEvent) return;
        List<PngAsset> assets = new List<PngAsset>();
        for (int i = 0; i < lvAssets.SelectedItems.Count; i++)
        {
            PngAsset a = lvAssets.SelectedItems[i].Tag as PngAsset;
            if (a != null) assets.Add(a);
        }
        selectedAssets = assets;
        if (assets.Count == 0)
        {
            selectedAsset = null;
            selectedOriginalBytes = null;
            SetPicture(picOriginal, null);
            lblOriginalInfo.Text = "未选择资源";
            UpdateButtons();
            return;
        }

        PngAsset asset = assets[0];
        selectedAsset = asset;
        try
        {
            selectedOriginalBytes = PngCodec.ExtractAsset(txtExe.Text, asset);
            asset.Sha256 = HashTools.Sha256Bytes(selectedOriginalBytes);
            SetPicture(picOriginal, ImageFromBytes(selectedOriginalBytes));
            if (assets.Count == 1)
            {
                lblOriginalInfo.Text = String.Format(CultureInfo.InvariantCulture,
                    "编号 {0} | {1} | {2:N0} 字节 | {3} | {4} | SHA-256 {5}...",
                    asset.Index, asset.Dimensions, asset.Length, asset.OffsetHex, asset.ColorTypeText, asset.Sha256.Substring(0, 16));
            }
            else if (SelectionIsSameSize())
            {
                long total = 0;
                long minLength = Int64.MaxValue;
                long maxLength = 0;
                for (int i = 0; i < assets.Count; i++)
                {
                    total += assets[i].Length;
                    if (assets[i].Length < minLength) minLength = assets[i].Length;
                    if (assets[i].Length > maxLength) maxLength = assets[i].Length;
                }
                lblOriginalInfo.Text = String.Format(CultureInfo.InvariantCulture,
                    "已选 {0} 张同尺寸启动图 | {1} | 总 {2:N0} 字节 | 单张 {3:N0}..{4:N0} 字节\r\n预览首个 #{5} {6} @ {7}",
                    assets.Count, asset.Dimensions, total, minLength, maxLength, asset.Index, asset.Dimensions, asset.OffsetHex);
            }
            else
            {
                lblOriginalInfo.Text = String.Format(CultureInfo.InvariantCulture,
                    "已选 {0} 项，但尺寸不一致：{1}、{2} 等。批量替换已禁用。",
                    assets.Count, assets[0].Dimensions, assets.Count > 1 ? assets[1].Dimensions : "-");
            }
            Log("选择 " + assets.Count + " 个资源，首个 #" + asset.Index + " " + asset.Dimensions + " @ " + asset.OffsetHex);
        }
        catch (Exception ex)
        {
            selectedOriginalBytes = null;
            MessageBox.Show(this, ex.Message, "读取资源失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        UpdateButtons();
    }

    private void SelectSameSize()
    {
        if (selectedAsset == null) return;
        suppressSelectionEvent = true;
        try
        {
            lvAssets.SelectedItems.Clear();
            for (int i = 0; i < lvAssets.Items.Count; i++)
            {
                PngAsset a = lvAssets.Items[i].Tag as PngAsset;
                if (a != null && a.Width == selectedAsset.Width && a.Height == selectedAsset.Height)
                    lvAssets.Items[i].Selected = true;
            }
        }
        finally
        {
            suppressSelectionEvent = false;
        }
        OnAssetSelected();
    }

    private Rectangle NormalizeDragRect(Point a, Point b)
    {
        int left = Math.Min(a.X, b.X);
        int top = Math.Min(a.Y, b.Y);
        int right = Math.Max(a.X, b.X);
        int bottom = Math.Max(a.Y, b.Y);
        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    private void DrawDragFrame(bool visible)
    {
        if (lvAssets == null) return;
        if (dragFrameVisible)
        {
            Rectangle oldScreen = lvAssets.RectangleToScreen(dragRect);
            ControlPaint.DrawReversibleFrame(oldScreen, BackColor, FrameStyle.Dashed);
            dragFrameVisible = false;
        }
        if (!visible) return;
        Rectangle screen = lvAssets.RectangleToScreen(dragRect);
        screen = Rectangle.Intersect(screen, lvAssets.RectangleToScreen(lvAssets.ClientRectangle));
        if (screen.Width <= 0 || screen.Height <= 0) return;
        ControlPaint.DrawReversibleFrame(screen, BackColor, FrameStyle.Dashed);
        dragFrameVisible = true;
    }

    private void OnListMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        dragStartPoint = e.Location;
        draggingSelection = false;
        dragFrameVisible = false;
    }

    private void OnListMouseMove(MouseEventArgs e)
    {
        if ((e.Button & MouseButtons.Left) == 0)
        {
            if (draggingSelection) OnListMouseUp(e);
            return;
        }
        int dx = Math.Abs(e.X - dragStartPoint.X);
        int dy = Math.Abs(e.Y - dragStartPoint.Y);
        if (!draggingSelection && dx < 4 && dy < 4) return;
        if (!draggingSelection)
        {
            draggingSelection = true;
            suppressSelectionEvent = true;
            lvAssets.Capture = true;
            dragBaseSelection.Clear();
            bool preserve = (Control.ModifierKeys & (Keys.Control | Keys.Shift)) != 0;
            if (preserve)
            {
                for (int i = 0; i < lvAssets.SelectedItems.Count; i++)
                {
                    PngAsset a = lvAssets.SelectedItems[i].Tag as PngAsset;
                    if (a != null) dragBaseSelection.Add(a.Offset);
                }
            }
            else
            {
                lvAssets.SelectedItems.Clear();
            }
        }

        DrawDragFrame(false);
        dragRect = NormalizeDragRect(dragStartPoint, e.Location);
        DrawDragFrame(true);

        lvAssets.SelectedItems.Clear();
        for (int i = 0; i < lvAssets.Items.Count; i++)
        {
            ListViewItem item = lvAssets.Items[i];
            PngAsset a = item.Tag as PngAsset;
            bool preserve = a != null && dragBaseSelection.Contains(a.Offset);
            if (preserve || item.Bounds.IntersectsWith(dragRect)) item.Selected = true;
        }
    }

    private void OnListMouseUp(MouseEventArgs e)
    {
        if (!draggingSelection) return;
        DrawDragFrame(false);
        draggingSelection = false;
        suppressSelectionEvent = false;
        if (lvAssets.Capture) lvAssets.Capture = false;
        OnAssetSelected();
    }

    private void ReloadSelectedPreview()
    {
        selectedAsset = null;
        OnAssetSelected();
    }

    private Image ImageFromBytes(byte[] bytes)
    {
        using (MemoryStream ms = new MemoryStream(bytes, false))
        using (Image image = Image.FromStream(ms, true, true))
        {
            return new Bitmap(image);
        }
    }

    private void SetPicture(PictureBox box, Image image)
    {
        if (box == null) return;
        Image old = box.Image;
        box.Image = image;
        if (old != null) old.Dispose();
    }

    private void ClearReplacement()
    {
        replacementOriginalBytes = null;
        replacementFittedBytes = null;
        replacementByLength.Clear();
        replacementPath = null;
        SetPicture(picNew, null);
        lblNewInfo.Text = "尚未选择替换图";
        UpdateButtons();
    }
    private void BrowseExe()
    {
        using (OpenFileDialog dlg = new OpenFileDialog())
        {
            dlg.Title = "选择 DaVinci Resolve 主程序";
            dlg.Filter = "Resolve.exe|Resolve.exe|可执行文件|*.exe|所有文件|*.*";
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                txtExe.Text = dlg.FileName;
                allAssets.Clear();
                currentPe = null;
                selectedAsset = null;
                selectedAssets.Clear();
                ClearReplacement();
                ApplyFilter();
                Log("已选择文件: " + dlg.FileName);
            }
        }
    }

    private void ScanFile()
    {
        if (scanning) return;
        string path = txtExe.Text.Trim().Trim('"');
        if (!File.Exists(path))
        {
            MessageBox.Show(this, "找不到 Resolve.exe，请先选择正确路径。", "路径错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        scanning = true;
        SetBusy(true, "正在扫描嵌入 PNG...");
        Log("开始扫描: " + path);
        BackgroundWorker worker = new BackgroundWorker();
        worker.DoWork += delegate(object sender, DoWorkEventArgs e)
        {
            string error;
            PeInfo pe;
            List<PngAsset> assets = PngScanner.Scan(path, 32, out pe, out error);
            if (assets == null) throw new Exception(error == null ? "扫描失败。" : error);
            assets.Sort(delegate(PngAsset a, PngAsset b)
            {
                int area = b.Area.CompareTo(a.Area);
                if (area != 0) return area;
                return a.Offset.CompareTo(b.Offset);
            });
            for (int i = 0; i < assets.Count; i++) assets[i].Index = i;
            ScanPayload payload = new ScanPayload();
            payload.Assets = assets;
            payload.Pe = pe;
            e.Result = payload;
        };
        worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
        {
            scanning = false;
            SetBusy(false, "就绪");
            if (e.Error != null)
            {
                Log("扫描失败: " + e.Error.Message);
                MessageBox.Show(this, e.Error.Message, "扫描失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            ScanPayload payload = (ScanPayload)e.Result;
            allAssets = payload.Assets;
            currentPe = payload.Pe;
            selectedAsset = null;
            selectedAssets.Clear();
            ClearReplacement();
            lblHeaderInfo.Text = String.Format(CultureInfo.InvariantCulture,
                "文件 {0:N0} 字节 | PE {1} | 嵌入 PNG {2} 个 | PE Checksum 0x{3:X8} | Authenticode {4}",
                currentPe.FileSize,
                currentPe.OptionalMagic == 0x20B ? "64-bit" : "32-bit",
                allAssets.Count,
                currentPe.StoredChecksum,
                currentPe.HasSecurityDirectory ? "存在（修改后会失效）" : "无");
            ApplyFilter();
            Log("扫描完成，找到 " + allAssets.Count + " 个 PNG 资源。");
            UpdateButtons();
        };
        worker.RunWorkerAsync();
    }

    private sealed class ScanPayload
    {
        public List<PngAsset> Assets;
        public PeInfo Pe;
    }

    private void SetBusy(bool busy, string text)
    {
        btnBrowse.Enabled = !busy;
        btnScan.Enabled = !busy;
        txtExe.Enabled = !busy;
        btnChooseReplacement.Enabled = !busy && selectedAssets.Count > 0 && SelectionIsSameSize();
        btnCreatePatch.Enabled = !busy && replacementOriginalBytes != null && replacementByLength.Count > 0;
        btnApply.Enabled = !busy && replacementOriginalBytes != null && replacementByLength.Count > 0;
        progressBar.Visible = busy;
        progressBar.Style = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
        statusLabel.Text = text;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
    }

    private void UpdateButtons()
    {
        bool hasAsset = selectedAssets.Count > 0 && selectedOriginalBytes != null && SelectionIsSameSize();
        bool hasReplacement = replacementByLength.Count > 0 && replacementOriginalBytes != null;
        btnChooseReplacement.Enabled = !scanning && hasAsset;
        btnCreatePatch.Enabled = !scanning && hasReplacement;
        btnApply.Enabled = !scanning && hasReplacement;
        btnRestore.Enabled = !scanning && txtExe != null && File.Exists(txtExe.Text.Trim().Trim('"') + ".original.bak");
    }

    private void Log(string text)
    {
        if (txtLog == null) return;
        string line = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "  " + text + "\r\n";
        txtLog.AppendText(line);
        txtLog.SelectionStart = txtLog.TextLength;
        txtLog.ScrollToCaret();
    }
    private void ChooseReplacement()
    {
        if (selectedAssets.Count == 0 || selectedOriginalBytes == null)
        {
            MessageBox.Show(this, "请先从列表中选择一个或多个原始 PNG 资源。", "未选择资源", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!SelectionIsSameSize())
        {
            MessageBox.Show(this, "批量替换只支持尺寸完全一致的资源。请重新选择相同尺寸的启动图。", "尺寸不一致", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        PngAsset first = selectedAssets[0];
        using (OpenFileDialog dlg = new OpenFileDialog())
        {
            dlg.Title = "选择新的 PNG 启动图";
            dlg.Filter = "PNG 图片|*.png";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                Cursor = Cursors.WaitCursor;
                byte[] raw = File.ReadAllBytes(dlg.FileName);
                string error;
                PngPacket packet = PngCodec.Parse(raw, true, out error);
                if (packet == null) throw new Exception(error);
                if (packet.Width != first.Width || packet.Height != first.Height)
                {
                    throw new Exception(String.Format(CultureInfo.InvariantCulture,
                        "新图尺寸必须与所选原图完全一致。原图 {0}x{1}，当前文件 {2}x{3}。",
                        first.Width, first.Height, packet.Width, packet.Height));
                }

                Dictionary<int, byte[]> fittedByLength = new Dictionary<int, byte[]>();
                List<string> fitErrors = new List<string>();
                for (int i = 0; i < selectedAssets.Count; i++)
                {
                    PngAsset asset = selectedAssets[i];
                    if (fittedByLength.ContainsKey(asset.Length)) continue;
                    byte[] fitted = PngCodec.FitToExactLength(raw, asset.Length, out error);
                    if (fitted == null) fitErrors.Add("#" + asset.Index + " (" + asset.Length.ToString("N0", CultureInfo.InvariantCulture) + " 字节): " + error);
                    else fittedByLength[asset.Length] = fitted;
                }
                if (fitErrors.Count > 0)
                {
                    throw new Exception("有资源无法精确适配，整个批量操作已取消：\r\n\r\n" + String.Join("\r\n", fitErrors.ToArray()));
                }

                replacementOriginalBytes = raw;
                replacementPath = dlg.FileName;
                replacementByLength = fittedByLength;
                replacementFittedBytes = fittedByLength[first.Length];
                SetPicture(picNew, ImageFromBytes(raw));

                long minLength = Int64.MaxValue;
                long maxLength = 0;
                for (int i = 0; i < selectedAssets.Count; i++)
                {
                    if (selectedAssets[i].Length < minLength) minLength = selectedAssets[i].Length;
                    if (selectedAssets[i].Length > maxLength) maxLength = selectedAssets[i].Length;
                }
                lblNewInfo.Text = String.Format(CultureInfo.InvariantCulture,
                    "{0} | {1:N0} 字节 | {2}x{3}\r\n将批量替换 {4} 张，覆盖 {5} 种原始长度（{6:N0}..{7:N0} 字节）",
                    Path.GetFileName(dlg.FileName), raw.Length, packet.Width, packet.Height,
                    selectedAssets.Count, fittedByLength.Count, minLength, maxLength);
                Log("已选择批量替换图: " + dlg.FileName + "，将替换 " + selectedAssets.Count + " 张同尺寸启动图。");
                UpdateButtons();
            }
            catch (Exception ex)
            {
                replacementOriginalBytes = null;
                replacementFittedBytes = null;
                replacementByLength.Clear();
                replacementPath = null;
                SetPicture(picNew, null);
                lblNewInfo.Text = "替换图不可用";
                MessageBox.Show(this, ex.Message, "替换图错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }
    }

    private string GetAssetHash(PngAsset asset, Dictionary<long, string> cache)
    {
        string hash;
        if (cache.TryGetValue(asset.Offset, out hash)) return hash;
        byte[] bytes = PngCodec.ExtractAsset(txtExe.Text, asset);
        hash = HashTools.Sha256Bytes(bytes);
        asset.Sha256 = hash;
        cache[asset.Offset] = hash;
        return hash;
    }

    private static void AddPatchItem(List<PatchItem> items, HashSet<long> addedOffsets, PngAsset asset, byte[] replacement, string replacementHash, string originalHash, string path)
    {
        if (addedOffsets.Contains(asset.Offset)) return;
        PatchItem item = new PatchItem();
        item.Original = asset;
        item.OriginalSha256 = originalHash;
        item.Replacement = replacement;
        item.ReplacementSha256 = replacementHash;
        item.ReplacementPath = path;
        items.Add(item);
        addedOffsets.Add(asset.Offset);
    }

    private List<PatchItem> BuildPatchItems(out string error)
    {
        error = null;
        List<PatchItem> items = new List<PatchItem>();
        if (selectedAssets.Count == 0 || replacementOriginalBytes == null || replacementByLength.Count == 0)
        {
            error = "请先选择一个或多个原始资源和新 PNG。";
            return null;
        }
        if (!SelectionIsSameSize())
        {
            error = "所选原始资源尺寸不一致，无法批量替换。";
            return null;
        }

        HashSet<long> addedOffsets = new HashSet<long>();
        Dictionary<long, string> originalHashCache = new Dictionary<long, string>();
        Dictionary<int, string> replacementHashCache = new Dictionary<int, string>();

        for (int i = 0; i < selectedAssets.Count; i++)
        {
            PngAsset asset = selectedAssets[i];
            byte[] replacement;
            if (!replacementByLength.TryGetValue(asset.Length, out replacement))
            {
                error = "资源 #" + asset.Index + " 缺少目标长度 " + asset.Length + " 的适配结果。";
                return null;
            }
            string replacementHash;
            if (!replacementHashCache.TryGetValue(asset.Length, out replacementHash))
            {
                replacementHash = HashTools.Sha256Bytes(replacement);
                replacementHashCache[asset.Length] = replacementHash;
            }
            string originalHash = GetAssetHash(asset, originalHashCache);
            AddPatchItem(items, addedOffsets, asset, replacement, replacementHash, originalHash, replacementPath);
        }

        if (chkPatchCopies.Checked)
        {
            for (int i = 0; i < selectedAssets.Count; i++)
            {
                PngAsset source = selectedAssets[i];
                string sourceHash = originalHashCache[source.Offset];
                byte[] replacement = replacementByLength[source.Length];
                string replacementHash = replacementHashCache[source.Length];
                for (int j = 0; j < allAssets.Count; j++)
                {
                    PngAsset candidate = allAssets[j];
                    if (candidate.Width != source.Width || candidate.Height != source.Height) continue;
                    if (candidate.Length != source.Length) continue;
                    if (addedOffsets.Contains(candidate.Offset)) continue;
                    try
                    {
                        string hash = GetAssetHash(candidate, originalHashCache);
                        if (!String.Equals(hash, sourceHash, StringComparison.OrdinalIgnoreCase)) continue;
                        AddPatchItem(items, addedOffsets, candidate, replacement, replacementHash, hash, replacementPath);
                    }
                    catch
                    {
                    }
                }
            }
        }
        return items;
    }

    private void CreatePatch()
    {
        CreatePatch(true);
    }

    private void CreatePatch(bool showMessage)
    {
        if (replacementByLength.Count == 0 || selectedAssets.Count == 0 || replacementOriginalBytes == null)
        {
            MessageBox.Show(this, "请先选择一个或多个原始资源和新 PNG。", "未准备完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string error;
        List<PatchItem> items = BuildPatchItems(out error);
        if (items == null)
        {
            MessageBox.Show(this, error, "无法生成补丁", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        string source = txtExe.Text.Trim().Trim('"');
        string suggested = Path.Combine(Path.GetDirectoryName(source), "Resolve.patched.exe");
        using (SaveFileDialog dlg = new SaveFileDialog())
        {
            dlg.Title = "保存精确补丁副本";
            dlg.Filter = "可执行文件|*.exe";
            dlg.FileName = suggested;
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                SetBusy(true, "正在生成补丁副本...");
                PatchResult result = Patcher.CreatePatchedCopy(source, dlg.FileName, items, chkClearSignature.Checked, out error);
                if (result == null) throw new Exception(error);
                Log("补丁生成成功: " + result.OutputPath);
                for (int i = 0; i < result.VerificationMessages.Count; i++) Log("  " + result.VerificationMessages[i]);
                Log("  SHA-256: " + result.OutputSha256);
                Log("  清单: " + result.ManifestPath);
                if (showMessage)
                {
                    MessageBox.Show(this,
                        "已生成精确补丁副本。\r\n\r\n替换资源: " + result.ReplacementCount +
                        "\r\n文件长度: " + result.OutputSize.ToString("N0", CultureInfo.InvariantCulture) +
                        "\r\nPE Checksum: 0x" + result.OriginalPeChecksum.ToString("X8") + " -> 0x" + result.PatchedPeChecksum.ToString("X8") +
                        "\r\n\r\n输出:\r\n" + result.OutputPath,
                        "生成完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                Log("生成补丁失败: " + ex.Message);
                MessageBox.Show(this, ex.Message, "生成失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false, "就绪");
                UpdateButtons();
            }
        }
    }
    private void ApplyPatch()
    {
        if (replacementByLength.Count == 0 || selectedAssets.Count == 0 || replacementOriginalBytes == null) return;
        string source = txtExe.Text.Trim().Trim('"');
        if (Patcher.IsProcessRunning("Resolve"))
        {
            MessageBox.Show(this, "DaVinci Resolve 正在运行。请完全退出后再应用补丁。", "Resolve 正在运行", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (MessageBox.Show(this,
            "将直接修改:\r\n" + source +
            "\r\n\r\n首次应用会创建 " + Path.GetFileName(source) + ".original.bak。\r\n程序目录通常需要以管理员身份运行本工具。\r\n\r\n继续吗？",
            "确认应用到 Resolve.exe", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        string error;
        List<PatchItem> items = BuildPatchItems(out error);
        if (items == null)
        {
            MessageBox.Show(this, error, "无法应用补丁", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        string temp = source + ".patch.tmp";
        string backup = source + ".original.bak";
        try
        {
            SetBusy(true, "正在生成并验证补丁...");
            if (!File.Exists(backup))
            {
                Log("创建原始备份: " + backup);
                File.Copy(source, backup, false);
            }
            else
            {
                Log("保留已有原始备份: " + backup);
            }

            if (File.Exists(temp)) File.Delete(temp);
            PatchResult result = Patcher.CreatePatchedCopy(source, temp, items, chkClearSignature.Checked, out error);
            if (result == null) throw new Exception(error);

            File.Replace(temp, source, null);
            string manifest = temp + ".manifest.json";
            string targetManifest = source + ".manifest.json";
            if (File.Exists(manifest))
            {
                if (File.Exists(targetManifest)) File.Delete(targetManifest);
                File.Move(manifest, targetManifest);
            }
            Log("已应用到 Resolve.exe。");
            for (int i = 0; i < result.VerificationMessages.Count; i++) Log("  " + result.VerificationMessages[i]);
            Log("  PE Checksum: 0x" + result.OriginalPeChecksum.ToString("X8") + " -> 0x" + result.PatchedPeChecksum.ToString("X8"));
            MessageBox.Show(this,
                "替换已应用。\r\n\r\n请完全退出所有 Resolve 进程后重新启动。\r\n如需还原，请点击“恢复原始备份”。",
                "应用完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            suppressSelectionEvent = true;
            lvAssets.SelectedItems.Clear();
            suppressSelectionEvent = false;
            selectedAsset = null;
            selectedOriginalBytes = null;
            selectedAssets.Clear();
            ClearReplacement();
            Log("列表仍保留原扫描信息；如需复核可再次点击“扫描”。");
        }
        catch (UnauthorizedAccessException ex)
        {
            Log("应用失败: " + ex.Message);
            MessageBox.Show(this,
                "没有权限写入 Resolve 安装目录。\r\n\r\n请右键以管理员身份运行本工具，或先使用“生成补丁副本”再手动替换。\r\n\r\n" + ex.Message,
                "需要管理员权限", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            Log("应用失败: " + ex.Message);
            MessageBox.Show(this, ex.Message, "应用失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            SetBusy(false, "就绪");
            UpdateButtons();
        }
    }

    private void RestoreBackup()
    {
        string source = txtExe.Text.Trim().Trim('"');
        string backup = source + ".original.bak";
        if (!File.Exists(backup))
        {
            MessageBox.Show(this, "找不到原始备份:\r\n" + backup, "无法恢复", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (Patcher.IsProcessRunning("Resolve"))
        {
            MessageBox.Show(this, "请先完全退出 DaVinci Resolve。", "Resolve 正在运行", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (MessageBox.Show(this, "将用原始备份覆盖当前 Resolve.exe。\r\n\r\n继续吗？", "确认恢复", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        string temp = source + ".restore.tmp";
        try
        {
            SetBusy(true, "正在恢复...");
            File.Copy(backup, temp, true);
            File.Replace(temp, source, null);
            Log("已从备份恢复 Resolve.exe: " + backup);
            MessageBox.Show(this, "已恢复原始 Resolve.exe。", "恢复完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            allAssets.Clear();
            currentPe = null;
            selectedAsset = null;
            ClearReplacement();
            ApplyFilter();
        }
        catch (UnauthorizedAccessException ex)
        {
            MessageBox.Show(this, "没有权限恢复，请以管理员身份运行。\r\n\r\n" + ex.Message, "需要管理员权限", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "恢复失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            SetBusy(false, "就绪");
            UpdateButtons();
        }
    }
}

public static class Program
{
    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}


