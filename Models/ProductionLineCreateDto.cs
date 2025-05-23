using System.ComponentModel.DataAnnotations;

namespace ManufacturingManagementSystem.Models.DTOs
{
    public class ProductionLineCreateDto
    {
        [Required]
        public string Name { get; set; }

        [Required]
        public string Status { get; set; } // Предполагаем string для совместимости

        [Required]
        [Range(0.5, 2.0, ErrorMessage = "Коэффициент эффективности должен быть от 0.5 до 2.0")]
        public float EfficiencyFactor { get; set; }

        public int? CurrentWorkOrderId { get; set; }
    }

    public class ProductionLineUpdateDto
    {
        [Required]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public string Status { get; set; }

        [Required]
        [Range(0.5, 2.0, ErrorMessage = "Коэффициент эффективности должен быть от 0.5 до 2.0")]
        public float EfficiencyFactor { get; set; }

        public int? CurrentWorkOrderId { get; set; }
    }
}