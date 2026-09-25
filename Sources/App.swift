import SwiftUI
import AppKit

private extension Notification.Name {
    static let saveMilestoneEditing = Notification.Name("Milestone.saveEditing")
}

@MainActor final class CanvasStore: ObservableObject {
    @Published private(set) var projects: [Project] = []
    @Published var error: String?
    @Published var ready = false
    private var database: Database?

    init() {
        do {
            let directory = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
                .appendingPathComponent("Milestone", isDirectory: true)
            let db = try Database(url: directory.appendingPathComponent("milestones.sqlite"))
            projects = try db.load()
            database = db
            ready = true
        } catch { self.error = "无法读取本地数据：\(error.localizedDescription)" }
    }

    @discardableResult func change(_ mutation: (inout [Project]) -> Void) -> Bool {
        guard let database, ready else { return false }
        var next = projects
        mutation(&next)
        guard next != projects else { return true }
        do {
            try database.save(next)
            projects = next
            return true
        } catch {
            self.error = "保存失败，原数据未被替换：\(error.localizedDescription)"
            return false
        }
    }
}

private enum Editor: Hashable { case project(UUID), node(UUID, UUID) }
private enum Removal: Identifiable {
    case project(UUID), node(UUID, UUID)
    var id: String { String(describing: self) }
}

@main struct MilestoneApp: App {
    @StateObject private var store = CanvasStore()
    var body: some Scene {
        Window("里程碑", id: "canvas") {
            BoardView(store: store)
                .frame(minWidth: 760, minHeight: 480)
        }
        .defaultSize(width: 1180, height: 740)
        .windowStyle(.hiddenTitleBar)
        .commands {
            CommandGroup(replacing: .newItem) { }
            CommandGroup(after: .saveItem) {
                Button("保存当前编辑") {
                    NotificationCenter.default.post(name: .saveMilestoneEditing, object: nil)
                }
                .keyboardShortcut(.return, modifiers: .command)
            }
            CommandGroup(replacing: .help) { }
        }
    }
}

private struct BoardView: View {
    @ObservedObject var store: CanvasStore
    @FocusState private var focused: Editor?
    @State private var drafts: [Editor: String] = [:]
    @State private var showNewProject = false
    @State private var projectName = ""
    @State private var removal: Removal?
    private let accent = Color(red: 0.19, green: 0.43, blue: 0.37)
    private let canvas = Color(nsColor: .windowBackgroundColor)

    var body: some View {
        VStack(spacing: 0) {
            header
            Divider()
            if store.projects.isEmpty {
                emptyState
            } else {
                ScrollView([.horizontal, .vertical]) {
                    VStack(alignment: .leading, spacing: 0) {
                        ForEach(Array(store.projects.enumerated()), id: \.element.id) { index, project in
                            projectRow(project, index: index)
                            Divider().padding(.leading, 28)
                        }
                        Color.clear.frame(height: 120)
                            .contentShape(Rectangle()).onTapGesture { finishEditing() }
                    }
                    .padding(.top, 12)
                    .frame(minWidth: 1100, alignment: .topLeading)
                }
                .background(canvas.onTapGesture { finishEditing() })
            }
            footer
        }
        .tint(accent)
        .background(canvas)
        .onChange(of: focused) { oldValue, _ in commit(oldValue) }
        .onReceive(NotificationCenter.default.publisher(for: .saveMilestoneEditing)) { _ in finishEditing() }
        .onReceive(NotificationCenter.default.publisher(for: NSApplication.willResignActiveNotification)) { _ in finishEditing() }
        .onReceive(NotificationCenter.default.publisher(for: NSApplication.willTerminateNotification)) { _ in finishEditing() }
        .onReceive(NotificationCenter.default.publisher(for: NSWindow.willCloseNotification)) { notification in
            if let window = notification.object as? NSWindow, window.title == "里程碑" { finishEditing() }
        }
        .sheet(isPresented: $showNewProject) { newProjectSheet }
        .alert("未能保存", isPresented: Binding(get: { store.error != nil }, set: { if !$0 { store.error = nil } })) {
            Button("知道了") { store.error = nil }
        } message: { Text(store.error ?? "") }
        .alert(item: $removal) { item in
            switch item {
            case .project(let id):
                return Alert(title: Text("删除这个项目？"), message: Text("该项目和其中的所有里程碑都会被删除。"), primaryButton: .destructive(Text("删除")) {
                    finishEditing()
                    store.change { $0.removeAll { $0.id == id } }
                }, secondaryButton: .cancel(Text("取消")))
            case .node(let projectID, let nodeID):
                return Alert(title: Text("删除这个里程碑？"), message: Text("删除后无法恢复。"), primaryButton: .destructive(Text("删除")) {
                    finishEditing()
                    store.change { projects in
                        guard let p = projects.firstIndex(where: { $0.id == projectID }) else { return }
                        projects[p].milestones.removeAll { $0.id == nodeID }
                    }
                }, secondaryButton: .cancel(Text("取消")))
            }
        }
    }

