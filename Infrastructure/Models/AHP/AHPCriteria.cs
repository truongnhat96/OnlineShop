using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Models.AHP
{
    public class AHPCriteria
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        public double Weight { get; set; }

        public int Priority { get; set; }

        public CriteriaType Type { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public enum CriteriaType
    {
        Cost,
        Benefit
    }
}




