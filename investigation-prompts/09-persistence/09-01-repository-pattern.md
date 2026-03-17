# Investigation Prompt: Repository Pattern Implementation

**Created**: 2026-03-16
**Focus**: Repository pattern and unit of work for data persistence layer

---

## Source Files

Investigate the following TypeScript source files for repository patterns:

### Primary Files to Analyze

| File Path | Purpose |
|-----------|---------|
| `packages/core/src/storage/` | Core storage implementations |
| `packages/core/src/persistence/` | Persistence layer abstractions |
| `packages/cli/src/services/session/` | Session management services |
| Any `*Repository*` or `*Store*` files | Repository/store pattern implementations |

### Search Patterns

```bash
# Find repository patterns
find . -type f -name "*.ts" | xargs grep -l "repository\|Repository" | head -20

# Find state management patterns
find . -type f -name "*.ts" | xargs grep -l "agent.*state\|session.*state" | head -20

# Find storage abstraction patterns
grep -r "interface.*Store\|interface.*Repository" --include="*.ts" | head -20

# Find unit of work patterns
grep -r "transaction\|unitOfWork\|saveChanges" --include="*.ts" | head -20
```

---

## Investigation Questions

### 1. How are repositories structured in TS?

- What interfaces define the repository contracts?
- How are CRUD operations abstracted?
- What is the relationship between repositories and data models?
- Are there generic repository implementations?

### 2. What generic repository patterns exist in .NET?

| Pattern | Description | When to Use |
|---------|-------------|-------------|
| `IRepository<T>` | Generic CRUD interface | Simple entities with standard operations |
| `IReadOnlyRepository<T>` | Read-only data access | Query scenarios, reporting |
| `IAsyncRepository<T>` | Async-first design | Modern async/await patterns |
| Specification Pattern | Complex query encapsulation | Business rule-based queries |

### 3. How to implement unit of work pattern?

- What is the scope of a unit of work (request, session, operation)?
- How are transactions managed across multiple repositories?
- When should changes be committed vs rolled back?
- How to handle concurrent modifications?

### 4. How to support multiple storage backends (SQLite, PostgreSQL)?

| Backend | Connection | Migrations | Use Case |
|---------|------------|------------|----------|
| SQLite | File-based, embedded | EF Core migrations | Desktop, development, testing |
| PostgreSQL | Server connection | EF Core migrations | Production, scalable deployments |
| In-Memory | No persistence | None | Testing, caching |

---

## Code Patterns to Investigate

### Generic Repository Interface Pattern

```csharp
// Look for equivalent TS patterns and design:
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(Guid id);
}
```

### Unit of Work Pattern

```csharp
// How should transactions be scoped?
public interface IUnitOfWork : IDisposable
{
    ISessionRepository Sessions { get; }
    IMessageRepository Messages { get; }
    IToolCallRepository ToolCalls { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync();
    Task CommitAsync();
    Task RollbackAsync();
}
```

### Storage Abstraction Pattern

```csharp
// How to support multiple backends?
public interface IStorageProvider
{
    string Name { get; }
    bool SupportsMigrations { get; }
    Task InitializeAsync();
    Task<bool> IsHealthyAsync();
}

public interface IStorageProviderFactory
{
    IStorageProvider Create(StorageConfiguration config);
    IEnumerable<string> GetAvailableProviders();
}
```

---

## Expected Deliverables

### 1. Generic Repository Interface

```csharp
// IRepository.cs
public interface IRepository<T> where T : class, IEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> FindAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IEntity
{
    Guid Id { get; }
}
```

### 2. Unit of Work Interface

```csharp
// IUnitOfWork.cs
public interface IUnitOfWork : IAsyncDisposable
{
    ISessionRepository Sessions { get; }
    IMessageRepository Messages { get; }
    IAgentStateRepository AgentStates { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
```

### 3. SQLite Implementation

```csharp
// SqliteRepository.cs
public abstract class SqliteRepository<T> : IRepository<T> where T : class, IEntity
{
    protected readonly ConversationDbContext Context;
    protected readonly ILogger Logger;

    protected SqliteRepository(ConversationDbContext context, ILogger logger)
    {
        Context = context;
        Logger = logger;
    }

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Context.Set<T>().FindAsync([id], cancellationToken);
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await Context.Set<T>().ToListAsync(cancellationToken);
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await Context.Set<T>().AddAsync(entity, cancellationToken);
        return entity;
    }

    public virtual Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        Context.Set<T>().Update(entity);
        return Task.CompletedTask;
    }

    public virtual async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity != null)
        {
            Context.Set<T>().Remove(entity);
        }
    }

    public virtual async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Context.Set<T>().AnyAsync(e => e.Id == id, cancellationToken);
    }
}
```

