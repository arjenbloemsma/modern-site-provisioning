#!/bin/bash

# Dependencies
# This script uses `jq` to parse return values of the az commands
# --> https://stedolan.github.io/jq

# Declare variables
# The values for some of the variables, like APP_NAME, are defined in Azure DevOps
APP_NAME=${APPLICATION_NAME}
APP_NAME_SHORT=${APPLICATION_NAME_SHORT} # some resources require a short name
# Transform the environment name to lower case
BUILD=$(echo $RELEASE_ENVIRONMENTNAME | tr '[:upper:]' '[:lower:]')
LOCATION=${LOCATION}
LOCATION_SHORT=${LOCATION_SHORT}
# The following notation is required to allow spaces in the values of the tags
TAGS=("owner=Arjen Bloemsma" "application=${APP_NAME}")
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
SUBSCRIPTION_NAME_CREATESITE="create-site-subscription"
SUBSCRIPTION_NAME_REGISTERSITE="register-site-subscription"
SUBSCRIPTION_NAME_SETDEFAULTCOLUMNVALUES="set-default-column-values-subscription"
SUBSCRIPTION_NAME_UPDATEMETADATA="update-metadata-subscription"
SUBSCRIPTION_NAME_UPDATETEMPLATE="update-template-subscription"
SUBSCRIPTION_NAME_APPLYTEMPLATE="apply-template-subscription"
RULE_NAME_DEFAULT="\$Default"
RULE_NAME_NEWSITE="new-site"
RULE_NAME_UPDATESITE="update-site"
RULE_NAME_CREATESITE="create-site"
RULE_NAME_REGISTERSITE="register-site"
RULE_NAME_SETDEFAULTCOLUMNVALUES="set-default-column-values"
RULE_NAME_UPDATESITEMETADATA="update-site-metadata"
RULE_NAME_UPDATESITETEMPLATE="update-site-template"
RULE_NAME_APPLYSITETEMPLATE="apply-site-template"
APP_SERVICE_PLAN_NAME="${APP_NAME}-${BUILD}-asp-${LOCATION_SHORT}"
FUNCTION_APP_NAME="${APP_NAME}-${BUILD}-fa-${LOCATION_SHORT}"
# Keyvault name must be between 3 and 24 characters in length and use numbers and lower-case letters only
KEY_VAULT_NAME="${APP_NAME_SHORT}${BUILD}kv${LOCATION_SHORT}"
OUTPUT_FORMAT="json"
# Retreive the ID of the default subscription and store in a variable
SUBSCRIPTION_ID=$(az account show --query "id")
# Counter for echoing the steps number
STEP=0

# Provision the resources in Azure
echo "Provision required resources in Azure for the 'Modern Site Provisioning' application."
# Create the resource group.
echo "$((STEP++)). Create resource group $RESOURCE_GROUP_NAME"
az group create \
    --name $RESOURCE_GROUP_NAME \
    --location $LOCATION \
    --output $OUTPUT_FORMAT \
    --tags "${TAGS[@]}"

# Create the storage account
echo "$((STEP++)). Create storage account $STORAGE_ACCOUNT_NAME"
az storage account create \
    --resource-group $RESOURCE_GROUP_NAME \
    --name $STORAGE_ACCOUNT_NAME \
    --location $LOCATION \
    --sku Standard_GRS \
    --kind StorageV2 \
    --output $OUTPUT_FORMAT \
    --tags "${TAGS[@]}"

# Retrieve the connection string of the storage account and store it in a variable
CONNECTION_STRING=$(az storage account show-connection-string \
    --name $STORAGE_ACCOUNT_NAME \
    --resource-group $RESOURCE_GROUP_NAME \
    --query "connectionString")

# Create the storage table
echo "$((STEP++)). Create storage table $TABLE_NAME_SITES"
az storage table create \
    --name $TABLE_NAME_SITES \
    --connection-string $CONNECTION_STRING \
    --output $OUTPUT_FORMAT

# Create the blob containers
echo "$((STEP++)). Create blob container $CONTAINER_NAME_PROVISIONINGJOBFILES"
az storage container create \
    --name $CONTAINER_NAME_PROVISIONINGJOBFILES \
    --connection-string $CONNECTION_STRING \
    --output $OUTPUT_FORMAT

