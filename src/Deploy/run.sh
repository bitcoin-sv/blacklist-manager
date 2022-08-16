#!/bin/bash

cp ./certificates/*.crt /usr/local/share/ca-certificates
update-ca-certificates
cd app
dotnet BlacklistManager.API.Rest.dll