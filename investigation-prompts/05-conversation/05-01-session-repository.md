# Investigation Prompt: Conversation Session Repository

**Created**: 2026-03-16
**Focus**: Conversation session persistence and repository pattern implementation

---

## Source Files

Investigate the following TypeScript source files for session/conversation persistence patterns:

### Primary Files to Analyze

| File Path | Purpose |
|-----------|---------|
| `packages/core/src/storage/` | Core storage implementations |
| `packages/core/src/persistence/` | Persistence layer abstractions |
| `packages/cli/src/services/session/` | Session management services |
| Any `*sqlite*` or `*database*` files | SQLite implementation details |

### Search Patterns

```bash
# Find session-related files
find . -type f -name "*.ts" | xargs grep -l "session" | head -20

# Find SQLite/Database implementations
find . -type f -name "*.ts" | xargs grep -l -i "sqlite\|database" | head -20

# Find message storage patterns
grep -r "message" --include="*.ts" | grep -i "insert\|save\|store" | head -20
```

---

## Investigation Questions

### 1. How does TS store conversation sessions?

- What is the session lifecycle (create, update, delete)?
- How are sessions identified (UUID, incremental ID)?
- What metadata is stored per session?
- Are sessions stored in memory, file system, or database?

### 2. What is the schema for sessions, messages, and tool calls?

- What tables/collections exist?
- What are the relationships between sessions, messages, and tool calls?
- What indexes are defined for performance?
- How is data versioned or migrated?

### 3. What .NET options exist for session storage?

| Option | Pros | Cons | Use Case |
|--------|------|------|----------|
| Entity Framework Core | Full-featured, LINQ support, migrations | Heavier, learning curve | Complex domain models |
| Dapper | Lightweight, fast, raw SQL control | Manual mapping, no migrations | High-performance scenarios |
| LiteDB | Embedded, NoSQL, zero-config | Less query flexibility | Desktop/mobile apps |
| SQLite via Microsoft.Data.Sqlite | Direct SQLite access, full control | Manual everything | Maximum control needed |

### 4. How to implement repository pattern in .NET?

- Define generic `IRepository<T>` interface
- Create specific `ISessionRepository`, `IMessageRepository` interfaces
- Implement with EF Core DbContext or Dapper
- Consider Unit of Work pattern for transactions

---

## Code Patterns to Investigate

### Session Creation Pattern

```typescript
// Look for patterns like:
interface Session {
  id: string;
  createdAt: Date;
  updatedAt: Date;
  metadata?: Record<string, unknown>;
}

// How is a new session created?
async createSession(): Promise<Session>
```

### Message Storage Pattern

```typescript
// Look for patterns like:
interface Message {
  id: string;
  sessionId: string;
  role: 'user' | 'assistant' | 'system';
  content: string;
  timestamp: Date;
  toolCalls?: ToolCall[];
}

// How are messages persisted?
async saveMessage(sessionId: string, message: Message): Promise<void>
```

### Retrieval Pattern

```typescript
// How are sessions and messages retrieved?
async getSession(id: string): Promise<Session | null>
async getMessages(sessionId: string, limit?: number): Promise<Message[]>
async getRecentSessions(count: number): Promise<Session[]>
```

---

## Expected Deliverables

### 1. Repository Interfaces

```csharp
// ISessionRepository.cs
public interface ISessionRepository
{
    Task<Session?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<Session>> GetRecentAsync(int count);
    Task<Session> CreateAsync(Session session);
    Task UpdateAsync(Session session);
    Task DeleteAsync(Guid id);
}

// IMessageRepository.cs
public interface IMessageRepository
{
    Task<IReadOnlyList<Message>> GetBySessionIdAsync(Guid sessionId, int? limit = null);
    Task<Message> AddAsync(Message message);
    Task DeleteBySessionIdAsync(Guid sessionId);
}
```

### 2. EF Core Implementation

```csharp
// SessionRepository.cs
public class SessionRepository : ISessionRepository
{
    private readonly ConversationDbContext _context;

    public SessionRepository(ConversationDbContext context)
    {
        _context = context;
    }

    // Implementation details...
}

// ConversationDbContext.cs
public class ConversationDbContext : DbContext
{
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<ToolCall> ToolCalls => Set<ToolCall>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure relationships, indexes, etc.
    }
}
```

### 3. Entity Models

```csharp
public class Session
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? Metadata { get; set; } // JSON serialized
    public ICollection<Message> Messages { get; set; } = [];
}

public class Message
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public MessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public ICollection<ToolCall> ToolCalls { get; set; } = [];
}

public class ToolCall
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string? Result { get; set; }
}
```

### 4. Service Layer (Optional)

```csharp
public interface IConversationService
{
    Task<Session> StartNewSessionAsync();
    Task AddMessageAsync(Guid sessionId, MessageRole role, string content);
    Task<IReadOnlyList<Message>> GetConversationHistoryAsync(Guid sessionId);
}
```

---

## Investigation Tasks

- [ ] Locate TypeScript session persistence implementation
- [ ] Document the database schema used
- [ ] Identify key CRUD operations
- [ ] Note any caching strategies
- [ ] Document migration patterns
- [ ] Design equivalent .NET interfaces
- [ ] Create EF Core entity configurations
- [ ] Implement repository classes
- [ ] Add unit tests for repositories

---

## Notes

- Consider using SQLite for parity with TypeScript implementation
- Evaluate whether to use code-first or database-first EF Core approach
- Consider connection string management for different environments
- Document any performance considerations for message retrieval
