using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ManufacturingManagementSystem.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Название продукта обязательно")]
        [StringLength(100)]
        public string Name { get; set; }

        public string Description { get; set; }

        public string Specifications { get; set; }

        [Required(ErrorMessage = "Категория обязательна")]
        [StringLength(50)]
        public string Category { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Минимальный запас должен быть неотрицательным")]
        public int MinimalStock { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Время производства должно быть больше 0")]
        public int ProductionTimePerUnit { get; set; }

        [JsonIgnore]
        public List<ProductMaterial> ProductMaterials { get; set; } = new();
    }

    public class ProductionLine
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Название линии обязательно")]
        [StringLength(100)]
        public string Name { get; set; }

        [Required(ErrorMessage = "Статус обязателен")]
        [RegularExpression("Active|Stopped", ErrorMessage = "Статус должен быть Active или Stopped")]
        public string Status { get; set; } = "Stopped";

        [Range(0.5, 2.0, ErrorMessage = "Коэффициент эффективности должен быть от 0.5 до 2.0")]
        public float EfficiencyFactor { get; set; } = 1.0f;

        public int? CurrentWorkOrderId { get; set; } // Nullable, опционально

        [JsonIgnore]
        public WorkOrder? CurrentWorkOrder { get; set; } // Необязательное поле

        [JsonIgnore]
        public List<WorkOrder> WorkOrders { get; set; } = new(); // Расписание
    }

    public class Material
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Название материала обязательно")]
        [StringLength(100)]
        public string Name { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Количество должно быть неотрицательным")]
        public decimal Quantity { get; set; }

        [Required(ErrorMessage = "Единица измерения обязательна")]
        [StringLength(20)]
        public string UnitOfMeasure { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Минимальный запас должен быть неотрицательным")]
        public decimal MinimalStock { get; set; }
    }

    public class ProductMaterial
    {
        public int ProductId { get; set; }
        public Product Product { get; set; }

        public int MaterialId { get; set; }
        public Material Material { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Количество материала должно быть неотрицательным")]
        public decimal QuantityNeeded { get; set; }
    }

    public class WorkOrder
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        public int? ProductionLineId { get; set; }
        public ProductionLine? ProductionLine { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Количество должно быть больше 0")]
        public int Quantity { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EstimatedEndDate { get; set; }

        [Required(ErrorMessage = "Статус обязателен")]
        [RegularExpression("Pending|InProgress|Completed|Cancelled", ErrorMessage = "Недопустимый статус")]
        public string Status { get; set; } = "Pending";
    }
}