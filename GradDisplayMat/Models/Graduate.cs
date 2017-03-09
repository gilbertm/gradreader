using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace GradDisplayMat.Models
{
    public class Graduate
    {

        [Key]
        [Required]
        [Display(Name = "ID")]
        public string GraduateId { get; set; }

        [Display(Name = "Scanner ID")]
        public string GraduateScannerId { get; set; }

        public int Status { get; set; }

        public int Arabic { get; set; }

        public string School { get; set; }

        public string Program { get; set; }

        public string Major { get; set; }

        public string Merit { get; set; }

        [Display(Name = "English Full Name")]
        public string Fullname { get; set; }

        [Display(Name = "Arabic Full Name")]
        public string ArabicFullname { get; set; }

        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Display(Name = "Middle Name")]
        public string MiddleName { get; set; }
    }

    public class GraduateDbContext : DbContext
    {
        public static string ConnectionString { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(ConnectionString);
        }

        public DbSet<Graduate> Graduate { get; set; }
    }
}
