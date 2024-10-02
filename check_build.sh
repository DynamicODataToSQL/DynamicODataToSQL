dotnet clean -c Release
dotnet restore
dotnet format --verify-no-changes -v diagnostic --no-restore
dotnet build -c Release --no-restore
dotnet test -c Release --no-build /p:CollectCoverage=true /p:Threshold=75 /p:CoverletOutput="../../artifacts/"
dotnet pack -c Release --no-build --include-symbols --include-source -p:SymbolPackageFormat=snupkg -o ./artifacts