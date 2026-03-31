using Microsoft.EntityFrameworkCore;
using DocVersion.Models;

namespace DocVersion.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<FileHistory> FileHistories { get; set; }

}