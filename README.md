Generic key-value store built on top of Azure functions

### Usage examples: 

Authenticate via https://wilsondata.azurewebsites.net/.auth/login/Google (or Facebook or whatever) and then call one of the following:

POST (Save)[https://wilsondata.azurewebsites.net/api/Save/ssid/party1]

```
curl https://wilsondata.azurewebsites.net/api/Save/ssid/party1 -D "['Eladriel Shadowdancer','Nevermore Jack','Vladimir Nightbinder','Cranduin the Lesser']"
```

GET (Load)[https://wilsondata.azurewebsites.net/api/Load/ssid/party1]
GET (List)[https://wilsondata.azurewebsites.net/api/List/ssid]

'ssid' represents a unique but arbitrary identifier chosen by the client app to identify a collection of data, 
e.g. all data for the application "Shining Sword: the Impossible Dream" (ssid).

If you're running in a browser (i.e. if UserAgent header is Mozilla/Chrome/etc.) authentication will be triggered automatically if you call an API endpoint 
without already being authenticated. (You'll get a 302 to identity provider instead of a 401.)

### Run locally:
```
dotnet build && func host start --script-root bin\Debug\netstandard2.0\
```

### Publishing
```
dotnet build -c Release && dotnet publish -c Release && func azure functionapp publish WilsonData --no-build --script-root bin\Release\netstandard2.0\publish
```
