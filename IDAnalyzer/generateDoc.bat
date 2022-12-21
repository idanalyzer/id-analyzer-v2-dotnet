docfx metadata IDAnalyzer.csproj
Xcopy "./_api" "./api" /E /H /C /I /Y
docfx docfx.json
docfx serve _site
pause