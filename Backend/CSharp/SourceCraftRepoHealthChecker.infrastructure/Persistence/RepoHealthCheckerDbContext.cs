using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Options;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Domain.Entities;

namespace SourceCraftRepoHealthChecker.infrastructure.Persistence;

public sealed class RepoHealthCheckerDbContext(
    DbContextOptions<RepoHealthCheckerDbContext> options,
    IOptions<UserOptions> userOptions,
    IOptions<UserAiOptions> userAiOptions,
    IOptions<RepositoryOptions> repositoryOptions,
    IOptions<RecommendationOptions> recommendationOptions)
    : DbContext(options), IRepoHealthCheckerDbContext
{
    private readonly UserOptions _userOptions = userOptions.Value;
    private readonly UserAiOptions _userAiOptions = userAiOptions.Value;
    private readonly RepositoryOptions _repositoryOptions = repositoryOptions.Value;
    private readonly RecommendationOptions _recommendationOptions = recommendationOptions.Value;

    public DbSet<User> Users => Set<User>();

    public DbSet<UserAi> UserAis => Set<UserAi>();

    public DbSet<Repository> Repositories => Set<Repository>();

    public DbSet<AnalysisRun> AnalysisRuns => Set<AnalysisRun>();

    public DbSet<CategoryScore> CategoryScores => Set<CategoryScore>();

    public DbSet<Recommendation> Recommendations => Set<Recommendation>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        #region User
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(x => x.YaId).HasMaxLength(_userOptions.MaxYaIdLength);
            entity.Property(x => x.Login).HasMaxLength(_userOptions.MaxLoginLength);
            entity.Property(x => x.DisplayName).HasMaxLength(_userOptions.MaxDisplayNameLength);
            entity.Property(x => x.Email).HasMaxLength(_userOptions.MaxEmailLength);
            entity.HasIndex(x => x.YaId).IsUnique();
        });
        #endregion

        #region UserAi
        modelBuilder.Entity<UserAi>(entity =>
        {
            entity.Property(x => x.AiBaseUrl).HasMaxLength(_userAiOptions.MaxBaseUrlLength);
            entity.Property(x => x.AiModel).HasMaxLength(_userAiOptions.MaxModelLength);
            entity.Property(x => x.AiToken).HasMaxLength(_userAiOptions.MaxTokenLength);
        });

        modelBuilder.Entity<User>()
            .HasOne(x => x.Ai)
            .WithOne()
            .HasForeignKey<UserAi>(x => x.UserId);
        #endregion

        #region Repository
        modelBuilder.Entity<Repository>(entity =>
        {
            entity.Property(x => x.SourceCraftId).HasMaxLength(_repositoryOptions.MaxSourceCraftIdLength);
            entity.Property(x => x.Name).HasMaxLength(_repositoryOptions.MaxNameLength);
            entity.Property(x => x.FullName).HasMaxLength(_repositoryOptions.MaxFullNameLength);
            entity.Property(x => x.Url).HasMaxLength(_repositoryOptions.MaxUrlLength);
            entity.Property(x => x.Language).HasMaxLength(_repositoryOptions.MaxLanguageLength);
            entity.HasIndex(x => x.SourceCraftId).IsUnique();
        });
        #endregion

        #region AnalysisRun
        modelBuilder.Entity<AnalysisRun>();
        #endregion

        #region CategoryScore
        modelBuilder.Entity<CategoryScore>();
        #endregion

        #region Recommendation
        modelBuilder.Entity<Recommendation>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(_recommendationOptions.MaxTitleLength);
            entity.Property(x => x.Problem).HasMaxLength(_recommendationOptions.MaxProblemLength);
            entity.Property(x => x.Action).HasMaxLength(_recommendationOptions.MaxActionLength);
            entity.Property(x => x.ExpectedImpact).HasMaxLength(_recommendationOptions.MaxExpectedImpactLength);
            entity.Property(x => x.SourceReference).HasMaxLength(_recommendationOptions.MaxSourceReferenceLength);
        });
        #endregion
    }
}
