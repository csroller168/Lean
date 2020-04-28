#!/bin/bash
nuget restore /home/ubuntu/git/Lean/QuantConnect.Lean.sln
msbuild /home/ubuntu/git/Lean/QuantConnect.Lean.sln
cp /home/ubuntu/credentials.json /home/ubuntu/git/Lean/Launcher/bin/Debug
cp /home/ubuntu/Google.Apis.Auth.OAuth2.Responses.TokenResponse-user /home/ubuntu/git/GmailSender/GmailSender/bin/Debug/token.json/

