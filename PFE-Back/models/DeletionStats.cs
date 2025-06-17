using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PFE_Back.models
{
    public class DeletionStats
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public DateTime Month { get; set; }  // We'll store the first day of each month
        
        [Required]
        public int DeleteCount { get; set; }
        
        [Required]
        public DateTime LastUpdated { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }
    }
} 