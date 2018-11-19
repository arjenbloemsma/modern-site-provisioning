#!/bin/bash

# Declare variables
# The values for some of the variables, like APP_NAME, are defined in Azure DevOps
APP_NAME=${APPLICATION_NAME}
# Transform the environment name to lower case
BUILD=$(echo $RELEASE_ENVIRONMENTNAME | tr '[:upper:]' '[:lower:]')
RESOURCE_GROUP_NAME="${APP_NAME}-${BUILD}-rg"

# !!! Remove the resource group and all it's content !!!
echo "Remove resource group ${RESOURCE_GROUP_NAME}."
az group delete --name $RESOURCE_GROUP_NAME --yes

echo "Done"