using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace GradDisplayMat.Models
{
    public class Queue
    {
        [Required]
        [Key]
        public string GraduateId { get; set; }

        public DateTime Created { get; set; }

    }

    public class QueueDbContext : DbContext
    {
        public static string ConnectionString { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(ConnectionString);
        }

        public DbSet<Queue> Queue { get; set; }
    }

}