echo "$((STEP++)). Create blob container $CONTAINER_NAME_PROVISIONINGTEMPLATEFILES"
az storage container create \
    --name $CONTAINER_NAME_PROVISIONINGTEMPLATEFILES \
    --connection-string $CONNECTION_STRING \
    --output $OUTPUT_FORMAT

# Create the service bus
echo "$((STEP++)). Create service bus $SERVICE_BUS_NAME"
az servicebus namespace create \
    --resource-group $RESOURCE_GROUP_NAME \
    --name $SERVICE_BUS_NAME \
    --location $LOCATION \
    --sku Standard \
    --output $OUTPUT_FORMAT \
    --tags "${TAGS[@]}"

# Create the topics with their subscriptions
echo "$((STEP++)). Create topic $TOPIC_NAME_SITEOPERATIONS"
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

echo "$((STEP++)). Create subscription $SUBSCRIPTION_NAME_NEWSITEREQUESTS for topic $TOPIC_NAME_SITEOPERATIONS"
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

# Remove the default rule that comes with the subscription
az servicebus topic subscription rule delete \
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICE_BUS_NAME \
    --topic-name $TOPIC_NAME_SITEOPERATIONS \
    --subscription-name $SUBSCRIPTION_NAME_NEWSITEREQUESTS \
    --name $RULE_NAME_DEFAULT

echo "$((STEP++)). Create rule $RULE_NAME_NEWSITE for subscription $SUBSCRIPTION_NAME_NEWSITEREQUESTS"
az servicebus topic subscription rule create \
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICE_BUS_NAME \
    --topic-name $TOPIC_NAME_SITEOPERATIONS \
    --subscription-name $SUBSCRIPTION_NAME_NEWSITEREQUESTS \
    --name $RULE_NAME_NEWSITE \
    --filter-sql-expression sys.Label=SiteProvisioning

echo "$((STEP++)). Create subscription $SUBSCRIPTION_NAME_UPDATESITEREQUESTS for topic $TOPIC_NAME_SITEOPERATIONS"
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

# Remove the default rule that comes with the subscription
az servicebus topic subscription rule delete \
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICE_BUS_NAME \
    --topic-name $TOPIC_NAME_SITEOPERATIONS \
    --subscription-name $SUBSCRIPTION_NAME_UPDATESITEREQUESTS \
    --name $RULE_NAME_DEFAULT

echo "$((STEP++)). Create rule $RULE_NAME_UPDATESITE for subscription $SUBSCRIPTION_NAME_UPDATESITEREQUESTS"
az servicebus topic subscription rule create \
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICE_BUS_NAME \
    --topic-name $TOPIC_NAME_SITEOPERATIONS \
    --subscription-name $SUBSCRIPTION_NAME_UPDATESITEREQUESTS \
    --name $RULE_NAME_UPDATESITE \
    --filter-sql-expression "sys.Label=UpdateSiteMetadata OR sys.Label=UpdateSiteTemplate"

echo "$((STEP++)). Create topic $TOPIC_NAME_NEWSITES"
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

echo "$((STEP++)). Create subscription $SUBSCRIPTION_NAME_CREATESITE for topic $TOPIC_NAME_NEWSITES"
az servicebus topic subscription create \
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICE_BUS_NAME \
    --topic-name $TOPIC_NAME_NEWSITES \
    --name $SUBSCRIPTION_NAME_CREATESITE \
    --status Active \
    --enable-dead-lettering-on-message-expiration true \
    --enable-batched-operations false \
    --dead-letter-on-filter-exceptions false \
    --default-message-time-to-live P14D  \
    --lock-duration PT1M \
    --max-delivery-count 10 \
    --output $OUTPUT_FORMAT

# Remove the default rule that comes with the subscription
az servicebus topic subscription rule delete \
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICE_BUS_NAME \
    --topic-name $TOPIC_NAME_NEWSITES \
    --subscription-name $SUBSCRIPTION_NAME_CREATESITE \
    --name $RULE_NAME_DEFAULT

