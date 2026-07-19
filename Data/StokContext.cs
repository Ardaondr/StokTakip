using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using StokTakip.Models;

namespace StokTakip.Data
{
    public class StokContext : DbContext
    {
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<StockLog> StockLogs { get; set; } = null!;
        public DbSet<AppUser> AppUsers { get; set; } = null!; 

        private static readonly string DbPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "stok.db");

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={DbPath}");
        }
    }
}