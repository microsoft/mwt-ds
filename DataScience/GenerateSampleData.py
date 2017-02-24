# This script mimics a very simple video recommendation scenario where users have unrealistically simplified preferences. 

import json, requests, random, configparser

numCorrect = 0
numTotal = 0

config = configparser.ConfigParser()
config.read('ds.config')
ds = config['DecisionService']
apiURL = ds["ManagementCenterURL"]
apiAuthToken = ds["WebServiceToken"]

# Available feature values
ageGroupList = ["GroupA", "GroupB"]
deviceTypeList = ["Desktop", "Mobile"]

for i in range(1000):
    
    # Choose context for the user randomly
    ageGroup = random.choice(ageGroupList)
    deviceType = random.choice(deviceTypeList)
    
    # For each deviceType, the editorial team might have chosen a different set of videos / different ranking.
    availableVideos = []
    if deviceType == "Desktop":
        availableVideos = [{'Video': {'id' : '1'}}, {'Video': {'id' : '2'}}, {'Video': {'id' : '3'}}] 
    elif deviceType == "Mobile": 
        availableVideos = [{'Video': {'id' : '2'}}, {'Video': {'id' : '3'}}, {'Video': {'id' : '4'}}]
        
    print ('Decision Request:')
    headers = {"Content-type": "application/json", "auth": apiAuthToken}
    params = json.dumps({'UserFeatures': {'deviceType' : deviceType, 'ageGroup' : ageGroup}, '_multi': availableVideos})
    URL = apiURL + '/API/Ranker'
    
    print ('URL: ' + URL)
    print ('Request body: ' + str(params))
    decision = requests.post(URL, params, headers=headers)
    decisionData = decision.json()
    print ('Response: ' + str(decisionData))
    numTotal += 1
    
    print ('')
    print ('Sending reward:')
    selectedVideo = decisionData["Actions"][0]
    # Positive reward if selected video matches our expected model
    if (deviceType == "Mobile" and selectedVideo == 3) \
    or (deviceType == "Desktop" and ageGroup == "GroupA" and selectedVideo == 3) \
    or (deviceType == "Desktop" and ageGroup == "GroupB" and selectedVideo == 1):
        params = '1'
        numCorrect += 1
    else:
        params = '0'
    
    headers = {"Content-type": "application/x-www-form-urlencoded; charset=UTF-8", "auth": apiAuthToken}
    
    rewardURL = apiURL + '/API/Reward/?eventId=' + decisionData["EventId"]
    print ('URL: ' + rewardURL)
    print ('Request body: ' + str(params))
    reward = requests.post(rewardURL, data=params, headers=headers)
    
    print ('% best decision: ' + str(float(numCorrect) / numTotal))

    print ('\n')