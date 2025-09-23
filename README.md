# Jenkins Agent

Jenkins agent for windows with custom configuration and extension.

## Build

Publish default
```
dotnet publish -c Release -r win-x64 --self-contained true
```

Publish with custom app name and ReadyToRun
```
dotnet publish -c Release -r win-x64 --self-contained -p:AssemblyName=AppName -p:AssemblyTitle="App Description" -p:Version=1.0.0-rc1 -p:PublishReadyToRun=true
```

## Config

Application config divided into two kinds, config from client and server.
Client config can be edited from UI and saved as [settings.json](settings.json) on user profile directory.
Server config must be hosted on web server that return [json](bot.json) and the url can be configured from settings url on client config.

## Log

Log for production and development environment is different. Development log will use default settings and additional formatted console log.
Production log will not use default settings. Both environments will log to file and elastic (elasticsearch/logstash) via serilog.
Elastic log can be configured from server config and elastic server must be configured to be ready to receive logs.
