# ds-provisioning
    <p>
        A default instance of the Decision Service currently includes the following Azure resources:
        <ul class="lead">
            <li> storage account</li>
            <li> clasic cloud service with standard D5 worker role</li>
            <li> service bus with 4 event hubs</li>
            <li> 2 stream analytics jobs</li>
            <li> web app service backed by app service plan, for the management console</li>
            <li> (optional) additional web app service backed by app service plan, for the HTTP endpoint</li>
        </ul>
    </p>

Create Decision Service <a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fdanmelamed%2Fds-provisioning%2Fmaster%2Fazuredeploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>
<a href="http://armviz.io/#/?load=https%3A%2F%2Fraw.githubusercontent.com%2Fdanmelamed%2Fds-provisioning%2Fmaster%2Fazuredeploy.json" target="_blank">
    <img src="http://armviz.io/visualizebutton.png"/>
</a>
