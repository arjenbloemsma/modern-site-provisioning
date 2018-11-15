#!/bin/bash

# Dependencies
# This script uses `jq` to parse return values of the az commands
# --> https://stedolan.github.io/jq

# Declare variables
APP_NAME="modern-site-provisioning"
APP_NAME_SHORT="modsiteprov" # some resources require a short name
BUILD="dev"
LOCATION="westeurope"
LOCATION_SHORT="we"
TAGS=(owner="Arjen Bloemsma" application=${APP_NAME})
RESOURCE_GROUP_NAME="${APP_NAME}-${BUILD}-rg"
# Storage account name must be between 3 and 24 characters in length and use numbers and lower-case letters only
STORAGE_ACCOUNT_NAME="${APP_NAME_SHORT}${BUILD}sa${LOCATION_SHORT}"
TABLE_NAME_SITES="Sites"
CONTAINER_NAME_PROVISIONINGJOBFILES="provisioning-job-files"
CONTAINER_NAME_PROVISIONINGTEMPLATEFILES="provisioning-template-files"
SERVICE_BUS_NAME="${APP_NAME}-${BUILD}-sb-${LOCATION_SHORT}"
TOPIC_NAME_SITEOPERATIONS="site-operations-topic"
TOPIC_NAME_NEWSITES="new-sites-topic"
TOPIC_NAME_UPDATESITES="update-sites-topic"
SUBSCRIPTION_NAME_NEWSITEREQUESTS="new-site-requests-subscription"
SUBSCRIPTION_NAME_UPDATESITEREQUESTS="update-site-requests-subscription"
APP_SERVICE_PLAN_NAME="${APP_NAME}-${BUILD}-asp-${LOCATION_SHORT}"
FUNCTION_APP_NAME="${APP_NAME}-${BUILD}-fa-${LOCATION_SHORT}"
# Keyvault name must be between 3 and 24 characters in length and use numbers and lower-case letters only
KEY_VAULT_NAME="${APP_NAME_SHORT}${BUILD}kv${LOCATION_SHORT}"
OUTPUT_FORMAT="json"

# Provision the resources in Azure
echo "Provision required resources in Azure for the 'Modern Site Provisioning' application."
# Create the resource group.
echo "1. Create resource group $RESOURCE_GROUP_NAME"
az group create \
    --name $RESOURCE_GROUP_NAME \
    --location $LOCATION \
	--output $OUTPUT_FORMAT

# Create the storage account
echo "2. Create storage account $STORAGE_ACCOUNT_NAME"
az storage account create \
    --resource-group $RESOURCE_GROUP_NAME \
    --name $STORAGE_ACCOUNT_NAME \
    --location $LOCATION \
    --sku Standard_GRS \
    --kind StorageV2 \
	--output $OUTPUT_FORMAT \
    --tags ${TAGS[*]}

# Retrieve the connection string of the storage account and store it in a variable
CONNECTION_STRING=$(az storage account show-connection-string --name $STORAGE_ACCOUNT_NAME \
    --resource-group $RESOURCE_GROUP_NAME | jq -r '.connectionString')

# Create the storage table
echo "3. Create storage table $TABLE_NAME_SITES"
az storage table create \
    --name $TABLE_NAME_SITES \
    --connection-string $CONNECTION_STRING \
	--output $OUTPUT_FORMAT

# Create the blob containers
echo "4. Create blob container $CONTAINER_NAME_PROVISIONINGJOBFILES"
az storage container create \
    --name $CONTAINER_NAME_PROVISIONINGJOBFILES \
    --connection-string $CONNECTION_STRING \
	--output $OUTPUT_FORMAT

echo "5. Create blob container $CONTAINER_NAME_PROVISIONINGTEMPLATEFILES"
az storage container create \
    --name $CONTAINER_NAME_PROVISIONINGTEMPLATEFILES \
    --connection-string $CONNECTION_STRING \
	--output $OUTPUT_FORMAT

# Create the service bus
echo "6. Create service bus $SERVICE_BUS_NAME"
az servicebus namespace create \
    --resource-group $RESOURCE_GROUP_NAME \
    --name $SERVICE_BUS_NAME \
    --location $LOCATION \
    --sku Standard \
	--output $OUTPUT_FORMAT \
    --tags ${TAGS[*]}