echo "$((STEP++)). Create rule $RULE_NAME_CREATESITE for subscription $SUBSCRIPTION_NAME_CREATESITE"
az servicebus topic subscription rule create \
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICE_BUS_NAME \
    --topic-name $TOPIC_NAME_NEWSITES \
    --subscription-name $SUBSCRIPTION_NAME_CREATESITE \
    --name $RULE_NAME_CREATESITE \
    --filter-sql-expression sys.Label=CreateSiteCollection

echo "$((STEP++)). Create subscription $SUBSCRIPTION_NAME_REGISTERSITE for topic $TOPIC_NAME_NEWSITES"
az servicebus topic subscription create \
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICE_BUS_NAME \
    --topic-name $TOPIC_NAME_NEWSITES \
    --name $SUBSCRIPTION_NAME_REGISTERSITE \
    --status Active \
    --enable-dead-lettering-on-message-expiration true \
    --enable-batched-operations false \
    --dead-letter-on-filter-exceptions false \
    --default-message-time-to-live P14D  \
    --lock-duration PT1M \
    --max-delivery-count 10 \
    --output $OUTPUT_FORMAT

# Remove the default rule that comes with the subscription
az servicebus topic subscription rule delete \
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICE_BUS_NAME \
    --topic-name $TOPIC_NAME_NEWSITES \
    --subscription-name $SUBSCRIPTION_NAME_REGISTERSITE \
    --name $RULE_NAME_DEFAULT

echo "$((STEP++)). Create rule $RULE_NAME_REGISTERSITE for subscription $SUBSCRIPTION_NAME_REGISTERSITE"
az servicebus topic subscription rule create \
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICE_BUS_NAME \
    --topic-name $TOPIC_NAME_NEWSITES \
    --subscription-name $SUBSCRIPTION_NAME_REGISTERSITE \
    --name $RULE_NAME_REGISTERSITE \
    --filter-sql-expression sys.Label=CreateSiteCollection

echo "$((STEP++)). Create subscription $SUBSCRIPTION_NAME_SETDEFAULTCOLUMNVALUES for topic $TOPIC_NAME_NEWSITES"
az servicebus topic subscription create \
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICE_BUS_NAME \
    --topic-name $TOPIC_NAME_NEWSITES \
    --name $SUBSCRIPTION_NAME_SETDEFAULTCOLUMNVALUES \
    --status Active \
    --enable-dead-lettering-on-message-expiration true \
    --enable-batched-operations false \
    --dead-letter-on-filter-exceptions false \
    --default-message-time-to-live P14D  \
    --lock-duration PT1M \
    --max-delivery-count 10 \
    --output $OUTPUT_FORMAT

# Remove the default rule that comes with the subscription
az servicebus topic subscription rule delete \
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICE_BUS_NAME \
    --topic-name $TOPIC_NAME_NEWSITES \
    --subscription-name $SUBSCRIPTION_NAME_SETDEFAULTCOLUMNVALUES \
    --name $RULE_NAME_DEFAULT

echo "$((STEP++)). Create rule $RULE_NAME_SETDEFAULTCOLUMNVALUES for subscription $SUBSCRIPTION_NAME_SETDEFAULTCOLUMNVALUES"
az servicebus topic subscription rule create \
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICE_BUS_NAME \
    --topic-name $TOPIC_NAME_NEWSITES \
    --subscription-name $SUBSCRIPTION_NAME_SETDEFAULTCOLUMNVALUES \
    --name $RULE_NAME_SETDEFAULTCOLUMNVALUES \
    --filter-sql-expression sys.Label=SetDefaultColumnValues

echo "$((STEP++)). Create topic $TOPIC_NAME_UPDATESITES"
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

echo "$((STEP++)). Create subscription $SUBSCRIPTION_NAME_UPDATEMETADATA for topic $TOPIC_NAME_UPDATESITES"
az servicebus topic subscription create \
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICE_BUS_NAME \
    --topic-name $TOPIC_NAME_UPDATESITES \
    --name $SUBSCRIPTION_NAME_UPDATEMETADATA \
    --status Active \
    --enable-dead-lettering-on-message-expiration true \
    --enable-batched-operations false \
    --dead-letter-on-filter-exceptions false \
    --default-message-time-to-live P14D  \
    --lock-duration PT1M \
    --max-delivery-count 10 \
    --output $OUTPUT_FORMAT

