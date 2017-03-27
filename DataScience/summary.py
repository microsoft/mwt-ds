import sys
import json
import pandas as pd
# import cPickle 
from tabulate import tabulate

if __name__ == '__main__':
    if len(sys.argv) < 2:
        print("Usage: python summary.py {file_name}. Where file_name is the merged Decision Service logs in JSON format")
        sys.exit()

    file_name = sys.argv[1]
file_name = 'c:\\data\complex-article\\data_20170226-20170304.json'
with gzip.open(file_name, 'rt', encoding='utf8') if file_name.endswith('.gz') else open(file_name, 'r', encoding="utf8") as data:
    
    m = []
    for line in data:
        js = json.loads(line)
        if '_eventid' in js:
            m.append([
                js['_eventid'], 
                js['_timestamp'],
                len(js['_multi']), 
                float(js['_label_cost']),
                float(js['_label_probability']),
                int(js['_label_action']),
                ])

    print (m[0])
    df = pd.DataFrame(m, columns=('id', 'timestamp', 'actions', 'cost', 'prob', 'action_observed'))

    #              
    df['actions'].describe()
    numNonZeroCost = (df['cost'] != 0).sum()
    print("# num non-zero cost: {0}/{1} = {2:.4f}".format(numNonZeroCost, len(df), numNonZeroCost/len(df)))
   
def ips(d, action_of_policy):
    return d.cost / d.prob if action_of_policy == d.action_observed else 0


maxNumActions = df['actions'].max()
for action in range(1, maxNumActions+1):
    df['constant_policy_{0}'.format(action)] =  df.apply(lambda x: ips(x, action), axis = 1)

print ('Online performance:     {0:.5f}'.format(df['cost'].mean()))
# lower bound (unrealistic)
print ('Overfitted performance: {0:.5f}'.format(df.apply(lambda d: d.cost/d.prob, axis = 1).mean()))

for action in range(1, maxNumActions+1):
    print ('Constant policy   {0:2d}: {1:.5f}'.format(action, df['constant_policy_{0}'.format(action)].mean()))
    
print ("Distribution over actions")
df['action_observed'].value_counts()
df['actions'].value_counts()
df['actions'].value_counts()/len(df)

# what actions does a good policy choose?
# vw --cb_adf -d c:\data\Complex-Article\data_20170221-20170224_nosave.json --json -c --power_t 0 -q NX -q MX --ignore S --ignore i --ignore E -q NR -l 0.005 --cb_type mtr -p predictions
preds = pd.read_csv('predictions', header=0, names=['action'])
preds['action'].value_counts() / preds['action'].sum()

# import matplotlib
# import pylab

# df.hist('action_observed')
# pylab.show()
