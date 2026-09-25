using Microsoft.EntityFrameworkCore;
using SourceCraftRepoHealthChecker.Domain.Entities;

namespace SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;

public interface IRepoHealthCheckerDbContext
{
    public DbSet<User> Users { get; }
    public DbSet<UserAi> UserAis { get; }
    public DbSet<Repository> Repositories { get; }
    public DbSet<AnalysisRun> AnalysisRuns { get; }
    public DbSet<CategoryScore> CategoryScores { get; }
    public DbSet<Recommendation> Recommendations { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
