#!/bin/bash

# Variables
promethei_image=$(awk '{print $NF}' <<< $1)
promethei_compose="nim-promethei-node.yaml"

# Check argument
if [[ -z "$1" ]]; then
  echo "Usage: $0 <promethei_image>"
  exit 0
fi

# Check image
if [[ "${promethei_image}" != "durabilitylabs/nim-promethei-node"** ]]; then
  echo "Wrong image name: ${promethei_image}"
  exit 0
else
  echo "Image for deployment: ${promethei_image}"
fi

# Try image
if ! docker pull "${promethei_image}" &> /dev/null; then
  echo "Failed to pull ${promethei_image}"
  exit 0
fi

# Directory
script_dir="$(dirname -- $0)"
cd "${script_dir}"

# Update promethei image
sed -i "s|image:.*|image: ${promethei_image}|" "${promethei_compose}"

# Stop and Start
echo "Stopping current autoclient containers..."
docker compose down
echo "Waiting a minute..."
sleep 60
echo "Starting updated autoclient containers..."
docker compose up -d

# Status
docker compose ps

echo "All done"