# Create the topics with their subscriptions
echo "7. Create topic $TOPIC_NAME_SITEOPERATIONS"
az servicebus topic create\
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICE_BUS_NAME \
    --name $TOPIC_NAME_SITEOPERATIONS \
    --default-message-time-to-live P14D \
    --max-size 1024 \
    --enable-duplicate-detection true \
    --duplicate-detection-history-time-window PT15M \
    --status Active \
    --enable-batched-operations true \
    --enable-ordering false \
    --enable-partitioning true \
    --enable-express=false \
	--output $OUTPUT_FORMAT

echo "8. Create subscription $SUBSCRIPTION_NAME_NEWSITEREQUESTS"
az servicebus topic subscription create \
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICE_BUS_NAME \
    --topic-name $TOPIC_NAME_SITEOPERATIONS \
    --name $SUBSCRIPTION_NAME_NEWSITEREQUESTS \
    --status Active \
    --enable-dead-lettering-on-message-expiration true \
    --enable-batched-operations false \
    --dead-letter-on-filter-exceptions false \
    --default-message-time-to-live P14D  \
    --lock-duration PT1M \
    --max-delivery-count 1 \
	--output $OUTPUT_FORMAT

echo "9. Create subscription $SUBSCRIPTION_NAME_UPDATESITEREQUESTS"
az servicebus topic subscription create \
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICE_BUS_NAME \
    --topic-name $TOPIC_NAME_SITEOPERATIONS \
    --name $SUBSCRIPTION_NAME_UPDATESITEREQUESTS \
    --status Active \
    --enable-dead-lettering-on-message-expiration true \
    --enable-batched-operations false \
    --dead-letter-on-filter-exceptions false \
    --default-message-time-to-live P14D  \
    --lock-duration PT1M \
    --max-delivery-count 1 \
	--output $OUTPUT_FORMAT

echo "10. Create topic $TOPIC_NAME_NEWSITES"
az servicebus topic create\
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICE_BUS_NAME \
    --name $TOPIC_NAME_NEWSITES \
    --default-message-time-to-live P14D \
    --max-size 1024 \
    --enable-duplicate-detection true \
    --duplicate-detection-history-time-window PT15M \
    --status Active \
    --enable-batched-operations true \
    --enable-ordering false \
    --enable-partitioning true \
    --enable-express=false \
	--output $OUTPUT_FORMAT

echo "11. Create topic $TOPIC_NAME_UPDATESITES"
az servicebus topic create\
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICE_BUS_NAME \
    --name $TOPIC_NAME_UPDATESITES \
    --default-message-time-to-live P14D \
    --max-size 1024 \
    --enable-duplicate-detection true \
    --duplicate-detection-history-time-window PT15M \
    --status Active \
    --enable-batched-operations true \
    --enable-ordering false \
    --enable-partitioning true \
    --enable-express=false \
	--output $OUTPUT_FORMAT

# Create the app service plan
echo "11. Create app service plan $APP_SERVICE_PLAN_NAME"
az appservice plan create \
    --resource-group $RESOURCE_GROUP_NAME \
    --name $APP_SERVICE_PLAN_NAME \
    --number-of-workers 1 \
    --sku S1 \
	--output $OUTPUT_FORMAT \
    --tags ${TAGS[*]}

# Create the function app
echo "12. Create function app $FUNCTION_APP_NAME"
az functionapp create \
    --resource-group $RESOURCE_GROUP_NAME \
    --name $FUNCTION_APP_NAME \
    --storage-account $STORAGE_ACCOUNT_NAME \
    --plan $APP_SERVICE_PLAN_NAME \
    --os-type Windows \
    --runtime dotnet \
	--output $OUTPUT_FORMAT \
    --tags ${TAGS[*]}

# Configure the function app: remote debugging and app settings
echo "13. Configure function app $FUNCTION_APP_NAME"
if [ $BUILD = "dev" ]
then
    az functionapp config set \
        --resource-group $RESOURCE_GROUP_NAME \
        --name $FUNCTION_APP_NAME \
        --remote-debugging-enabled true \
        --output $OUTPUT_FORMAT
