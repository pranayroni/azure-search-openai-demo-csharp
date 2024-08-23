# Troubleshooting

## .azure file
This file contains environment variable definitions when the app is run in an azure container. This means that for local development, the environment variables must be set. This can be easily done by ensuring that the appsettings are configured for Visual Studio with all the necessary Environment Variables. The variables here will only affect the local development.

## `azd up`

You may encounter several issues running `azd up` if your development environment is not setup correctly. Make sure the following prerequisites are met:

1. You have Contributor Access of the Azure resource group you are working on
2. When running azd up, ensure resource group deployments are enabled by running `azd config set alpha.resourceGroupDeployments on`
3. Ensure the .azure file is populated with the correct resource names of your resource group for this project. Failure to do so will cause duplicate deployments of potentially existing resources on each deployment.

## CI / CD
This repository supports Continuous Integration and Development with Github Actions. If you plan on using Azure Pipelines ensure that all required environment variables are set and defined. The required variables can be found by visiting `.github/workflows/azure-dev.yml`
<br />
<br />
<br />
The following are required for Github Actions:
- AZURE_CREDENTIALS
- AZURE_CLIENT_ID
- AZURE_RESOURCE_GROUP
- AZURE_SUBSCRIPTION_ID
- AZURE_TENANT_ID

## Local Development
Assuming you are able to run `azd up`,

You may encounter issues when trying to build the dotnet project on your local machine for development. 

### Running the Frontend
If your goal is only to make changes to the frontend, you can run the ClientApp project. This will only run the local server to host the user interface. All backend dependent services (this doesn't include API requests) will be non-functional.

### Running the Backend
If your goal is only to make changes to the backend, you can run the MinimalAPI project. This will run **both** local server for user interface as well as the backend server. Your default browser should automatically open with the Frontend, if it does not, check the console for the localhost port number and paste the URL into your browser. 

### Azure Search CORS
Ensure that your Azure Search service has CORS enabled and allows connections from services that require the API like the URL for the Web App. If unsure, use `*` to accept all incoming connections.

### Prepdocs script not running
Ensure that your user has execute permissions for the script. You can check by running `ls -ld` and checking if the last character has an 'x'.

If you are unsure or don't have executable permission for the file, run `chmod a+x [path to script]` to change the file permission and make it executable.