### 4. Specific Repository Interfaces

```csharp
// ISessionRepository.cs
public interface ISessionRepository : IRepository<Session>
{
    Task<IReadOnlyList<Session>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Session>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task UpdateLastActivityAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

// IMessageRepository.cs
public interface IMessageRepository : IRepository<Message>
{
    Task<IReadOnlyList<Message>> GetBySessionIdAsync(Guid sessionId, int? limit = null, CancellationToken cancellationToken = default);
    Task<Message?> GetLastMessageAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<int> GetMessageCountAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

// IAgentStateRepository.cs
public interface IAgentStateRepository : IRepository<AgentState>
{
    Task<AgentState?> GetLatestAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentState>> GetHistoryAsync(Guid sessionId, int? limit = null, CancellationToken cancellationToken = default);
}
```

---

## Drizzle ORM → EF Core Mapping

The TypeScript architecture uses **Drizzle ORM** for SQLite with the following patterns:

### TypeScript (Drizzle ORM)
```typescript
// Drizzle schema definition
import { sqliteTable, text, integer } from 'drizzle-orm/sqlite-core';

export const agents = sqliteTable('agents', {
  id: text('id').primaryKey(),
  sessionId: text('session_id').notNull(),
  status: text('status').notNull(),
  config: text('config'), // JSON
  turnCount: integer('turn_count').default(0),
  createdAt: integer('created_at'),
});

// Drizzle query patterns
const agent = await db.select().from(agents).where(eq(agents.id, id));
const sessionAgents = await db.select().from(agents).where(eq(agents.sessionId, sessionId));
```

### C# (Entity Framework Core)
```csharp
// EF Core entity definition
public class Agent
{
    public string Id { get; set; }
    public string SessionId { get; set; }
    public AgentStatus Status { get; set; }
    public string? Config { get; set; } // JSON or owned type
    public int TurnCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

// EF Core DbContext
public class AgentDbContext : DbContext
{
    public DbSet<Agent> Agents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Agent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasConversion<string>();
            // JSON column mapping
            entity.Property(e => e.Config).HasColumnType<string>();
        });
    }
}

// EF Core query patterns
var agent = await context.Agents.FindAsync(id);
var sessionAgents = await context.Agents
    .Where(a => a.SessionId == sessionId)
    .ToListAsync();
```

### Key Mapping Table

| Drizzle ORM Concept | EF Core Equivalent | Notes |
|---------------------|-------------------|-------|
| `sqliteTable()` | `DbSet<T>` property | Table mapping |
| `text()`, `integer()` | CLR types (string, int) | Column type mapping |
| `.primaryKey()` | `HasKey()` | Primary key configuration |
| `.notNull()` | Required property | Non-null constraint |
| `.default()` | Default value in property | Default value |
| `db.select().from()` | `DbContext.Set<T>() | Query source |
| `.where(eq())` | `.Where()` | Filter clause |
| `.where(inArray())` | `.Contains()` | IN clause |
| JSON columns | `.HasColumnType<string>() + conversion | EF Core stores as string,| Migrations | `dotnet ef migrations add` | Schema evolution |

### Transaction Patterns

```typescript
// Drizzle transaction
await db.transaction(async (tx) => {
  await tx.insert(agents).values(newAgent);
  await tx.update(agents).set({ status: 'running' }).where(eq(agents.id, id));
});
```

```csharp
// EF Core transaction
using var transaction = await context.Database.BeginTransactionAsync();
try
{
    context.Agents.Add(newAgent);
    agent.Status = AgentStatus.Running;
    await context.SaveChangesAsync();
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

---

## Investigation Tasks

- [x] Locate TypeScript repository implementations
- [x] Document generic patterns used in TS
- [x] Identify transaction boundaries
- [x] Note storage backend abstractions
- [x] Design .NET generic repository interface
- [x] Design unit of work interface
- [x] Implement SQLite-specific base class
- [x] Create specific repository interfaces
- [x] Add async cancellation support
- [ ] Write unit tests for repositories

---

## Notes

- Consider using the Specification pattern for complex queries
- Evaluate MediatR for CQRS pattern implementation
- Document connection string management strategies
- Consider connection pooling for PostgreSQL
- Evaluate bulk operation support for high-volume scenarios
- **Drizzle ORM → EF Core**: TypeScript uses Drizzle for SQLite; .NET uses Entity Framework Core with similar patterns