    private var header: some View {
        HStack(alignment: .center, spacing: 14) {
            Image(systemName: "point.3.connected.trianglepath.dotted")
                .font(.system(size: 24, weight: .medium)).foregroundStyle(accent)
            VStack(alignment: .leading, spacing: 4) {
                Text("里程碑").font(.system(size: 23, weight: .semibold))
                Text("把每一步，留在这里。").font(.system(size: 12)).foregroundStyle(.secondary)
            }
            Spacer()
            Button { finishEditing(); projectName = ""; showNewProject = true } label: {
                Label("新建项目", systemImage: "plus").padding(.horizontal, 8).padding(.vertical, 5)
            }
            .buttonStyle(.borderedProminent).disabled(!store.ready)
            .keyboardShortcut("n", modifiers: .command)
        }
        .padding(.horizontal, 28).padding(.top, 32).padding(.bottom, 22)
        .contentShape(Rectangle()).onTapGesture { finishEditing() }
    }

    private var emptyState: some View {
        VStack(spacing: 17) {
            HStack(spacing: 0) {
                ForEach(0..<3) { index in
                    RoundedRectangle(cornerRadius: 10)
                        .fill(accent.opacity(index == 2 ? 0.15 : 0.06))
                        .overlay(RoundedRectangle(cornerRadius: 10).stroke(accent.opacity(0.25)))
                        .frame(width: 76, height: 48)
                        .overlay(Image(systemName: index == 2 ? "flag.fill" : "circle.fill").font(.system(size: index == 2 ? 14 : 6)).foregroundStyle(accent.opacity(0.7)))
                    if index < 2 { Rectangle().fill(accent.opacity(0.25)).frame(width: 28, height: 1) }
                }
            }.padding(.bottom, 8)
            Text("从第一个项目开始").font(.system(size: 21, weight: .medium))
            Text("一行一个项目，一步一个里程碑。").foregroundStyle(.secondary)
            Button("新建项目") { projectName = ""; showNewProject = true }
                .buttonStyle(.borderedProminent).controlSize(.large).padding(.top, 6).disabled(!store.ready)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .contentShape(Rectangle()).onTapGesture { finishEditing() }
    }

    private var footer: some View {
        HStack(spacing: 6) {
            Image(systemName: "internaldrive").foregroundStyle(accent)
            Text(store.ready ? "本地保存" : "数据库不可用")
            Text("·").padding(.horizontal, 4)
            Text("\(store.projects.count) 个项目")
            Spacer()
            Text("回车换行 · ⌘Enter 或点击空白处保存")
        }
        .font(.system(size: 11)).foregroundStyle(.secondary)
        .padding(.horizontal, 28).padding(.vertical, 13)
        .background(.bar)
        .contentShape(Rectangle()).onTapGesture { finishEditing() }
    }

    private func projectRow(_ project: Project, index: Int) -> some View {
        HStack(alignment: .center, spacing: 0) {
            VStack(alignment: .leading, spacing: 10) {
                Text(String(format: "%02d", index + 1)).font(.system(size: 11, weight: .medium, design: .monospaced)).foregroundStyle(.tertiary)
                TextField("项目名称", text: binding(.project(project.id), fallback: project.name))
                    .textFieldStyle(.plain).font(.system(size: 16, weight: .semibold))
                    .focused($focused, equals: .project(project.id))
                    .onSubmit { finishEditing() }
                    .accessibilityLabel("项目名称")
                    .help("点击或双击修改项目名称")
                HStack {
                    Text("\(project.milestones.count) 个里程碑").font(.system(size: 11)).foregroundStyle(.secondary)
                    Spacer()
                    Menu {
                        Button("重命名") { focused = .project(project.id) }
                        Button("删除项目…", role: .destructive) { finishEditing(); removal = .project(project.id) }
                    } label: { Image(systemName: "ellipsis") }
                        .menuStyle(.borderlessButton).fixedSize().accessibilityLabel("项目操作：\(project.name)")
                }
            }
            .frame(width: 166, alignment: .leading).padding(.trailing, 30)

            ForEach(Array(project.milestones.enumerated()), id: \.element.id) { nodeIndex, node in
                if nodeIndex > 0 {
                    HStack(spacing: 0) {
                        Rectangle().fill(accent.opacity(0.25)).frame(width: 26, height: 1)
                        Image(systemName: "chevron.right").font(.system(size: 8, weight: .medium)).foregroundStyle(accent.opacity(0.45))
                    }.frame(width: 40)
                }
                nodeCard(node, project: project, index: nodeIndex)
            }
            Button { addNode(project) } label: {
                VStack(spacing: 8) {
                    Image(systemName: "plus").font(.system(size: 16, weight: .medium))
                    Text("里程碑").font(.system(size: 11))
                }
                .foregroundStyle(accent.opacity(0.8)).frame(width: 80, height: 104)
                .background(RoundedRectangle(cornerRadius: 13).strokeBorder(accent.opacity(0.25), style: StrokeStyle(lineWidth: 1, dash: [4, 4])))
            }
            .buttonStyle(.plain).padding(.leading, project.milestones.isEmpty ? 0 : 22)
            .accessibilityLabel("为\(project.name)添加里程碑")
            Spacer(minLength: 28)
        }
        .padding(.horizontal, 28).padding(.vertical, 30)
        .background(canvas.onTapGesture { finishEditing() })
    }

    private func nodeCard(_ node: Milestone, project: Project, index: Int) -> some View {
        let current = index == project.milestones.count - 1
        let key = Editor.node(project.id, node.id)
        return VStack(alignment: .leading, spacing: 12) {
            HStack {
                Text(current ? "当前进度" : String(format: "%02d", index + 1))
                    .font(.system(size: 10, weight: .medium)).foregroundStyle(current ? accent : .secondary)
                Spacer()
                Menu {
                    Button("编辑文字") { focused = key }
                    Button("向左移动") { move(project.id, node.id, offset: -1) }.disabled(index == 0)
                    Button("向右移动") { move(project.id, node.id, offset: 1) }.disabled(current)
                    Divider()
                    Button("删除里程碑…", role: .destructive) { finishEditing(); removal = .node(project.id, node.id) }
                } label: { Image(systemName: "ellipsis").foregroundStyle(.secondary) }
                    .menuStyle(.borderlessButton).fixedSize().accessibilityLabel("里程碑操作")
            }
            // Keep the actual editor mounted. Creating it only after its own
            // FocusState becomes active makes focus acquisition circular.
            ZStack(alignment: .topLeading) {
                if (drafts[key] ?? node.text).isEmpty {
                    Text("写下这个里程碑…")
                        .font(.system(size: 14, weight: .medium))
                        .foregroundStyle(.secondary)
                        .padding(.leading, 5).padding(.top, 1)
                        .allowsHitTesting(false)
                }
                TextEditor(text: binding(key, fallback: node.text))
                    .font(.system(size: 14, weight: .medium))
                    .scrollContentBackground(.hidden)
                    .focused($focused, equals: key)
                    .accessibilityLabel("里程碑文字")
                    .help("回车换行，⌘Enter 或点击文本框外保存")
            }
            .frame(height: editorHeight(drafts[key] ?? node.text))
            Text(node.modifiedAt.map { Self.timestamp.string(from: $0) } ?? "等待填写")
                .font(.system(size: 10, design: .monospaced)).foregroundStyle(.secondary)
        }
        .padding(16).frame(width: 238, alignment: .leading)
        .background(RoundedRectangle(cornerRadius: 13).fill(Color(nsColor: .controlBackgroundColor)))
        .overlay(RoundedRectangle(cornerRadius: 13).strokeBorder(current ? accent.opacity(0.6) : Color.primary.opacity(0.10), lineWidth: current ? 1.4 : 1).allowsHitTesting(false))
        .background(RoundedRectangle(cornerRadius: 13).fill(accent.opacity(current ? 0.04 : 0)).shadow(color: .black.opacity(0.025), radius: 6, y: 3))
    }

    private var newProjectSheet: some View {
        NewProjectSheet(name: $projectName) {
            let name = projectName.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !name.isEmpty else { return }
            if store.change({ $0.append(Project(name: name)) }) { showNewProject = false }
        } cancel: { showNewProject = false }
    }

    private static let timestamp: DateFormatter = {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "zh_CN")
        formatter.dateFormat = "yyyy-MM-dd HH:mm"
        return formatter
    }()

