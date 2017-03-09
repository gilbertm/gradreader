using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;

namespace GradDisplayMat.Models
{
    public class Teleprompt
    {
        [Required]
        [Key]
        public string GraduateId { get; set; }

        public DateTime Created { get; set; }

        public Int16 Status { get; set; }

    }

    public class TelepromptDbContext : DbContext
    {
        public static string ConnectionString { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(ConnectionString);
        }

        public DbSet<Teleprompt> Teleprompt { get; set; }
    }

}