# Remove the default rule that comes with the subscription
az servicebus topic subscription rule delete \
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICE_BUS_NAME \
    --topic-name $TOPIC_NAME_UPDATESITES \
    --subscription-name $SUBSCRIPTION_NAME_UPDATEMETADATA \
    --name $RULE_NAME_DEFAULT

echo "$((STEP++)). Create rule $RULE_NAME_UPDATESITEMETADATA for subscription $SUBSCRIPTION_NAME_UPDATEMETADATA"
az servicebus topic subscription rule create \
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICE_BUS_NAME \
    --topic-name $TOPIC_NAME_UPDATESITES \
    --subscription-name $SUBSCRIPTION_NAME_UPDATEMETADATA \
    --name $RULE_NAME_UPDATESITEMETADATA \
    --filter-sql-expression sys.Label=UpdateSiteMetadata

echo "$((STEP++)). Create subscription $SUBSCRIPTION_NAME_UPDATETEMPLATE for topic $TOPIC_NAME_UPDATESITES"
az servicebus topic subscription create \
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICE_BUS_NAME \
    --topic-name $TOPIC_NAME_UPDATESITES \
    --name $SUBSCRIPTION_NAME_UPDATETEMPLATE \
    --status Active \
    --enable-dead-lettering-on-message-expiration true \
    --enable-batched-operations false \
    --dead-letter-on-filter-exceptions false \
    --default-message-time-to-live P14D  \
    --lock-duration PT1M \
    --max-delivery-count 10 \
    --output $OUTPUT_FORMAT

# Remove the default rule that comes with the subscription
az servicebus topic subscription rule delete \
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICE_BUS_NAME \
    --topic-name $TOPIC_NAME_UPDATESITES \
    --subscription-name $SUBSCRIPTION_NAME_UPDATETEMPLATE \
    --name $RULE_NAME_DEFAULT

echo "$((STEP++)). Create rule $RULE_NAME_UPDATESITETEMPLATE for subscription $SUBSCRIPTION_NAME_UPDATETEMPLATE"
az servicebus topic subscription rule create \
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICE_BUS_NAME \
    --topic-name $TOPIC_NAME_UPDATESITES \
    --subscription-name $SUBSCRIPTION_NAME_UPDATETEMPLATE \
    --name $RULE_NAME_UPDATESITETEMPLATE \
    --filter-sql-expression sys.Label=UpdateSiteTemplate

echo "$((STEP++)). Create subscription $SUBSCRIPTION_NAME_APPLYTEMPLATE for topic $TOPIC_NAME_UPDATESITES"
az servicebus topic subscription create \
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICE_BUS_NAME \
    --topic-name $TOPIC_NAME_UPDATESITES \
    --name $SUBSCRIPTION_NAME_APPLYTEMPLATE \
    --status Active \
    --enable-dead-lettering-on-message-expiration true \
    --enable-batched-operations false \
    --dead-letter-on-filter-exceptions false \
    --default-message-time-to-live P14D  \
    --lock-duration PT1M \
    --max-delivery-count 10 \
    --output $OUTPUT_FORMAT

# Remove the default rule that comes with the subscription
az servicebus topic subscription rule delete \
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICE_BUS_NAME \
    --topic-name $TOPIC_NAME_UPDATESITES \
    --subscription-name $SUBSCRIPTION_NAME_APPLYTEMPLATE \
    --name $RULE_NAME_DEFAULT

echo "$((STEP++)). Create rule $RULE_NAME_APPLYSITETEMPLATE for subscription $SUBSCRIPTION_NAME_APPLYTEMPLATE"
az servicebus topic subscription rule create \
    --resource-group $RESOURCE_GROUP_NAME \
    --namespace-name $SERVICE_BUS_NAME \
    --topic-name $TOPIC_NAME_UPDATESITES \
    --subscription-name $SUBSCRIPTION_NAME_APPLYTEMPLATE \
    --name $RULE_NAME_APPLYSITETEMPLATE \
    --filter-sql-expression sys.Label=ApplyTemplate