    private func binding(_ key: Editor, fallback: String) -> Binding<String> {
        Binding(get: { drafts[key] ?? fallback }, set: { drafts[key] = $0 })
    }

    private func editorHeight(_ text: String) -> CGFloat {
        let size = (text + "\n ") as NSString
        let bounds = size.boundingRect(
            with: NSSize(width: 196, height: CGFloat.greatestFiniteMagnitude),
            options: [.usesLineFragmentOrigin, .usesFontLeading],
            attributes: [.font: NSFont.systemFont(ofSize: 14, weight: .medium)]
        )
        return min(240, max(44, ceil(bounds.height) + 8))
    }

    private func commit(_ key: Editor?) {
        guard let key, let value = drafts[key] else { return }
        let saved: Bool
        switch key {
        case .project(let id):
            let name = value.trimmingCharacters(in: .whitespacesAndNewlines)
            if name.isEmpty { drafts.removeValue(forKey: key); return }
            saved = store.change { projects in
                if let p = projects.firstIndex(where: { $0.id == id }) { projects[p].name = name }
            }
        case .node(let projectID, let nodeID):
            saved = store.change { CanvasMutation.updateText(&$0, projectID: projectID, nodeID: nodeID, text: value) }
        }
        if saved { drafts.removeValue(forKey: key) }
    }

