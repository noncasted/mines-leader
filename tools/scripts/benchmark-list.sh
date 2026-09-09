#!/bin/bash
# List all benchmarks or by group
# Usage:
#   tools/benchmark-list.sh            -- list all
#   tools/benchmark-list.sh Messaging  -- list group

GROUP="$1"

if [ -z "$GROUP" ]; then
  curl -s ${CONSOLE_URL:-http://localhost:7103}/api/benchmarks
else
  curl -s "${CONSOLE_URL:-http://localhost:7103}/api/benchmarks/group/${GROUP}"
fi
