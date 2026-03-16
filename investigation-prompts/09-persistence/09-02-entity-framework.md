# Investigation Prompt: Entity Framework Core Integration

**Created**: 2026-03-16
**Focus**: Entity Framework Core setup and configuration for conversation persistence

---

## Microsoft Docs

Reference the official Entity Framework Core documentation:

| Resource | URL | Purpose |
|----------|-----|---------|
| EF Core Documentation | https://learn.microsoft.com/ef/core/ | Official documentation hub |
| Getting Started | https://learn.microsoft.com/ef/core/get-started/ | Initial setup guide |
| DbContext Configuration | https://learn.microsoft.com/ef/core/dbcontext-configuration/ | Context setup options |
| SQLite Provider | https://learn.microsoft.com/ef/core/providers/sqlite/ | SQLite-specific docs |
| PostgreSQL Provider | https://www.npgsql.org/efcore/ | Npgsql EF Core provider |
| Migrations | https://learn.microsoft.com/ef/core/managing-schemas/migrations/ | Schema evolution |
| Query Optimization | https://learn.microsoft.com/ef/core/performance/ | Performance best practices |

---

## Investigation Questions

### 1. How to design entities for sessions, messages, tool calls?

- What is the optimal entity granularity?
- How to model one-to-many relationships (session -> messages)?
- How to model one-to-one relationships (message -> tool call result)?
- Should entities use navigation properties or just foreign keys?
- How to handle JSON columns for flexible metadata?

### 2. How to configure EF Core with SQLite provider?

| Configuration | Option | Recommendation |
|---------------|--------|----------------|
| Connection String | File path | `Data Source=conversations.db` |
| Connection Pooling | Enabled by default | Keep default for SQLite |
| Lazy Loading | Proxies or explicit | Explicit loading preferred |
| Change Tracking | Snapshot or proxies | Snapshot (default) |
| Query Tracking | Tracking or NoTracking | NoTracking for read-heavy |

### 3. How to handle migrations in production?

- Should migrations be applied at startup or via CLI?
- How to handle breaking schema changes?
- What is the rollback strategy?
- How to seed initial data?
- How to version migrations across environments?

### 4. How to optimize queries for conversation retrieval?

| Optimization | Technique | Use Case |
|--------------|-----------|----------|
| Eager Loading | `.Include()` | Known related data needed |
| Explicit Loading | `.ThenInclude()` | Conditional related data |
| No Tracking | `.AsNoTracking()` | Read-only queries |
| Split Queries | `.AsSplitQuery()` | Multiple collections |
| Compiled Queries | `.CompileQuery()` | Frequently used queries |
| Indexing | `[Index]` attribute | Fast lookups |

---

## Code Patterns to Investigate

### Entity Configuration Pattern

```csharp
// How to configure entities using Fluent API?
public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(s => s.Metadata)
            .HasColumnType("TEXT") // SQLite JSON
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new()
            );

        builder.HasIndex(s => s.CreatedAt);
        builder.HasIndex(s => s.UpdatedAt);
    }
}
```

### DbContext Setup Pattern

```csharp
// How to configure DbContext with dependency injection?
public class ConversationDbContext : DbContext
{
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<ToolCall> ToolCalls => Set<ToolCall>();
    public DbSet<AgentState> AgentStates => Set<AgentState>();

    public ConversationDbContext(DbContextOptions<ConversationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new SessionConfiguration());
        modelBuilder.ApplyConfiguration(new MessageConfiguration());
        modelBuilder.ApplyConfiguration(new ToolCallConfiguration());
        modelBuilder.ApplyConfiguration(new AgentStateConfiguration());
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Auto-update timestamps
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
            }
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
```

### Query Optimization Pattern

```csharp
// How to write efficient queries?
public class SessionQueries
{
    private readonly ConversationDbContext _context;

    // Compiled query for frequently used operations
    private static readonly Func<ConversationDbContext, Guid, Task<Session?>> GetByIdCompiled =
        EF.CompileAsyncQuery((ConversationDbContext context, Guid id) =>
            context.Sessions.FirstOrDefault(s => s.Id == id));

    public async Task<Session?> GetWithMessagesAsync(Guid sessionId)
    {
        return await _context.Sessions
            .AsNoTracking()
            .Include(s => s.Messages.OrderBy(m => m.Timestamp))
            .ThenInclude(m => m.ToolCalls)
            .FirstOrDefaultAsync(s => s.Id == sessionId);
    }

    public async Task<IReadOnlyList<Message>> GetRecentMessagesAsync(Guid sessionId, int count)
    {
        return await _context.Messages
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            .OrderByDescending(m => m.Timestamp)
            .Take(count)
            .ToListAsync();
    }
}
```

---