    private func finishEditing() { commit(focused); focused = nil }

    private func addNode(_ project: Project) {
        finishEditing()
        let node = Milestone(text: "", modifiedAt: nil)
        if store.change({ projects in
            if let p = projects.firstIndex(where: { $0.id == project.id }) { projects[p].milestones.append(node) }
        }) {
            // The new field must join the view hierarchy before requesting focus.
            DispatchQueue.main.async { focused = .node(project.id, node.id) }
        }
    }

    private func move(_ projectID: UUID, _ nodeID: UUID, offset: Int) {
        finishEditing()
        store.change { CanvasMutation.move(&$0, projectID: projectID, nodeID: nodeID, offset: offset) }
    }
}

private struct NewProjectSheet: View {
    @Binding var name: String
    let create: () -> Void
    let cancel: () -> Void
    @FocusState private var focused: Bool
    var body: some View {
        VStack(alignment: .leading, spacing: 20) {
            Text("新建项目").font(.system(size: 22, weight: .semibold))
            TextField("项目名称", text: $name).textFieldStyle(.roundedBorder)
                .focused($focused).onSubmit { create() }
            HStack {
                Spacer()
                Button("取消", action: cancel).keyboardShortcut(.cancelAction)
                Button("创建", action: create).buttonStyle(.borderedProminent)
                    .keyboardShortcut(.defaultAction)
                    .disabled(name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
            }
        }.padding(28).frame(width: 360).onAppear { focused = true }
    }
}
