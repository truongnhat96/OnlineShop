using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Models.AHP
{
    public class AHPComparisonMatrix
    {
        public int Id { get; set; }

        [Required]
        public int CriteriaId1 { get; set; }

        [Required]
        public int CriteriaId2 { get; set; }

        [Range(1, 9)]
        public double ComparisonValue { get; set; }

        public string? UserSessionId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class AHPProductScore
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public double TotalScore { get; set; }
        public double NormalizedScore { get; set; }
        public Dictionary<string, double> CriteriaScores { get; set; } = new();
        public int Rank { get; set; }
    }

    public class AHPRecommendationRequest
    {
        public string UserSessionId { get; set; } = string.Empty;
        public List<AHPComparisonMatrix> Comparisons { get; set; } = new();
        public List<int> ProductIds { get; set; } = new();
        public int? CategoryId { get; set; }
        public string? SearchQuery { get; set; }
    }
}




