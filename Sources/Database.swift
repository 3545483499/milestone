import Foundation

struct Milestone: Codable, Identifiable, Equatable {
    var id = UUID()
    var text: String
    var modifiedAt: Date?
}

struct Project: Codable, Identifiable, Equatable {
    var id = UUID()
    var name: String
    var milestones: [Milestone] = []
}

struct StorageError: LocalizedError {
    let message: String
    var errorDescription: String? { message }
}

final class Database {
    private var connection: OpaquePointer?
    let url: URL

    init(url: URL) throws {
        self.url = url
        try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
        guard sqlite3_open_v2(url.path, &connection, SQLITE_OPEN_CREATE | SQLITE_OPEN_READWRITE | SQLITE_OPEN_FULLMUTEX, nil) == SQLITE_OK else {
            let message = connection.map { String(cString: sqlite3_errmsg($0)) } ?? "无法打开数据库"
            sqlite3_close(connection)
            connection = nil
            throw StorageError(message: message)
        }
        sqlite3_busy_timeout(connection, 3000)
        try execute("PRAGMA journal_mode=WAL;")
        try execute("CREATE TABLE IF NOT EXISTS canvas (id INTEGER PRIMARY KEY CHECK(id=1), data BLOB NOT NULL);")
    }

    deinit { sqlite3_close(connection) }

    private func execute(_ sql: String) throws {
        guard sqlite3_exec(connection, sql, nil, nil, nil) == SQLITE_OK else { throw failure() }
    }

    private func failure() -> StorageError {
        StorageError(message: String(cString: sqlite3_errmsg(connection)))
    }

    func load() throws -> [Project] {
        var statement: OpaquePointer?
        guard sqlite3_prepare_v2(connection, "SELECT data FROM canvas WHERE id=1", -1, &statement, nil) == SQLITE_OK else { throw failure() }
        defer { sqlite3_finalize(statement) }
        let result = sqlite3_step(statement)
        if result == SQLITE_DONE { return [] }
        guard result == SQLITE_ROW, let pointer = sqlite3_column_blob(statement, 0) else { throw failure() }
        let data = Data(bytes: pointer, count: Int(sqlite3_column_bytes(statement, 0)))
        return try JSONDecoder().decode([Project].self, from: data)
    }

    func save(_ projects: [Project]) throws {
        let data = try JSONEncoder().encode(projects)
        var statement: OpaquePointer?
        guard sqlite3_prepare_v2(connection, "INSERT INTO canvas(id,data) VALUES(1,?) ON CONFLICT(id) DO UPDATE SET data=excluded.data", -1, &statement, nil) == SQLITE_OK else { throw failure() }
        defer { sqlite3_finalize(statement) }
        try data.withUnsafeBytes { bytes in
            let transient = unsafeBitCast(-1, to: sqlite3_destructor_type.self)
            guard sqlite3_bind_blob(statement, 1, bytes.baseAddress, Int32(bytes.count), transient) == SQLITE_OK,
                  sqlite3_step(statement) == SQLITE_DONE else { throw failure() }
        }
    }
}

enum CanvasMutation {
    static func updateText(_ projects: inout [Project], projectID: UUID, nodeID: UUID, text: String, now: Date = Date()) {
        guard let p = projects.firstIndex(where: { $0.id == projectID }),
              let n = projects[p].milestones.firstIndex(where: { $0.id == nodeID }),
              projects[p].milestones[n].text != text else { return }
        projects[p].milestones[n].text = text
        projects[p].milestones[n].modifiedAt = now
    }

    static func move(_ projects: inout [Project], projectID: UUID, nodeID: UUID, offset: Int) {
        guard let p = projects.firstIndex(where: { $0.id == projectID }),
              let n = projects[p].milestones.firstIndex(where: { $0.id == nodeID }) else { return }
        let destination = n + offset
        guard projects[p].milestones.indices.contains(destination) else { return }
        projects[p].milestones.swapAt(n, destination)
    }
}
