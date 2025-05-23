using System.ComponentModel.DataAnnotations;

namespace ManufacturingManagementSystem.Models
{
    public class ProductionLineCreateDto
    {
        [Required(ErrorMessage = "Название линии обязательно")]
        [StringLength(100)]
        public string Name { get; set; }

        [Required(ErrorMessage = "Статус обязателен")]
        [RegularExpression("Active|Stopped", ErrorMessage = "Статус должен быть Active или Stopped")]
        public string Status { get; set; } = "Stopped";

        [Range(0.5, 2.0, ErrorMessage = "Коэффициент эффективности должен быть от 0.5 до 2.0")]
        public float EfficiencyFactor { get; set; } = 1.0f;

        public int? CurrentWorkOrderId { get; set; }
    }
}