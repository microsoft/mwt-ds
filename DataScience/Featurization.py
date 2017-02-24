import json, sys

# Identify namespaces and detect marginal features
if len(sys.argv) < 3:
    print("Usage: python experimenter.py {source_data_unmodified} {dest_data_featurized}. Where source_data_unmodified is the Decision Service data you want to featurize and \
        dest_data_featurized is the name of the featurized data file you are creating.")
    sys.exit()

source_file_name = sys.argv[1]
dest_file_name = sys.argv[2]

readerFile = open(source_file_name,"r")
writerFile = open(dest_file_name,"w")

counter = 0

print("Starting featurization")

for event in readerFile:
    if counter % 500000 == 0:
        print("counter: " + str(counter))
    
    # Parse event
    parsedEvent = json.loads(event)

    if "_tag" in event:
        continue

    # Featurize shared context
    # Sample: Separate device type and age group into different namespaces
    parsedEvent["Device"] = {"deviceType" : parsedEvent["UserFeatures"]["deviceType"]}
    parsedEvent["User"] = {"ageGroup" : parsedEvent["UserFeatures"]["ageGroup"]}

    del parsedEvent["UserFeatures"]

    # Featurize actions
    actionList = parsedEvent["_multi"]
    
    for action in actionList:
        # Change "id" feature into the format required for marginal features
        action["VideoId"] = {"constant": 1, "id": action["Video"]["id"]}
        del action["Video"]

    writerFile.write(json.dumps(parsedEvent) + "\n")   
    
    counter += 1

print("Done")