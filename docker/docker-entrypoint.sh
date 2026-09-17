#!/bin/bash

# Variables
## Common
SOURCE="${SOURCE:-https://github.com/promethei-project/cs-promethei-dist-tests.git}"
BRANCH="${BRANCH:-main}"
FOLDER="${FOLDER:-/opt/cs-promethei-dist-tests}"

## Tests specific
DEPLOYMENT_PROMETHEINETDEPLOYER_PATH="${DEPLOYMENT_PROMETHEINETDEPLOYER_PATH:-Tools/PrometheiNetDeployer}"
DEPLOYMENT_PROMETHEINETDEPLOYER_RUNNER="${DEPLOYMENT_PROMETHEINETDEPLOYER_RUNNER:-deploy-continuous-testnet.sh}"
CONTINUOUS_TESTS_FOLDER="${CONTINUOUS_TESTS_FOLDER:-Tests/PrometheiContinuousTests}"
CONTINUOUS_TESTS_RUNNER="${CONTINUOUS_TESTS_RUNNER:-run.sh}"

# Get code
echo -e "Cloning ${SOURCE} to ${FOLDER}\n"
git clone -b "${BRANCH}" "${SOURCE}" "${FOLDER}"
echo -e "\nChanging folder to ${FOLDER}\n"
cd "${FOLDER}"

# Run tests
echo -e "Running tests from branch '$(git branch --show-current) ($(git rev-parse --short HEAD))'\n"

echo -e "Running ${TESTS_TYPE}\n"
dotnet build
exec "$@"
