﻿# QuantConnect Lean Algorithmic Trading Engine: Python Builder Script

# Clean output directory
rm QuantConnect.Algorithm.Python.dll
rm ../Launcher/bin/Debug/QuantConnect.Algorithm.Python.dll

# Set the script variables: assuming installing the ./compiler/library/ and the caller is in ./compiler/bin/Debug/build.sh
ipy="../../IronPython-2.7.5/ipy.exe"
pyc="../../IronPython-2.7.5/Tools/Scripts/pyc.py"

# Get algorithm-type-name from config.json
pyfile=$(grep -Po '(?<="algorithm-type-name": ")[^"]*' ../Launcher/config.json).py

# Call the compiler:
mono $ipy $pyc /target:dll /out:QuantConnect.Algorithm.Python $pyfile

# Copy to the Lean Algorithm Project
cp QuantConnect.Algorithm.Python.dll ../Launcher/bin/Debug/QuantConnect.Algorithm.Python.dll