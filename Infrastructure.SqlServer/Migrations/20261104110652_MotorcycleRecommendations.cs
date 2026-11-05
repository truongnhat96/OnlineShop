using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class MotorcycleRecommendations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FitmentKind",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsSampleData",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ManufacturerCode",
                table: "Products",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecommendationSeedKey",
                table: "Products",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecommendationTags",
                table: "Products",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecommendationUseCases",
                table: "Products",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceUrl",
                table: "Products",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VehicleModels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Brand = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Variant = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Market = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Aliases = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleModels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductFitments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    VehicleModelId = table.Column<int>(type: "int", nullable: false),
                    YearFrom = table.Column<int>(type: "int", nullable: false),
                    YearTo = table.Column<int>(type: "int", nullable: false),
                    EvidenceUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductFitments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductFitments_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductFitments_VehicleModels_VehicleModelId",
                        column: x => x.VehicleModelId,
                        principalTable: "VehicleModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_RecommendationSeedKey",
                table: "Products",
                column: "RecommendationSeedKey",
                unique: true,
                filter: "[RecommendationSeedKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductFitments_ProductId_VehicleModelId_YearFrom_YearTo",
                table: "ProductFitments",
                columns: new[] { "ProductId", "VehicleModelId", "YearFrom", "YearTo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductFitments_VehicleModelId",
                table: "ProductFitments",
                column: "VehicleModelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductFitments");

            migrationBuilder.DropTable(
                name: "VehicleModels");

            migrationBuilder.DropIndex(
                name: "IX_Products_RecommendationSeedKey",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "FitmentKind",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsSampleData",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ManufacturerCode",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "RecommendationSeedKey",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "RecommendationTags",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "RecommendationUseCases",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ReferenceUrl",
                table: "Products");
        }
    }
}
