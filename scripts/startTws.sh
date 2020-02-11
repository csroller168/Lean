#!/bin/bash
export DISPLAY=:1
export PATH=/home/ubuntu/bin:/home/ubuntu/.local/bin:/home/ubuntu/miniconda3/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:/usr/games:/usr/local/games:/snap/bin
/opt/IBController/IBControllerStart.sh -inline &