## Expected Deliverables

### 1. Entity Classes

```csharp
// Session.cs
public class Session : BaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? Title { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Active;

    // JSON metadata for extensibility
    public string? Metadata { get; set; }

    // Navigation properties
    public ICollection<Message> Messages { get; set; } = [];
    public ICollection<AgentState> AgentStates { get; set; } = [];
}

// Message.cs
public class Message : BaseEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public MessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public int TokenCount { get; set; }

    // Navigation properties
    public Session Session { get; set; } = null!;
    public ICollection<ToolCall> ToolCalls { get; set; } = [];
}

// ToolCall.cs
public class ToolCall : BaseEntity
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string? Result { get; set; }
    public DateTime ExecutedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }

    // Navigation properties
    public Message Message { get; set; } = null!;
}

// AgentState.cs
public class AgentState : BaseEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string StateType { get; set; } = string.Empty;
    public string StateData { get; set; } = string.Empty; // JSON
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Session Session { get; set; } = null!;
}

// Enums
public enum SessionStatus
{
    Active,
    Completed,
    Abandoned
}

public enum MessageRole
{
    System,
    User,
    Assistant,
    Tool
}
```

### 2. DbContext Configuration

```csharp
// ConversationDbContext.cs
public class ConversationDbContext : DbContext
{
    private readonly IHostEnvironment? _environment;

    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<ToolCall> ToolCalls => Set<ToolCall>();
    public DbSet<AgentState> AgentStates => Set<AgentState>();

    public ConversationDbContext(
        DbContextOptions<ConversationDbContext> options,
        IHostEnvironment? environment = null)
        : base(options)
    {
        _environment = environment;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite("Data Source=conversations.db");
        }

        if (_environment?.IsDevelopment() == true)
        {
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ConversationDbContext).Assembly);
    }
}

// Service registration
public static class DbContextExtensions
{
    public static IServiceCollection AddConversationPersistence(
        this IServiceCollection services,
        string connectionString,
        string provider = "sqlite")
    {
        services.AddDbContext<ConversationDbContext>(options =>
        {
            switch (provider.ToLowerInvariant())
            {
                case "sqlite":
                    options.UseSqlite(connectionString);
                    break;
                case "postgresql":
                    options.UseNpgsql(connectionString);
                    break;
                default:
                    throw new NotSupportedException($"Provider '{provider}' is not supported.");
            }
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IToolCallRepository, ToolCallRepository>();
        services.AddScoped<IAgentStateRepository, AgentStateRepository>();

        return services;
    }
}
```

### 3. Migration Strategy

```bash
# Create initial migration
dotnet ef migrations add InitialCreate --project src/Persistence

# Apply migrations at runtime (Program.cs)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ConversationDbContext>();
    context.Database.Migrate();
}

# Generate SQL script for production
dotnet ef migrations script --output migrations.sql
```

### 4. Index Configuration

```csharp
// SessionConfiguration.cs
public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .IsRequired();

        // Indexes for common queries
        builder.HasIndex(s => s.CreatedAt)
            .HasDatabaseName("ix_sessions_created_at");

        builder.HasIndex(s => s.Status)
            .HasDatabaseName("ix_sessions_status");

        builder.HasIndex(s => new { s.Status, s.CreatedAt })
            .HasDatabaseName("ix_sessions_status_created_at");
    }
}

// MessageConfiguration.cs
public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Content)
            .IsRequired()
            .HasColumnType("TEXT"); // Supports long content

        // Foreign key relationship
        builder.HasOne(m => m.Session)
            .WithMany(s => s.Messages)
            .HasForeignKey(m => m.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(m => m.SessionId)
            .HasDatabaseName("ix_messages_session_id");

        builder.HasIndex(m => new { m.SessionId, m.Timestamp })
            .HasDatabaseName("ix_messages_session_timestamp");

        builder.HasIndex(m => new { m.SessionId, m.Role })
            .HasDatabaseName("ix_messages_session_role");
    }
}
```

---

## Investigation Tasks

- [ ] Review EF Core documentation for latest features
- [ ] Design entity model for sessions, messages, tool calls
- [ ] Configure entity relationships and indexes
- [ ] Set up SQLite provider with connection pooling
- [ ] Configure PostgreSQL provider as alternative
- [ ] Create initial migration
- [ ] Implement auto-migration at startup
- [ ] Add compiled queries for hot paths
- [ ] Configure query optimization strategies
- [ ] Write integration tests for persistence layer
- [ ] Document migration deployment process

---

## Notes

- SQLite is suitable for development and single-instance deployments
- Consider connection string encryption for production
- Evaluate bulk insert performance for message-heavy workloads
- Consider soft delete pattern for data retention requirements
- Document backup and restore procedures
