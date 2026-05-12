using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceCase.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexesForIdempotentImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE AssetRecords
                SET Period = DATEFROMPARTS(YEAR(Period), MONTH(Period), 1);

                WITH RankedAssetRecords AS (
                    SELECT Id, ROW_NUMBER() OVER (PARTITION BY Period ORDER BY Id DESC) AS RowNumber
                    FROM AssetRecords
                )
                DELETE FROM RankedAssetRecords
                WHERE RowNumber > 1;
                """);

            migrationBuilder.Sql("""
                UPDATE InflationIndexRecords
                SET Period = DATEFROMPARTS(YEAR(Period), MONTH(Period), 1),
                    Year = YEAR(Period),
                    Month = MONTH(Period);

                WITH RankedInflationIndexRecords AS (
                    SELECT Id, ROW_NUMBER() OVER (PARTITION BY Period ORDER BY Id DESC) AS RowNumber
                    FROM InflationIndexRecords
                )
                DELETE FROM RankedInflationIndexRecords
                WHERE RowNumber > 1;
                """);

            migrationBuilder.Sql("""
                WITH RankedExchangeRates AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY BaseCurrencyCode, ForeignCurrencyCode, CurrentDate
                               ORDER BY Id DESC
                           ) AS RowNumber
                    FROM ExchangeRates
                )
                DELETE FROM RankedExchangeRates
                WHERE RowNumber > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AssetRecords_Period",
                table: "AssetRecords",
                column: "Period",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InflationIndexRecords_Period",
                table: "InflationIndexRecords",
                column: "Period",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_BaseCurrencyCode_ForeignCurrencyCode_CurrentDate",
                table: "ExchangeRates",
                columns: new[] { "BaseCurrencyCode", "ForeignCurrencyCode", "CurrentDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AssetRecords_Period",
                table: "AssetRecords");

            migrationBuilder.DropIndex(
                name: "IX_InflationIndexRecords_Period",
                table: "InflationIndexRecords");

            migrationBuilder.DropIndex(
                name: "IX_ExchangeRates_BaseCurrencyCode_ForeignCurrencyCode_CurrentDate",
                table: "ExchangeRates");
        }
    }
}
