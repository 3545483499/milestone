using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("里程碑")]
[assembly: AssemblyDescription("离线项目里程碑画布")]
[assembly: AssemblyProduct("里程碑")]
[assembly: AssemblyVersion("1.1.1.0")]

namespace MilestoneWindows
{
    internal static class Program
    {
        [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
        [STAThread] private static int Main(string[] args)
        {
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length > 0 && args[0] == "--self-test") return SelfTests.Run(args.Length > 1 ? args[1] : "test-results.txt");
            bool created;
            using (var mutex = new Mutex(true, @"Local\MilestoneCanvasWindows", out created))
            {
                if (!created) { MessageBox.Show("里程碑已经打开，请切换到现有窗口。", "里程碑"); return 0; }
                try
                {
                    string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Milestone");
                    using (var database = new Database(Path.Combine(directory, "milestones.sqlite")))
                    using (var window = new MainForm(database)) Application.Run(window);
                    return 0;
                }
                catch (Exception error)
                {
                    MessageBox.Show("无法启动里程碑：\n" + error.Message + "\n\n请检查数据库或系统环境。原数据不会被清空。", "里程碑", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
            }
        }
    }

    internal sealed class CanvasPanel : Panel
    {
        public CanvasPanel() { DoubleBuffered = true; SetStyle(ControlStyles.Selectable, true); }
    }

    internal sealed class CardPanel : Panel
    {
        public bool Current;
        public float UiScale = 1f;
        public CardPanel() { DoubleBuffered = true; BackColor = Color.White; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = new GraphicsPath())
            using (var pen = new Pen(Current ? Color.FromArgb(112, 160, 145) : Color.FromArgb(218, 226, 222), (Current ? 1.5f : 1f) * UiScale))
            {
                int w = Width - 2, h = Height - 2, d = (int)Math.Round(22 * UiScale);
                path.AddArc(1, 1, d, d, 180, 90);
                path.AddArc(w - d, 1, d, d, 270, 90);
                path.AddArc(w - d, h - d, d, d, 0, 90);
                path.AddArc(1, h - d, d, d, 90, 90);
                path.CloseFigure();
                e.Graphics.DrawPath(pen, path);
            }
        }
    }

    internal sealed class EditorBinding
    {
        public TextBox Box;
        public string ProjectId;
        public string NodeId;
        public Label TimeLabel;
    }

    internal sealed class MainForm : Form
    {
        private static readonly Color Green = Color.FromArgb(48, 110, 94);
        private static readonly Color CanvasColor = Color.FromArgb(249, 250, 248);
        private readonly Database database;
        private List<Project> projects;
        private readonly CanvasPanel canvas = new CanvasPanel();
        private readonly Panel header = new Panel();
        private readonly Panel footer = new Panel();
        private readonly Label countLabel = new Label();
        private readonly List<EditorBinding> editors = new List<EditorBinding>();
        private readonly List<Panel> rows = new List<Panel>();
        private readonly ToolTip toolTip = new ToolTip();
        private readonly List<ContextMenuStrip> menus = new List<ContextMenuStrip>();
        private bool rebuilding;
        private bool saving;
        private readonly float uiScale;

