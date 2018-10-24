Generic key-value store built on top of Azure functions

### Url: 

(List)[http://wilsondata.azurewebsites.net/api/List]

### Run locally:
```
dotnet build && func host start --script-root bin\Debug\netstandard2.0\
```

### Publishing
```
dotnet build -c Release && dotnet publish -c Release && func azure functionapp publish WilsonData --no-build --script-root bin\Release\netstandard2.0\publish
```
