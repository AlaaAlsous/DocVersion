$RESOURCE_GROUP="DocVersion-RG"
$APPNAME="DocVersion-App"
$NET_VERSION="net10.0"
$PUBLISH_DIR = ".\bin\Release\$NET_VERSION\publish"

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest


Write-Host "🧹 Cleaning..."
Remove-Item ".\bin\Release" -Recurse -Force -Confirm:$false -ErrorAction SilentlyContinue

Write-Host "🎨 Building frontend (SCSS + JS + Monaco)..."
npm run build

Write-Host "📦 Publishing..."
dotnet publish -c Release -o $PUBLISH_DIR

Write-Host "🧹 Cleaning PDB files..."
Remove-Item "$PUBLISH_DIR\*.pdb" -Force -Confirm:$false -ErrorAction SilentlyContinue

Write-Host "🗜 Creating ZIP..."
Remove-Item "deploy.zip" -Force -Confirm:$false -ErrorAction SilentlyContinue
Compress-Archive -Path "$PUBLISH_DIR\*" -DestinationPath "deploy.zip" -Force

Write-Host "☁ Deploying to Azure..."
az webapp deployment source config-zip `
  --resource-group $RESOURCE_GROUP `
  --name $APPNAME `
  --src ".\deploy.zip"

Remove-Item "deploy.zip" -Force -Confirm:$false -ErrorAction SilentlyContinue
Write-Host "✅ Done!"