        public MainForm(Database database, float? testScale = null)
        {
            this.database = database;
            using (var graphics = Graphics.FromHwnd(IntPtr.Zero))
                uiScale = testScale ?? (graphics.DpiX / 96f);
            projects = database.Load();
            Text = "里程碑";
            Font = UiFont(9);
            // Every coordinate and font uses the same logical-pixel scale. Disable
            // implicit WinForms scaling so controls rebuilt at runtime do not diverge.
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = UiSize(1180, 720);
            MinimumSize = UiSize(800, 500);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = CanvasColor;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AppIcon.ico"))
                if (stream != null) Icon = new Icon(stream);
            header.Dock = DockStyle.Top; header.Height = S(110); header.BackColor = Color.White;
            footer.Dock = DockStyle.Bottom; footer.Height = S(38); footer.BackColor = Color.White;
            canvas.Dock = DockStyle.Fill; canvas.AutoScroll = true; canvas.BackColor = CanvasColor;
            Controls.Add(canvas); Controls.Add(footer); Controls.Add(header);
            var logo = new PictureBox { Location = UiPoint(28, 29), Size = UiSize(40, 40), SizeMode = PictureBoxSizeMode.Zoom };
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Logo.png"))
                if (stream != null) { using (var image = Image.FromStream(stream)) logo.Image = new Bitmap(image); }
            header.Controls.Add(logo);
            header.Controls.Add(LabelAt("里程碑", 82, 18, 280, 42, 21, Color.FromArgb(35, 42, 39), true));
            header.Controls.Add(LabelAt("把每一步，留在这里。", 84, 65, 300, 24, 9, Color.Gray, false));
            var create = ButtonAt("＋ 新建项目", 0, 31, 130, 38, true);
            create.Anchor = AnchorStyles.Top | AnchorStyles.Right; create.Left = ClientSize.Width - S(158);
            create.Click += (s, e) => NewProject(); header.Controls.Add(create);
            countLabel.Location = UiPoint(28, 9); countLabel.AutoSize = true; countLabel.ForeColor = Color.Gray;
            footer.Controls.Add(countLabel);
            var hint = LabelAt("回车换行 · Ctrl+Enter 或点击空白处保存", 0, 9, 380, 22, 9, Color.Gray, false);
            hint.TextAlign = ContentAlignment.TopRight; hint.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            hint.Left = ClientSize.Width - S(408); footer.Controls.Add(hint);
            footer.Resize += (s, e) => LayoutFooter(hint);
            countLabel.TextChanged += (s, e) => LayoutFooter(hint);
            canvas.MouseDown += (s, e) => SaveAndBlur();
            header.MouseDown += (s, e) => SaveAndBlur();
            footer.MouseDown += (s, e) => SaveAndBlur();
            foreach (Control child in header.Controls) if (!(child is Button)) child.MouseDown += (s, e) => SaveAndBlur();
            hint.MouseDown += (s, e) => SaveAndBlur(); countLabel.MouseDown += (s, e) => SaveAndBlur();
            canvas.Resize += (s, e) => LayoutRows();
            FormClosing += (s, e) => { if (!SaveEdits()) e.Cancel = true; };
            Deactivate += (s, e) => { if (!rebuilding && !IsDisposed) SaveEdits(); };
            Rebuild();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Enter)) { SaveAndBlur(); return true; }
            if (keyData == (Keys.Control | Keys.N)) { NewProject(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private int S(int value) { return (int)Math.Round(value * uiScale); }
        private Size UiSize(int width, int height) { return new Size(S(width), S(height)); }
        private Point UiPoint(int x, int y) { return new Point(S(x), S(y)); }
        private Font UiFont(float points, bool bold = false)
        {
            return new Font("Microsoft YaHei UI", points * (96f / 72f) * uiScale,
                bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        }

        private Label LabelAt(string text, int x, int y, int width, int height, float size, Color color, bool bold)
        {
            return new Label { Text = text, Location = UiPoint(x, y), Size = UiSize(width, height),
                Font = UiFont(size, bold), ForeColor = color, BackColor = Color.Transparent };
        }
        private Button ButtonAt(string text, int x, int y, int width, int height, bool primary)
        {
            var button = new Button { Text = text, Location = UiPoint(x, y), Size = UiSize(width, height), FlatStyle = FlatStyle.Flat,
                Font = UiFont(9), BackColor = primary ? Green : Color.White, ForeColor = primary ? Color.White : Green, Cursor = Cursors.Hand };
            button.FlatAppearance.BorderColor = Color.FromArgb(203, 220, 212);
            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            return button;
        }

        private void Rebuild(string focusId = null)
        {
            rebuilding = true;
            Point scroll = canvas.AutoScrollPosition;
            canvas.SuspendLayout();
            foreach (var menu in menus) menu.Dispose(); menus.Clear();
            editors.Clear(); rows.Clear();
            while (canvas.Controls.Count > 0) canvas.Controls[0].Dispose();
            canvas.AutoScrollPosition = Point.Empty;
            if (projects.Count == 0)
            {
                var empty = new Panel { Location = UiPoint(28, 75), Size = UiSize(670, 190) };
                empty.Controls.Add(LabelAt("从第一个项目开始", 0, 10, 620, 42, 22, Green, true));
                empty.Controls.Add(LabelAt("一行一个项目，一步一个里程碑。", 0, 63, 620, 30, 11, Color.Gray, false));
                var add = ButtonAt("＋ 新建项目", 0, 114, 150, 40, true);
                add.Click += (s, e) => NewProject(); empty.Controls.Add(add); canvas.Controls.Add(empty);
            }
            for (int p = 0; p < projects.Count; p++) BuildRow(projects[p], p);
            countLabel.Text = "本地保存  ·  " + projects.Count + " 个项目";
            canvas.ResumeLayout();
            rebuilding = false;
            LayoutRows();
            canvas.AutoScrollPosition = new Point(-scroll.X, -scroll.Y);
            if (focusId != null)
            {
                var binding = editors.FirstOrDefault(x => x.NodeId == focusId || (x.NodeId == null && x.ProjectId == focusId));
                if (binding != null && IsHandleCreated) BeginInvoke((Action)(() => { canvas.ScrollControlIntoView(binding.Box.Parent); binding.Box.Focus(); binding.Box.SelectionStart = binding.Box.TextLength; }));
            }
        }

        private void BuildRow(Project project, int index)
        {
            var row = new Panel { BackColor = CanvasColor, Height = S(188), Tag = project.milestones.Count };
            rows.Add(row); canvas.Controls.Add(row);
            row.MouseDown += (s, e) => SaveAndBlur();
            row.Controls.Add(LabelAt((index + 1).ToString("D2"), 0, 27, 166, 20, 9, Color.DarkGray, false));
            var name = new TextBox { Text = project.name, Location = UiPoint(0, 55), Width = S(168), BorderStyle = BorderStyle.None,
                BackColor = CanvasColor, Font = UiFont(12, true), AccessibleName = "项目名称" };
            name.Leave += (s, e) => { if (!rebuilding) SaveEdits(); };
            name.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SaveAndBlur(); } };
            editors.Add(new EditorBinding { Box = name, ProjectId = project.id }); row.Controls.Add(name);
            row.Controls.Add(LabelAt(project.milestones.Count + " 个里程碑", 0, 92, 130, 23, 9, Color.Gray, false));
            var projectMenu = NewMenu();
            projectMenu.Items.Add("重命名", null, (s, e) => { name.Focus(); name.SelectAll(); });
            projectMenu.Items.Add("删除项目…", null, (s, e) => {
                if (SaveEdits() && Confirm("删除项目「" + project.name + "」及全部里程碑？"))
                    Mutate(list => list.RemoveAll(x => x.id == project.id), null);
            });
            var projectMore = ButtonAt("···", 134, 88, 34, 26, false);
            projectMore.FlatAppearance.BorderSize = 0; projectMore.BackColor = CanvasColor;
            projectMore.Click += (s, e) => projectMenu.Show(projectMore, 0, projectMore.Height); row.Controls.Add(projectMore);
            for (int n = 0; n < project.milestones.Count; n++) BuildCard(row, project, project.milestones[n], n);
            int addX = 196 + project.milestones.Count * 278;
            var add = ButtonAt("＋\n里程碑", addX, 26, 82, 112, false);
            add.Click += (s, e) => {
                var node = new Milestone();
                Mutate(list => list.First(x => x.id == project.id).milestones.Add(node), node.id);
            };
            row.Controls.Add(add);
            row.Paint += (s, e) => {
                using (var pen = new Pen(Color.FromArgb(188, 207, 198), 1.2f * uiScale))
                {
                    for (int n = 1; n < project.milestones.Count; n++)
                    {
                        int x = S(196 + n * 278 - 36);
                        e.Graphics.DrawLine(pen, x, S(83), x + S(27), S(83));
                        e.Graphics.DrawLine(pen, x + S(23), S(79), x + S(27), S(83));
                        e.Graphics.DrawLine(pen, x + S(23), S(87), x + S(27), S(83));
                    }
                    e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1);
                }
            };
        }

