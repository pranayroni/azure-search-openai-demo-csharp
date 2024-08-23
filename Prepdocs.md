# Prepdocs Quickstart

Running the prepdocs scripts will allow you to perform Knowledgebase Document Upload(s) locally without using the Client side interface. 

In order for these scripts to work, the **environment variables** corresponding to the Azure Resources you are developing with **must be set in the .azure file**. If you are already able to run the application, you won't have to worry about this as the prepdocs scripts automatically load the same variables when uploading.


> [!Note]<br>
> Large batches may cause Azure to rate limit your access to the REST service. Monitor console logs for files successfully uploaded and embedded and retry as necessary according to the limits.

## Running in Visual Studio
Set the selected project to Prepdocs and run. Assuming preqrequisites listed above are met, the program will scan the docs folder and upload all files with tags according to file structure (all files under the docs path are treated as a tag)

## Running in CLI
If running the shell script, make sure you adjust the script to be executable <br />by running `chmod a+x [path to prepdocs.sh]`
