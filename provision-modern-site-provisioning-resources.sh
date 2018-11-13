#!/bin/bash

# Dependencies
# This script uses `jq` to parse return values of the az commands
# --> https://stedolan.github.io/jq

# Declare variables
APP_NAME="modern-site-provisioning"
APP_NAME_SHORT="modernsiteprov" # some resources require a short name
BUILD="dev"
LOCATION="westeurope"
LOCATION_SHORT="we"
TAGS="owner=Arjen Bloemsma application=${APP_NAME}"
RESOURCE_GROUP_NAME="${APP_NAME}-${BUILD}-rg"
# Storage account name must be between 3 and 24 characters in length and use numbers and lower-case letters only.
STORAGE_ACCOUNT_NAME="${APP_NAME_SHORT}${BUILD}sa${LOCATION_SHORT}"
TABLE_NAME_SITES="Sites"
CONTAINER_NAME_PROVISIONINGJOBFILES="provisioning-job-files"
CONTAINER_NAME_PROVISIONINGTEMPLATEFILES="provisioning-template-files"
SERVICEBUS_NAME="${APP_NAME}-${BUILD}-sb-${LOCATION_SHORT}"
TOPIC_NAME_SITEOPERATIONS="site-operations-topic"
TOPIC_NAME_NEWSITES="new-sites-topic"
TOPIC_NAME_UPDATESITES="update-sites-topic"
SUBSCRIPTION_NAME_NEWSITEREQUESTS="new-site-requests-subscription"
SUBSCRIPTION_NAME_UPDATESITEREQUESTS="update-site-requests-subscription"

# Provision the resources in Azure
echo "Provision required resources in Azure for the 'Modern Site Provisioning' application."
# Create the resource group.
echo "1. Create resource group $RESOURCE_GROUP_NAME"
az group create --name $RESOURCE_GROUP_NAME --location $LOCATION --output table

# Create the storage account
echo "2. Create storage account $STORAGE_ACCOUNT_NAME"
az storage account create \
    --resource-group $RESOURCE_GROUP_NAME \
    --name $STORAGE_ACCOUNT_NAME \
    --location $LOCATION \
    --sku Standard_RAGRS \
    --kind StorageV2 --output table \
    --tags $TAGS

# Retrieve the connection string of the storage account and place it in a variable
CONNECTION_STRING=$(az storage account show-connection-string --name $STORAGE_ACCOUNT_NAME \
    --resource-group $RESOURCE_GROUP_NAME | jq -r '.connectionString')

# Create the storage table
echo "3. Create storage table $TABLE_NAME_SITES"
az storage table create --name $TABLE_NAME_SITES --connection-string $CONNECTION_STRING --output table

# Create the blob containers
echo "4. Create blob container $CONTAINER_NAME_PROVISIONINGJOBFILES"
az storage container create --name $CONTAINER_NAME_PROVISIONINGJOBFILES --connection-string $CONNECTION_STRING --output table

echo "5. Create blob container $CONTAINER_NAME_PROVISIONINGTEMPLATEFILES"
az storage container create --name $CONTAINER_NAME_PROVISIONINGTEMPLATEFILES --connection-string $CONNECTION_STRING --output table

# Create the service bus
echo "6. Create service bus $SERVICEBUS_NAME"
az servicebus namespace create \
    --resource-group $RESOURCE_GROUP_NAME \
    --name $SERVICEBUS_NAME \
    --location $LOCATION \
    --sku Standard --output table \
    --tags $TAGS

# Create the topics
echo "7. Create topic $TOPIC_NAME_SITEOPERATIONS"
az servicebus topic create\
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICEBUS_NAME \
    --name $TOPIC_NAME_SITEOPERATIONS \
    --default-message-time-to-live="P14D" \
    --max-size=1024 \
    --enable-duplicate-detection=true \
    --duplicate-detection-history-time-window="PT15M" \
    --status="Active" \
    --enable-batched-operations=true \
    --enable-ordering=false \
    --enable-partitioning=true \
    --enable-express=false --output table

echo "8 Create subscription $SUBSCRIPTION_NAME_NEWSITEREQUESTS"
az servicebus topic subscription create \
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICEBUS_NAME \
    --topic-name $TOPIC_NAME_SITEOPERATIONS \
    --name $SUBSCRIPTION_NAME_NEWSITEREQUESTS \
    --status="Active" \
    --enable-dead-lettering-on-message-expiration=true \
    --enable-batched-operations=false \
    --dead-letter-on-filter-exceptions=true \
    --default-message-time-to-live="P14D" \
    --lock-duration="PT1M" \
    --max-delivery-count=1

echo "8. Create topic $TOPIC_NAME_NEWSITES"
az servicebus topic create\
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICEBUS_NAME \
    --name $TOPIC_NAME_NEWSITES \
    --default-message-time-to-live="P14D" \
    --max-size=1024 \
    --enable-duplicate-detection=true \
    --duplicate-detection-history-time-window="PT15M" \
    --status="Active" \
    --enable-batched-operations=true \
    --enable-ordering=false \
    --enable-partitioning=true \
    --enable-express=false --output table

echo "9. Create topic $TOPIC_NAME_UPDATESITES"
az servicebus topic create\
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICEBUS_NAME \
    --name $TOPIC_NAME_UPDATESITES \
    --default-message-time-to-live="P14D" \
    --max-size=1024 \
    --enable-duplicate-detection=true \
    --duplicate-detection-history-time-window="PT15M" \
    --status="Active" \
    --enable-batched-operations=true \
    --enable-ordering=false \
    --enable-partitioning=true \
    --enable-express=false --output table

echo "Done"
echo "Press [ENTER] to continue."
read continue