        private void BuildCard(Panel row, Project project, Milestone node, int index)
        {
            bool current = index == project.milestones.Count - 1;
            var card = new CardPanel { Current = current, UiScale = uiScale, Location = UiPoint(196 + index * 278, 12), Size = UiSize(238, 148) };
            card.Controls.Add(LabelAt(current ? "当前进度" : (index + 1).ToString("D2"), 16, 13, 165, 20, 8.5f, current ? Green : Color.Gray, false));
            var editor = new TextBox { Text = Database.Normalize(node.text).Replace("\n", "\r\n"), Multiline = true, AcceptsReturn = true, WordWrap = true,
                Location = UiPoint(16, 43), Size = UiSize(206, 56), BorderStyle = BorderStyle.None,
                Font = UiFont(10), BackColor = Color.White, ForeColor = Color.FromArgb(35, 42, 39),
                AccessibleName = "里程碑文字", ScrollBars = ScrollBars.Vertical };
            var time = LabelAt(Database.DisplayTime(node.modifiedAt), 16, 115, 207, 20, 8, Color.Gray, false);
            var placeholder = LabelAt("写下这个里程碑…", 16, 44, 194, 28, 10, Color.DarkGray, false);
            placeholder.Cursor = Cursors.IBeam;
            placeholder.Visible = editor.TextLength == 0 && !editor.Focused;
            placeholder.Click += (s, e) => editor.Focus();
            editor.Enter += (s, e) => placeholder.Visible = false;
            editor.Leave += (s, e) => { placeholder.Visible = editor.TextLength == 0; if (!rebuilding) SaveEdits(); };
            Action resize = () => {
                int textHeight = TextRenderer.MeasureText(editor.Text + "\n ", editor.Font, new Size(S(183), Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix).Height;
                editor.Height = Math.Min(S(216), Math.Max(S(56), textHeight + S(4)));
                time.Top = editor.Bottom + S(14); card.Height = time.Bottom + S(12);
                if (!rebuilding) LayoutRows();
            };
            editor.TextChanged += (s, e) => { placeholder.Visible = editor.TextLength == 0 && !editor.Focused; resize(); };
            toolTip.SetToolTip(editor, "回车换行，Ctrl+Enter 或点击文本框外保存");
            editors.Add(new EditorBinding { Box = editor, ProjectId = project.id, NodeId = node.id, TimeLabel = time });
            card.Controls.Add(editor); card.Controls.Add(time); card.Controls.Add(placeholder); placeholder.BringToFront();
            var menu = NewMenu();
            menu.Items.Add("编辑文字", null, (s, e) => editor.Focus());
            menu.Items.Add("向左移动", null, (s, e) => Mutate(list => Database.Move(list.First(x => x.id == project.id), node.id, -1), node.id)).Enabled = index > 0;
            menu.Items.Add("向右移动", null, (s, e) => Mutate(list => Database.Move(list.First(x => x.id == project.id), node.id, 1), node.id)).Enabled = !current;
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("删除里程碑…", null, (s, e) => {
                if (SaveEdits() && Confirm("删除这个里程碑？删除后无法恢复。"))
                    Mutate(list => list.First(x => x.id == project.id).milestones.RemoveAll(x => x.id == node.id), null);
            });
            var more = ButtonAt("···", 188, 8, 34, 26, false); more.FlatAppearance.BorderSize = 0;
            more.Click += (s, e) => { SaveEdits(); menu.Show(more, 0, more.Height); };
            card.Controls.Add(more); card.MouseDown += (s, e) => SaveAndBlur();
            foreach (Control child in card.Controls) if (child is Label && child != placeholder) child.MouseDown += (s, e) => SaveAndBlur();
            row.Controls.Add(card); resize();
        }

        private ContextMenuStrip NewMenu() { var menu = new ContextMenuStrip { Font = UiFont(9) }; menus.Add(menu); return menu; }
        private void LayoutFooter(Label hint)
        {
            if (footer.ClientSize.Width <= 0) return;
            int margin = S(28);
            int hintWidth = S(380);
            bool stacked = countLabel.Right + S(24) + hintWidth + margin > footer.ClientSize.Width;
            hint.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            hint.Width = stacked ? Math.Max(1, footer.ClientSize.Width - margin * 2) : hintWidth;
            int textHeight = TextRenderer.MeasureText(hint.Text, hint.Font, new Size(hint.Width, Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
            hint.Height = Math.Max(S(22), textHeight);
            hint.Location = new Point(stacked ? margin : footer.ClientSize.Width - margin - hintWidth,
                stacked ? countLabel.Bottom + S(8) : S(9));
            hint.TextAlign = stacked ? ContentAlignment.TopLeft : ContentAlignment.TopRight;
            int needed = hint.Bottom + S(9);
            if (footer.Height != needed) footer.Height = needed;
        }
        private void LayoutRows()
        {
            if (rebuilding) return;
            int top = S(20);
            Point scroll = canvas.AutoScrollPosition;
            int widest = 0;
            foreach (var row in rows)
            {
                int bottom = S(148);
                foreach (Control child in row.Controls) if (child is CardPanel) bottom = Math.Max(bottom, child.Bottom);
                row.Height = bottom + S(28);
                row.Width = Math.Max(canvas.ClientSize.Width - S(56), S(196 + (int)row.Tag * 278 + 110));
                row.Location = new Point(S(28) + scroll.X, top + scroll.Y);
                top += row.Height + S(12);
                widest = Math.Max(widest, row.Width + S(56));
            }
            canvas.AutoScrollMinSize = new Size(widest, rows.Count == 0 ? 0 : top + S(40));
        }

        private bool Confirm(string message) { return MessageBox.Show(this, message, "里程碑", MessageBoxButtons.OKCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.OK; }
        private bool SaveEdits()
        {
            if (saving || rebuilding) return true;
            saving = true;
            try
            {
                var next = Database.Clone(projects);
                bool changed = false;
                foreach (var binding in editors)
                {
                    var project = next.FirstOrDefault(x => x.id == binding.ProjectId);
                    if (project == null) continue;
                    if (binding.NodeId == null)
                    {
                        string name = binding.Box.Text.Trim();
                        if (name.Length == 0) { binding.Box.Text = project.name; continue; }
                        if (project.name != name) { project.name = name; changed = true; }
                    }
                    else
                    {
                        var node = project.milestones.FirstOrDefault(x => x.id == binding.NodeId);
                        if (node != null && Database.Edit(node, binding.Box.Text, DateTime.UtcNow)) changed = true;
                    }
                }
                if (changed) { database.Save(next); projects = next; }
                foreach (var binding in editors.Where(x => x.NodeId != null))
                {
                    var project = projects.FirstOrDefault(x => x.id == binding.ProjectId);
                    var node = project == null ? null : project.milestones.FirstOrDefault(x => x.id == binding.NodeId);
                    if (node != null) binding.TimeLabel.Text = Database.DisplayTime(node.modifiedAt);
                }
                return true;
            }
            catch (Exception error)
            {
                MessageBox.Show(this, "保存失败，文字仍保留在文本框中。\n" + error.Message, "里程碑", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            finally { saving = false; }
        }
        private void SaveAndBlur() { if (SaveEdits()) canvas.Focus(); }
        private void Mutate(Action<List<Project>> mutation, string focusId)
        {
            if (!SaveEdits()) return;
            try
            {
                var next = Database.Clone(projects); mutation(next); database.Save(next); projects = next; Rebuild(focusId);
            }
            catch (Exception error) { MessageBox.Show(this, "无法保存变更：\n" + error.Message, "里程碑", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        private void NewProject()
        {
            if (!SaveEdits()) return;
            using (var dialog = new ProjectDialog(uiScale))
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    var project = new Project { name = dialog.ProjectName };
                    Mutate(list => list.Add(project), project.id);
                }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { toolTip.Dispose(); foreach (var menu in menus) menu.Dispose(); }
            base.Dispose(disposing);
        }

        // Exercises actual editor controls and command routing in the isolated test database.
        internal void VerifyLayout()
        {
            VerifyLabels(header); VerifyLabels(footer); VerifyLabels(canvas);
            var heading = header.Controls.OfType<Label>().ToArray();
            if (heading[0].Bounds.IntersectsWith(heading[1].Bounds)) throw new Exception("标题与副标题重叠");
            var foot = footer.Controls.OfType<Label>().ToArray();
            if (foot[0].Bounds.IntersectsWith(foot[1].Bounds)) throw new Exception("底栏文字重叠 scale=" + uiScale + " client=" + ClientSize + " left=" + foot[0].Bounds + " right=" + foot[1].Bounds);
            for (int i = 0; i < rows.Count; i++)
            {
                if (i > 0 && rows[i - 1].Bottom > rows[i].Top) throw new Exception("项目行重叠");
                foreach (var card in rows[i].Controls.OfType<CardPanel>())
                {
                    var editor = card.Controls.OfType<TextBox>().Single();
                    foreach (var label in card.Controls.OfType<Label>())
                    {
                        if (label.Text != "写下这个里程碑…" && editor.Bounds.IntersectsWith(label.Bounds))
                            throw new Exception("文本与状态或时间重叠");
                        if (label.Right > card.ClientSize.Width || label.Bottom > card.ClientSize.Height)
                            throw new Exception("节点文字超出卡片");
                    }
                }
            }
        }

        private void VerifyLabels(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                var label = child as Label;
                if (label != null)
                {
                    var measured = TextRenderer.MeasureText(label.Text, label.Font, new Size(label.Width, Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                    if (measured.Height > label.Height || measured.Width > label.Width)
                        throw new Exception("文字裁切: " + label.Text + " measured=" + measured + " bounds=" + label.Size + " scale=" + uiScale);
                    if ((parent == header || parent == footer) && (label.Top < 0 || label.Bottom > parent.Height))
                        throw new Exception("标题或底栏文字超出容器");
                }
                VerifyLabels(child);
            }
        }

        internal void VerifyRebuildAndResize()
        {
            Rebuild(); Application.DoEvents(); VerifyLayout();
            var added = new Milestone();
            Mutate(list => list[0].milestones.Add(added), null);
            Application.DoEvents(); VerifyLayout();
            Mutate(list => list[0].milestones.RemoveAll(x => x.id == added.id), null);
            ClientSize = UiSize(800, 580); Application.DoEvents(); VerifyLayout();
            var binding = editors.First(x => x.NodeId != null);
            binding.Box.Text = String.Join("\r\n", Enumerable.Repeat("多行内容测试，卡片需要自动增高。", 15));
            Application.DoEvents(); VerifyLayout();
            SaveAndBlur();
        }

        internal void VerifyEditorBehavior()
        {
            var binding = editors.First(x => x.NodeId != null);
            binding.Box.Text = "第一行\r\n第二行\r\n\r\n末行 🌱";
            Message message = new Message();
            if (!ProcessCmdKey(ref message, Keys.Control | Keys.Enter)) throw new Exception("Ctrl+Enter 未处理");
            var saved = database.Load().First(x => x.id == binding.ProjectId).milestones.First(x => x.id == binding.NodeId);
            if (saved.text != "第一行\n第二行\n\n末行 🌱") throw new Exception("多行编辑未保存");
            var time = saved.modifiedAt;
            if (!SaveEdits()) throw new Exception("失焦保存失败");
            saved = database.Load().First(x => x.id == binding.ProjectId).milestones.First(x => x.id == binding.NodeId);
            if (saved.modifiedAt != time) throw new Exception("未修改文本却更新时间");
            if (!binding.Box.Multiline || !binding.Box.AcceptsReturn) throw new Exception("回车换行未启用");
            binding.Box.AppendText("\r\n失焦保存");
            SaveAndBlur();
            saved = database.Load().First(x => x.id == binding.ProjectId).milestones.First(x => x.id == binding.NodeId);
            if (!saved.text.EndsWith("\n失焦保存")) throw new Exception("点击外部保存未生效");
        }
    }

    internal sealed class ProjectDialog : Form
    {
        private readonly TextBox input = new TextBox();
        public string ProjectName { get { return input.Text.Trim(); } }
        public ProjectDialog(float scale)
        {
            Func<int, int> px = value => (int)Math.Round(value * scale);
            AutoScaleMode = AutoScaleMode.None;
            Text = "新建项目"; ClientSize = new Size(px(380), px(146)); FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Microsoft YaHei UI", 10 * (96f / 72f) * scale, FontStyle.Regular, GraphicsUnit.Pixel); ShowInTaskbar = false;
            input.SetBounds(px(24), px(28), px(332), px(30)); Controls.Add(input);
            var cancel = new Button { Text = "取消", Location = new Point(px(182), px(92)), Size = new Size(px(80), px(30)), DialogResult = DialogResult.Cancel };
            var create = new Button { Text = "创建", Location = new Point(px(276), px(92)), Size = new Size(px(80), px(30)), Enabled = false };
            input.TextChanged += (s, e) => create.Enabled = ProjectName.Length > 0;
            create.Click += (s, e) => { if (ProjectName.Length > 0) DialogResult = DialogResult.OK; };
            Controls.Add(cancel); Controls.Add(create); AcceptButton = create; CancelButton = cancel;
            Shown += (s, e) => input.Focus();
        }
    }
}
