# Jenkins Agent
publish default

    dotnet publish -c Release -r win-x64 --self-contained true

publish with custom app name and ReadyToRun

    dotnet publish -c Release -r win-x64 --self-contained -p:AssemblyName=AppName -p:AssemblyTitle="App Description" -p:PublishReadyToRun=true