# Create the app service plan
echo "$((STEP++)). Create app service plan $APP_SERVICE_PLAN_NAME"
az appservice plan create \
    --resource-group $RESOURCE_GROUP_NAME \
    --name $APP_SERVICE_PLAN_NAME \
    --number-of-workers 1 \
    --sku S1 \
    --output $OUTPUT_FORMAT \
    --tags "${TAGS[@]}"

# Create the function app
echo "$((STEP++)). Create function app $FUNCTION_APP_NAME"
az functionapp create \
    --resource-group $RESOURCE_GROUP_NAME \
    --name $FUNCTION_APP_NAME \
    --storage-account $STORAGE_ACCOUNT_NAME \
    --plan $APP_SERVICE_PLAN_NAME \
    --os-type Windows \
    --runtime dotnet \
    --output $OUTPUT_FORMAT \
    --tags "${TAGS[@]}"

# Configure the function app: remote debugging and app settings
echo "$((STEP++)). Configure function app $FUNCTION_APP_NAME"
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
echo "$((STEP++)). Create application insights for function app $FUNCTION_APP_NAME"   
az resource create \
    --name ${FUNCTION_APP_NAME} \
    --resource-group $RESOURCE_GROUP_NAME \
    --resource-type "Microsoft.Insights/components" \
    --location $LOCATION \
    --properties '{"Application_Type":"web","ApplicationId":"${FUNCTION_APP_NAME}","Request_Source":"IbizaWebAppExtensionCreate"}' \
    --output $OUTPUT_FORMAT

# Add tag hidden-link, used by Azure portal for functionality like displaying application map
APP_INSIGHTS_ID="/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/$RESOURCE_GROUP_NAME/providers/microsoft.insights/components/${FUNCTION_APP_NAME}"
FUNCTION_APP_ID="/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/$RESOURCE_GROUP_NAME/providers/Microsoft.Web/sites/${FUNCTION_APP_NAME}"
TAG_KEY="hidden-link:${FUNCTION_APP_ID}"
TAG_VAL="Resource"

az resource tag --ids $APP_INSIGHTS_ID --tags $TAG_KEY=$TAG_VAL "${TAGS[@]}"

# Retrieve the instrumentation key of application insights and store it in a variable
INSTRUMENTATION_KEY=$(az resource show \
    --name ${FUNCTION_APP_NAME} \
    --resource-group $RESOURCE_GROUP_NAME \
    --resource-type "Microsoft.Insights/components" | jq --raw-output ".properties.InstrumentationKey")

# Connect the function app to application insights via the instrumentation key
az functionapp config appsettings set \
--name $FUNCTION_APP_NAME \
--resource-group $RESOURCE_GROUP_NAME \
--settings APPINSIGHTS_INSTRUMENTATIONKEY=$INSTRUMENTATION_KEY \
--output $OUTPUT_FORMAT

# Create the key vault
echo "$((STEP++)). Create key vault $KEY_VAULT_NAME"
az keyvault create \
    --resource-group $RESOURCE_GROUP_NAME \
    --name $KEY_VAULT_NAME \
    --location $LOCATION \
    --bypass AzureServices \
    --default-action Deny \
    --enabled-for-deployment false \
    --enabled-for-disk-encryption false \
    --enabled-for-template-deployment true \
    --no-self-perms true \
    --sku premium \
    --output $OUTPUT_FORMAT \
    --tags "${TAGS[@]}"

# Create a managed identityy for the function app
echo "$((STEP++)). Configure managed identity for function app $FUNCTION_APP_NAME"
az functionapp identity assign \
    --name $FUNCTION_APP_NAME \
    --resource-group $RESOURCE_GROUP_NAME \
    --output $OUTPUT_FORMAT

# Retrieve the principal id of the managed identity and store it in a variable
PRINCIPAL_ID=$(az functionapp identity show \
    --name $FUNCTION_APP_NAME \
    --resource-group $RESOURCE_GROUP_NAME | jq --raw-output ".principalId")

# Grant the function app get secret permissions on the key vault
echo "$((STEP++)). Grant $FUNCTION_APP_NAME get secret permissions for key vault $KEY_VAULT_NAME"
az keyvault set-policy \
    --name $KEY_VAULT_NAME \
    --resource-group $RESOURCE_GROUP_NAME \
    --object-id $PRINCIPAL_ID \
    --secret-permissions get \
    --output $OUTPUT_FORMAT

echo "Done"