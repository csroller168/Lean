#!/bin/bash
nuget restore /home/dennis/git/Lean/QuantConnect.Lean.sln
msbuild /home/dennis/git/Lean/QuantConnect.Lean.sln /t:Build /p:Configuration=Release
mkdir /home/dennis/git/Lean/Launcher/bin/Release/token.json
cp /home/dennis/credentials.json /home/dennis/git/Lean/Launcher/bin/Release
cp /home/dennis/Google.Apis.Auth.OAuth2.Responses.TokenResponse-user /home/dennis/git/Lean/Launcher/bin/Release/token.json
cp /home/dennis/Google.Apis.Auth.OAuth2.Responses.TokenResponse-user /home/dennis/git/GmailSender/GmailSender/bin/Release/token.json/

