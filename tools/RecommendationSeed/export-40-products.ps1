param(
    [string]$Server = '.\SQLEXPRESS',
    [string]$Database = 'OnlineShop',
    [string]$OutputFile = (Join-Path $PSScriptRoot 'seed-40-products.sql')
)
$ErrorActionPreference = 'Stop'
$builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
$builder['Data Source'] = $Server
$builder['Initial Catalog'] = $Database
$builder['Integrated Security'] = $true
$builder['TrustServerCertificate'] = $true
$connection = [System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
function Read-SqlText([string]$query) {
    $command = $connection.CreateCommand()
    $command.CommandText = $query
    $reader = $command.ExecuteReader()
    try {
        $text = [System.Text.StringBuilder]::new()
        while ($reader.Read()) { [void]$text.Append($reader.GetString(0)) }
        return $text.ToString()
    } finally { $reader.Dispose(); $command.Dispose() }
}
try {
    $connection.Open()
    $products = Read-SqlText @'
SELECT p.Name,p.Brand,p.Quantity,p.Date_Import,p.Price,p.Sold,c.Name AS Category,
 p.Description,p.ImageUrl,p.OldPrice,p.FitmentKind,p.IsSampleData,p.ManufacturerCode,
 p.RecommendationSeedKey,p.RecommendationTags,p.RecommendationUseCases,p.ReferenceUrl
FROM dbo.Products p JOIN dbo.Categories c ON c.Id=p.CategoryId
WHERE p.RecommendationSeedKey LIKE 'motoshop-v1-%'
ORDER BY p.RecommendationSeedKey FOR JSON PATH, INCLUDE_NULL_VALUES
'@
    $vehicles = Read-SqlText @'
SELECT DISTINCT v.Brand,v.Model,v.Variant,v.Market,v.Aliases
FROM dbo.VehicleModels v JOIN dbo.ProductFitments f ON f.VehicleModelId=v.Id
JOIN dbo.Products p ON p.Id=f.ProductId WHERE p.RecommendationSeedKey LIKE 'motoshop-v1-%'
FOR JSON PATH, INCLUDE_NULL_VALUES
'@
    $fitments = Read-SqlText @'
SELECT p.RecommendationSeedKey,v.Brand,v.Model,v.Variant,v.Market,f.YearFrom,f.YearTo,f.EvidenceUrl,f.Notes
FROM dbo.ProductFitments f JOIN dbo.Products p ON p.Id=f.ProductId
JOIN dbo.VehicleModels v ON v.Id=f.VehicleModelId WHERE p.RecommendationSeedKey LIKE 'motoshop-v1-%'
ORDER BY p.RecommendationSeedKey,f.YearFrom FOR JSON PATH, INCLUDE_NULL_VALUES
'@
    $parsedProducts = ConvertFrom-Json -InputObject $products
    if ($parsedProducts.Count -ne 40) { throw 'Expected exactly 40 seed products; export aborted.' }
    if ([string]::IsNullOrWhiteSpace($vehicles)) { $vehicles = '[]' }
    if ([string]::IsNullOrWhiteSpace($fitments)) { $fitments = '[]' }
    $template = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'seed-40-products.template.sql') -Raw -Encoding UTF8
    $sql = $template.Replace('__PRODUCTS_JSON__', $products.Replace("'", "''"))
    $sql = $sql.Replace('__VEHICLES_JSON__', $vehicles.Replace("'", "''"))
    $sql = $sql.Replace('__FITMENTS_JSON__', $fitments.Replace("'", "''"))
    $path = [IO.Path]::GetFullPath($OutputFile)
    [IO.File]::WriteAllText($path, $sql, [Text.UTF8Encoding]::new($true))
    Write-Output "Exported 40 products to $path"
} finally { $connection.Dispose() }
