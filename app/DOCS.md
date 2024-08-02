## Document Bot Documentation

### Table of Contents

#### 1. [Introduction](#introduction)
#### 2. [Deployment](#deployment)
#### 3. [API](#api)
#### 4. [Usage](#usage)

---

### 1. Introduction

Welcome to Knipper's Document Bot. This project aims to provide a comprehensive solution for managing documents and interacting with the contained data through a chat-based interface.

Key features of the Document Bot include:
- **Chat-Based Interaction**: Engage in conversations about documents, ask questions, and receive real-time responses.
- **Document Upload and Deletion**: Easily upload and delete documents to the database directly through the website.
- **Document Categorization and Filtering**: Label documents and tailor responses based on the requested documents.
- **API Integration**: Integrate with external systems and services through a well-defined API.

This documentation will guide you through the deployment, API usage, and general usage of the Document Bot. We hope you find this tool valuable and easy to use.

---

### 2. Deployment

### 2.1. Prerequisites
In order to deploy and run this project, you'll need

- **Azure Account** - If you're new to Azure, get an [Azure account for free](https://aka.ms/free) and you'll get some free Azure credits to get started.
- **Azure subscription with access enabled for the Azure OpenAI service** - [You can request access](https://aka.ms/oaiapply). You can also visit [the Cognitive Search docs](https://azure.microsoft.com/free/cognitive-search/) to get some free Azure credits to get you started.
- **Azure account permissions** - Your Azure Account must have `Microsoft.Authorization/roleAssignments/write` permissions, such as [User Access Administrator](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles#user-access-administrator) or [Owner](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles#owner).
    - You may require additional permissions depending on the resources you are deploying.

### 2.2. Deployment Options
The Document Bot can be deployed using the following methods:

#### GitHub Codespaces / VS Code Remote Containers
- Run this repo virtually by using GitHub Codespaces, which will open a web-based VS Code in your browser.
- Run this project in VS Code Remote Containers, which will open the project in your local VS Code using the [Dev Containers](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers) extension.
- *If you don't have Docker, you will have to use either of these two virtual methods to run* ```azd up```.

#### Local environment
Install the following prerequisites:

- [Azure Developer CLI](https://aka.ms/azure-dev/install)
- [.NET 8](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Git](https://git-scm.com/downloads)
- [Powershell 7+ (pwsh)](https://github.com/powershell/powershell) - For Windows users only.
  
   > **Important**<br> 
   > Ensure you can run `pwsh.exe` from a PowerShell command. If this fails, you likely need to upgrade PowerShell.

- [Docker](https://www.docker.com/products/docker-desktop/)

   > **Important**<br>
   > Ensure Docker is running before running any `azd` provisioning / deployment commands.

Then, run the following commands to get the project on your local environment:

   1. Run `azd auth login`
   1. Clone the repository or run `azd init -t azure-search-openai-demo-csharp`
   1. Run `azd env new azure-search-openai-demo-csharp`

### 2.3. Deployment Steps

#### Use existing resources
***This method is reccommended for use at Knipper. Please request to be added to the*** ```rg-ai-knipper-docbot-csharp``` ***resource group, or an identical one.***

If you have existing resources in Azure that you wish to use, you can configure `azd` to use those by setting the following `azd` environment variables:

1. Run `azd env set AZURE_OPENAI_SERVICE {Name of existing OpenAI service}`
1. Run `azd env set AZURE_OPENAI_RESOURCE_GROUP {Name of existing resource group that OpenAI service is provisioned to}`
1. Run `azd env set AZURE_OPENAI_CHATGPT_DEPLOYMENT {Name of existing ChatGPT deployment}`. Only needed if your ChatGPT deployment is not the default 'chat'.
1. Run `azd env set AZURE_OPENAI_EMBEDDING_DEPLOYMENT {Name of existing embedding model deployment}`. Only needed if your embedding model deployment is not the default `embedding`.
1. Run `azd up`

> [!NOTE]<br> 
> You can also use existing Search and Storage Accounts. See `./infra/main.parameters.json` for list of environment variables to pass to `azd env set` to configure those existing resources.

#### Deploying from scratch

> **Important**<br>
> Ensure Docker is running before running any `azd` provisioning / deployment commands.

Execute the following command, if you don't have any pre-existing Azure services and want to start from a fresh deployment.

1. Run `azd up` - This will provision Azure resources and deploy this sample to those resources, including building the search index based on the files found in the `./data` folder.
   - For the target location, the regions that currently support the model used in this sample are **East US 2** , **East US** or **South Central US**. For an up-to-date list of regions and models, check [here](https://learn.microsoft.com/azure/cognitive-services/openai/concepts/models)
   - If you have access to multiple Azure subscriptions, you will be prompted to select the subscription you want to use. If you only have access to one subscription, it will be selected automatically.

   > **Note**<br>
   > This application uses the `gpt4o` model. When choosing which region to deploy to, make sure they're available in that region (i.e. EastUS). For more information, see the [Azure OpenAI Service documentation](https://learn.microsoft.com/azure/cognitive-services/openai/concepts/models#gpt-4o-and-gpt-4-turbo).

1. After the application has been successfully deployed you will see a URL printed to the console. Click that URL to interact with the application in your browser.

> [!NOTE]<br>
> It may take a few minutes for the application to be fully deployed.

#### Deploying or re-deploying a local clone of the repo

> [!IMPORTANT]<br>
> Ensure Docker is running before running any `azd` provisioning / deployment commands.

- Run `azd up`

#### Deploying your repo using App Spaces

> [!NOTE]<br>
> Make sure you have AZD supported bicep files in your repository and add an initial GitHub Actions Workflow file which can either be triggered manually (for initial deployment) or on code change (automatically re-deploying with the latest changes)
> To make your repository compatible with App Spaces, you need to make changes to your main bicep and main parameters file to allow AZD to deploy to an existing resource group with the appropriate tags.

1. Add AZURE_RESOURCE_GROUP to main parameters file to read the value from environment variable set in GitHub Actions workflow file by App Spaces.
   ```json
   "resourceGroupName": {
      "value": "${AZURE_RESOURCE_GROUP}"
    }
   ```
2. Add AZURE_TAGS to main parameters file to read the value from environment variable set in GitHub Actions workflow file by App Spaces.
   ```json
   "tags": {
      "value": "${AZURE_TAGS}"
    }
   ```
3. Add support for resource group and tags in your main bicep file to read the value being set by App Spaces.
   ```bicep
   param resourceGroupName string = ''
   param tags string = ''
   ```
4. Combine the default tags set by Azd with those being set by App Spaces. Replace _tags initialization_ in your main bicep file with the following -
   ````bicep
   var baseTags = { 'azd-env-name': environmentName }
   var updatedTags = union(empty(tags) ? {} : base64ToJson(tags), baseTags)
   Make sure to use "updatedTags" when assigning "tags" to resource group created in your bicep file and update the other resources to use "baseTags" instead of "tags". For example -
   ```json
   resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
     name: !empty(resourceGroupName) ? resourceGroupName : '${abbrs.resourcesResourceGroups}${environmentName}'
     location: location
     tags: updatedTags
   }
   ````

#### Running locally

> [!IMPORTANT]<br>
> Ensure Docker is running before running any `azd` provisioning / deployment commands. If you do not have docker, run with the .NET MAUI client.

1. Run `azd auth login`
2. After the application deploys, set the environment variable `AZURE_KEY_VAULT_ENDPOINT`. You can find the value in the _.azure/YOUR-ENVIRONMENT-NAME/.env_ file or the Azure portal.
3. Run the following .NET CLI command to start the ASP.NET Core Minimal API server (client host):

   ```dotnetcli
   dotnet run --project ./app/backend/MinimalApi.csproj --urls=http://localhost:7181/
   ```

Navigate to <http://localhost:7181>, and test out the app.

#### Running locally with the .NET MAUI client

This sample includes a .NET MAUI client, packaging the experience as an app that can run on a Windows/macOS desktop or on Android and iOS devices. The MAUI client here is implemented using Blazor hybrid, letting it share most code with the website frontend.

1. Open _app/app-maui.sln_ to open the solution that includes the MAUI client

2. Edit _app/maui-blazor/MauiProgram.cs_, updating `client.BaseAddress` with the URL for the backend.

   If it's running in Azure, use the URL for the service backend from the steps above. If running locally, use <http://localhost:7181>.

3. Set **MauiBlazor** as the startup project and run the app

#### Sharing Environments

Run the following if you want to give someone else access to the deployed and existing environment.

1. Install the [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
1. Run `azd init -t azure-search-openai-demo-csharp`
1. Run `azd env refresh -e {environment name}` - Note that they will need the azd environment name, subscription Id, and location to run this command - you can find those values in your `./azure/{env name}/.env` file. This will populate their azd environment's .env file with all the settings needed to run the app locally.
1. Run `pwsh ./scripts/roles.ps1` - This will assign all of the necessary roles to the user so they can run the app locally. If they do not have the necessary permission to create roles in the subscription, then you may need to run this script for them. Just be sure to set the `AZURE_PRINCIPAL_ID` environment variable in the azd .env file or in the active shell to their Azure Id, which they can get with `az account show`.

#### Clean up resources

Run `azd down`

### 3. API
#### 3.1. Frontend API
##### [url]:
- **GET**: Returns the homepage of the Document Bot.
##### [url]/authentication/login:
- **GET**: Sends user to sign in portal with their Microsoft account.
##### [url]/user:
- **GET**: Returns the user's profile information.
##### [url]/documents:
- **GET**: Returns all documents in the database.
##### [url]/[chatId]/chat:
- **GET**: Returns the page displaying the chat history assigned to chatId.

#### 3.2. Backend API
All Backend API endpoints are prefixed with `/api`. They are called by the frontend through `ApiClient.cs` and are used to interact with the backend services. They are defined in `WebApplicationExtensions.cs`.
##### [url]/api/username:
- **GET**: *This endpoint is not complete. It is set up to be a framework for user preferences later.*
##### [url]/api/documents:
- **GET**: Retrieves all documents from the database.
    - This response will be extremely large as it contains every document blob in storage.
    - Example Response:
```
[
    {
        "name":"2021 Remote Work Pilot (Update Nov 2021)-0.pdf",
        "contentType":"application/pdf",
        "size":87601,
        "lastModified":"2024-07-23T13:50:57+00:00",
        "url":"https://{storage_container_name}.blob.core.windows.net:443/content/2021%20Remote%20Work%20Pilot%20(Update%20Nov%202021)-0.pdf",
        "status":0,
        "embeddingType":0
    },
    { ... }
]
```
- **POST**: Uploads new documents to the database.
    - Example Request:
        - The actual file content is sent as binary data in a multipart form-data request.
        - `category` is a list of categories, delimited by commas.
```
{
  "files": [
    { ... },
    { ... }
  ],
  "maxAllowedSize": 10,
  "cookie": "example-csrf-token",
  "category": "example-category",
  "cancellationToken": "cancellation-token-placeholder"
}
```
- Example Response:
```
{
  "success": true,
  "message": "Files uploaded successfully."
}
```
##### [url]/api/chat:
- **POST**: Sends the lastest chat along with the chat history to generate a response. 
Citations are marked in square brackets [] and follow-up questions are marked in <<>>.
    - Example Request:
```

{
    "messages":
    [   
        {
            "role":"user",
            "content":"How often does the Quality Council meet?",
            "isUser":true
        },
        {
            "role":"assistant",
            "content":"The Quality Council meets at least monthly.... [QA-021-1.pdf]",
            "isUser":false
        },
        {
            "role":"user",
            "content":"Who are the members of the Quality Council?",
            "isUser":true
        }
    ],
    "overrides":
    {
        "semantic_ranker":true,
        "retrieval_mode":"Hybrid",
        "semantic_captions":false,
        "exclude_category":[],
        "top":5,
        "temperature":null,
        "prompt_template":null,
        "prompt_template_prefix":null,
        "prompt_template_suffix":null,
        "suggest_followup_questions":true,
        "use_gpt4v":false,
        "use_oid_security_filter":false,
        "use_groups_security_filter":false,
        "vector_fields":false},
        "lastUserQuestion":"Who are the members of the Quality Council?",
        "approach":0
    }
}
```
 - Example Response:
 ```
 {
    "choices": [
        {
            "index": 0,
            "message": {
                "role": "assistant",
                "content": "The Quality Council is a cross-functional team ... [QA-045-15.pdf]. Departments include ... [QA-021-2.pdf]. <<What are the main responsibilities of the Quality Council?>>  <<...>> "
            },
            "context": {
                "dataPointsContent": [
                    {
                        "title": "QA-021-0.pdf",
                        "content": "of this procedure is ..."
                    },
                    { ... }
                ],
                "dataPointsImages": null,
                "followup_questions": [
                    "What are the main responsibilities of the Quality Council?",
                    "..."
                ],
                "thoughts": [
                    {
                        "title": "Thoughts",
                        "description": "I utilized multiple sources ...",
                        "props": null
                    }
                ],
                "data_points": {
                    "text": [
                        "QA-021-0.pdf: of this procedure is ...",
                        "QA-021-1.pdf:  Those in attendance ...",
                        "foo.pdf: ..."
                    ]
                },
                "thoughtsString": "Thoughts: I utilized multiple sources ..."
            },
            "citationBaseUrl": "https://{storage_container_name}.blob.core.windows.net/content",
            "content_filter_results": null
        }
    ]
}
 ```

##### [url]/api/categories:
- **GET**: Retrieves a list of all categories given to documents.
- Example Response:
```
[
    "Business Rules",
    "Client",
    "Knipper",
    ...
]
```
##### [url]/api/delete/blobs:
- **POST**: Deletes a document from the blob storage.
    - Example Request:
```
{
    "file":"2024 Holiday Schedule.pdf"
}
```
##### [url]/api/delete/embeddings:
- **POST**: Deletes a document's embeddings from the knowledge base.
```
{
    "file":"2024 Holiday Schedule.pdf"
}
```
##### [url]/api/sourcefiles:
- **POST**: When given a list of document blob names, retrieves the respective links.
    - Example Request:
```
{
    "FileNames": 
    [
        "Current Handbook as of 3-12-2014-0.pdf", 
        "Current Handbook as of 3-12-2014-1.pdf"
    ]
}
```
    - Example Response:
```
[
    "https://{storage_container_name}.blob.core.windows.net/content/Current%20Handbook%20as%20of%203-12-2014-0.pdf",
    "https://{storage_container_name}.blob.core.windows.net/content/Current%20Handbook%20as%20of%203-12-2014-1.pdf"
]
``` 

### 4. Usage
#### 4.1. Logging In
To ensure only authorized Knipper employees can use Document Bot, you must first log in with your Microsoft account. This is done by clicking the "Log In" button on the homepage and following the prompts to sign in with your Microsoft account.
#### 4.2. Documents
On this page, you can view all documents in the database, search for specific documents, upload new documents, and delete existing documents. Documents are shown not as wholes, but as individual pages.

***Both Uploading and Deletion occur within the scope of the session. You may switch to other pages within the site, but do not close the tab until uploading or deletion is complete in order to prevent errors in the database.***

#### 4.2.1. Uploading Documents
You can upload documents through the Documents page. Simply select the files you wish to upload, label them with one or more categories, and click "Upload". 
Up to 10 documents can be uploaded at once. All the uploaded documents in the same batch will be labeled with the same categories. 
This process may take a few minutes based on the number and size of documents being uploaded.
When labeling documents, you can search for existing categories through the Multi-Select Autocomplete. To create new categories, you can type in the category name and press enter.
#### 4.2.2. Deleting Documents
To delete a document, click the trash icon next to the document you wish to delete. Despite the documents being displayed as pages, this action does not delete only that page.
Deletion will delete all pages of the document selected. 
#### 4.3. Chat
To chat, press the "New Chat" button to create a new instance of a chat. You can ask it questions about the documents and it will search the database for an answer.
If no relevant documents are found, the chat search the internet for an answer. 
Each chat instance maintains its own chat history and can be accessed by clicking on the chat instance in the chat list. 
Chats will only take into account the chat history of the specific instance they are part of when generating responses.
You can exclude categories of documents from your chat responses by searching them in the Multi-Select Autocomplete.
#### 4.4. Profile
The profile page displays your Microsoft account information. This page is not currently used for any functionality, but may be used in the future for account management.
You may find your auth token to be used in the API here.