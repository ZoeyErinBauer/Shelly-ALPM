#!/bin/bash

# Publish Shelly.Worker with Native AOT for linux-x64
dotnet publish Shelly.Worker/Shelly.Worker.csproj -c Release -r linux-x64 --self-contained true
