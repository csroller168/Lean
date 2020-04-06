#!/bin/bash
nuget restore /home/ubuntu/git/Lean/QuantConnect.Lean.sln
msbuild /home/ubuntu/git/Lean/QuantConnect.Lean.sln
