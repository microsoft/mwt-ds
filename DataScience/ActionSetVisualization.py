import numpy as np
import matplotlib.pyplot as plt
import ds_parse, json, argparse, collections

def update(files, dt_str=13):
    fp_list = ds_parse.input_files_to_fp_list(files)
    l = []
    c_imp = collections.Counter()
    c_clk = collections.Counter()
    c_imp_all = collections.Counter()
    for fp in fp_list:
        for x in (gzip.open(fp, 'rb') if fp.endswith('.gz') else open(fp, 'rb')):
            if x.startswith(b'{"_label'):
                data = ds_parse.json_cooked(x)
                
                c_imp_all.update([data['ts'][:dt_str]])
                if not data['skipLearn']:
                    c_imp.update([data['ts'][:dt_str]])
                    l.append((data, x.strip()))
                    if float(data['cost']) < 0:
                        c_clk.update([data['ts'][:dt_str]])
                    
    ctr = []
    ts = []
    print('Timestamp (UTC),Clicks,Activated Imp.,CTR,Total Imp.')
    for x in c_imp_all:
        ctr.append(c_clk[x]/max(c_imp[x],1))
        ts.append(x)
        print('{},{},{},{:.2%},{}'.format(x,c_clk[x],c_imp[x],ctr[-1],c_imp_all[x]))
    print()
    return ts,ctr,l

def create_act_d(l):
    act_d = {}
    data = []
    ctr_all = {}
    for i,x in enumerate(l):
        js = json.loads(x[1])
        model_ind = js['a'][np.argmax(js['p'])]-1
        actions = set()
        temp = []
        for j,y in enumerate(js['c']['_multi']):
            
            ##########################################################################################
            action_name = y['Action']['PayloadID']               # scenario dependent              ###
            ##########################################################################################
            if j == js['_labelIndex']:
                a = action_name
            if j == model_ind:
                a_mod = action_name
            
            is_firstAction = int(j > 0)
            if action_name not in act_d:
                act_d[action_name] = (len(act_d),[],[])
            if action_name not in actions:
                actions.add(action_name)
                act_d[action_name][is_firstAction+1].append(i)
            temp.append(action_name)
        data.append((a, a_mod, js['_label_cost'], model_ind, js['_label_Action'], js['Timestamp'],temp, js.get('VWState', {}).get('m', 'N/A')))
        if a not in ctr_all:
            ctr_all[a] = [0,0,0,0]
        ctr_all[a][1] += 1
        ctr_all[a][3] += 1/js['_label_probability']
        if js['_label_cost'] != 0:
            ctr_all[a][0] -= js['_label_cost']
            ctr_all[a][2] -= js['_label_cost']/js['_label_probability']

        if (i+1) % 50000 == 0:
            print(i+1)

    print('Action,Clicks,Imp.,Clicks IPS,Imp. IPS,CTR,SNIPS')
    for a in ctr_all:
        print(','.join(map(str,[a]+ctr_all[a]+[ctr_all[a][0]/max(ctr_all[a][1],1),ctr_all[a][2]/max(ctr_all[a][3],1)])))
        
    return act_d,data,ctr_all

def plot_act_d(act_d, data, num_ticks=31, plot_rew_zero=False):
    vw_model_d = {}
    for i,x in enumerate(data):
        if x[-1] not in vw_model_d:
            vw_model_d[x[-1]] = ([],[])
        vw_model_d[x[-1]][0].append(i)
        vw_model_d[x[-1]][1].append(x[1])

    for x in vw_model_d:
        if x == 'N/A':
            plt.plot(vw_model_d[x][0], vw_model_d[x][1], "rs")
        else:
            plt.plot(vw_model_d[x][0], vw_model_d[x][1], "s")

    for a in act_d:
        plt.plot(act_d[a][1], [a for _ in range(len(act_d[a][1]))], "c.")
        plt.plot(act_d[a][2], [a for _ in range(len(act_d[a][2]))], "y.")
    plt.plot([i for i,x in enumerate(data) if x[2] < 0], [x[0] for x in data if x[2] < 0], "b.")
    if plot_rew_zero:
        plt.plot([i for i,x in enumerate(data) if x[2] == 0], [x[0] for x in data if x[2] == 0], "r.")

    indices = np.linspace(0, len(data)-1, num_ticks, endpoint=True, dtype=int)
    plt.xticks(indices, [data[i][5].split('.')[0][5:-3].replace('T', '\n') for i in indices])
    plt.ylabel('Actions')
    plt.xlabel('Timestamp (UTC)')
    plt.show()
    
    
if __name__ == '__main__':

    parser = argparse.ArgumentParser()
    parser.add_argument('-l','--log_fp', help="data file path (.json or .json.gz format - each line is a dsjson)", required=True)
    
    args = parser.parse_args()
    
    ts,ctr,l = update(args.log_fp)
    act_d,data,ctr_all = create_act_d(l)
    plot_act_d(act_d, data)
    