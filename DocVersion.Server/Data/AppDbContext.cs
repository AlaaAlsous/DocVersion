using Microsoft.EntityFrameworkCore;
using DocVersion.Server.Models;


namespace DocVersion.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<FileHistory> FileHistories { get; set; }
    public DbSet<UserAccount> UserAccounts { get; set; }
    public DbSet<ShareLink> ShareLinks { get; set; }

}