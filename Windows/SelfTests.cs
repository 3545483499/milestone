using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace MilestoneWindows
{
    internal static class SelfTests
    {
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        public static int Run(string report)
        {
            string folder = Path.Combine(Path.GetTempPath(), "MilestoneTest-" + Guid.NewGuid());
            string path = Path.Combine(folder, "milestones.sqlite");
            try
            {
                var initial = new DateTime(2026, 9, 25, 8, 0, 0, DateTimeKind.Utc);
                var first = new Milestone { text = "硬件验证", modifiedAt = Database.Timestamp(initial) };
                var second = new Milestone { text = "固件调试", modifiedAt = Database.Timestamp(initial) };
                var project = new Project { name = "ESP32 小黄人" };
                project.milestones.Add(first); project.milestones.Add(second);
                var projects = new List<Project> { project, new Project { name = "网站设计" } };
                projects[1].milestones.Add(new Milestone());
                using (var db = new Database(path))
                {
                    Check(db.Load().Count == 0, "新库非空"); db.Save(projects);
                }
                using (var db = new Database(path))
                {
                    var restored = db.Load();
                    Check(restored.Count == 2 && restored[0].milestones[0].id == first.id, "重启持久化失败");
                    Check(!Database.Edit(first, first.text, initial.AddMinutes(2)), "相同文字被修改");
                    Check(first.modifiedAt == Database.Timestamp(initial), "未修改文字却计时");
                    Check(Database.Edit(first, "第一行\r\n第二行 🌱\r\n\r\n末行\r\n", initial.AddMinutes(2)), "编辑失败");
                    Check(first.modifiedAt == Database.Timestamp(initial.AddMinutes(2)), "修改计时失败");
                    Check(first.text == "第一行\n第二行 🌱\n\n末行\n", "换行格式失败");
                    Check(!Database.Edit(first, first.text.Replace("\n", "\r\n"), initial.AddMinutes(4)), "Windows 换行转换不应更新时间");
                    Check(Database.Move(project, first.id, 1), "排序失败");
                    Check(project.milestones[1].id == first.id, "最右节点错误");
                    Check(!Database.Move(project, first.id, 1), "边界检查失败");
                    Check(first.modifiedAt == Database.Timestamp(initial.AddMinutes(2)), "排序更改了时间");
                    db.Save(projects);
                    restored = db.Load();
                    Check(restored[0].milestones[1].text == first.text, "Unicode 与空行保存失败");
                    // Render and exercise the real WinForms controls against this disposable database.
                    foreach (float scale in new float[] { 1f, 1.25f, 1.5f, 2f })
                    {
                        db.Save(projects);
                        using (var form = new MainForm(db, scale))
                        {
                            form.Show(); Application.DoEvents(); form.VerifyEditorBehavior(); Application.DoEvents();
                            form.VerifyLayout();
                            using (var image = new Bitmap(form.Width, form.Height))
                            {
                                form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                                image.Save(Path.ChangeExtension(report, ".dpi-" + (scale * 100).ToString("0") + ".png"), System.Drawing.Imaging.ImageFormat.Png);
                            }
                            form.VerifyRebuildAndResize();
                            form.Close();
                        }
                    }
                    restored = db.Load(); restored[0].milestones.RemoveAt(0); restored.RemoveAt(1); db.Save(restored);
                    Check(db.Load().Count == 1 && db.Load()[0].milestones.Count == 1, "删除持久化失败");
                }
                File.WriteAllText(report, "PASS: Windows SQLite, restart persistence, multiline, blank lines, Unicode, timestamps, reorder, boundaries, deletion, actual editor controls, Ctrl+Enter command routing, blur-save, layout at 100/125/150/200 percent, label measurement, card overlap, dynamic rebuild, narrow window and long text.\r\nOS: " + Environment.OSVersion + "\r\n64-bit process: " + Environment.Is64BitProcess, Encoding.UTF8);
                return 0;
            }
            catch (Exception error) { File.WriteAllText(report, "FAIL\r\n" + error, Encoding.UTF8); return 1; }
            finally { try { Directory.Delete(folder, true); } catch { } }
        }
    }
}
