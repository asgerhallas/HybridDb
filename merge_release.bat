set bindir=HybridDb\bin\Release
Libs\ilmerge /targetplatform:v4,C:\Windows\Microsoft.NET\Framework64\v4.0.30319 /target:library /internalize /out:Build\HybridDb.dll %bindir%\HybridDb.dll %bindir%\Dapper.dll %bindir%\Newtonsoft.Json.dll %bindir%\Inflector.dll
pause



