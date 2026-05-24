using Microsoft.EntityFrameworkCore;

namespace SampleEFProject;

public class Blog
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Url { get; set; }
}

public class AppDbContext : DbContext
{
    public DbSet<Blog> Blogs { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite("Data Source=sample.db");
}
