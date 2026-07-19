using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StokTakip.Models
{
    public class Product
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }

    public class StockLog
    {
        public int Id { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string Operator { get; set; } = "Sistem";
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    //kullanıcı tablosu
    public class AppUser
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Username { get; set; } = string.Empty;
    }
}