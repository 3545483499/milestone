using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;

namespace MilestoneWindows
{
    public sealed class Milestone
    {
        public string id { get; set; }
        public string text { get; set; }
        public double? modifiedAt { get; set; }
        public Milestone() { id = Guid.NewGuid().ToString().ToUpperInvariant(); text = ""; }
    }

    public sealed class Project
    {
        public string id { get; set; }
        public string name { get; set; }
        public List<Milestone> milestones { get; set; }
        public Project() { id = Guid.NewGuid().ToString().ToUpperInvariant(); name = ""; milestones = new List<Milestone>(); }
    }

    public sealed class Database : IDisposable
    {
        private IntPtr connection;
        private static readonly IntPtr Transient = new IntPtr(-1);
        public readonly string Path;
        private static readonly DateTime SwiftEpoch = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public Database(string path)
        {
            Path = path;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            int result = sqlite3_open_v2(Utf8(path), out connection, 2 | 4 | 0x10000, IntPtr.Zero);
            if (result != 0) { var error = Failure(); Dispose(); throw error; }
            try
            {
                sqlite3_busy_timeout(connection, 3000);
                Execute("PRAGMA journal_mode=WAL;");
                Execute("CREATE TABLE IF NOT EXISTS canvas (id INTEGER PRIMARY KEY CHECK(id=1), data BLOB NOT NULL);");
            }
            catch { Dispose(); throw; }
        }

        public List<Project> Load()
        {
            IntPtr statement = Prepare("SELECT data FROM canvas WHERE id=1");
            try
            {
                int result = sqlite3_step(statement);
                if (result == 101) return new List<Project>();
                if (result != 100) throw Failure();
                int size = sqlite3_column_bytes(statement, 0);
                IntPtr pointer = sqlite3_column_blob(statement, 0);
                if (size == 0 || pointer == IntPtr.Zero) throw new InvalidDataException("数据库内容为空或损坏。");
                byte[] bytes = new byte[size];
                Marshal.Copy(pointer, bytes, 0, size);
                var projects = Serializer().Deserialize<List<Project>>(Encoding.UTF8.GetString(bytes));
                if (projects == null) throw new InvalidDataException("数据库格式不正确。");
                var ids = new HashSet<string>();
                foreach (var project in projects)
                {
                    if (project == null || String.IsNullOrEmpty(project.id) || !ids.Add(project.id) || project.name == null || project.milestones == null)
                        throw new InvalidDataException("项目记录不完整。");
                    foreach (var node in project.milestones)
                        if (node == null || String.IsNullOrEmpty(node.id) || !ids.Add(node.id) || node.text == null ||
                            (node.modifiedAt.HasValue && (Double.IsNaN(node.modifiedAt.Value) || Double.IsInfinity(node.modifiedAt.Value))))
                            throw new InvalidDataException("里程碑记录不完整。");
                }
                return projects;
            }
            finally { sqlite3_finalize(statement); }
        }

        public void Save(List<Project> projects)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(Serializer().Serialize(projects));
            // INSERT OR REPLACE works with the SQLite version supplied by older Windows 10 releases.
            IntPtr statement = Prepare("INSERT OR REPLACE INTO canvas(id,data) VALUES(1,?)");
            try
            {
                if (sqlite3_bind_blob(statement, 1, bytes, bytes.Length, Transient) != 0 || sqlite3_step(statement) != 101)
                    throw Failure();
            }
            finally { sqlite3_finalize(statement); }
        }

        public static double Timestamp(DateTime utc) { return (utc.ToUniversalTime() - SwiftEpoch).TotalSeconds; }
        public static string DisplayTime(double? timestamp)
        {
            if (!timestamp.HasValue) return "等待填写";
            try { return SwiftEpoch.AddSeconds(timestamp.Value).ToLocalTime().ToString("yyyy-MM-dd HH:mm"); }
            catch (ArgumentOutOfRangeException) { return "时间不可用"; }
        }
        public static string Normalize(string text) { return text.Replace("\r\n", "\n").Replace("\r", "\n"); }
        public static bool Edit(Milestone node, string text, DateTime now)
        {
            text = Normalize(text);
            if (Normalize(node.text) == text) return false;
            node.text = text;
            node.modifiedAt = Timestamp(now);
            return true;
        }
        public static bool Move(Project project, string nodeId, int offset)
        {
            int index = project.milestones.FindIndex(n => n.id == nodeId);
            int destination = index + offset;
            if (index < 0 || destination < 0 || destination >= project.milestones.Count) return false;
            var node = project.milestones[index];
            project.milestones.RemoveAt(index);
            project.milestones.Insert(destination, node);
            return true;
        }
        public static List<Project> Clone(List<Project> projects)
        {
            var serializer = Serializer();
            return serializer.Deserialize<List<Project>>(serializer.Serialize(projects));
        }
        private static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }; }
        private void Execute(string sql)
        {
            if (sqlite3_exec(connection, Utf8(sql), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero) != 0) throw Failure();
        }
        private IntPtr Prepare(string sql)
        {
            IntPtr statement;
            if (sqlite3_prepare_v2(connection, Utf8(sql), -1, out statement, IntPtr.Zero) != 0) throw Failure();
            return statement;
        }
        private Exception Failure()
        {
            IntPtr pointer = connection == IntPtr.Zero ? IntPtr.Zero : sqlite3_errmsg(connection);
            if (pointer == IntPtr.Zero) return new IOException("无法打开本地数据库。");
            int length = 0;
            while (Marshal.ReadByte(pointer, length) != 0) length++;
            byte[] bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return new IOException(Encoding.UTF8.GetString(bytes));
        }
        private static byte[] Utf8(string value) { return Encoding.UTF8.GetBytes(value + "\0"); }
        public void Dispose() { if (connection != IntPtr.Zero) { sqlite3_close(connection); connection = IntPtr.Zero; } }

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_open_v2(byte[] filename, out IntPtr db, int flags, IntPtr vfs);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_close(IntPtr db);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_busy_timeout(IntPtr db, int milliseconds);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr sqlite3_errmsg(IntPtr db);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_exec(IntPtr db, byte[] sql, IntPtr callback, IntPtr arg, IntPtr error);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int bytes, out IntPtr statement, IntPtr tail);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_step(IntPtr statement);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_finalize(IntPtr statement);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_bind_blob(IntPtr statement, int index, byte[] value, int bytes, IntPtr destructor);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr sqlite3_column_blob(IntPtr statement, int index);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_column_bytes(IntPtr statement, int index);
    }
}
