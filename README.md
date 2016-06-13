# ds-provisioning

A default instance of the Decision Service currently includes the following Azure resources:
* storage account
* clasic cloud service with standard D5 worker role
* service bus with 4 event hubs
* 2 stream analytics jobs
* web app service backed by app service plan, for the management console
* (optional) additional web app service backed by app service plan, for the HTTP endpoint

The Decision Service currently does NOT use any third-party resources.  Of course, you are free to change the default configuration.

Create Decision Service <a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fmultiworldtesting%2Fds-provisioning%2Fmaster%2Fazuredeploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>
<a href="http://armviz.io/#/?load=https%3A%2F%2Fraw.githubusercontent.com%2Fmultiworldtesting%2Fds-provisioning%2Fmaster%2Fazuredeploy.json" target="_blank">
    <img src="http://armviz.io/visualizebutton.png"/>
</a>
