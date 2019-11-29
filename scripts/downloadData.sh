#!/bin/bash
cd /home/ubuntu/Lean/Launcher/bin/Debug
NOW=$(date +"%Y%m%d-00:00:00")
mono QuantConnect.ToolBox.exe --app=YahooDownloader --tickers=SPY,TLT --resolution=Daily --from-date=20030502-00:00:00 --to-date=$NOW