else
    az functionapp config set \
        --resource-group $RESOURCE_GROUP_NAME \
        --name $FUNCTION_APP_NAME \
        --remote-debugging-enabled false \
        --output $OUTPUT_FORMAT
fi

az functionapp config appsettings set \
    --name $FUNCTION_APP_NAME \
    --resource-group $RESOURCE_GROUP_NAME \
    --settings SitesTable=$TABLE_NAME_SITES JobFilesContainer=$CONTAINER_NAME_PROVISIONINGJOBFILES TemplateFilesContainer=$CONTAINER_NAME_PROVISIONINGTEMPLATEFILES \
    --output $OUTPUT_FORMAT

# Create the application insights
echo "14. Create application insights for function app $FUNCTION_APP_NAME"   
az resource create \
    --name ${FUNCTION_APP_NAME} \
    --resource-group $RESOURCE_GROUP_NAME \
    --resource-type "Microsoft.Insights/components" \
    --location $LOCATION \
    --properties '{"Application_Type":"web","ApplicationId":"${FUNCTION_APP_NAME}","Request_Source":"IbizaWebAppExtensionCreate"}' \
    --output $OUTPUT_FORMAT

# Add tag hidden-link, used by Azure portal for functionality like displaying application map
app_insights_id="/subscriptions/1e42c44c-bc55-4b8a-b35e-de1dfbcfe495/resourceGroups/$RESOURCE_GROUP_NAME/providers/microsoft.insights/components/${FUNCTION_APP_NAME}"
function_app_id="/subscriptions/1e42c44c-bc55-4b8a-b35e-de1dfbcfe495/resourceGroups/$RESOURCE_GROUP_NAME/providers/Microsoft.Web/sites/${FUNCTION_APP_NAME}"
tag_key="hidden-link:${function_app_id}"
tag_val="Resource"

az resource tag --ids $app_insights_id --tags $tag_key=$tag_val

# Retrieve the instrumentation key of application insights and store it in a variable
INSTRUMENTATION_KEY=$(az resource show \
    --name ${FUNCTION_APP_NAME} \
    --resource-group $RESOURCE_GROUP_NAME \
    --resource-type "Microsoft.Insights/components" | jq -r ".properties.InstrumentationKey")

# Connect the function app to application insights via the instrumentation key
az functionapp config appsettings set \
--name $FUNCTION_APP_NAME \
--resource-group $RESOURCE_GROUP_NAME \
--settings APPINSIGHTS_INSTRUMENTATIONKEY=$INSTRUMENTATION_KEY \
--output $OUTPUT_FORMAT

# Create the key vault
echo "15. Create key vault $KEY_VAULT_NAME"
az keyvault create \
    --resource-group $RESOURCE_GROUP_NAME \
    --name $KEY_VAULT_NAME \
    --location $LOCATION \
    --bypass AzureServices \
    --default-action Deny \
    # --enable-purge-protection false \
    # --enable-soft-delete false \
    --enabled-for-deployment false \
    --enabled-for-disk-encryption false \
    --enabled-for-template-deployment true \
    --no-self-perms true \
    --sku premium \
    --output $OUTPUT_FORMAT \
    --tags ${TAGS[*]}

# Create a managed identityu for the function app
echo "16. Configure managed identity for function app $FUNCTION_APP_NAME"
az functionapp identity assign \
    --name $FUNCTION_APP_NAME \
    --resource-group $RESOURCE_GROUP_NAME \
    --output $OUTPUT_FORMAT

# Retrieve the principal id of the managed identity and store it in a variable
PRINCIPAL_ID=$(az functionapp identity show --name $FUNCTION_APP_NAME --resource-group $RESOURCE_GROUP_NAME | jq -r ".principalId")

# Grant the function app get secret permissions on the key vault
echo "17. Grant $FUNCTION_APP_NAME get secret permissions for key vault $KEY_VAULT_NAME"
az keyvault set-policy \
    --name $KEY_VAULT_NAME \
    --resource-group $RESOURCE_GROUP_NAME \
    --object-id $PRINCIPAL_ID \
    --secret-permissions get \
    --output $OUTPUT_FORMAT

echo "Done"