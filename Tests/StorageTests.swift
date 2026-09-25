import Foundation

@main struct StorageTests {
    static func main() throws {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent("milestone-tests-\(UUID())")
        defer { try? FileManager.default.removeItem(at: directory) }
        let url = directory.appendingPathComponent("test.sqlite")
        let initial = Date(timeIntervalSince1970: 1_800_000_000)
        let edited = initial.addingTimeInterval(120)
        let first = Milestone(text: "设计完成", modifiedAt: initial)
        let second = Milestone(text: "实现中", modifiedAt: initial)
        var projects = [Project(name: "本地项目", milestones: [first, second]), Project(name: "另一个项目")]
        do {
            let db = try Database(url: url)
            let empty = try db.load()
            precondition(empty.isEmpty, "新数据库必须为空")
            try db.save(projects)
        }
        let reopened = try Database(url: url)
        let restored = try reopened.load()
        precondition(restored == projects, "重新打开必须保留所有数据")
        CanvasMutation.updateText(&projects, projectID: projects[0].id, nodeID: first.id, text: first.text, now: edited)
        precondition(projects[0].milestones[0].modifiedAt == initial, "未改文字不得更新时间")
        let text = "最终方案：中文、emoji 🌱、引号 '\" 与自由文本\n第二行：硬件验证\n\n第四行：保留空行\n"
        CanvasMutation.updateText(&projects, projectID: projects[0].id, nodeID: first.id, text: text, now: edited)
        precondition(projects[0].milestones[0].modifiedAt == edited, "改字必须记录本次修改时间")
        CanvasMutation.move(&projects, projectID: projects[0].id, nodeID: first.id, offset: 1)
        precondition(projects[0].milestones.last?.id == first.id, "向右移动必须改变当前进度")
        precondition(projects[0].milestones.last?.modifiedAt == edited, "排序不得更新时间")
        CanvasMutation.move(&projects, projectID: projects[0].id, nodeID: first.id, offset: 1)
        precondition(projects[0].milestones.last?.id == first.id, "边界移动应安全忽略")
        try reopened.save(projects)
        let final = try reopened.load()
        precondition(final == projects, "Unicode 文字及顺序必须完整保存")
        projects[0].milestones.removeAll { $0.id == second.id }
        projects.removeLast()
        try reopened.save(projects)
        let deleted = try reopened.load()
        precondition(deleted == projects, "删除必须持久化")
        print("PASS: 重启持久化、多个项目、文字修改计时、未修改不计时、排序、边界、Unicode、删除")
    }
}
