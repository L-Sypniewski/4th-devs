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

## Investigation Tasks

- [ ] Locate TypeScript repository implementations
- [ ] Document generic patterns used in TS
- [ ] Identify transaction boundaries
- [ ] Note storage backend abstractions
- [ ] Design .NET generic repository interface
- [ ] Design unit of work interface
- [ ] Implement SQLite-specific base class
- [ ] Create specific repository interfaces
- [ ] Add async cancellation support
- [ ] Write unit tests for repositories

---

## Notes

- Consider using the Specification pattern for complex queries
- Evaluate MediatR for CQRS pattern implementation
- Document connection string management strategies
- Consider connection pooling for PostgreSQL
- Evaluate bulk operation support for high-volume scenarios
