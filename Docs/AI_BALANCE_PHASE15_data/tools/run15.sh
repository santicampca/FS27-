#!/bin/bash
# usage: run15.sh MODE A B FIRST SEEDS SECONDS OUT
cd /home/user/fs27-
BAT_MODE=$1 BAT_A=$2 BAT_B=$3 BAT_FIRST=$4 BAT_SEEDS=$5 BAT_SECONDS=$6 BAT_OUT=$7 dotnet test Tests/Core.Tests --no-build --filter "Name=Phase15Battery_WriteTheCsv" 2>&1 | grep -E " error |Failed!|Passed!|Error Message|Expected|But was"
