import sys
import json
import pandas as pd
import gzip
# import cPickle 

if __name__ == '__main__':
    if len(sys.argv) < 2:
        print("Usage: python summary.py {file_name}. Where file_name is the merged Decision Service logs in JSON format")
        sys.exit()

    file_name = sys.argv[1]

with gzip.open(file_name, 'rt', encoding='utf8') if file_name.endswith('.gz') else open(file_name, 'r', encoding="utf8") as data:
    
    m = []
    ii = 0
    for line in data:
        ii += 1
        if ii % 100000 == 0:
            print(ii)
        js = json.loads(line)
        if 'EventId' in js:
            m.append([
                js['EventId'], 
                js['Timestamp'],
                len(js['c']['_multi']), 
                float(js['_label_cost']),
                float(js['_label_probability']),
                int(js['_label_Action']),
                ])

    print (m[0])
    df = pd.DataFrame(m, columns=('id', 'timestamp', 'num_of_actions', 'cost', 'prob', 'action_observed'))

    #              
    print(df['num_of_actions'].describe())
    numNonZeroCost = (df['cost'] != 0).sum()
    print("# num non-zero cost: {0}/{1} = {2:.4f}".format(numNonZeroCost, len(df), numNonZeroCost/len(df)))
   
def ips(d, action_of_policy):
    return d.cost / d.prob if action_of_policy == d.action_observed else 0


maxNumActions = 1
for action in range(1, maxNumActions+1):
    df['constant_policy_{0}'.format(action)] =  df.apply(lambda x: ips(x, action), axis = 1)
    print ('Constant policy   {0:2d}: {1:.9f}'.format(action, df['constant_policy_{0}'.format(action)].mean()))

print ('Online performance:     {0:.9f}'.format(df['cost'].mean()))
    
print ("Distribution over actions")
print(df['action_observed'].value_counts())
print(df['num_of_actions'].value_counts())
print(df['num_of_actions'].value_counts()/len(df))

# lower bound (unrealistic)
print ('Overfitted performance: {0:.9f}'.format(df.apply(lambda d: d.cost/d.prob, axis = 1).mean()))


# what actions does a good policy choose?
# vw --cb_adf -d c:\data\Complex-Article\data_20170221-20170224_nosave.json --json -c --power_t 0 -q NX -q MX --ignore S --ignore i --ignore E -q NR -l 0.005 --cb_type mtr -p predictions
# preds = pd.read_csv('predictions', header=0, names=['action'])
# preds['action'].value_counts() / preds['action'].sum()

# import matplotlib
# import pylab

# df.hist('action_observed')
# pylab.show()
