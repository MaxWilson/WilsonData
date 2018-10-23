Generic key-value store built on top of Azure functions

### Url: 

(List)[http://wilsondata.azurewebsites.net/api/List]

### Publishing
```
dotnet build -c Release && dotnet publish -c Release && func azure functionapp publish WilsonData --no-build --script-root bin\Release\netstandard2.0\publish
```
