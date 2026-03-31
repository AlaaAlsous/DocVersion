using Microsoft.EntityFrameworkCore;
using DocVersion.Core.Models;
using DocVersion.Server.Models;

namespace DocVersion.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<FileHistory> FileHistories { get; set; }

}